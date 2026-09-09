namespace XIVBlackjack.Data;

public enum BankrollMode
{
    /// <summary>The dealer's own gil funds the table; the venue takes a cut of the profit.</summary>
    SelfBankroll,

    /// <summary>The venue fronts a float; the dealer takes a cut plus a fixed paycheck.</summary>
    VenueBankroll
}

/// <summary>
/// End-of-night reconciliation. Every figure here is derived from the inputs on each read —
/// nothing is stored — so editing any field updates the whole picture at once, the same way
/// the spreadsheet this replaces did.
/// </summary>
public class PayoutResult
{
    public long TipsTotal;

    // Self-bankroll
    public long Base;            // starting gil + tips: the figure profit is measured against
    public long GrossProfit;
    public long VenueCut;
    public long ToPocket;
    public long PocketTotal;

    // Venue-bankroll
    public long PersonalGil;
    public long StartingTotal;
    public long Difference;
    public long DealerCut;
    public long VenueOwesDealer;
    public long PersonalGilEnd;
    public long TradeToVenue;
    public long VenueProfit;
}

public static class PayoutManager
{
    public static PayoutResult Calculate(Configuration config)
    {
        var r = new PayoutResult
        {
            TipsTotal = config.Tips.Sum(t => (long)t)
        };

        // Tips sit inside the base figure rather than the profit figure. That is deliberate:
        // it keeps them out of the percentage split, so a cut is only ever taken on gil won
        // at the table.
        r.Base = config.StartingGil + r.TipsTotal;

        // --- Self-bankroll ---
        r.GrossProfit = config.EndingGil - r.Base;
        r.VenueCut = r.GrossProfit > 0 ? (long)(r.GrossProfit * (config.VenuePercent / 100.0)) : 0;
        r.ToPocket = r.GrossProfit - r.VenueCut;

        // Base already contains the tips. The sheet added them a second time here, which
        // overstated the total by exactly the night's tips.
        r.PocketTotal = r.Base + r.ToPocket;

        // --- Venue-bankroll ---
        r.PersonalGil = r.Base;
        r.StartingTotal = r.PersonalGil + config.VenueFloat;
        r.Difference = config.EndingGil - r.StartingTotal;
        r.DealerCut = r.Difference > 0 ? (long)(r.Difference * (config.DealerPercent / 100.0)) : 0;
        r.VenueOwesDealer = r.DealerCut + config.VenuePaycheck;
        r.PersonalGilEnd = r.PersonalGil + r.VenueOwesDealer;
        r.TradeToVenue = config.EndingGil - r.PersonalGilEnd;
        r.VenueProfit = r.TradeToVenue - config.VenueFloat;

        return r;
    }

    public static string SelfLedgerStatement(Configuration config, PayoutResult r)
    {
        var tips = r.TipsTotal > 0 ? $" + (Tips) {r.TipsTotal:N0} = {r.Base:N0}" : "";
        return $"Starting Gil: {config.StartingGil:N0}{tips} | Ending Gil: {config.EndingGil:N0}";
    }

    public static string SelfVenueStatement(Configuration config, PayoutResult r) =>
        $"Gross Profit: {r.GrossProfit:N0}. Venue Percent Cut: {config.VenuePercent}%. " +
        $"Venue Gamba Profit: {r.VenueCut:N0} ||| Total owed to Venue: {r.VenueCut:N0}";

    public static string VenueCutStatement(Configuration config, PayoutResult r) =>
        $"Gross profit: {r.Difference:N0}. Dealer's {config.DealerPercent}% cut: {r.DealerCut:N0}. " +
        $"With a fixed payment of {config.VenuePaycheck:N0}, total due to Dealer: {r.VenueOwesDealer:N0}.";

    public static string VenueSettleStatement(Configuration config, PayoutResult r)
    {
        var direction = r.TradeToVenue < 0
            ? "After accounting for profits and payouts, the venue owes the dealer "
            : "After accounting for profits and payouts, the dealer will exchange ";

        return $"Initially, the venue provided the dealer {config.VenueFloat:N0}. " +
               $"{direction}{Math.Abs(r.TradeToVenue):N0}.";
    }
}
