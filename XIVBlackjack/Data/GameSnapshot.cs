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

    public GameSnapshot(string label, GameState state, int currentPlayerIndex, BlackjackPlayer dealer, List<BlackjackPlayer> players)
    {
        Label = label;
        State = state;
        CurrentPlayerIndex = currentPlayerIndex;
        Dealer = dealer.Clone();
        Players = players.Select(p => p.Clone()).ToList();
    }
}
