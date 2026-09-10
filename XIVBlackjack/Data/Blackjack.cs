using static XIVBlackjack.Data.Cards;

namespace XIVBlackjack.Data;

/// <summary>
/// How a hand finally resolved. This is the single source of truth for money:
/// nothing about a wager is ever mutated, the outcome is recorded once and every
/// figure is derived from it. Pending means the hand still needs comparing to
/// the dealer.
/// </summary>
public enum HandOutcome
{
    Pending,
    Win,
    Blackjack,
    Push,
    Loss,
    Bust,
    Surrender
}

/// <summary>What the player last DID. Display only — never drives payouts.</summary>
public enum BlackjackActions
{
    None,
    Hit,
    Bust,
    Stay,
    Split,
    Blackjack,
    Surrender,
    DoubleDown,
    FullHand
}

public class BlackjackPlayer
{
    public readonly string Name;
    public readonly string AnonName;
    public readonly bool IsSplit;

    /// <summary>
    /// Derived, never stored. A hand can be split whenever it is two cards of matching
    /// rank and still live — which means a split hand that draws another pair can split
    /// again, with no limit. Deriving it also makes it impossible for the flag to drift
    /// out of step with the cards actually on the table.
    /// </summary>
    public bool CanSplit => !HandClosed && Cards.Count == 2 && Cards[0].Rank == Cards[1].Rank;

    /// <summary>True once the hand stops taking cards (stand, bust, 21, surrender, double).</summary>
    public bool HandClosed;

    /// <summary>Display only. Never drives money.</summary>
    public BlackjackActions LastAction = BlackjackActions.None;

    /// <summary>How the hand resolved. The only thing that determines payout.</summary>
    public HandOutcome Outcome = HandOutcome.Pending;

    /// <summary>
    /// The player's standing bet. Set by the dealer at registration and carried
    /// into subsequent rounds. Never touched by game logic.
    /// </summary>
    public int Wager;

    /// <summary>
    /// Amount at risk on THIS hand — equals Wager, doubled if the player doubles down.
    /// The only figure that game logic modifies, and only in that one place.
    /// </summary>
    public int Stake;

    public readonly List<Card> Cards = new();

    /// <summary>
    /// Gil the player wins (+) or loses (-) on this hand. Derived, never stored.
    ///
    /// House rule on a push: only the standing wager comes back, so anything staked
    /// on top of it — the extra put up for a double down — is forfeited. For a hand
    /// that never doubled, Stake equals Wager and this is simply zero.
    /// </summary>
    public int Net => Outcome switch
    {
        HandOutcome.Win => Stake,
        HandOutcome.Blackjack => (int)(Stake * 1.5f),
        HandOutcome.Push => Wager - Stake,
        HandOutcome.Loss or HandOutcome.Bust => -Stake,
        HandOutcome.Surrender => -(Stake / 2),
        _ => 0
    };

    /// <summary>Total gil the dealer is holding for this hand once it resolves. Derived.</summary>
    public int Return => Stake + Net;

    /// <summary>
    /// A natural: 21 on the opening two cards. Only a natural pays 3:2 — a 21 built
    /// from three or more cards is an ordinary winning hand worth even money.
    /// </summary>
    public bool IsNatural => Cards.Count == 2 && CalculateCardValues() == 21;

    /// <summary>
    /// The name with any " Split" suffixes stripped. Identifies the PERSON rather
    /// than the hand — split hands settle together, since one player holds them both.
    /// </summary>
    public string RootName
    {
        get
        {
            var n = Name;
            while (n.EndsWith(" Split"))
                n = n[..^" Split".Length];

            return n;
        }
    }

    /// <summary>Still needs the dealer to play before it can be settled.</summary>
    /// <summary>
    /// True when the player ended last round with nothing left staked with the dealer,
    /// so a fresh bet has to be traded in before the next hand. Deliberately a flag and
    /// not an amount — the player is free to come back at a different bet.
    /// </summary>
    public bool NeedsRestake;

    /// <summary>
    /// How many of this round's trades have been sent. Lets the dealer work through a
    /// payout that exceeds the per-trade cap without losing their place. Transient —
    /// resets when the next round's players are created.
    /// </summary>
    public int TradesCompleted;

    public bool NeedsDealer => Outcome is HandOutcome.Pending or HandOutcome.Blackjack;

    public string DisplayName => !DebugConfig.RandomizeNames ? Name.Replace("\uE05D", "\uE05D ") : AnonName;

