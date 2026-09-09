using System.IO;
using Dalamud.Interface.ManagedFontAtlas;

namespace XIVBlackjack;

public class FontManager
{
    public readonly IFontHandle SourceCode20;

    public FontManager()
    {
        var sourcecode = Path.Combine(Plugin.PluginDir, @"Resources\Fonts\SourceCodePro-Medium.ttf");

        var range = "♠♥♦♣─＼～┐│┌┘└"
            .Concat(Enumerable.Range(20, 127).Select(x => (char)x))
            .ToGlyphRange();

        SourceCode20 = Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
            e => e.OnPreBuild(
                tk => tk.AddFontFromFile(sourcecode, new SafeFontConfig { SizePx = 20, GlyphRanges = range })
            ));
    }

    public void Dispose()
    {
        SourceCode20.Dispose();
    }
}
