using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using Callback = ECommons.Automation.Callback;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XIVBlackjack.Data;

public enum TradeStep
{
    Idle,
    Targeting,
    SendingCommand,
    AwaitingWindow,
    OpeningGilInput,
    EnteringAmount,
    AwaitingConfirm,
    BetweenTrades,
    Completed,
    Failed
}

/// <summary>
/// What the game said about the last trade window. Unknown is not the same as a cancel: it
/// means no outcome message was seen, which is a reason to stop and ask rather than to guess.
/// </summary>
public enum TradeResult
{
    Unknown,
    Completed,
    Cancelled
}

/// <summary>
/// Sets a gil trade up and then gets out of the way: target the player, open the trade, open
/// the gil field, fill in the amount. The dealer clicks Trade; the partner accepts.
///
/// Deliberately stops short of confirming. Beyond the obvious — a human reading the number
/// before real gil moves — it also means nothing here depends on node-list positions, which
/// are the part of addon automation that silently breaks when the UI is revised. Everything
/// used below is either a first-party Dalamud API or an ECommons helper that gets maintained.
///
/// Written as an explicit state machine on the framework tick rather than an async queue so
/// the current step and its age are inspectable at any instant.
/// </summary>
public unsafe class TradeAutomation : IDisposable
{
    private readonly Plugin Plugin;

    public TradeStep Step { get; private set; } = TradeStep.Idle;
    public string StatusMessage { get; private set; } = string.Empty;

    public string IntendedPartner { get; private set; } = string.Empty;
    public int IntendedAmount { get; private set; }
    private BlackjackPlayer? Subject;

    // A payout larger than the per-trade cap runs as a chain of trades. The dealer confirms
    // the run once; each trade then starts as soon as the previous window closes.
    private readonly Queue<int> Pending = new();
    private int RunTotal;
    private int RunIndex;

    public bool IsMultiTrade => RunTotal > 1;
    public string RunProgress => IsMultiTrade ? $" (trade {RunIndex} of {RunTotal})" : string.Empty;

    private DateTime StepStarted = DateTime.MinValue;
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PartnerTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(3);

    // The client needs a beat between UI actions; callbacks fired on consecutive frames
    // get dropped.
    private static readonly TimeSpan ActionDelay = TimeSpan.FromMilliseconds(350);

    // Breathing room between chained trades so the previous window is fully torn down.
    private static readonly TimeSpan BetweenTradesDelay = TimeSpan.FromMilliseconds(1500);

    private bool Resent;

    /// <summary>
    /// Collect mode opens the window and stops. Used for a double down or a split, where the
    /// player is putting gil in rather than taking it out, so there is nothing to fill.
    /// </summary>
    public bool CollectOnly { get; private set; }

    public bool IsRunning => Step is not (TradeStep.Idle or TradeStep.Completed or TradeStep.Failed);

    public TradeAutomation(Plugin plugin)
    {
        Plugin = plugin;
        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
    }

    public void Begin(BlackjackPlayer player, IEnumerable<int> chunks)
    {
        if (IsRunning)
            return;

        var queue = chunks.Where(c => c > 0).ToArray();

        if (queue.Length == 0)
        {
            Fail("Nothing to trade.");
            return;
        }

        if (queue.Any(c => c > Blackjack.TradeCap))
        {
            Fail($"A trade exceeds the {Blackjack.TradeCap:N0} cap.");
            return;
        }

        if (player.Name == Plugin.LocalPlayer)
        {
            Fail("You cannot trade with yourself.");
            return;
        }

        if (Plugin.Condition[ConditionFlag.TradeOpen])
        {
            Fail("A trade is already open.");
            return;
        }

        Pending.Clear();
        foreach (var chunk in queue)
            Pending.Enqueue(chunk);

        Subject = player;
        IntendedPartner = player.Name;
        RunTotal = queue.Length;
        RunIndex = 0;
        StatusMessage = string.Empty;

        Plugin.Log.Information($"[Trade] Run begins: partner={IntendedPartner}, {RunTotal} trade(s), total={queue.Sum():N0}");
        StartNextTrade();
    }

    /// <summary>Opens a trade window on a player so they can hand over an extra stake.</summary>
    public void BeginCollect(BlackjackPlayer player)
    {
        if (IsRunning)
            return;

        if (player.RootName == Plugin.LocalPlayer)
        {
            Fail("You cannot trade with yourself.");
            return;
        }

        if (Plugin.Condition[ConditionFlag.TradeOpen])
            return;

        Pending.Clear();
        Subject = player;

        // A split hand is named "<player> Split", which matches nobody in the world. The
        // person handing over the stake is always the root player.
        IntendedPartner = player.RootName;
        IntendedAmount = 0;
        RunTotal = 1;
        RunIndex = 1;
        CollectOnly = true;
        Resent = false;
        StatusMessage = string.Empty;

        Plugin.Log.Information($"[Trade] Collect from {IntendedPartner}");
        Advance(TradeStep.Targeting);
    }