    public BlackjackPlayer(string name, int bet)
    {
        Wager = bet;
        Stake = bet;
        Name = name;
        AnonName = Utils.GenerateHashedName(name);
    }

    public BlackjackPlayer(BlackjackPlayer player)
    {
        Wager = player.Wager;
        Stake = player.Stake;
        NeedsRestake = player.NeedsRestake;
        Name = $"{player.Name} Split";
        AnonName = $"{player.AnonName} Split";

        IsSplit = true;
    }

    /// <summary>Private copy constructor used only by Clone(), preserving identity exactly.</summary>
    private BlackjackPlayer(BlackjackPlayer source, bool _)
    {
        Name = source.Name;
        AnonName = source.AnonName;
        IsSplit = source.IsSplit;

        HandClosed = source.HandClosed;
        LastAction = source.LastAction;
        Outcome = source.Outcome;
        Wager = source.Wager;
        Stake = source.Stake;
        NeedsRestake = source.NeedsRestake;
        TradesCompleted = source.TradesCompleted;

        Cards.AddRange(source.Cards.Select(c => c.Clone()));
    }

    public BlackjackPlayer Clone() => new(this, true);

    public int CalculateCardValues()
    {
        var cards = 0;
        var hasAce = false;
        foreach (var card in Cards.Where(c => !c.IsHidden))
        {
            cards += card.Value;
            if (card.IsAce)
                hasAce = true;
        }

        return !hasAce ? cards : cards - 1 < 11 ? cards + 10 : cards;
    }
}

public class Blackjack
{
    private readonly Plugin Plugin;

    public BlackjackPlayer Dealer = new("Dealer", 0);
    public List<BlackjackPlayer> Players = new();
    public int CurrentPlayerIndex;

    public BlackjackPlayer CurrentPlayer => Players[CurrentPlayerIndex];

    #region Settlement

    /// <summary>Every hand belonging to the same person, split hands included.</summary>
    public BlackjackPlayer[] HandsOf(BlackjackPlayer player)
    {
        var root = player.RootName;
        return Players.Where(p => p.RootName == root).ToArray();
    }

    /// <summary>
    /// What the dealer is still holding for this person once the hand has resolved:
    /// everything they staked, plus or minus what the hand did. Never negative — the
    /// most a hand can cost is what was put up for it.
    /// </summary>
    public int ResidualFor(BlackjackPlayer player) => TotalWagered(player) + TotalWinnings(player);

    /// <summary>
    /// True when the residual will not cover next round's stake, so the player has to
    /// buy back in rather than roll over. A total loss is the ordinary case; a surrender
    /// also lands here, because half a wager is not a whole one.
    /// </summary>
    public bool NeedsRestakeAfter(BlackjackPlayer player) => ResidualFor(player) < RollOverBet(player);

    /// <summary>
    /// What actually gets traded to a PERSON at the end of a round. Never negative:
    /// the dealer is already holding the stakes, so settlement only ever hands gil back.
    ///
    /// Two cases. If the residual covers next round's stake, hold the stake back and
    /// trade out the rest. If it does not — a loss, or a surrender that left only half
    /// a wager — nothing can roll over, so the whole residual goes back and the player
    /// restakes. That second branch is what stops a surrender quietly keeping the half
    /// wager the player is still owed.
    /// </summary>
    public int SettlementFor(BlackjackPlayer player)
    {
        var residual = ResidualFor(player);
        var rollOver = RollOverBet(player);

        return residual >= rollOver ? residual - rollOver : residual;
    }

    /// <summary>Everything this person put up across all their hands, doubles included.</summary>
    public int TotalWagered(BlackjackPlayer player) => HandsOf(player).Sum(h => h.Stake);

    /// <summary>What they won (+) or lost (-) across all their hands.</summary>
    public int TotalWinnings(BlackjackPlayer player) => HandsOf(player).Sum(h => h.Net);

    /// <summary>The stake the dealer holds back for the next round — the standing wager.</summary>
    public int RollOverBet(BlackjackPlayer player) =>
        HandsOf(player).FirstOrDefault(h => !h.IsSplit)?.Wager ?? player.Wager;

