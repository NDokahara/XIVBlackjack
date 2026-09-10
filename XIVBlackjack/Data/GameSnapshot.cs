namespace XIVBlackjack.Data;

/// <summary>
/// A full-fidelity copy of the blackjack table at a single moment.
///
/// Undo is snapshot-based rather than inverse-operation-based on purpose: it
/// captures split creation, index movement and state transitions for free,
/// none of which have clean inverses.
/// </summary>
public class GameSnapshot
{
    public readonly string Label;
    public readonly GameState State;
    public readonly int CurrentPlayerIndex;
    public readonly BlackjackPlayer Dealer;
    public readonly List<BlackjackPlayer> Players;

    /// <summary>
    /// Whether the round had already been written to player banks. Restored with everything
    /// else, so undoing past the point banks were applied re-offers the button instead of
    /// leaving balances describing a round that no longer exists.
    /// </summary>
    public readonly bool BanksApplied;

    public GameSnapshot(string label, GameState state, int currentPlayerIndex, BlackjackPlayer dealer, List<BlackjackPlayer> players, bool banksApplied)
    {
        Label = label;
        State = state;
        CurrentPlayerIndex = currentPlayerIndex;
        Dealer = dealer.Clone();
        Players = players.Select(p => p.Clone()).ToList();
        BanksApplied = banksApplied;
    }
}
