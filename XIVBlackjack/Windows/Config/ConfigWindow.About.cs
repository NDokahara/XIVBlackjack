using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

namespace XIVBlackjack.Windows.Config;

public partial class ConfigWindow
{
    private const string KofiUrl = "https://ko-fi.com/nanodokahara";
    private const string UpstreamUrl = "https://github.com/Infiziert90/DeathRoll";
    private const string ForkUrl = "https://github.com/caitlyn-gg/DeathRoll";

    private static void About()
    {
        if (!ImGui.BeginTabItem("About"))
            return;

        var buttonHeight = ImGui.GetFrameHeight() + (16.0f * ImGuiHelpers.GlobalScale);

        if (ImGui.BeginChild("AboutContent", new Vector2(0, -buttonHeight)))
        {
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TextColored(ImGuiColors.DalamudViolet, Plugin.PluginInterface.Manifest.Name);
            ImGui.TextUnformatted(Plugin.PluginInterface.Manifest.Punchline);

            ImGuiHelpers.ScaledDummy(6.0f);

            ImGui.TextUnformatted("Version:");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedOrange, Plugin.PluginInterface.Manifest.AssemblyVersion.ToString());

            ImGui.TextUnformatted("Author:");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGold, Plugin.PluginInterface.Manifest.Author);

            ImGuiHelpers.ScaledDummy(10.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(6.0f);

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Credits");
            ImGuiHelpers.ScaledDummy(3.0f);

            ImGui.TextWrapped("The blackjack engine at the heart of this plugin comes from DeathRoll Helper " +
                              "by Infi (Infiziert90), by way of Caitlyn's fork. Everything else that plugin " +
                              "does has been stripped out, and the dealer tooling built on top is new, but the " +
                              "card handling, roll capture and game flow are all their work.");

            ImGuiHelpers.ScaledDummy(6.0f);

            if (ImGui.Button("DeathRoll Helper (Infi)"))
                Util.OpenLink(UpstreamUrl);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(UpstreamUrl);

            ImGui.SameLine();

            if (ImGui.Button("Caitlyn's fork"))
                Util.OpenLink(ForkUrl);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(ForkUrl);

            ImGuiHelpers.ScaledDummy(6.0f);
            ImGui.TextWrapped("Built with ECommons by NightmareXIV, and Dalamud by goatcorp.");

            ImGuiHelpers.ScaledDummy(10.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(6.0f);

            ImGui.TextColored(ImGuiColors.DalamudViolet, "Support");
            ImGuiHelpers.ScaledDummy(3.0f);
            ImGui.TextWrapped("This is free, and stays free. If it has saved you some time behind the table, " +
                              "a coffee is always appreciated.");
        }

        ImGui.EndChild();

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2.0f);

        ImGui.PushStyleColor(ImGuiCol.Text, Helper.White);

        var pressed = ImGuiComponents.IconButtonWithText(
            FontAwesomeIcon.Coffee,
            "Support on Ko-fi",
            Helper.KofiRed,
            Helper.KofiRedActive,
            Helper.KofiRedHover);

        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(KofiUrl);

        if (pressed)
            Util.OpenLink(KofiUrl);

        ImGui.EndTabItem();
    }
}
