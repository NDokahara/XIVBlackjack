using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using XIVBlackjack.Data;

namespace XIVBlackjack.Windows.Config;

public partial class ConfigWindow
{
    private const string DealerHitMsg = "Hard = Dealer stays at number" +
                                        "\nHard17 Example:" +
                                        "\n- Hand: Ace (11) + 6 Value: 17" +
                                        "\n- Dealer has 17 and must stay" +
                                        "\n\nSoft = Dealer hits with ace" +
                                        "\nSoft17 Example:" +
                                        "\n- Hand: Ace (11) + 6 Value: 17" +
                                        "\n- Draws 3" +
                                        "\n- Hand: Ace (11) + 6 + 3 Value: 20" +
                                        "\n- Dealer has 20 and must stay" +
                                        "\n\nAlternative Soft16 Example:" +
                                        "\n- Hand: Ace (11) + 5 Value: 16" +
                                        "\n- Draws Queen" +
                                        "\n- Hand: Ace (1) + 5 + Queen Value: 16" +
                                        "\n- Dealer has hard 16 and must stay";

    private const string DealerVenueMsg = "Venue:" +
                                          "\nModifies the game into a format suitable for a public venue, " +
                                          "this will force all players and dealer to roll for cards - but in a different " +
                                          "order to preserve the 'Hidden Card' status.";

    private void Blackjack(ref bool changed)
    {
        var current = Configuration.BlackjackMode;
        ImGui.RadioButton("Normal", ref current, 0);
        ImGui.SameLine();
        ImGui.RadioButton("Venue", ref current, 1);
        ImGuiComponents.HelpMarker(DealerVenueMsg);

        if (current != Configuration.BlackjackMode)
        {
            changed = true;
            Configuration.BlackjackMode = current;
            switch (current)
            {
                case 0:
                    Configuration.AutoDrawCard = Configuration.AutoDrawOpening = Configuration.AutoDrawDealer = true;
                    Configuration.VenueDealer = false;
                    break;
                case 1:
                    Configuration.AutoDrawCard = Configuration.AutoDrawOpening = Configuration.AutoDrawDealer = false;
                    Configuration.VenueDealer = true;
                    break;
            }
        }


        changed |= ImGui.Checkbox("Draw Both On Start", ref Configuration.StartingDraw);
        ImGuiComponents.HelpMarker("This changes the starting behaviour from single draws into both draws at once.");

        if (Configuration.BlackjackMode == 0)
        {
            changed |= ImGui.Checkbox("Automate Player Draws", ref Configuration.AutoDrawCard);
            ImGuiComponents.HelpMarker("Automatically draw cards for players on hit, double down and split.");

            changed |= ImGui.Checkbox("Automate Start Draws", ref Configuration.AutoDrawOpening);
            ImGuiComponents.HelpMarker("Automatically draw the first two cards for all players.");

            changed |= ImGui.Checkbox("Automate Dealer Draws", ref Configuration.AutoDrawDealer);
            ImGuiComponents.HelpMarker("Automatically draw all dealer cards (first two cards are excluded).");

            if (changed)
                Configuration.VenueDealer = false;
        }
        else
        {
            changed |= ImGui.Checkbox("Dealer Draws All Cards", ref Configuration.DealerDrawsAll);

        changed |= ImGui.Checkbox("Read rolls from chat", ref Configuration.ReadRollsFromChat);
        ImGuiComponents.HelpMarker("Reads rolls out of the chat line instead of relying on the game-function hooks. Needed when a hook's signature goes stale after a patch. Duplicates are filtered, so it is safe to leave on.");

        changed |= ImGui.Checkbox("Buttons roll for you", ref Configuration.ButtonsRoll);
        ImGuiComponents.HelpMarker("Hit sends the roll command itself, and a Roll button appears whenever the plugin is waiting on a card.");

        changed |= ImGui.Checkbox("Open a trade on double down and split", ref Configuration.CollectOnDoubleSplit);
        ImGuiComponents.HelpMarker("Both put a second stake up, so the trade window opens first. Roll once it closes.");

        var useDice = Configuration.UseDiceCommand ? 0 : 1;
        ImGui.Text("Roll with:");
        ImGui.SameLine();
        if (ImGui.RadioButton("/dice 13", ref useDice, 0))
        {
            Configuration.UseDiceCommand = true;
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("/random 13", ref useDice, 1))
        {
            Configuration.UseDiceCommand = false;
            changed = true;
        }
        ImGuiComponents.HelpMarker("/dice stays inside the party, alliance or linkshell the table is run in. /random broadcasts to everyone nearby.");
            ImGuiComponents.HelpMarker("Dealer draws all cards instead of players. Faster gameplay!");

            if (changed)
                Configuration.VenueDealer = true;
        }

        ImGuiHelpers.ScaledDummy(5.0f);



        var dealerRule = (int) Configuration.DealerRule;
        var rules = RuleUtils.ListOfNames;
        ImGui.PushItemWidth(120f);
        if (ImGui.Combo("##DealerRulesCombo", ref dealerRule, rules, rules.Length))
        {
            changed = true;
            Configuration.DealerRule = (DealerRules) dealerRule;
        }
        ImGui.SameLine();
        ImGui.Text("Dealer Rule");
        ImGuiComponents.HelpMarker(DealerHitMsg);
    }
}