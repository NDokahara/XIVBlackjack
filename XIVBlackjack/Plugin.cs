using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using XIVBlackjack.Attributes;
using XIVBlackjack.Data;
using XIVBlackjack.Windows.Config;
using XIVBlackjack.Windows.Main;

namespace XIVBlackjack;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static ICommandManager Commands { get; private set; } = null!;
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] public static IChatGui Chat { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static IObjectTable Objects { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static INotificationManager Notification { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;

    public static string PluginDir => PluginInterface.AssemblyLocation.DirectoryName!;

    private readonly WindowSystem WindowSystem = new("XIVBlackjack");
    public MainWindow MainWindow { get; init; }
    public ConfigWindow ConfigWindow { get; init; }

    public readonly Configuration Configuration;
    public readonly FontManager FontManager;
    public readonly HookManager HookManager;

    public string LocalPlayer = string.Empty;
    public GameState State = GameState.NotRunning;

    public readonly Blackjack Blackjack;
    public readonly TradeAutomation TradeAutomation;

    private readonly PluginCommandManager<Plugin> CommandManager;

    // Chat has to be sent from the game's main thread. UiBuilder.Draw does not run there, so
    // anything fired from a button is queued and drained on the next framework tick.
    private readonly Queue<Action> MainThreadQueue = new();

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ECommons.ECommonsMain.Init(PluginInterface, this);

        HookManager = new HookManager(this);
        Blackjack = new Blackjack(this);
        TradeAutomation = new TradeAutomation(this);

        FontManager = new FontManager();

        MainWindow = new MainWindow(this);
        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager = new PluginCommandManager<Plugin>(this, Commands);

        Framework.Update += DrainMainThreadQueue;
        Chat.ChatMessage += OnChatMessage;

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi += OpenMain;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        Framework.Update -= DrainMainThreadQueue;
        Chat.ChatMessage -= OnChatMessage;

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMain;

        FontManager.Dispose();
        CommandManager.Dispose();
        HookManager.Dispose();
        TradeAutomation.Dispose();

        ECommons.ECommonsMain.Dispose();
    }

    [Command("/blackjack")]
    [Aliases("/bj")]
    [HelpMessage("Opens the table window.\nArguments:\nconfig - Opens settings\non - Start watching rolls\noff - Stop watching rolls")]
    public void PluginCommand(string _, string args)
    {
        switch (args)
        {
            case "on":
                Configuration.On = true;
                Configuration.Save();
                break;
            case "off":
                Configuration.On = false;
                Configuration.Save();
                break;
            case "config":
                ConfigWindow.IsOpen = true;
                break;
            default:
                MainWindow.IsOpen = true;
                break;
        }
    }

    public static string GetTargetName()
    {
        var target = TargetManager.SoftTarget ?? TargetManager.Target;
        if (target is not IPlayerCharacter pc || pc.HomeWorld.ValueNullable == null)
            return string.Empty;

        return $"{pc.Name}\uE05D{pc.HomeWorld.Value.Name}";
    }

    /// <summary>
    /// Targets a registered player by their full "Name\uE05DWorld" string, so the dealer
    /// can open a trade without hunting for them in a crowd. Sets the target only —
    /// opening and confirming the trade stays a manual act.
    /// </summary>
    public static bool TargetPlayerByName(string fullName)
    {
        foreach (var obj in Objects)
        {
            if (obj is not IPlayerCharacter pc || pc.HomeWorld.ValueNullable == null)
                continue;

            if ($"{pc.Name}\uE05D{pc.HomeWorld.Value.Name}" != fullName)
                continue;

            TargetManager.Target = pc;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads two things off the chat stream: the outcome of a trade, and rolls, the latter as a
    /// fallback for when a roll hook's byte signature has gone stale. Rolls cost nothing when
    /// the hooks are healthy, because duplicates are filtered downstream.
    /// </summary>
    private void OnChatMessage(IHandleableChatMessage message)
    {
        var text = message.Message.TextValue;

        // Not gated on ReadRollsFromChat: the two read the same stream but answer unrelated
        // questions, and a dealer who turned roll-reading off still needs trades counted right.
        var outcome = TradeMessages.Classify(text);
        if (outcome != TradeResult.Unknown)
        {
            if (DebugConfig.Debug)
                Log.Information($"[Trade] Chat outcome: kind={message.LogKind} result={outcome} text=\"{text}\"");

            TradeAutomation.NoteTradeResult(outcome);
        }

        if (!Configuration.ReadRollsFromChat)
            return;

        if (!ChatRollParser.TryParse(text, out var roll, out var outOf))
        {
            if (DebugConfig.Debug && text.Contains("Random", StringComparison.OrdinalIgnoreCase))
                Log.Information($"[Chat] Unparsed roll-like line: kind={message.LogKind} text=\"{text}\"");

            return;
        }

        // The sender payload carries the real name and world; chat itself may be abbreviating
        // it. Own messages usually have no payload, in which case it is us.
        var payload = message.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        var fullName = payload is not null ? payload.DisplayedName : ResolveLocalPlayer();

        if (fullName == string.Empty)
            return;

        if (DebugConfig.Debug)
            Log.Information($"[Chat] Parsed roll: kind={message.LogKind} name=\"{fullName}\" roll={roll} outOf={outOf}");

        ProcessIncomingMessage(fullName, roll, outOf);
    }

    private static string ResolveLocalPlayer()
    {
        if (!PlayerState.IsLoaded || PlayerState.HomeWorld.ValueNullable?.Name == null)
            return string.Empty;

        return $"{PlayerState.CharacterName}\uE05D{PlayerState.HomeWorld.Value.Name}";
    }

    /// <summary>Queues an action to run on the next framework tick, i.e. the main thread.</summary>
    public void RunOnTick(Action action)
    {
        lock (MainThreadQueue)
            MainThreadQueue.Enqueue(action);
    }

    private void DrainMainThreadQueue(IFramework _)
    {
        while (true)
        {
            Action action;
            lock (MainThreadQueue)
            {
                if (MainThreadQueue.Count == 0)
                    return;

                action = MainThreadQueue.Dequeue();
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Queued action failed");
            }
        }
    }

    /// <summary>
    /// Rolls for a card by sending the roll command as if typed. The plugin's own hook picks
    /// the result up like any other roll, so nothing special happens on the receiving side.
    ///
    /// Queued rather than sent inline: button handlers run during UI drawing, and the game's
    /// chat entry point has to be called from the main thread.
    /// </summary>
    public void SendRollCommand()
    {
        var command = Configuration.RollCommand;

        RunOnTick(() =>
        {
            Log.Information($"[Roll] Sending: {command}");
            ECommons.Automation.Chat.SendMessage(command);
        });
    }

    private (string Name, int Roll, int OutOf) LastRoll;
    private DateTime LastRollAt = DateTime.MinValue;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMilliseconds(750);

    public void ProcessIncomingMessage(string fullName, int roll, int outOf)
    {
        // The hook and the chat reader can both see the same roll. Whichever arrives first
        // wins; the other is dropped, so neither path has to be switched off.
        var signature = (fullName, roll, outOf);
        if (signature == LastRoll && DateTime.UtcNow - LastRollAt < DuplicateWindow)
        {
            if (DebugConfig.Debug)
                Log.Information($"[Roll] Duplicate ignored: {fullName} {roll}/{outOf}");

            return;
        }

        LastRoll = signature;
        LastRollAt = DateTime.UtcNow;

        if (!Configuration.On)
        {
            Log.Information($"[Roll] Ignored {fullName}: plugin is switched off (On is unchecked).");
            return;
        }

        if (State is GameState.NotRunning or GameState.Done or GameState.Crash)
        {
            Log.Information($"[Roll] Ignored {fullName}: state is {State}.");
            return;
        }

        if (PlayerState.IsLoaded && PlayerState.HomeWorld.ValueNullable?.Name != null)
            LocalPlayer = $"{PlayerState.CharacterName}\uE05D{PlayerState.HomeWorld.Value.Name}";

        // Several parse paths only accept the dealer's own rolls, comparing on this exact
        // string. A mismatch here is silent and looks identical to the hook not firing.
        Log.Information($"[Roll] {fullName} rolled {roll} (outOf {outOf}); local player is \"{LocalPlayer}\"; state {State}");

        if (Configuration.ActiveBlocklist && Configuration.SavedBlocklist.Contains(fullName.Replace("\uE05D", "@")))
        {
            Log.Information("Blocked player tried to roll.");
            return;
        }

        try
        {
            Blackjack.Parser(new Roll(fullName, roll, outOf));
        }
        catch (FormatException e)
        {
            Chat.PrintError("Unable to parse roll.");
            Log.Error(e.ToString());
        }
    }

    public void SwitchState(GameState newState)
    {
        State = newState;
    }

    #region UI Toggles
    private void DrawUI() => WindowSystem.Draw();
    public void OpenMain() => MainWindow.IsOpen = true;
    public void OpenConfig() => ConfigWindow.IsOpen = true;
    #endregion
}