    /// <summary>
    /// The moves open to a hand right now, in the order the buttons appear. Empty unless
    /// it is actually this player's turn — mirrors what the dealer can genuinely click,
    /// so the copied text never offers a player something the dealer cannot then do.
    /// </summary>
    public string[] AvailableActions(BlackjackPlayer player)
    {
        if (Plugin.State is not GameState.PlayerRound)
            return Array.Empty<string>();

        if (CurrentPlayerIndex >= Players.Count || !ReferenceEquals(player, CurrentPlayer))
            return Array.Empty<string>();

        if (player.HandClosed)
            return Array.Empty<string>();

        var actions = new List<string> { "Hit", "Stay", "Surrender", "Double Down" };
        if (player.CanSplit)
            actions.Add("Split");

        return actions.ToArray();
    }

    /// <summary>
    /// The game caps a direct gil trade at 1,000,000, so anything larger has to go across
    /// several trades. Returns the individual amounts, largest first.
    /// </summary>
    public const int TradeCap = 1_000_000;

    public static int[] SplitIntoTrades(int amount)
    {
        if (amount <= 0)
            return Array.Empty<int>();

        var chunks = new List<int>();
        while (amount > TradeCap)
        {
            chunks.Add(TradeCap);
            amount -= TradeCap;
        }

        chunks.Add(amount);
        return chunks.ToArray();
    }

    /// <summary>Split hands fold into their parent row, so only the parent shows a figure.</summary>
    public static bool IsSettlementRow(BlackjackPlayer player) => !player.IsSplit;

    #endregion

    #region Undo

    private const int MaxUndoDepth = 25;
    private readonly List<GameSnapshot> UndoStack = new();

    /// <summary>Set once the round's results have been written to player banks.</summary>
    public bool BanksApplied;

    public bool CanUndo => UndoStack.Count > 0;
    public int UndoDepth => UndoStack.Count;
    public string LastUndoLabel => CanUndo ? UndoStack[^1].Label : string.Empty;

    /// <summary>
    /// Capture the table before a mutating action. Call this BEFORE the mutation,
    /// with a label describing the action the dealer is about to take.
    /// </summary>
    public void PushUndo(string label)
    {
        UndoStack.Add(new GameSnapshot(label, Plugin.State, CurrentPlayerIndex, Dealer, Players, BanksApplied));

        if (UndoStack.Count > MaxUndoDepth)
            UndoStack.RemoveAt(0);
    }

    /// <summary>Roll the table back to the most recent snapshot.</summary>
    public void Undo()
    {
        if (!CanUndo)
            return;

        var snapshot = UndoStack.PopAt(UndoStack.Count - 1);

        Players = snapshot.Players.Select(p => p.Clone()).ToList();
        Dealer = snapshot.Dealer.Clone();
        CurrentPlayerIndex = snapshot.CurrentPlayerIndex;
        BanksApplied = snapshot.BanksApplied;

        Plugin.SwitchState(snapshot.State);
        Plugin.Log.Information($"Undid: {snapshot.Label}");
    }

    public void ClearUndo() => UndoStack.Clear();

    #endregion

    public Blackjack(Plugin plugin)
    {
        Plugin = plugin;
    }

    public void Reset()
    {
        BanksApplied = false;
        ClearUndo();
        Players.Clear();
        CurrentPlayerIndex = 0;
        Dealer = new BlackjackPlayer("Dealer", 0);
    }

    private void NextPlayer()
    {
        CurrentPlayerIndex++;
        if (CurrentPlayerIndex < Players.Count)
        {
            Plugin.SwitchState(GameState.PlayerRound);

            if (!IsPlayerDone())
                return;

            NextPlayer();
            return;
        }

        CurrentPlayerIndex = 0;
        if (Plugin.Configuration.VenueDealer)
        {
            Plugin.SwitchState(GameState.DealerSecondCards);
            return;
        }

        Dealer.Cards.Last().IsHidden = false;
        Plugin.SwitchState(GameState.DealerRound);

        CheckForRemainingPlayers();
    }

    private void FirstPlayer()
    {
        CurrentPlayerIndex = 0;
        if (IsPlayerDone())
        {
            NextPlayer();
            return;
        }

        Plugin.SwitchState(GameState.PlayerRound);
    }

    public void FinishDrawingRound(GameState state)
    {
        Plugin.SwitchState(state);
    }

    public void SetDrawingRound()
    {
        if (!Plugin.Configuration.VenueDealer)
            GiveDealerCard(false);

        if (Plugin.Configuration.AutoDrawOpening)
        {
            Plugin.SwitchState(GameState.PrepareRound);
            return;
        }

        Plugin.SwitchState(CurrentPlayer.Cards.Count == 0 ? GameState.DrawFirstCards : GameState.DrawSecondCards);
    }

