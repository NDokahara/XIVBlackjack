using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using XIVBlackjack.Data;

namespace XIVBlackjack.Windows.Main;

public partial class MainWindow
{
    private int NewBankDeposit = 1_000_000;
    private readonly Dictionary<string, int> AdjustAmounts = new();

    private void BankingTab()
    {
        if (!ImGui.BeginTabItem("Banking"))
            return;

        var banks = Plugin.Configuration.Banks;

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextWrapped("A player with a bank settles against their balance instead of trading \u2014 " +
                          "wins credit it, losses debit it. Gil only moves when they deposit or cash out. " +
                          "Their bet each round is set on the Table tab as usual.");
        ImGuiHelpers.ScaledDummy(5.0f);

        AddBankPanel(banks);

        ImGuiHelpers.ScaledDummy(8.0f);

        if (!banks.Any())
        {
            ImGui.TextColored(Helper.Yellow, "No accounts open.");
            ImGui.EndTabItem();
            return;
        }

        BankTable(banks);

        ImGuiHelpers.ScaledDummy(8.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4.0f);

        var total = banks.Values.Sum(b => b.Balance);
        ImGui.Text($"Holding {total:N0} across {banks.Count} account(s).");

        ImGui.SameLine(ImGui.GetWindowWidth() - 110.0f);
        ImGui.PushStyleColor(ImGuiCol.Button, Helper.Red);
        if (ImGui.Button("End Night"))
            ImGui.OpenPopup("##EndNightConfirm");
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Closes every account. Cash everyone out first.");

        EndNightPopup(banks);

        ImGui.EndTabItem();
    }

    private void AddBankPanel(Dictionary<string, BankAccount> banks)
    {
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Open an account:");

        var target = Plugin.GetTargetName();
        var hasTarget = target != string.Empty;
        var already = hasTarget && banks.ContainsKey(target);

        ImGui.PushItemWidth(120f);
        ImGui.InputInt("Deposit##NewBankDeposit", ref NewBankDeposit, 0);
        ImGui.PopItemWidth();

        NewBankDeposit = Math.Max(0, NewBankDeposit);

        if (!hasTarget)
        {
            ImGui.TextColored(Helper.Yellow, "Target a player to open an account for them.");
            return;
        }

        var flavour = target.Replace("\uE05D", "\uE05D ").Split("\uE05D ").First();

        if (already)
        {
            ImGui.TextColored(Helper.Yellow, $"{flavour} already has an account.");
            return;
        }

        if (!ImGui.Button($"Open account for {flavour}"))
            return;

        banks[target] = new BankAccount
        {
            Name = target,
            Balance = NewBankDeposit,
            Deposited = NewBankDeposit
        };

        Plugin.Configuration.Save();
    }

    private void BankTable(Dictionary<string, BankAccount> banks)
    {
        if (!ImGui.BeginTable("##BankTable", 5))
            return;

        ImGui.TableSetupColumn("Name", 0, 0.55f);
        ImGui.TableSetupColumn("Bank", 0, 0.45f);
        ImGui.TableSetupColumn("Deposited", 0, 0.45f);
        ImGui.TableSetupColumn("Tonight", 0, 0.4f);
        ImGui.TableSetupColumn("Adjust", 0, 0.8f);
        ImGui.TableHeadersRow();

        string? remove = null;

        foreach (var (key, account) in banks)
        {
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            var seated = Plugin.Blackjack.Players.Any(p => p.Name == key);
            if (seated)
            {
                ImGui.TextColored(Helper.Green, account.DisplayName);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Seated at the table right now.");
            }
            else
            {
                ImGui.Text(account.DisplayName);
            }

            ImGui.TableNextColumn();
            if (account.Balance < 0)
                ImGui.TextColored(Helper.Red, $"{account.Balance:N0}");
            else
                ImGui.Text($"{account.Balance:N0}");

            ImGui.TableNextColumn();
            ImGui.Text($"{account.Deposited:N0}");

            ImGui.TableNextColumn();
            if (account.WonLost > 0)
                ImGui.TextColored(Helper.Green, $"+{account.WonLost:N0}");
            else if (account.WonLost < 0)
                ImGui.TextColored(Helper.Red, $"{account.WonLost:N0}");
            else
                ImGui.Text("0");

            ImGui.TableNextColumn();
            AdjustAmounts.TryAdd(key, 250_000);
            var amount = AdjustAmounts[key];

            ImGui.PushItemWidth(90f);
            if (ImGui.InputInt($"##adj{key}", ref amount, 0))
                AdjustAmounts[key] = Math.Max(0, amount);
            ImGui.PopItemWidth();

            ImGui.SameLine();
            if (ImGui.SmallButton($"In##{key}"))
            {
                account.Balance += AdjustAmounts[key];
                account.Deposited += AdjustAmounts[key];
                Plugin.Configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("They traded gil in. Credits the account.\n\nAlso how to fix a cash-out entered too large.");

            ImGui.SameLine();
            if (ImGui.SmallButton($"Out##{key}"))
            {
                var take = Math.Min(AdjustAmounts[key], account.Balance);
                account.Balance -= take;
                account.Deposited -= take;
                Plugin.Configuration.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("You traded gil back. Debits the account.\n\nThe table's Undo doesn't reverse this \u2014 use In to correct it.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton($"##close{key}", FontAwesomeIcon.Trash) && ImGui.GetIO().KeyShift)
                remove = key;

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(account.Balance > 0
                    ? $"Hold Shift and click to close. {account.Balance:N0} still on the books \u2014 cash them out first."
                    : "Hold Shift and click to close this account.");
        }

        ImGui.EndTable();

        if (remove is null)
            return;

        banks.Remove(remove);
        Plugin.Configuration.Save();
    }

    private void EndNightPopup(Dictionary<string, BankAccount> banks)
    {
        if (!ImGui.BeginPopup("##EndNightConfirm"))
            return;

        var outstanding = banks.Values.Where(b => b.Balance > 0).ToArray();

        if (outstanding.Any())
        {
            ImGui.TextColored(Helper.Red, $"{outstanding.Length} account(s) still hold gil:");
            foreach (var account in outstanding)
                ImGui.Text($"  {account.DisplayName}: {account.Balance:N0}");

            ImGuiHelpers.ScaledDummy(4.0f);
            ImGui.TextWrapped("Closing now writes those balances off. Cash them out first unless you mean to.");
            ImGuiHelpers.ScaledDummy(4.0f);
        }
        else
        {
            ImGui.Text("All accounts are empty. Safe to close.");
            ImGuiHelpers.ScaledDummy(4.0f);
        }

        if (ImGui.Button("Close all accounts"))
        {
            banks.Clear();
            Plugin.Configuration.Save();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }
}
