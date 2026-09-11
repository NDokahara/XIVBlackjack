using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

namespace XIVBlackjack.Windows.Config;

public partial class ConfigWindow
{
    private const string BugFormUrl = "https://forms.gle/9wa27ViAzpPZ3aAL8";
    private const string IssuesUrl = "https://github.com/NDokahara/XIVBlackjack/issues";

    private void General()
    {
        if (!ImGui.BeginTabItem("General"))
            return;

        // Options scroll in a child so the bug report footer stays pinned to the bottom,
        // the same way the Ko-fi button is on the About tab.
        var footerHeight = ImGui.GetTextLineHeightWithSpacing()
                           + ImGui.GetFrameHeightWithSpacing()
                           + (16.0f * ImGuiHelpers.GlobalScale);

        if (ImGui.BeginChild("GeneralContent", new Vector2(0, -footerHeight)))
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
        }

        ImGui.EndChild();

        BugReportFooter();

        ImGui.EndTabItem();
    }

    /// <summary>
    /// Two ways in, so nobody gets stuck: the form needs no account, GitHub is better for
    /// anyone who wants to attach screenshots or logs and follow the fix.
    /// </summary>
    private static void BugReportFooter()
    {
        var version = Plugin.PluginInterface.Manifest.AssemblyVersion.ToString();

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2.0f);

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Found a bug?");
        ImGui.SameLine();
        ImGui.TextDisabled($"You're on v{version} \u2014 pop that in your report.");

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Bug, "Report a bug"))
            Util.OpenLink(BugFormUrl);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Quick form, no account needed.\n{BugFormUrl}");

        ImGui.SameLine();

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkAlt, "Report on GitHub"))
            Util.OpenLink(NewIssueUrl(version));

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Needs a GitHub account. Best if you want to attach screenshots or logs,\nor follow along with the fix.\n{IssuesUrl}");
    }

    /// <summary>Opens a new issue with the version and a short template already filled in.</summary>
    private static string NewIssueUrl(string version)
    {
        var body = $"**Plugin version:** {version}\n\n" +
                   "**What happened?**\n\n\n" +
                   "**What should have happened?**\n\n\n" +
                   "**Steps to reproduce** (table mode, which buttons, anything the Undo tooltip said)\n\n";

        return $"{IssuesUrl}/new?body={Uri.EscapeDataString(body)}";
    }
}
