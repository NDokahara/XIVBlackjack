using System.Text.RegularExpressions;

namespace XIVBlackjack.Data;

/// <summary>
/// Reads a roll out of the chat line the game prints, as a signature-free alternative to
/// hooking the print function.
///
/// The dice hook is a byte-pattern match against the game executable and stops resolving
/// every time that function is recompiled — roughly every API bump. Chat text is stable
/// across patches, so this keeps working when the signature does not.
/// </summary>
public static partial class ChatRollParser
{
    /// <summary>Matches the "(1-13)" range the game prints for a bounded roll.</summary>
    [GeneratedRegex(@"\((\d+)\s*-\s*(\d+)\)")]
    private static partial Regex RangePattern();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();

    /// <summary>
    /// Pulls the result and the upper bound out of a roll line. Returns false for anything
    /// that is not a roll.
    ///
    /// Deliberately permissive about surrounding text — the line carries icons, channel tags
    /// and a name, all of which vary — and anchored only on the numbers, which do not.
    /// </summary>
    public static bool TryParse(string text, out int roll, out int outOf)
    {
        roll = 0;
        outOf = 0;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!text.Contains("Random!", StringComparison.OrdinalIgnoreCase))
            return false;

        var range = RangePattern().Match(text);

        if (range.Success)
        {
            // "Random! (1-13) 5" — the bound is in the brackets, the result follows them.
            if (!int.TryParse(range.Groups[2].Value, out outOf))
                return false;

            var after = text[(range.Index + range.Length)..];
            var result = NumberPattern().Match(after);

            if (!result.Success || !int.TryParse(result.Value, out roll))
                return false;

            return true;
        }

        // "Random! 594" — an unbounded roll, which is how players register.
        var lone = NumberPattern().Match(text);
        if (!lone.Success || !int.TryParse(lone.Value, out roll))
            return false;

        outOf = 0;
        return true;
    }
}
