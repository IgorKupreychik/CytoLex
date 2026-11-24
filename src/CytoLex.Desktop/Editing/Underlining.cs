using System.Text;

namespace CytoLex.Desktop.Editing;

/// <summary>One replacement in the editor text: <paramref name="Length"/> characters at <paramref name="Start"/> become <paramref name="NewText"/>.</summary>
public sealed record TextEdit(int Start, int Length, string NewText);

/// <summary>
/// The "Underline homologue" editor command (T-042): ISCN underlines a chromosome number to tell homologues apart
/// (<c>der(1̲)</c>); in text that is U+0332 after each underlined character, the only input form.
/// </summary>
public static class Underlining
{
    /// <summary>U+0332 COMBINING LOW LINE.</summary>
    public const char Mark = '̲';

    /// <summary>
    /// Toggles underlining on the selection, or with an empty selection on the letter/digit run at the caret. When every
    /// letter and digit of the target is underlined the marks are removed, otherwise each one without a mark gets one.
    /// Other characters are left alone.
    /// </summary>
    /// <returns>The edit, whose new text should end up selected; <c>null</c> when the target has no letter or digit.</returns>
    public static TextEdit? Toggle(string text, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var end = Math.Clamp(selectionStart + selectionLength, start, text.Length);
        if (start == end)
        {
            // The digit run (7 of 7p13) or the letter run (X) next to the caret, marks included.
            var digits = Neighbour(text, start) is { } n && char.IsDigit(n);
            bool InRun(char c) => c == Mark || (digits ? char.IsDigit(c) : char.IsLetter(c));
            while (start > 0 && InRun(text[start - 1]))
            {
                start--;
            }

            while (end < text.Length && InRun(text[end]))
            {
                end++;
            }
        }

        // A mark right after the target belongs to its last character.
        while (end < text.Length && text[end] == Mark)
        {
            end++;
        }

        var units = new List<(char Char, bool Underlined)>();
        var prefix = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            if (text[i] == Mark)
            {
                if (units.Count == 0)
                {
                    prefix.Append(Mark); // belongs to the character before the target
                }

                continue;
            }

            units.Add((text[i], i + 1 < end && text[i + 1] == Mark));
        }

        var targets = units.Where(u => char.IsLetterOrDigit(u.Char)).ToList();
        if (targets.Count == 0)
        {
            return null;
        }

        var underline = !targets.All(u => u.Underlined);
        var result = new StringBuilder(prefix.ToString());
        foreach (var (c, underlined) in units)
        {
            result.Append(c);
            if (char.IsLetterOrDigit(c) ? underline : underlined)
            {
                result.Append(Mark);
            }
        }

        var newText = result.ToString();
        return newText == text[start..end] ? null : new TextEdit(start, end - start, newText);
    }

    /// <summary>The character before the caret (skipping its marks), else the one after it.</summary>
    private static char? Neighbour(string text, int caret)
    {
        var i = caret - 1;
        while (i >= 0 && text[i] == Mark)
        {
            i--;
        }

        return i >= 0 && char.IsLetterOrDigit(text[i]) ? text[i]
            : caret < text.Length && char.IsLetterOrDigit(text[caret]) ? text[caret]
            : null;
    }
}
