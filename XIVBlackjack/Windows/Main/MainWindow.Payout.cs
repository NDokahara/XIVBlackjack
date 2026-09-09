using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using XIVBlackjack.Data;

namespace XIVBlackjack.Windows.Main;

public partial class MainWindow
{
    private int NewTip = 100_000;

    private void PayoutTab()
    {
        if (!ImGui.BeginTabItem("Payout"))
            return;

        var config = Plugin.Configuration;
        var result = PayoutManager.Calculate(config);
        var changed = false;

        ImGuiHelpers.ScaledDummy(5.0f);

        var mode = (int)config.PayoutMode;
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Bankroll:");
        ImGui.SameLine();
        changed |= ImGui.RadioButton("Mine", ref mode, (int)BankrollMode.SelfBankroll);
        ImGui.SameLine();
        changed |= ImGui.RadioButton("Venue's", ref mode, (int)BankrollMode.VenueBankroll);
        config.PayoutMode = (BankrollMode)mode;

        ImGuiHelpers.ScaledDummy(5.0f);
        changed |= WalletPanel(config, result);

        ImGuiHelpers.ScaledDummy(8.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4.0f);

        if (config.PayoutMode is BankrollMode.SelfBankroll)
            changed |= SelfBankrollPanel(config, result);
        else
            changed |= VenueBankrollPanel(config, result);

        if (changed)
            config.Save();

        ImGui.EndTabItem();
    }

    private bool WalletPanel(Configuration config, PayoutResult result)
    {
        var changed = false;

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Wallet:");
        ImGui.Indent(10.0f);

        changed |= GilInput("Starting Gil", ref config.StartingGil);
        changed |= GilInput("Ending Gil", ref config.EndingGil);

        ImGuiHelpers.ScaledDummy(3.0f);
        ImGui.Text($"Tips: {result.TipsTotal:N0}");
        ImGuiComponents.HelpMarker("Tips are added to the starting figure rather than to profit, " +
                                   "so no percentage cut is ever taken on them.");

        ImGui.PushItemWidth(120f);
        ImGui.InputInt("##NewTip", ref NewTip, 0);
        ImGui.PopItemWidth();
        ImGui.SameLine();

        if (ImGui.Button("Add Tip") && NewTip > 0)
        {
            config.Tips.Add(NewTip);
            changed = true;
        }

        if (config.Tips.Any())
        {
            ImGui.SameLine();
            if (ImGui.Button("Clear Tips"))
            {
                config.Tips.Clear();
                changed = true;
            }

            ImGui.TextDisabled(string.Join(" + ", config.Tips.Select(t => $"{t:N0}")));
        }

        ImGui.Unindent(10.0f);
        return changed;
    }

    private static bool SelfBankrollPanel(Configuration config, PayoutResult result)
    {
        var changed = false;

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Self-Bankroll");
        ImGui.Indent(10.0f);

        var percent = (float)config.VenuePercent;
        ImGui.PushItemWidth(100f);
        if (ImGui.InputFloat("Venue %##VenuePercent", ref percent, 0, 0, "%.0f"))
        {
            config.VenuePercent = Math.Clamp(percent, 0, 100);
            changed = true;
        }
        ImGui.PopItemWidth();

        ImGuiHelpers.ScaledDummy(4.0f);

        Line("Base (start + tips)", result.Base);
        Line("Gross profit", result.GrossProfit, colour: true);
        Line("Owed to venue", result.VenueCut, emphasis: true);
        Line("To pocket", result.ToPocket, colour: true);
        Line("Pocket total", result.PocketTotal, emphasis: true);

        ImGuiHelpers.ScaledDummy(6.0f);
        CopyButton("Copy ledger line", PayoutManager.SelfLedgerStatement(config, result));
        ImGui.SameLine();
        CopyButton("Copy venue line", PayoutManager.SelfVenueStatement(config, result));

        ImGui.Unindent(10.0f);
        return changed;
    }

    private static bool VenueBankrollPanel(Configuration config, PayoutResult result)
    {
        var changed = false;

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Venue-Bankroll");
        ImGui.Indent(10.0f);

        var percent = (float)config.DealerPercent;
        ImGui.PushItemWidth(100f);
        if (ImGui.InputFloat("Dealer %##DealerPercent", ref percent, 0, 0, "%.0f"))
        {
            config.DealerPercent = Math.Clamp(percent, 0, 100);
            changed = true;
        }
        ImGui.PopItemWidth();

        changed |= GilInput("Venue float", ref config.VenueFloat);
        changed |= GilInput("Venue paycheck", ref config.VenuePaycheck);

        ImGuiHelpers.ScaledDummy(4.0f);

        Line("Personal gil (start + tips)", result.PersonalGil);
        Line("Starting total", result.StartingTotal);
        Line("Gross profit", result.Difference, colour: true);
        Line("Dealer cut", result.DealerCut);
        Line("Venue owes dealer", result.VenueOwesDealer, emphasis: true);
        Line("Personal gil at end", result.PersonalGilEnd);
        Line(result.TradeToVenue < 0 ? "Venue owes you" : "Trade to venue",
            Math.Abs(result.TradeToVenue), emphasis: true);
        Line("Venue profit", result.VenueProfit, colour: true);

        ImGuiHelpers.ScaledDummy(6.0f);
        CopyButton("Copy cut line", PayoutManager.VenueCutStatement(config, result));
        ImGui.SameLine();
        CopyButton("Copy settle line", PayoutManager.VenueSettleStatement(config, result));

        ImGui.Unindent(10.0f);
        return changed;
    }

    private static bool GilInput(string label, ref long field)
    {
        var value = (int)Math.Clamp(field, 0, int.MaxValue);

        ImGui.PushItemWidth(140f);
        var edited = ImGui.InputInt($"{label}##{label}", ref value, 0);
        ImGui.PopItemWidth();

        if (!edited)
            return false;

        field = Math.Max(0, value);
        return true;
    }

    private static void Line(string label, long value, bool colour = false, bool emphasis = false)
    {
        ImGui.Text($"{label}:");
        ImGui.SameLine(220.0f);

        if (colour && value < 0)
            ImGui.TextColored(Helper.Red, $"{value:N0}");
        else if (colour && value > 0)
            ImGui.TextColored(Helper.Green, $"+{value:N0}");
        else if (emphasis)
            ImGui.TextColored(Helper.Yellow, $"{value:N0}");
        else
            ImGui.Text($"{value:N0}");
    }

    private static void CopyButton(string label, string text)
    {
        if (ImGui.Button(label))
            ImGui.SetClipboardText(text);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }
}
