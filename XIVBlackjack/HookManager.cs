using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;
using FFXIVClientStructs.STD;
using Lumina.Excel.Sheets;
using XIVBlackjack.Data;

namespace XIVBlackjack;

public unsafe class HookManager
{
    private readonly Plugin Plugin;

    [Signature("E8 ?? ?? ?? ?? EB ?? 45 33 C9 4C 8B C6", DetourName = nameof(RandomPrintLogDetour))]
    private Hook<RandomPrintLogDelegate>? RandomPrintLogHook { get; set; }
    private delegate void RandomPrintLogDelegate(RaptureLogModule* module, int logMessageId, byte* playerName, byte sex, StdDeque<TextParameter>* parameter, byte flags, ushort homeWorldId);

    // Kept in step with caitlyn-gg/DeathRoll, which is the fork still being maintained;
    // Infiziert90's original stopped at API 13. A stale signature does not error — it leaves
    // the hook null and ?.Enable() quietly does nothing, so rolls vanish with no clue why.
    // This pattern was re-derived for patch 7.5 on 2026-05-03; the previous one no longer
    // resolves.
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B7 BC 24", DetourName = nameof(DicePrintLogDetour))]
    private Hook<DicePrintLogDelegate>? DicePrintLogHook { get; set; }
    private delegate void DicePrintLogDelegate(RaptureLogModule* module, ushort chatType, byte* userName, void* unused, ushort worldId, ulong accountId, ulong contentId, ushort roll, ushort outOf, uint entityId, byte ident);


    public HookManager(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.GameInteropProvider.InitializeFromAttributes(this);

        RandomPrintLogHook?.Enable();
        DicePrintLogHook?.Enable();

        RandomHookReady = RandomPrintLogHook is not null;
        DiceHookReady = DicePrintLogHook is not null;

        if (!RandomHookReady)
            Plugin.Log.Error("[Hook] /random signature did not resolve — those rolls will be ignored.");

        if (!DiceHookReady)
            Plugin.Log.Error("[Hook] /dice signature did not resolve — those rolls will be ignored.");

        if (RandomHookReady && DiceHookReady)
            Plugin.Log.Information("[Hook] Both roll hooks resolved.");
    }

    public static bool RandomHookReady { get; private set; }
    public static bool DiceHookReady { get; private set; }

    public void Dispose()
    {
        RandomPrintLogHook?.Dispose();
        DicePrintLogHook?.Dispose();
    }

    private void RandomPrintLogDetour(RaptureLogModule* module, int logMessageId, byte* playerName, byte sex, StdDeque<TextParameter>* parameter, byte flags, ushort homeWorldId)
    {
        // Log every id that reaches the detour, not just the two we act on. If a roll shows
        // in chat but never registers, this says whether the hook fired at all and under
        // which id — the two failure modes look identical from the outside otherwise.
        if (DebugConfig.Debug)
            Plugin.Log.Information($"[Hook] RandomPrintLog fired: logMessageId={logMessageId}, worldId={homeWorldId}");

        if (logMessageId != 856 && logMessageId != 3887)
        {
            RandomPrintLogHook!.Original(module, logMessageId, playerName, sex, parameter, flags, homeWorldId);
            return;
        }

        try
        {
            var name = MemoryHelper.ReadStringNullTerminated((nint)playerName);
            var world = Plugin.Data.GetExcelSheet<World>().GetRow(homeWorldId);
            var fullName = $"{name}\uE05D{world.Name}";

            var roll = (*parameter)[1].IntValue;
            var outOf = logMessageId == 3887 ? (*parameter)[2].IntValue : 0;

            Plugin.Log.Information($"[Hook] /random captured: name=\"{fullName}\" roll={roll} outOf={outOf} (msgId {logMessageId}, worldId {homeWorldId})");
            Plugin.ProcessIncomingMessage(fullName, roll, outOf);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to process random roll");
        }

        RandomPrintLogHook!.Original(module, logMessageId, playerName, sex, parameter, flags, homeWorldId);
    }

    private void DicePrintLogDetour(RaptureLogModule* module, ushort chatType, byte* playerName, void* unused, ushort worldId, ulong accountId, ulong contentId, ushort roll, ushort outOf, uint entityId, byte ident)
    {
        try
        {
            var name = MemoryHelper.ReadStringNullTerminated((nint)playerName);
            var world = Plugin.Data.GetExcelSheet<World>().GetRow(worldId);
            var fullName = $"{name}\uE05D{world.Name}";

            Plugin.Log.Information($"[Hook] /dice captured: name=\"{fullName}\" roll={roll} outOf={outOf} (worldId {worldId})");
            Plugin.ProcessIncomingMessage(fullName, roll, outOf);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Unable to process dice roll");
        }

        DicePrintLogHook!.Original(module, chatType, playerName, unused, worldId, accountId, contentId, roll, outOf, entityId, ident);
    }
}