    public void PreparePlayers()
    {
        if (!Plugin.Configuration.VenueDealer)
            GiveDealerCard(true);

        FirstPlayer();
    }

    public void StartRound()
    {
        GiveEachPlayerOneCard();
        GiveEachPlayerOneCard();
    }

    public void Parser(Roll roll)
    {
        // Snapshot before any roll that could place a card. Players are seated by hand
        // rather than by rolling, so nothing happens during registration.
        if (Plugin.State is not GameState.Registration && roll.OutOf == 13)
            PushUndo($"roll of {roll.Result}");

        switch (Plugin.State)
        {
            case GameState.Hit or GameState.DoubleDown or GameState.DrawSplit or GameState.FillDraw:
                RollParse(roll);
                break;
            case GameState.DrawFirstCards or GameState.DrawSecondCards:
                ParseStartingCards(roll);
                break;
            case GameState.DealerFirstCards or GameState.DealerSecondCards or GameState.DrawDealerCard:
                ParseDealerCards(roll);
                break;
            default:
                return;
        }
    }

    #region Roll Parsing

    private void ParseStartingCards(Roll roll)
    {
        if (roll.OutOf != 13)
            return;

        if (Plugin.Configuration.DealerDrawsAll)
        {
            if (Plugin.LocalPlayer != roll.PlayerName)
                return;
        }
        else
        {
            if (CurrentPlayer.Name != roll.PlayerName)
                return;
        }

        CurrentPlayer.Cards.Add(new Card(roll.Result, DrawCard().Suit));
        if (Plugin.Configuration.StartingDraw && CurrentPlayer.Cards.Count < 2)
            return;

        if (CurrentPlayer.Cards.Count >= 2)
            CurrentPlayerIndex++;
    }

    private void ParseDealerCards(Roll roll)
    {
        if (roll.OutOf != 13)
            return;

        if (Plugin.LocalPlayer != roll.PlayerName)
            return;

        Dealer.Cards.Add(new Card(roll.Result, DrawCard().Suit));
    }

    private void RollParse(Roll roll)
    {
        if (roll.OutOf != 13)
            return;

        // Check if 'dealer draws all cards' is enabled.
        // If so, spaghetti swap out the player var so it doesn't try to insert the dealer as a player
        if (Plugin.Configuration.DealerDrawsAll)
        {
            if (Plugin.LocalPlayer != roll.PlayerName)
                return;
        }
        else
        {
            if (CurrentPlayer.IsSplit)
                roll.PlayerName = $"{roll.PlayerName} Split";

            if (CurrentPlayer.Name != roll.PlayerName)
                return;
        }

        var card = new Card(roll.Result, DrawCard().Suit);
        switch (Plugin.State)
        {
            case GameState.Hit:
                Hit(card);
                return;
            case GameState.DoubleDown:
                DoubleDown(card);
                return;
            case GameState.DrawSplit:
                AddSplit(card);
                return;
            case GameState.FillDraw:
                FillSplit(card);
                return;
        }
    }

    #endregion

    private void AddSplit(Card card)
    {
        var split = new BlackjackPlayer(CurrentPlayer);
        var existingCard = CurrentPlayer.Cards.PopAt(1);

        split.Cards.Add(existingCard);
        split.Cards.Add(card);
        Players.Insert(CurrentPlayerIndex + 1, split);

        Plugin.SwitchState(GameState.FillDraw);
    }

    private void FillSplit(Card card)
    {
        CurrentPlayer.LastAction = BlackjackActions.Split;
        CurrentPlayer.Cards.Add(card);

        if (IsPlayerDone())
        {
            NextPlayer();
            return;
        }

        Plugin.SwitchState(GameState.PlayerRound);
    }

    public void Split()
    {
        // Guard against a stale click: the hand must still actually be a live pair.
        if (!CurrentPlayer.CanSplit)
            return;

        if (Plugin.Configuration is { VenueDealer: true })
        {
            Plugin.SwitchState(GameState.DrawSplit);
            return;
        }

        var split = new BlackjackPlayer(CurrentPlayer);
        var existingCard = CurrentPlayer.Cards.PopAt(1);

        split.Cards.Add(existingCard);
        split.Cards.Add(DrawCard());

        // Sits directly after its parent so hands are played in a sensible order.
        Players.Insert(CurrentPlayerIndex + 1, split);

        CurrentPlayer.Cards.Add(DrawCard());

        Plugin.SwitchState(GameState.PlayerRound);
    }

