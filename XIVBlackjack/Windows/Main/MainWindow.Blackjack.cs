using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using XIVBlackjack.Data;

namespace XIVBlackjack.Windows.Main;

public partial class MainWindow
{
    private const string HelpText = "- Hit: Player draws a card," +
                                    "\n- Stay: Player holds hand as it is" +
                                    "\n- Surrender: Player drops out of round and loses half the bet" +
                                    "\n- Double Down: Player bets double, but receives exactly one more card" +
                                    "\n- Split: Only possible at round start, and if the player has same Rank cards (e.g K and K)" +
                                    "\n    > Player opens a new hand with one card in each hand, puts another bet of same amount, and draws with both hands a card" +
                                    "\n    > Round continues as before, with the split hands turn happening later";

    private void BlackjackMode()
    {
        BlackjackControlPanel();
        ImGuiHelpers.ScaledDummy(5.0f);

        switch (Plugin.State)
        {
            case GameState.Registration:
                BlackjackRegistrationPanel();
                break;
            case GameState.DealerFirstCards or GameState.DealerSecondCards:
                DealerStartingDraw();
                break;
            case GameState.PrepareRound:
                MatchBeginningPanel();
                break;
            case GameState.DrawFirstCards or GameState.DrawSecondCards:
                MatchBeginDraw();
                break;
            case GameState.PlayerRound:
                PlayerRoundPanel();
                break;
            case GameState.Hit or GameState.DoubleDown or GameState.DrawSplit or GameState.FillDraw:
                WaitForRollPanel();
                break;
            case GameState.DealerRound:
                DealerRoundPanel();
                break;
            case GameState.DrawDealerCard:
                DealerDrawRender();
                break;
            case GameState.DealerDone:
                DealerDonePanel();
                break;
            case GameState.Done:
                MatchDonePanel();
                break;
        }

        switch (Plugin.State)
        {
            case GameState.PlayerRound:
            case GameState.DealerRound:
            case GameState.DealerDone:
            case GameState.Done:
                CardDeckRender();
                break;
        }
    }

    private void BlackjackControlPanel()
    {
        // Round control sits top-left, where the eye lands first and where it gets clicked
        // most. Settings drops to the row below it.
        RoundButton();

        ImGui.SameLine();
        UndoButton();

        if (ImGui.Button("Show Settings"))
            Plugin.OpenConfig();

        KofiButton();

        HookWarning();

        RollButton();
    }

    /// <summary>
    /// Only shown between rounds. FontAwesome Free has no Ko-fi glyph, so Coffee stands in —
    /// which is what most Dalamud plugins settle on.
    /// </summary>
    private void KofiButton()
    {
        if (Plugin.State is not GameState.NotRunning)
            return;

        ImGui.PushStyleColor(ImGuiCol.Text, Helper.White);

        var pressed = ImGuiComponents.IconButtonWithText(
            FontAwesomeIcon.Coffee,
            "Support on Ko-fi",
            Helper.KofiRed,
            Helper.KofiRedActive,
            Helper.KofiRedHover);

        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("ko-fi.com/nanodokahara");

        if (pressed)
            Dalamud.Utility.Util.OpenLink("https://ko-fi.com/nanodokahara");
    }

    private void RoundButton()
    {
        switch (Plugin.State)
        {
            case GameState.NotRunning:
                if (!ImGui.Button("Start Round"))
                    return;

                Plugin.Blackjack.Reset();
                Plugin.SwitchState(GameState.Registration);
                return;

            case GameState.Crash:
                ImGui.PushStyleColor(ImGuiCol.Button, Helper.Red);
                var forced = ImGui.Button("Force Stop Round");
                ImGui.PopStyleColor();

                if (!forced)
                    return;

                Plugin.Blackjack.Reset();
                Plugin.SwitchState(GameState.NotRunning);
                return;

            default:
                if (!ImGui.Button("Stop Round"))
                    return;

                Plugin.Blackjack.Reset();
                Plugin.SwitchState(GameState.NotRunning);
                return;
        }
    }

