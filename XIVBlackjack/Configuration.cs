using Dalamud.Configuration;
using XIVBlackjack.Data;

namespace XIVBlackjack;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool On = false;

    // Blocklist
    public bool ActiveBlocklist = false;
    public List<string> SavedBlocklist = new();

    // Blackjack
    // Defaults are Venue mode with the dealer drawing every card.
    // BlackjackMode: 0 = Normal (plugin deals internally), 1 = Venue (cards come from rolls).
    public bool AutoDrawCard = false;
    public bool AutoDrawOpening = false;
    public bool AutoDrawDealer = false;
    public bool DealerDrawsAll = true;
    public bool VenueDealer = true;
    public bool StartingDraw = false;
    public int BlackjackMode = 1;
    public DealerRules DealerRule = DealerRules.DealerHard16;

    // Venue-mode buttons
    public bool ButtonsRoll = true;
    public bool CollectOnDoubleSplit = true;
    public bool UseDiceCommand = true;

    /// <summary>
    /// Reads rolls from the chat line as well as from the hooks. Signature-free, so it keeps
    /// working across patches that break byte matching. Duplicates are filtered, so leaving
    /// this on alongside healthy hooks is harmless.
    /// </summary>
    public bool ReadRollsFromChat = true;

    /// <summary>
    /// Waits for the game's own trade outcome message before marking a trade as sent, rather
    /// than treating the window closing as success. Reads English text, so turn it off on
    /// other clients — at the cost of a cancelled trade counting as sent again.
    /// </summary>
    public bool ConfirmTradesFromChat = true;

    /// <summary>
    /// Tables are normally run in a party, alliance or linkshell, where "/dice" keeps the
    /// rolls inside the channel. "/random" broadcasts to everyone nearby instead — some
    /// dealers prefer it, so both are offered.
    /// </summary>
    public string RollCommand => UseDiceCommand ? "/dice 13" : "/random 13";

    // Player banking. Persisted so a mid-event reload does not wipe balances; cleared
    // deliberately at close via "End Night" rather than on shutdown.
    public Dictionary<string, BankAccount> Banks = new();

    // Payout manager
    public BankrollMode PayoutMode = BankrollMode.SelfBankroll;
    public long StartingGil;
    public long EndingGil;
    public List<int> Tips = new();
    public double VenuePercent = 30.0;
    public double DealerPercent = 30.0;
    public long VenueFloat = 5_000_000;
    public long VenuePaycheck = 400_000;


    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