    private void Hit(Card card)
    {
        CurrentPlayer.LastAction = BlackjackActions.Hit;
        CurrentPlayer.Cards.Add(card);

        if (IsPlayerDone())
        {
            NextPlayer();
            return;
        }

        Plugin.SwitchState(GameState.PlayerRound);
    }

    private void DoubleDown(Card card)
    {
        CurrentPlayer.Stake *= 2;
        CurrentPlayer.LastAction = BlackjackActions.DoubleDown;
        CurrentPlayer.HandClosed = true;

        CurrentPlayer.Cards.Add(card);

        IsPlayerDone();
        NextPlayer();
    }

    public void Surrender()
    {
        PushUndo($"{CurrentPlayer.DisplayName} surrender");
        CurrentPlayer.Outcome = HandOutcome.Surrender;
        CurrentPlayer.HandClosed = true;
        CurrentPlayer.LastAction = BlackjackActions.Surrender;

        NextPlayer();
    }

    public void Stay()
    {
        PushUndo($"{CurrentPlayer.DisplayName} stay");
        CurrentPlayer.LastAction = BlackjackActions.Stay;
        CurrentPlayer.HandClosed = true;

        NextPlayer();
    }

    public void PlayerAction()
    {
        if (!Plugin.Configuration.AutoDrawCard)
            return;

        var card = DrawCard();
        switch (Plugin.State)
        {
            case GameState.Hit:
                Hit(card);
                break;
            case GameState.DoubleDown:
                DoubleDown(card);
                break;
            default:
                return;
        }
    }

    public void DealerAction()
    {
        PushUndo("begin dealer round");
        if (!Plugin.Configuration.AutoDrawDealer)
        {
            Plugin.SwitchState(GameState.DrawDealerCard);
            return;
        }

        while (IsDealerDone())
            GiveDealerCard(false);

        DealerRound();

        if (Plugin.State != GameState.Done)
            Plugin.SwitchState(GameState.DealerDone);
    }

    public void DealerRound()
    {
        var value = Dealer.CalculateCardValues();
        switch (value)
        {
            case 21:
                DealerBlackjack();
                break;
            case > 21:
                DealerBust();
                break;
        }
    }

    private void DealerBust()
    {
        Dealer.LastAction = BlackjackActions.Bust;
        ResolveAgainstDealer();
        Plugin.SwitchState(GameState.Done);
    }

    private void DealerBlackjack()
    {
        Dealer.LastAction = Dealer.IsNatural ? BlackjackActions.Blackjack : BlackjackActions.FullHand;
        ResolveAgainstDealer();
        Plugin.SwitchState(GameState.Done);
    }

    /// <summary>
    /// The one and only place a hand is settled against the dealer. Idempotent —
    /// it assigns outcomes rather than mutating balances, so running it twice
    /// (via undo, or a re-resolve after editing a card) gives the same answer.
    /// </summary>
    private void ResolveAgainstDealer()
    {
        var dealerValue = Dealer.CalculateCardValues();
        var dealerBust = dealerValue > 21;
        var dealerNatural = !dealerBust && Dealer.IsNatural;

        foreach (var player in Players)
        {
            // Bust and surrender never depend on what the dealer does.
            if (player.Outcome is HandOutcome.Bust or HandOutcome.Surrender)
                continue;

            // A natural pushes only against another natural. Against a dealer 21 built
            // from three or more cards it still wins, and pays 3:2.
            if (player.Outcome is HandOutcome.Blackjack)
            {
                player.Outcome = dealerNatural ? HandOutcome.Push : HandOutcome.Blackjack;
                continue;
            }

            // The mirror of the above: a dealer natural beats every non-natural hand,
            // including a player 21 made from three or more cards.
            if (dealerNatural)
            {
                player.Outcome = HandOutcome.Loss;
                continue;
            }

            var value = player.CalculateCardValues();
            player.Outcome = dealerBust || value > dealerValue
                ? HandOutcome.Win
                : value < dealerValue
                    ? HandOutcome.Loss
                    : HandOutcome.Push;
        }
    }