    private void StartNextTrade()
    {
        IntendedAmount = Pending.Dequeue();
        RunIndex++;
        Resent = false;
        CollectOnly = false;

        Plugin.Log.Information($"[Trade] Trade {RunIndex}/{RunTotal}: {IntendedAmount:N0}");
        Advance(TradeStep.Targeting);
    }

    /// <summary>
    /// Clears a finished run's result line. Without this, "All 3 trades done" from the last
    /// round sits under the next round's settlement, right beside a payout that hasn't gone.
    /// Leaves a run that is still going alone.
    /// </summary>
    public void ClearStatus()
    {
        if (IsRunning)
            return;

        Step = TradeStep.Idle;
        StatusMessage = string.Empty;
    }

    public void Cancel()
    {
        Plugin.Log.Information($"[Trade] Cancelled at {Step}, {Pending.Count} trade(s) abandoned");
        Pending.Clear();
        Step = TradeStep.Idle;
        StatusMessage = string.Empty;
        IntendedPartner = string.Empty;
        IntendedAmount = 0;
        RunTotal = 0;
        RunIndex = 0;
        Subject = null;
    }

    private void Advance(TradeStep next)
    {
        Plugin.Log.Information($"[Trade] {Step} -> {next}");
        Step = next;
        StepStarted = DateTime.UtcNow;
    }

    private void Fail(string reason)
    {
        Step = TradeStep.Failed;
        StatusMessage = reason;
        Plugin.Log.Warning($"[Trade] Stopped: {reason}");
    }