    /// <summary>
    /// Writes the round's result into each banked player's balance. Guarded so it can only
    /// land once per round — applying it automatically when the round resolves would fight
    /// with Undo and could double-credit.
    /// </summary>
    private void ApplyToBanksPanel()
    {
        var banks = Plugin.Configuration.Banks;

        var banked = Plugin.Blackjack.Players
            .Where(Blackjack.IsSettlementRow)
            .Where(p => banks.ContainsKey(p.Name))
            .ToArray();

        if (!banked.Any())
            return;

        ImGuiHelpers.ScaledDummy(4.0f);

        if (Plugin.Blackjack.BanksApplied)
        {
            ImGui.TextColored(Helper.Green, $"Banked for {banked.Length} player(s).");
            return;
        }

        var net = banked.Sum(p => (long)Plugin.Blackjack.TotalWinnings(p));

        if (ImGui.Button("Apply to Banks"))
        {
            foreach (var player in banked)
            {
                var account = banks[player.Name];
                var delta = (long)Plugin.Blackjack.TotalWinnings(player);

                account.Balance += delta;
                account.WonLost += delta;
            }

            Plugin.Blackjack.BanksApplied = true;
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        if (net > 0)
            ImGui.TextColored(Helper.Green, $"+{net:N0} to {banked.Length} account(s)");
        else if (net < 0)
            ImGui.TextColored(Helper.Red, $"{net:N0} from {banked.Length} account(s)");
        else
            ImGui.Text($"no change across {banked.Length} account(s)");
    }

    private void UndoButton()
    {
        if (!Plugin.Blackjack.CanUndo)
            return;

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, Helper.Red);
        var pressed = ImGui.Button("Undo");
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Undo: {Plugin.Blackjack.LastUndoLabel}\n{Plugin.Blackjack.UndoDepth} step(s) available");

        if (pressed)
            Plugin.Blackjack.Undo();
    }

    private void MatchDonePanel()
    {
        ImGui.TextColored(Helper.Green, $"Round finished.");
        ImGui.TextColored(Helper.Yellow, $"All bets are adjusted to the correct amount for payouts.");

        if (ImGui.Button("Play Again"))
            Plugin.Blackjack.TakePeopleIntoNextRound();

        ImGui.SameLine();

        // Copy out all winnings to a single string to show all players the final standings (and leave a record if a player wants to play their push/winnings into the next round)
        if (ImGui.Button("Copy Payout"))
        {
            var finalPayout = "Payouts: | ";
            foreach (var player in Plugin.Blackjack.Players)
            {
                if (!Blackjack.IsSettlementRow(player))
                    continue;

                if (Plugin.Configuration.Banks.ContainsKey(player.Name))
                    continue;

                var settlement = Plugin.Blackjack.SettlementFor(player);
                var text = settlement > 0
                    ? $"+{settlement:N0}"
                    : Plugin.Blackjack.NeedsRestakeAfter(player)
                        ? "lost"
                        : "even";
                finalPayout += $"{player.Name.Split()[0]} -> {text} | ";
            }
            ImGui.SetClipboardText(finalPayout);
        }

        // Let's allow 'Copy Dealer' everywhere so newbies can be shown the dealer's hand (especially if it resolves on the first two cards)
        ImGui.SameLine();
        DealerCardButton();
    }

    private void DealerDonePanel()
    {
        ImGui.TextColored(Helper.Yellow, $"The dealer is not allowed to hit anymore!");
        ImGui.TextColored(Helper.Yellow, $"Please proceed by pressing 'Calculate Winnings' below.");
        if (ImGui.Button("Calculate Winnings"))
            Plugin.Blackjack.EndMatch();

        DealerCardButton();
    }

    private void DealerCardButton()
    {
        if (ImGui.Button("Copy Dealer"))
        {
            var cards = Plugin.Blackjack.Dealer.CalculateCardValues();
            ImGui.SetClipboardText($"Dealer's Hand: {string.Join(" ", Plugin.Blackjack.Dealer.Cards.Select(Cards.ShowCardSimple))} -- Total: {cards}");
        }
    }