    /// <summary>
    /// Decides whether the current hand stops taking cards, and records HOW it ended.
    /// Records outcome only — never touches money — so calling it more than once on the
    /// same hand is harmless.
    /// </summary>
    private bool IsPlayerDone()
    {
        var cards = CurrentPlayer.CalculateCardValues();
        switch (cards)
        {
            case > 21:
                CurrentPlayer.Outcome = HandOutcome.Bust;
                CurrentPlayer.HandClosed = true;
                CurrentPlayer.LastAction = BlackjackActions.Bust;
                return true;
            case 21 when CurrentPlayer.Cards.Count == 2:
                // Provisional. A natural still has to be compared against the dealer:
                // it pushes against another natural, and beats everything else.
                CurrentPlayer.Outcome = HandOutcome.Blackjack;
                CurrentPlayer.HandClosed = true;
                CurrentPlayer.LastAction = BlackjackActions.Blackjack;
                return true;
            case 21:
                // 21 from three or more cards pays even money, so it settles by plain
                // comparison like any other standing hand.
                CurrentPlayer.HandClosed = true;
                CurrentPlayer.LastAction = BlackjackActions.FullHand;
                return true;
        }

        return false;
    }

    private void GiveDealerCard(bool isHidden)
    {
        Dealer.Cards.Add(DrawCard(isHidden));
    }

    public bool IsDealerDone()
    {
        var cards = Dealer.CalculateCardValues();
        var hasAce = Dealer.Cards.Any(c => c.IsAce);

        return Plugin.Configuration.DealerRule switch
        {
            DealerRules.DealerHard17 => cards < 17,
            DealerRules.DealerHard16 => cards < 16,
            DealerRules.DealerSoft17 => !hasAce ? cards < 17 : HasDealerSoftHand() <= 17 || cards < 17,
            DealerRules.DealerSoft16 => !hasAce ? cards < 16 : HasDealerSoftHand() <= 16 || cards < 16,
            _ => cards < 16
        };
    }

    private int HasDealerSoftHand()
    {
        return Dealer.Cards.Sum(c => c.Value) + 10;
    }

    public void CheckForRemainingPlayers()
    {
        // A provisional blackjack still needs the dealer to play, since a dealer 21
        // turns it into a push.
        if (!Players.Any(x => x.NeedsDealer))
        {
            Plugin.SwitchState(GameState.Done);
            return;
        }

        DealerRound();
    }

    public void EndMatch()
    {
        PushUndo("calculate winnings");
        ResolveAgainstDealer();
        Plugin.SwitchState(GameState.Done);
    }

    public void TakePeopleIntoNextRound()
    {
        var l = new List<BlackjackPlayer>();
        // A doubled-down stake deliberately does not persist; the player returns
        // to their standing wager next round.
        foreach (var player in Players.Where(p => !p.IsSplit))
        {
            // Split hands are separate rows but the same person, so they settle together.
            // A negative settlement means the dealer is no longer holding a full stake
            // for them, whatever the shortfall happened to be.
            l.Add(new BlackjackPlayer(player.Name, player.Wager) { NeedsRestake = NeedsRestakeAfter(player) });
        }
        Players = l;

        Dealer = new BlackjackPlayer("Dealer", 0);
        BanksApplied = false;
        ClearUndo();
        Plugin.SwitchState(GameState.Registration);
    }

    #region Internal

    private readonly Random RNG = new(unchecked(Environment.TickCount * 31));

    private Card DrawCard(bool isHidden = false)
    {
        return new Card(RNG.Next(1, 14), RNG.Next(0, 4), isHidden);
    }

    private void GiveEachPlayerOneCard()
    {
        foreach (var player in Players)
            player.Cards.Add(DrawCard());
    }

    #endregion
}

public static class BlackjackActionsExtensions
{
    public static string Name(this HandOutcome outcome)
    {
        return outcome switch
        {
            HandOutcome.Pending => "-",
            HandOutcome.Win => "Win",
            HandOutcome.Blackjack => "Blackjack",
            HandOutcome.Push => "Push",
            HandOutcome.Loss => "Loss",
            HandOutcome.Bust => "Bust",
            HandOutcome.Surrender => "Surrender",
            _ => "Unknown"
        };
    }

    public static string Name(this BlackjackActions action)
    {
        return action switch
        {
            BlackjackActions.None => "",
            BlackjackActions.Hit => "Hit",
            BlackjackActions.Bust => "Bust",
            BlackjackActions.Stay => "Stay",
            BlackjackActions.Split => "Split",
            BlackjackActions.FullHand => "Full Hand",
            BlackjackActions.Blackjack => "Blackjack",
            BlackjackActions.Surrender => "Surrender",
            BlackjackActions.DoubleDown => "Double Down",
            _ => "Unknown"
        };
    }
}