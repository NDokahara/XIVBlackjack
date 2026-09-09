using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using XIVBlackjack.Data;

namespace XIVBlackjack.Windows.Config;

public partial class ConfigWindow
{
    private void Debug()
    {
        if (ImGui.BeginTabItem("Debug"))
        {
            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Roll capture:");

            ImGui.Checkbox("Verbose roll logging", ref DebugConfig.Debug);
            ImGuiComponents.HelpMarker("Logs every roll message the hook sees and every roll-like chat line, to /xllog.");


            ImGui.Text("/random hook:");
            ImGui.SameLine();
            if (HookManager.RandomHookReady)
                ImGui.TextColored(Helper.Green, "resolved");
            else
                ImGui.TextColored(Helper.Red, "FAILED - signature is out of date");

            ImGui.Text("/dice hook:");
            ImGui.SameLine();
            if (HookManager.DiceHookReady)
                ImGui.TextColored(Helper.Green, "resolved");
            else
                ImGui.TextColored(Helper.Red, "FAILED - signature is out of date");

            ImGui.Text("Capture enabled:");
            ImGui.SameLine();
            if (Plugin.Configuration.On)
                ImGui.TextColored(Helper.Green, "yes");
            else
                ImGui.TextColored(Helper.Red, "no - tick 'On' in General");

            ImGui.Text($"Local player: {(Plugin.LocalPlayer == string.Empty ? "not seen yet" : Plugin.LocalPlayer.Replace("\uE05D", "@"))}");
            ImGuiComponents.HelpMarker("Only set once a roll has been captured. Several parse paths compare against it exactly.");

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Trade addon inspection:");
            ImGui.TextWrapped("Open a trade window in game, then dump the node list. Results go to /xllog.");

            if (ImGui.Button("Dump Trade addon"))
                TradeAutomation.DumpAddon("Trade");

            ImGui.SameLine();
            if (ImGui.Button("Dump SelectYesno"))
                TradeAutomation.DumpAddon("SelectYesno");

            ImGuiHelpers.ScaledDummy(5.0f);

            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TextColored(Helper.Red,"Please do not run debug all the time!");
            ImGui.TextColored(Helper.Red,"This will bloat your log.");

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            if (ImGui.Checkbox("Debug", ref DebugConfig.Debug))
            {

                DebugConfig.RandomizeNames = false;

                Configuration.Save();
            }

            if (DebugConfig.Debug)
            {
                ImGuiHelpers.ScaledIndent(10.0f);
                ImGui.Checkbox("Randomize Names", ref DebugConfig.RandomizeNames);
                ImGuiHelpers.ScaledIndent(-10.0f);
            }

            ImGui.EndTabItem();
        }
    }
}