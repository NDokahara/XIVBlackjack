namespace XIVBlackjack.Data;

/// <summary>
/// Reads the outcome of a trade off the message the game prints, because ConditionFlag.TradeOpen
/// only reports that the window closed, not whether any gil moved. Those two are the same event
/// from the client's point of view and completely different from the player's.
///
/// Text matching for the same reason <see cref="ChatRollParser"/> uses it: these strings survive
/// patches in a way byte signatures and node positions do not. English client only — anything
/// else falls through as Unknown, which stops the run and says so rather than miscounting it.
/// </summary>
public static class TradeMessages
{
    public static TradeResult Classify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return TradeResult.Unknown;

        // "Trade complete."
        if (text.Contains("trade complete", StringComparison.OrdinalIgnoreCase))
            return TradeResult.Completed;

        // "Trade canceled." — matched on the stem so both spellings land, and so a reworded
        // line ("Trade cancelled by ...") still classifies.
        if (text.Contains("trade cancel", StringComparison.OrdinalIgnoreCase))
            return TradeResult.Cancelled;

        return TradeResult.Unknown;
    }
}