    private void OnUpdate(IFramework _)
    {
        if (!IsRunning)
            return;

        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            // Without this a throw silently kills the handler and the sequence just stops.
            Plugin.Log.Error(ex, "[Trade] Exception during automation");
            Fail($"Error: {ex.Message}");
        }
    }

    private void Tick()
    {
        var elapsed = DateTime.UtcNow - StepStarted;
        var budget = Step switch
        {
            TradeStep.AwaitingConfirm => PartnerTimeout,
            TradeStep.BetweenTrades => BetweenTradesDelay + StepTimeout,
            _ => StepTimeout
        };

        if (elapsed > budget)
        {
            Fail($"Timed out during {Step}.");
            return;
        }

        switch (Step)
        {
            case TradeStep.Targeting:
                if (!Plugin.TargetPlayerByName(IntendedPartner))
                    return; // keep retrying; they may still be loading in

                Advance(TradeStep.SendingCommand);
                return;

            case TradeStep.SendingCommand:
                // /trade acts on whoever is targeted RIGHT NOW, so re-verify every tick.
                if (!TargetMatchesIntent())
                {
                    Plugin.Log.Warning("[Trade] Target changed before the command fired; re-targeting.");
                    Advance(TradeStep.Targeting);
                    return;
                }

                SendTradeCommand();
                Advance(TradeStep.AwaitingWindow);
                StatusMessage = $"Opening trade...{RunProgress}";
                return;

            case TradeStep.AwaitingWindow:
                if (Plugin.Condition[ConditionFlag.TradeOpen])
                {
                    if (CollectOnly)
                    {
                        Advance(TradeStep.Completed);
                        StatusMessage = "Trade open \u2014 take their stake, then roll.";
                        return;
                    }

                    Advance(TradeStep.OpeningGilInput);
                    return;
                }

                if (!Resent && elapsed > ResendAfter && TargetMatchesIntent())
                {
                    Resent = true;
                    Plugin.Log.Warning("[Trade] Window did not open; sending once more.");
                    SendTradeCommand();
                }

                return;

            case TradeStep.OpeningGilInput:
                if (elapsed < ActionDelay)
                    return;

                if (!TryGetAddon("Trade", out var tradeAddon))
                    return;

                // Callback case 2 on the Trade addon opens the gil field.
                Callback.Fire(tradeAddon, true, 2, Callback.ZeroAtkValue);
                Advance(TradeStep.EnteringAmount);
                StatusMessage = $"Entering {IntendedAmount:N0}...{RunProgress}";
                return;

            case TradeStep.EnteringAmount:
                if (elapsed < ActionDelay)
                    return;

                if (!TryGetAddon("InputNumeric", out var numeric))
                    return;

                new AddonMaster.InputNumeric(numeric).Ok(IntendedAmount);
                Advance(TradeStep.AwaitingConfirm);
                StatusMessage = $"{IntendedAmount:N0} is in the window \u2014 hit Trade.{RunProgress}";
                return;

            case TradeStep.AwaitingConfirm:
                if (Plugin.Condition[ConditionFlag.TradeOpen])
                    return;

                // A closed window is treated as a completed trade. It is not the same thing —
                // a cancel looks identical from here — but the chat-message approach that could
                // tell them apart did not fire reliably, and stalling a real payout to ask about
                // a trade that went through is worse than over-counting a rare cancel. The
                // dealer can reset the row's count. See the [Trade] chat logging in Plugin.OnChatMessage for the
                // diagnostic groundwork if this gets revisited.
                MarkSentAndContinue();
                return;

            case TradeStep.BetweenTrades:
                if (elapsed < BetweenTradesDelay)
                    return;

                StartNextTrade();
                return;
        }
    }

    private void MarkSentAndContinue()
    {
        if (Subject is not null)
            Subject.TradesCompleted++;

        if (Pending.Count > 0)
        {
            Advance(TradeStep.BetweenTrades);
            StatusMessage = $"Sent {IntendedAmount:N0}. Opening the next trade...";
            return;
        }

        Advance(TradeStep.Completed);
        StatusMessage = RunTotal > 1
            ? $"All {RunTotal} trades done."
            : $"Trade closed. Marked {IntendedAmount:N0} as sent.";
    }

    private bool TargetMatchesIntent()
    {
        if (Plugin.TargetManager.Target is not IPlayerCharacter pc)
            return false;

        if (pc.HomeWorld.ValueNullable == null)
            return false;

        return $"{pc.Name}\uE05D{pc.HomeWorld.Value.Name}" == IntendedPartner;
    }

    private static bool TryGetAddon(string name, out AtkUnitBase* addon)
    {
        // ECommons' helper rather than IGameGui.GetAddonByName, which now returns a
        // wrapper struct instead of a raw pointer.
        return GenericHelpers.TryGetAddonByName(name, out addon)
               && GenericHelpers.IsAddonReady(addon);
    }

    /// <summary>
    /// Walks an addon's node list and logs what is actually there — index, node ID, type, and
    /// any text — plus its AtkValues.
    ///
    /// Nothing in the trade flow depends on node positions any more, so this is purely a
    /// diagnostic. It exists because node LIST POSITIONS shift between UI revisions while node
    /// IDs do not: if anything here ever needs to reach into an addon again, measure the IDs
    /// with this rather than inheriting positional guesses from another plugin.
    /// </summary>
    public static void DumpAddon(string name)
    {
        if (!TryGetAddon(name, out var addon))
        {
            Plugin.Log.Warning($"[Dump] Addon '{name}' is not open or not ready.");
            return;
        }

        var count = addon->UldManager.NodeListCount;
        Plugin.Log.Information($"[Dump] === {name}: {count} nodes ===");

        for (var i = 0; i < count; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node is null)
            {
                Plugin.Log.Information($"[Dump] [{i}] <null>");
                continue;
            }

            var detail = string.Empty;

            var text = node->GetAsAtkTextNode();
            if (text is not null)
                detail = $" text=\"{text->NodeText}\"";

            var component = node->GetAsAtkComponentNode();
            if (component is not null && component->Component is not null)
            {
                detail += " component";

                var button = (AtkComponentButton*)component->Component;
                if (button->ButtonTextNode is not null)
                    detail += $" buttonText=\"{button->ButtonTextNode->NodeText}\"";
            }

            Plugin.Log.Information($"[Dump] [{i}] id={node->NodeId} type={node->Type} visible={node->IsVisible()}{detail}");
        }

        var valueCount = addon->AtkValuesCount;
        Plugin.Log.Information($"[Dump] --- {name}: {valueCount} AtkValues ---");

        // Numeric fields only. String rendering needs a pointer type that has changed shape
        // across FFXIVClientStructs versions.
        for (var i = 0; i < valueCount; i++)
        {
            var v = addon->AtkValues[i];
            Plugin.Log.Information($"[Dump] value[{i}] type={v.Type} int={v.Int} uint={v.UInt}");
        }

        Plugin.Log.Information($"[Dump] === end {name} ===");
    }

    /// <summary>
    /// The only call with no first-party Dalamud equivalent — it needs a signature-scanned
    /// hook into the game's chat processing, which is the sole reason ECommons is a
    /// dependency. "&lt;t&gt;" is the chat placeholder for the current target; bare "/trade"
    /// does not reliably resolve one.
    /// </summary>
    private static void SendTradeCommand()
    {
        const string command = "/trade <t>";

        Plugin.Log.Information($"[Trade] Sending: {command}");
        Chat.SendMessage(command);
    }
}