    private void DealerDrawRender()
    {
        if (!Plugin.Blackjack.IsDealerDone())
        {
            Plugin.Blackjack.DealerRound();
            if (Plugin.State == GameState.Done)
                return;

            Plugin.Blackjack.Dealer.LastAction = BlackjackActions.None;
            Plugin.SwitchState(GameState.DealerDone);
            return;
        }

        ImGui.TextColored(Helper.Green, "Waiting for dealer roll ...");
        ImGui.TextColored(Helper.Green, "Dealer must draw a card with /random 13 or /dice 13");
        ImGui.TextColored(Helper.Green, $"The cards have currently a value of {Plugin.Blackjack.Dealer.CalculateCardValues()}");

        ImGuiHelpers.ScaledDummy(5.0f);
        DealerCardButton();
    }

    private void DealerRoundPanel()
    {
        ImGui.TextColored(Helper.Yellow, "All players done!");
        if (ImGui.Button("Begin Dealer Round"))
            Plugin.Blackjack.DealerAction();

        ImGuiHelpers.ScaledDummy(5.0f);
        DealerCardButton();
    }

    private void WaitForRollPanel()
    {
        ImGui.TextColored(Helper.Green, $"Awaiting {Plugin.Blackjack.CurrentPlayer.DisplayName}'s roll ...");
        ImGui.TextColored(Helper.Green, $"{(Plugin.Configuration.DealerDrawsAll ? "Dealer" : "Player")} must draw a card with /random 13 or /dice 13");
    }

    private void PlayerRoundPanel()
    {
        var blackjack = Plugin.Blackjack;
        var player = blackjack.CurrentPlayer;
        var venue = Plugin.Configuration is { VenueDealer: true };
        var rolls = venue && Plugin.Configuration.ButtonsRoll;
        var collects = venue && Plugin.Configuration.CollectOnDoubleSplit;

        ImGui.TextColored(Helper.Yellow, $"Current Player: {player.DisplayName}");
        ImGui.Text("Player Options:");
        ImGuiComponents.HelpMarker(HelpText);

        if (ImGui.Button("Hit"))
        {
            blackjack.PushUndo($"{player.DisplayName} hit");
            Plugin.SwitchState(GameState.Hit);
            blackjack.PlayerAction();

            // One click: switch to the waiting state, then roll for the card.
            if (rolls)
                Plugin.SendRollCommand();
        }

        if (ImGui.Button("Stay"))
            blackjack.Stay();

        if (ImGui.Button("Surrender"))
            blackjack.Surrender();

        if (ImGui.Button("Double Down"))
        {
            blackjack.PushUndo($"{player.DisplayName} double down");
            Plugin.SwitchState(GameState.DoubleDown);
            blackjack.PlayerAction();

            // A double puts a second stake up, so the trade comes first and the roll waits
            // for the Roll button once the window has closed.
            if (collects)
                Plugin.TradeAutomation.BeginCollect(player);
            else if (rolls)
                Plugin.SendRollCommand();
        }

        if (!player.CanSplit)
            return;

        if (!ImGui.Button("Split"))
            return;

        blackjack.PushUndo($"{player.DisplayName} split");
        blackjack.Split();

        if (collects)
            Plugin.TradeAutomation.BeginCollect(player);
        else if (rolls)
            Plugin.SendRollCommand();
    }

    /// <summary>
    /// Shown whenever the plugin is sitting waiting on a card. One click sends one roll, so a
    /// split — which needs two — is two clicks rather than a hidden loop.
    /// </summary>
    /// <summary>
    /// A failed signature is otherwise invisible: rolls simply never arrive, which looks
    /// exactly like the plugin being switched off.
    /// </summary>
    private void HookWarning()
    {
        var dice = HookManager.DiceHookReady;
        var random = HookManager.RandomHookReady;

        if (dice && random)
            return;

        // A dead hook only matters if chat reading is also off — otherwise it is covered.
        if (Plugin.Configuration.ReadRollsFromChat)
            return;

        ImGuiHelpers.ScaledDummy(3.0f);

        var dead = !dice && !random ? "roll" : dice ? "/random" : "/dice";

        ImGui.TextColored(Helper.Red, $"The {dead} hook did not resolve after a game patch.");
        ImGui.TextColored(Helper.Yellow, "Turn on 'Read rolls from chat' in Settings \u2192 Blackjack Options.");
    }

