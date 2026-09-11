using Dalamud.Interface.Windowing;

namespace XIVBlackjack.Windows.Main;

public partial class MainWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    public MainWindow(Plugin plugin) : base("Blackjack##XIVBlackjack")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 620),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        // First open only — anyone who has already sized the window keeps their size.
        Size = new Vector2(760, 640);
        SizeCondition = ImGuiCond.FirstUseEver;

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##MainTabBar"))
            return;

        if (ImGui.BeginTabItem("Table"))
        {
            BlackjackMode();
            ImGui.EndTabItem();
        }

        BankingTab();
        PayoutTab();

        ImGui.EndTabBar();
    }
}
