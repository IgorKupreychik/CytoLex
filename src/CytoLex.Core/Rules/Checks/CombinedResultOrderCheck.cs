using CytoLex.Core.Segmentation;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>ARR.COMBINED_WITH_OTHER_METHODS</c> (§14.2 ¶5): in a combined result the karyotype comes first, then FISH, then
/// the other techniques such as the array, separated by periods. Judged once per string, on its first implemented
/// segment, from the segment types; the fix reorders the segments and keeps the separators. A karyotype the segmenter
/// leaves inside an arr segment (<c>…dn.47,XY</c>: no split before a digit) is reported by the arr parser.
/// </summary>
[KbRule("ARR.COMBINED_WITH_OTHER_METHODS")]
internal sealed class CombinedResultOrderCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var segments = context.Segments;
        if (segments.Length < 2 || segments.FirstOrDefault(s => context.ImplementedTypes.Contains(s.Type)) != context.Segment)
        {
            return;
        }

        var written = segments.ToList();
        var order = written.OrderBy(s => Rank(s.Type)).ToList(); // OrderBy is stable
        var i = written.FindIndex(s => !ReferenceEquals(s, order[written.IndexOf(s)]));
        if (i < 0)
        {
            return;
        }

        var input = context.Input;
        var separators = written.Skip(1).Select(s => s.Separator is { } sep ? sep.Of(input) : ".").ToList();
        var text = string.Concat(order.Select((s, k) => s.Span.Of(input) + (k < separators.Count ? separators[k] : "")));
        var whole = TextSpan.FromBounds(written[0].Span.Start, written[^1].Span.End);
        var misplaced = written[i];
        context.ReportWithFix(
            misplaced.Span,
            $"The {Name(order[i].Type)} result comes before the {Name(misplaced.Type)} result: karyotype, then FISH, then other techniques such as the array.",
            FixProposal.Replace(whole, text),
            [order[i].Span]);
    }

    /// <summary>Karyotype notation first, then FISH (ch. 13 ish family), then every other technique (§14.2 ¶5).</summary>
    private static int Rank(NotationType type) => type switch
    {
        NotationType.Karyotype or NotationType.Breakage or NotationType.Meiotic => 0,
        NotationType.Ish or NotationType.NucIsh or NotationType.FibIsh or NotationType.RevIsh => 1,
        _ => 2,
    };

    private static string Name(NotationType type) => Rank(type) switch
    {
        0 => "karyotype",
        1 => "FISH",
        _ => type.ToKbName(),
    };
}