    private void RollButton()
    {
        if (Plugin.Configuration is not { VenueDealer: true, ButtonsRoll: true })
            return;

        var waiting = Plugin.State is GameState.Hit or GameState.DoubleDown or GameState.DrawSplit
            or GameState.FillDraw or GameState.DrawFirstCards or GameState.DrawSecondCards
            or GameState.DealerFirstCards or GameState.DealerSecondCards or GameState.DrawDealerCard;

        if (!waiting)
            return;

        // Rolling mid-trade would land the card before the stake is collected.
        if (Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.TradeOpen])
        {
            ImGui.TextColored(Helper.Yellow, "Finish the trade, then roll.");
            return;
        }

        ImGuiHelpers.ScaledDummy(3.0f);

        ImGui.PushStyleColor(ImGuiCol.Button, Helper.SoftBlue);
        var pressed = ImGui.Button($"Roll {Plugin.Configuration.RollCommand}");
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Plugin.State is GameState.DrawSplit
                ? "Split needs two rolls: one for the new hand, one to refill the original."
                : "Sends the roll command. The plugin picks the result up as the next card.");

        if (pressed)
            Plugin.SendRollCommand();
    }

    private void CardDeckRender()
    {
        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var settled = Plugin.State is GameState.Done;

        if (ImGui.BeginTable("##BlackjackDeckTable", 5))
        {
            ImGui.TableSetupColumn("Name - (Click to Copy)", 0, 0.6f);
            ImGui.TableSetupColumn("Cards");
            ImGui.TableSetupColumn("Total", 0, 0.3f);
            ImGui.TableSetupColumn("Wagered", 0, 0.4f);
            ImGui.TableSetupColumn(settled ? "Result" : "Last Action", 0, 0.4f);

            ImGui.TableHeadersRow();
            foreach (var player in Plugin.Blackjack.Players)
            {
                var cards = player.CalculateCardValues();

                // Feature by request so a player's hand can always be copied out
                ImGui.TableNextColumn();
                ImGui.PushStyleColor(ImGuiCol.Button, Helper.SoftBlue);
                var pFlavor = $"{player.DisplayName.Split("\uE05D ").First()}";
                if (ImGui.Button($"{pFlavor}##{player.DisplayName}"))
                {
                    var participantsString = $"{pFlavor}'s hand: {string.Join(" ", player.Cards.Select(Cards.ShowCardSimple))} -- Total: {cards}";

                    var actions = Plugin.Blackjack.AvailableActions(player);
                    if (actions.Any())
                    {
                        var firstName = pFlavor.Split(' ').First();
                        participantsString += $" -- What would you like to do, {firstName}? {string.Join(", ", actions)}";
                    }

                    ImGui.SetClipboardText(participantsString);
                }

                // Must follow the button — IsItemHovered reads the item just submitted.
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Click to copy this hand, plus their options if it is their turn.");

                ImGui.PopStyleColor();

                ImGui.TableNextColumn();
                Plugin.FontManager.SourceCode20.Push();
                ImGui.Text(string.Join(" ", player.Cards.Select(Cards.ShowCardSimple)));
                Plugin.FontManager.SourceCode20.Pop();

                ImGui.TableNextColumn();
                ImGui.Text($"{cards}");

                ImGui.TableNextColumn();
                ImGui.Text($"{player.Stake:N0}");

                ImGui.TableNextColumn();
                ImGui.Text(settled ? player.Outcome.Name() : player.LastAction.Name());
            }

            ImGui.TableNextRow();
            ImGui.TableNextRow();

            var dcards = Plugin.Blackjack.Dealer.CalculateCardValues();

            ImGui.TableNextColumn();
            if (ImGui.Button($"Dealer"))
                ImGui.SetClipboardText($"Dealer's Hand: {string.Join(" ", Plugin.Blackjack.Dealer.Cards.Select(Cards.ShowCardSimple))}");

            ImGui.TableNextColumn();
            Plugin.FontManager.SourceCode20.Push();
            ImGui.Text(string.Join(" ", Plugin.Blackjack.Dealer.Cards.Select(Cards.ShowCardSimple)));
            Plugin.FontManager.SourceCode20.Pop();

            ImGui.TableNextColumn();
            ImGui.Text($"{dcards}");

            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            ImGui.Text(Plugin.Blackjack.Dealer.LastAction.Name());

            ImGui.EndTable();
        }

        if (settled)
            SettlementRender();
    }

    private void SettlementRender()
    {
        ImGuiHelpers.ScaledDummy(8.0f);
        ImGui.TextColored(Helper.Yellow, "Settlement:");

        if (!ImGui.BeginTable("##BlackjackSettlementTable", 7))
            return;

        ImGui.TableSetupColumn("Player", 0, 0.6f);
        ImGui.TableSetupColumn("Total Wagered", 0, 0.45f);
        ImGui.TableSetupColumn("Winnings", 0, 0.45f);
        ImGui.TableSetupColumn("Player Total", 0, 0.45f);
        ImGui.TableSetupColumn("Roll-over Bet", 0, 0.45f);
        ImGui.TableSetupColumn("Trade", 0, 0.55f);
        ImGui.TableSetupColumn("##Action", 0, 0.4f);
        ImGui.TableHeadersRow();

        var totalOut = 0;

        foreach (var player in Plugin.Blackjack.Players.Where(Blackjack.IsSettlementRow))
        {
            var wagered = Plugin.Blackjack.TotalWagered(player);
            var winnings = Plugin.Blackjack.TotalWinnings(player);
            var rollOver = Plugin.Blackjack.RollOverBet(player);
            var settlement = Plugin.Blackjack.SettlementFor(player);
            var hands = Plugin.Blackjack.HandsOf(player).Length;

            var pFlavor = $"{player.DisplayName.Split("\uE05D ").First()}";

            // A banked player settles against their balance, so they are excluded from the
            // trade figure, the action buttons and the night's totals alike.
            var isBanked = Plugin.Configuration.Banks.TryGetValue(player.Name, out var bank);

            ImGui.TableNextColumn();
            if (isBanked)
            {
                ImGui.TextColored(Helper.SoftBlue, hands > 1 ? $"{pFlavor} ({hands} hands)" : pFlavor);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Banked \u2014 settles against their balance.");
            }
            else
            {
                ImGui.Text(hands > 1 ? $"{pFlavor} ({hands} hands)" : pFlavor);
            }

            ImGui.TableNextColumn();
            ImGui.Text($"{wagered:N0}");
            if (ImGui.IsItemHovered() && wagered != rollOver)
                ImGui.SetTooltip($"Standing bet {rollOver:N0}, plus {wagered - rollOver:N0} put up for splits or doubles.");

            ImGui.TableNextColumn();
            if (winnings > 0)
                ImGui.TextColored(Helper.Green, $"+{winnings:N0}");
            else if (winnings < 0)
                ImGui.TextColored(Helper.Red, $"{winnings:N0}");
            else
                ImGui.Text("0");

            // Total Wagered + Winnings — everything the dealer is holding for this player.
            // Subtracting Roll-over Bet from it gives the Trade figure, so the row reads
            // left to right as the arithmetic actually performed.
            var held = wagered + winnings;

            ImGui.TableNextColumn();
            ImGui.Text($"{held:N0}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"What you are holding for {pFlavor}: {wagered:N0} wagered {(winnings < 0 ? "-" : "+")} {Math.Abs(winnings):N0} winnings.");

            // Nothing rolls over when the residual could not cover it — the player restakes,
            // so showing their old wager here would claim the dealer is holding gil they are not.
            var restaking = Plugin.Blackjack.NeedsRestakeAfter(player);

            ImGui.TableNextColumn();
            ImGui.Text($"{(restaking ? 0 : rollOver):N0}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(restaking
                    ? "Nothing held back \u2014 they need to restake next round."
                    : "Held back as their stake for the next round.");

            var chunks = Blackjack.SplitIntoTrades(settlement);

            ImGui.TableNextColumn();
            if (isBanked)
            {
                var after = Plugin.Blackjack.BanksApplied ? bank!.Balance : bank!.Balance + winnings;
                ImGui.TextColored(Helper.SoftBlue, $"bank \u2192 {after:N0}");
            }
            else if (settlement > 0)
            {
                totalOut += settlement;
                ImGui.TextColored(Helper.Green, chunks.Length > 1
                    ? $"trade {settlement:N0} ({chunks.Length} trades)"
                    : $"trade {settlement:N0}");
            }
            else
            {
                ImGui.Text("nothing");
            }

            if (ImGui.IsItemHovered())
            {
                if (isBanked)
                {
                    ImGui.SetTooltip(Plugin.Blackjack.BanksApplied
                        ? $"Already applied. Balance is {bank!.Balance:N0}."
                        : $"{bank!.Balance:N0} now, {bank.Balance + winnings:N0} once applied. Nothing to trade.");
                }
                else if (settlement > 0)
                {
                    var detail = chunks.Length > 1
                        ? $"\n\nOver the {Blackjack.TradeCap:N0} per-trade cap, so send:\n  {string.Join("\n  ", chunks.Select(c => $"{c:N0}"))}"
                        : "";
                    ImGui.SetTooltip($"{held:N0} held, minus {rollOver:N0} roll-over = {settlement:N0} to {pFlavor}.{detail}");
                }
                else
                {
                    ImGui.SetTooltip(settlement < 0
                        ? $"Nothing to trade. {pFlavor} has no stake left and will need to buy back in next round."
                        : "Their stake carries straight into the next round.");
                }
            }

            ImGui.TableNextColumn();

            if (isBanked)
            {
                // Nothing to send; Apply to Banks below handles them.
            }
            else if (settlement > 0)
            {
                var sent = Math.Clamp(player.TradesCompleted, 0, chunks.Length);
                var allSent = sent >= chunks.Length;

                if (allSent)
                {
                    ImGui.TextColored(Helper.Green, chunks.Length > 1 ? $"all {chunks.Length} sent" : "sent");
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"undo##settle{player.Name}"))
                        player.TradesCompleted = 0;

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Reset this player's trade progress.");
                }
                else
                {
                    var amount = chunks[sent];
                    var label = chunks.Length > 1 ? $"Target ({sent + 1}/{chunks.Length})" : "Target";

                    if (ImGui.SmallButton($"{label}##settle{player.Name}"))
                    {
                        if (Plugin.TargetPlayerByName(player.Name))
                            ImGui.SetClipboardText(amount.ToString());
                        else
                            Plugin.Notification.AddNotification(new Notification
                            {
                                Content = $"{pFlavor} is not nearby.",
                                Type = NotificationType.Warning
                            });
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"Targets {pFlavor} and copies {amount:N0} to the clipboard.\nOpen the trade yourself and paste.");

                    ImGui.SameLine();

                    var auto = Plugin.TradeAutomation;
                    var busy = auto.IsRunning;

                    if (busy)
                        ImGui.BeginDisabled();

                    // Queues every remaining chunk, so a payout over the cap runs as a chain
                    // off one click rather than one click per trade.
                    var remaining = chunks.Skip(sent).ToArray();
                    var autoLabel = remaining.Length > 1 ? $"Auto ({remaining.Length} trades)" : "Auto";

                    if (ImGui.SmallButton($"{autoLabel}##settle{player.Name}"))
                        auto.Begin(player, remaining);

                    if (busy)
                        ImGui.EndDisabled();

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"Targets {pFlavor}, opens the trade and fills in the amount.\n" +
                                         $"Runs {string.Join(" + ", remaining.Select(c => $"{c:N0}"))}.\n" +
                                         "You hit Trade; the next window opens once the last one closes.");

                    ImGui.SameLine();

                    // Advancing is a separate, deliberate click — a trade can be cancelled
                    // or declined, so sending is never assumed from targeting.
                    if (ImGui.SmallButton($"sent##settle{player.Name}"))
                        player.TradesCompleted = sent + 1;

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(chunks.Length > 1
                            ? $"Mark the {amount:N0} trade as sent and move to trade {sent + 2} of {chunks.Length}."
                            : $"Mark the {amount:N0} trade as sent.");
                }
            }
        }

        ImGui.EndTable();

        ApplyToBanksPanel();

        var autoState = Plugin.TradeAutomation;
        if (autoState.IsRunning || autoState.Step is TradeStep.Failed or TradeStep.Completed)
        {
            ImGuiHelpers.ScaledDummy(3.0f);

            var colour = autoState.Step switch
            {
                TradeStep.Failed => Helper.Red,
                TradeStep.Completed => Helper.Green,
                _ => Helper.Yellow
            };

            var text = autoState.Step is TradeStep.Failed or TradeStep.Completed
                ? autoState.StatusMessage
                : $"{autoState.Step}...";

            ImGui.TextColored(colour, $"Auto trade: {text}");

            if (autoState.IsRunning)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Cancel##autotrade"))
                    autoState.Cancel();
            }
        }

        if (totalOut > 0)
        {
            var tradable = Plugin.Blackjack.Players
                .Where(Blackjack.IsSettlementRow)
                .Where(p => !Plugin.Configuration.Banks.ContainsKey(p.Name))
                .ToArray();

            var tradeCount = tradable
                .Sum(p => Blackjack.SplitIntoTrades(Plugin.Blackjack.SettlementFor(p)).Length);

            var sentCount = tradable
                .Sum(p => Math.Clamp(p.TradesCompleted, 0, Blackjack.SplitIntoTrades(Plugin.Blackjack.SettlementFor(p)).Length));

            ImGuiHelpers.ScaledDummy(3.0f);
            ImGui.TextColored(sentCount >= tradeCount ? Helper.Green : Helper.Yellow,
                $"Trading out: {totalOut:N0} \u2014 {sentCount} of {tradeCount} trade(s) sent");
        }
    }

    private void MatchBeginDraw()
    {
        if (Plugin.Blackjack.CurrentPlayerIndex >= Plugin.Blackjack.Players.Count)
        {
            switch (Plugin.State)
            {
                case GameState.DrawFirstCards:
                    if (Plugin.Blackjack.Players.Last().Cards.Count >= 2)
                    {
                        Plugin.Blackjack.FinishDrawingRound(GameState.PrepareRound);
                        Plugin.Blackjack.PreparePlayers();
                        return;
                    }

                    Plugin.Blackjack.FinishDrawingRound(GameState.DrawSecondCards);
                    return;
                case GameState.DrawSecondCards:
                    Plugin.Blackjack.FinishDrawingRound(GameState.PrepareRound);
                    Plugin.Blackjack.PreparePlayers();
                    return;
            }
        }


        ImGui.TextWrapped($"To begin the round, players must draw their cards with either /random 13 or /dice 13 respectively.");
        if (Plugin.Configuration.DealerDrawsAll)
            ImGui.TextUnformatted($"Dealer draws all");

        ImGui.TextUnformatted("The draw order is:");
        foreach (var (player, idx) in Plugin.Blackjack.Players.Select((var, i) => (var, i)))
        {
            if (Plugin.Blackjack.CurrentPlayerIndex == idx)
                ImGui.TextColored(Helper.Green, $"{player.DisplayName}");
            else
                ImGui.TextUnformatted($"{player.DisplayName}");

            Plugin.FontManager.SourceCode20.Push();
            ImGui.SameLine();
            ImGui.TextUnformatted($"{(player.Cards.Count > 0 ? Cards.ShowCardSimple(player.Cards[0]) : "?")} {(player.Cards.Count > 1 ? Cards.ShowCardSimple(player.Cards[1]) : "?")}");
            Plugin.FontManager.SourceCode20.Pop();
        }
    }

    private void MatchBeginningPanel()
    {
        Plugin.Blackjack.StartRound();
        Plugin.Blackjack.PreparePlayers();
    }

    private void DealerStartingDraw()
    {
        switch (Plugin.State)
        {
            case GameState.DealerFirstCards when Plugin.Blackjack.Dealer.Cards.Any():
                Plugin.Blackjack.SetDrawingRound();
                break;
            case GameState.DealerSecondCards when Plugin.Blackjack.Dealer.Cards.Count == 2:
                Plugin.SwitchState(GameState.DealerRound);
                Plugin.Blackjack.CheckForRemainingPlayers();
                break;
        }

        ImGui.TextColored(Helper.Green, $"Waiting for dealer roll ...");
        ImGui.TextColored(Helper.Green, $"Dealer must draw a starting card with /random 13 or /dice 13");
    }

    private void BlackjackRegistrationPanel()
    {
        var unset = Plugin.Blackjack.Players.Count(p => p.Wager <= 0);

        if (Plugin.Blackjack.Players.Any())
        {
            if (ImGui.Button("Close Registration"))
            {
                if (Plugin.Configuration.VenueDealer)
                    Plugin.SwitchState(GameState.DealerFirstCards);
                else
                    Plugin.Blackjack.SetDrawingRound();

                return;
            }

            if (unset > 0)
            {
                ImGui.SameLine();
                ImGui.TextColored(Helper.Red, $"{unset} player(s) have no bet set");
            }

            var owing = Plugin.Blackjack.Players
                .Count(p => p.NeedsRestake && !Plugin.Configuration.Banks.ContainsKey(p.Name));
            if (owing > 0)
                ImGui.TextColored(Helper.Red, $"{owing} player(s) lost last round \u2014 collect their bet before starting");
        }

        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.TextColored(Helper.Green,$"Awaiting more players ...");
        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextWrapped("Target a player and press 'Add Target' to seat them, then set their bet below.");
        BlackjackAddTargetButton();
        TableBetRender();
    }

    private void BlackjackAddTargetButton()
    {
        ImGuiHelpers.ScaledDummy(5.0f);
        if (ImGui.Button("Add Target"))
        {
            var result = BlackjackTargetRegistration();
            if (result != string.Empty)
                Plugin.Notification.AddNotification(new Notification {Content = result, Type = NotificationType.Error});
        }
        ImGuiHelpers.ScaledDummy(10.0f);
    }

    private string BlackjackTargetRegistration()
    {
        var name = Plugin.GetTargetName();
        if (name == string.Empty)
            return "Target not found";

        if (Plugin.Blackjack.Players.Exists(p => p.Name == name))
            return "Target already registered";

        Plugin.Blackjack.Players.Add(new BlackjackPlayer(name, 0));
        return string.Empty;
    }

    private void TableBetRender()
    {
        if (!Plugin.Blackjack.Players.Any())
            return;

        ImGui.TextColored(Helper.Yellow, "Player Bets:");
        if (ImGui.BeginTable("##BlackjackBetTable", 3))
        {
            ImGui.TableSetupColumn("##Name", ImGuiTableColumnFlags.None, 0.65f);
            ImGui.TableSetupColumn("##Number", ImGuiTableColumnFlags.None, 0.3f);
            ImGui.TableSetupColumn("##Input", ImGuiTableColumnFlags.None, 0.5f);

            foreach (var (player, idx) in Plugin.Blackjack.Players.Select((var, i) => (var, i)))
            {
                var currentBet = player.Wager;
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();

                var banked = Plugin.Configuration.Banks.TryGetValue(player.Name, out var account);

                // A banked player never owes a restake — their balance carries the loss.
                var owes = player.NeedsRestake && !banked;

                if (owes)
                    ImGui.PushStyleColor(ImGuiCol.Text, Helper.Red);
                else if (banked)
                    ImGui.PushStyleColor(ImGuiCol.Text, Helper.SoftBlue);

                ImGui.Selectable($"{player.DisplayName}##Selectable{idx}");

                if (owes || banked)
                    ImGui.PopStyleColor();
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyShift)
                {
                    Plugin.Blackjack.Players.RemoveAt(idx);
                    break;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(banked
                        ? $"Banked \u2014 balance {account!.Balance:N0}. Wins and losses settle against it.\n\nHold Shift and right-click to delete."
                        : owes
                            ? "Lost last round \u2014 collect their bet before starting.\n\nHold Shift and right-click to delete."
                            : "Hold Shift and right-click to delete.");

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                if (currentBet <= 0)
                    ImGui.TextColored(Helper.Red, "no bet set");
                else if (owes)
                    ImGui.TextColored(Helper.Red, $"{currentBet:N0}  (collect)");
                else if (banked)
                    ImGui.TextColored(Helper.SoftBlue, $"{currentBet:N0}  (bank {account!.Balance:N0})");
                else
                    ImGui.Text($"{currentBet:N0}");

                ImGui.TableNextColumn();
                ImGui.PushItemWidth(100);
                ImGui.InputInt($"##playerBet{idx}", ref currentBet, 0);

                if (currentBet == player.Wager)
                    continue;

                // Registration only: the hand has not started, so the stake tracks the wager.
                player.Wager = Math.Max(0, currentBet);
                player.Stake = player.Wager;
            }

            ImGui.EndTable();
        }
    }
}