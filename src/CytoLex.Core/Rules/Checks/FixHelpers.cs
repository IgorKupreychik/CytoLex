using System.Text.RegularExpressions;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// Shared pieces of the level-1 fixes (T-035): the form of the multiplication sign in new text, and edits that remove
/// items of a cell line. Other new text reuses the input's own symbols (arrows, joins, signs), so typed forms survive. Fixes edit the original input (docs/design/core.md §10); they never re-serialise the tree.
/// </summary>
internal static partial class FixHelpers
{
    /// <summary>
    /// The multiplication sign for new text: the printed <c>×</c>, or the typed <c>x</c> when the input already writes
    /// copies that way (<c>x2</c>) and never with <c>×</c> (T-053: the forms are equivalent; fixes keep the user's style).
    /// </summary>
    public static string Multiply(string input) =>
        !input.Contains('×', StringComparison.Ordinal) && TypedMultiply().IsMatch(input) ? "x" : "×";

    /// <summary>
    /// The span that removes <paramref name="item"/> from its cell line together with the comma before it (from the end
    /// of the previous item, sex chromosomes or count); for a line's first element, the item and the comma after it.
    /// </summary>
    public static TextSpan RemovalSpan(CellLine line, KaryotypeItem item)
    {
        var index = line.Items.IndexOf(item);
        if (index > 0)
        {
            return TextSpan.FromBounds(line.Items[index - 1].Span.End, item.Span.End);
        }

        MeaningNode? before = line.Sex is { } sex ? sex : line.Count is { IsMissing: false } count ? count : null;
        if (before is not null)
        {
            return TextSpan.FromBounds(before.Span.End, item.Span.End);
        }

        return index + 1 < line.Items.Length
            ? TextSpan.FromBounds(item.Span.Start, line.Items[index + 1].Span.Start)
            : item.Span;
    }

    /// <summary>A fix that removes <paramref name="items"/> (each with its comma) from <paramref name="line"/>.</summary>
    public static FixProposal Remove(CellLine line, params IEnumerable<KaryotypeItem> items) =>
        new([.. items.Select(i => new TextEdit(RemovalSpan(line, i), string.Empty))]);

    /// <summary>The cell line that lists <paramref name="item"/> directly; <c>null</c> for nested items (<c>or</c> readings).</summary>
    public static CellLine? LineOf(this KaryotypeModel model, KaryotypeItem item) =>
        model.CellLines.FirstOrDefault(l => l.Items.Contains(item));

    /// <summary>The sex field is written as plain letters (<c>XXY</c>, no <c>c</c>, <c>?</c> or underlining), so a fix may rewrite it.</summary>
    public static bool IsPlainSexField(SexComplement sex, CellLine line) =>
        sex.Constitutional is null && !sex.IsPartial && sex.Chromosomes.All(c => c is "X" or "Y")
        && sex.Span.Length == sex.Chromosomes.Length && line.Sex == sex;

    [GeneratedRegex(@"x\d")]
    private static partial Regex TypedMultiply();
}
