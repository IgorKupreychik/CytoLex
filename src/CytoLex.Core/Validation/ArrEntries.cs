using System.Collections.Immutable;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Validation;

/// <summary>
/// Builds the <see cref="AbnormalityEntry"/> of one arr result (<see cref="ArrAberration"/>, T-072): regions or a
/// chromosome group, bands, positions, the copy number as written, proportion, zygosity and origin.
/// </summary>
internal static class ArrEntries
{
    /// <summary>The entry of <paramref name="a"/> in the group <paramref name="lineIndex"/>.</summary>
    public static AbnormalityEntry Entry(string input, ArrAberration a, string? build, int lineIndex, int index)
    {
        var copyText = CopyText(input, a);
        var regions = a.Regions.Select(r => Region(input, r)).ToImmutableArray();
        var arr = new ArrResultSummary(
            a.Copy?.Kind ?? DetailedCopy(a)?.Kind ?? ArrResultKind.CopyNumber,
            a.Copy?.Min ?? DetailedCopy(a)?.Min,
            a.Copy?.Max ?? DetailedCopy(a)?.Max,
            copyText,
            a.IsWholeChromosomes,
            a.IsRegionGroup,
            a.IsDetailed,
            regions,
            a.Proportion?.Text,
            a.Qualifiers.FirstOrDefault(q => q.IsZygosity)?.Word,
            a.Qualifiers.Any(q => q.Word == "mos"),
            build);

        var breakpoints = a.Regions.SelectMany(r => r.Bands).ToList();
        var questioned = breakpoints.Any(b => b.IsQuestioned || b.Landmark == "?");
        var uncertain = questioned
            || (a.Copy?.IsRange ?? false)
            || a.Regions.Any(r => !r.Alternatives.IsEmpty || (r.Positions?.Items.Any(i => !i.Alternatives.IsEmpty) ?? false));
        var constitutional = a.Qualifiers.FirstOrDefault(q => q.Word == "c");
        var flags = new AbnormalityFlags(
            a.IsPartial,
            questioned,
            uncertain,
            constitutional is not null,
            false,
            a.Qualifiers.FirstOrDefault(q => q.IsInheritance)?.Word,
            null,
            null);

        return new AbnormalityEntry(
            index,
            lineIndex,
            a.Span,
            a.Span.Of(input),
            AbnormalityKind.ArrResult,
            GainLoss.None,
            null,
            null,
            [.. Chromosomes(a).Distinct(StringComparer.Ordinal)],
            [.. breakpoints.Select(AbnormalityList.Summary)],
            ShortForm(input, a, copyText),
            [],
            [],
            flags,
            [],
            [.. a.Kb.Concat(a.Results.SelectMany(r => r.Kb)).Concat(a.Proportion?.Kb ?? []).Concat(a.Qualifiers.SelectMany(q => q.Kb)).Distinct()],
            null,
            arr);
    }

    /// <summary>
    /// The result without positions: <c>4q32.2q35.1×1</c>, <c>(8)×3</c>, <c>(15q11.2q26.3,21q11.2q22.3)×2</c>,
    /// <c>Xq28 or Yq12×1</c>; the bands and chromosomes as written.
    /// </summary>
    internal static string ShortForm(string input, ArrAberration a, string? copyText)
    {
        string what;
        if (a.IsWholeChromosomes)
        {
            what = "(" + string.Join(",", a.Chromosomes.Select(c => c.Span.Of(input))) + ")";
        }
        else
        {
            what = string.Join(",", a.Regions.Select(r => string.Join(" or ", new[] { r }.Concat(r.Alternatives).Select(x => x.BandSpan.Of(input)))));
            if (a.IsRegionGroup)
            {
                what = "(" + what + ")";
            }
        }

        return what + copyText;
    }

    /// <summary>The chromosomes in written order: <c>16q</c> for an arm, every number of a range (<c>1</c> … <c>22</c>).</summary>
    private static IEnumerable<string> Chromosomes(ArrAberration a) =>
        a.IsWholeChromosomes
            ? a.Chromosomes.SelectMany(c => c.Last is null ? [c.First.Name + c.First.Arm] : c.Names.AsEnumerable())
            : a.Regions.SelectMany(r => new[] { r }.Concat(r.Alternatives)).Select(r => r.Chromosome?.Name).OfType<string>();

    /// <summary>The result as written; in the detailed system the copy number of the one span (the abnormal segment).</summary>
    private static string? CopyText(string input, ArrAberration a) =>
        a.Copy is { } copy ? copy.Span.Of(input) : DetailedCopy(a)?.Span.Of(input);

    /// <summary>The copy number of the one span of a detailed-system result; <c>null</c> when there is not exactly one.</summary>
    private static ArrCopyNumber? DetailedCopy(ArrAberration a)
    {
        var spans = a.Regions.SelectMany(r => r.Positions?.Items ?? []).Where(i => i.IsSpan && i.Copy is not null).ToList();
        return spans is [var one] ? one.Copy : null;
    }

    private static ArrRegionSummary Region(string input, ArrRegion r) => new(
        r.Span,
        r.Chromosome?.Name,
        r.BandSpan.Of(input),
        [.. r.Bands.Select(AbnormalityList.Summary)],
        [.. (r.Positions?.Items ?? []).Select(i => Position(input, i))],
        [.. r.Alternatives.Select(x => Region(input, x))]);

    private static ArrPositionSummary Position(string input, ArrPositionItem i) => new(
        i.Span,
        i.Start,
        i.End,
        i.Copy?.Span.Of(input),
        [.. i.Alternatives.Select(x => Position(input, x))]);
}
