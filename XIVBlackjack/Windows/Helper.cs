namespace XIVBlackjack.Windows;

public static class Helper
{
    public static readonly Vector4 White = new(1.0f, 1.0f, 1.0f, 1.0f);
    public static readonly Vector4 Green = new(0.0f, 1.0f, 0.0f, 1.0f);
    public static readonly Vector4 Red = new(0.980f, 0.245f, 0.245f, 1.0f);
    public static readonly Vector4 Yellow = new(0.959f, 1.0f, 0.0f, 1.0f);
    public static readonly Vector4 SoftBlue = new(0.031f, 0.376f, 0.768f, 1.0f);

    /// <summary>Ko-fi brand red, #FF5E5B.</summary>
    public static readonly Vector4 KofiRed = new(1.0f, 0.369f, 0.357f, 1.0f);
    public static readonly Vector4 KofiRedHover = new(1.0f, 0.478f, 0.463f, 1.0f);
    public static readonly Vector4 KofiRedActive = new(0.878f, 0.298f, 0.290f, 1.0f);

    public static bool CenterButton(string text)
    {
        var buttonStyle = ImGui.GetStyle().ButtonTextAlign.X;
        ImGui.SetCursorPosX((ImGui.GetWindowSize().X - ImGui.CalcTextSize(text).X - buttonStyle) * 0.5f);
        return ImGui.Button(text);
    }

    public static void SetTextCenter(string text, Vector4 color = default)
    {
        ImGui.SetCursorPosX((ImGui.GetWindowSize().X - ImGui.CalcTextSize(text).X) * 0.5f);

        // Alpha 0 means empty color
        if (color.W == 0)
            ImGui.TextUnformatted(text);
        else
            ImGui.TextColored(color, text);
    }

    public static void TableCenterText(string text, Vector4 color = default)
    {
        var pos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(pos with { X = pos.X + (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X) * 0.5f });
        if (color.W == 0)
            ImGui.TextUnformatted(text);
        else
            ImGui.TextColored(color, text);
    }

    private static float Saturate(float f) => f < 0.0f ? 0.0f : f > 1.0f ? 1.0f : f;
    private static uint FloatToUintSat(float val) => (uint)(Saturate(val) * 255.0f + 0.5f);

    public static uint Vec4ToUintColor(Vector4 i)
    {
        var o = FloatToUintSat(i.X) << 0;
        o |= FloatToUintSat(i.Y) << 8;
        o |= FloatToUintSat(i.Z) << 16;
        o |= FloatToUintSat(i.W) << 24;

        return o;
    }
}
