using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;

namespace XIVBlackjack.Windows.Config;

public partial class ConfigWindow
{
    private void General()
    {
        if (ImGui.BeginTabItem("General"))
        {
            ImGuiHelpers.ScaledDummy(5.0f);

            var changed = false;
            changed |= ImGui.Checkbox("On", ref Configuration.On);

            var spacing = ImGui.GetScrollMaxY() == 0 ? 65.0f : 80.0f;
            ImGui.SameLine(ImGui.GetWindowWidth() - spacing);

            if (ImGui.Button("Show UI"))
                Plugin.OpenMain();

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Blackjack Options:");
            ImGui.Indent(10.0f);
            Blackjack(ref changed);
            ImGui.Unindent(10.0f);

            if (changed)
                Configuration.Save();

            ImGui.EndTabItem();
        }
    }
}
