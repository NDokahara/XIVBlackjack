using Dalamud.Interface.Windowing;

namespace XIVBlackjack.Windows.Config;

public partial class ConfigWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin) : base("Configuration##XIVBlackjack")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 530),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##ConfigTabBar"))
        {
            General();

            Blocklist();

            Debug();

            About();

            ImGui.EndTabBar();
        }
    }
}
