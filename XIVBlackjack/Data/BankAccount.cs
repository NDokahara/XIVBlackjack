namespace XIVBlackjack.Data;

/// <summary>
/// Gil a player is holding with the dealer. While an account exists, that player's rounds
/// settle against this balance instead of producing a trade — wins credit it, losses debit
/// it — so gil only physically moves on deposit and cash-out.
/// </summary>
public class BankAccount
{
    /// <summary>Full "Name\uE05DWorld", matching BlackjackPlayer.Name.</summary>
    public string Name = string.Empty;

    public long Balance;

    /// <summary>Total credited and debited tonight, purely so the dealer can audit a dispute.</summary>
    public long Deposited;
    public long WonLost;

    public string DisplayName => Name.Replace("\uE05D", "\uE05D ").Split("\uE05D ").First();

}
