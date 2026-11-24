using System.Collections.Immutable;
using CytoLex.Core.Rules.Checks;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>
/// The in situ parts of the <see cref="AbnormalityEntry"/> of one <see cref="FishItem"/> (T-092): the region entry,
/// the locus groups, the cells and the normal mark (<see cref="InSituResultSummary"/>).
/// </summary>
internal static class InSituEntries
{
    /// <summary>The rule of the short normal subtelomere panel <c>subtel(41×2)</c>.</summary>
    internal const string SubtelNormalShort = "ISH.SUBTEL_NORMAL_SHORT";

    /// <summary>The in situ details of <paramref name="item"/> in a segment of type <paramref name="notation"/>.</summary>
    public static InSituResultSummary Summary(string input, FishItem item, NotationType notation)
    {
        var normalRule = NormalRule(item, notation);
        return new InSituResultSummary(
            notation,
            [.. item.LocusGroups.Select(g => Group(input, g))],
            item.Cells is { } c ? new InSituCellsSummary(c.Text, c.Count, c.Total, c.IsPercentage) : null,
            normalRule is not null,
            normalRule);
    }

    /// <summary>
    /// The KB rule that makes <paramref name="item"/> a normal result, or <c>null</c>: the ish normal-chromosome form,
    /// <c>subtel(41×2)</c>, or a nuc ish result whose loci all show two signals.
    /// </summary>
    internal static string? NormalRule(FishItem item, NotationType notation)
    {
        if (item.Region is { } region)
        {
            return notation switch
            {
                NotationType.Ish when !region.Groups.IsEmpty => FishAnalyses.NormalChromosomeFormat,
                NotationType.NucIsh when FishNormal.AllTwoSignals(region.Groups) => NucIshAnalyses.AbnormalOverTotal,
                _ => null,
            };
        }

        return notation == NotationType.Ish && item.Abnormality is Abnormality { Kind: "subtel" } && FishNormal.AllTwoSignals(item.LocusGroups)
            ? SubtelNormalShort
            : null;
    }

    /// <summary>The written result without its cells: <c>22q11.2(D22S75×2)</c>, <c>(ABL1,BCR)×2(ABL1 con BCR×1)</c>.</summary>
    public static string WithoutCells(string input, FishItem item) =>
        (item.Cells is { } c ? TextSpan.FromBounds(item.Span.Start, c.Span.Start) : item.Span).Of(input).TrimEnd();

    /// <summary>The entry of a region result (<see cref="AbnormalityKind.InSituRegion"/>).</summary>
    public static AbnormalityEntry Region(string input, FishItem item, FishRegion region, int lineIndex, int index)
    {
        var breakpoints = new[] { region.Band }.OfType<Breakpoint>().Concat(region.Groups.SelectMany(g => g.Breakpoints)).ToList();
        var chromosome = region.Chromosome?.Name ?? region.Band?.Chromosome?.Name;
        var questioned = breakpoints.Any(b => b.IsQuestioned || b.Landmark == "?") || (region.Chromosome?.IsQuestioned ?? false);
        var flags = new AbnormalityFlags(Partial(item), questioned, questioned, false, false, null, null, null);
        return new AbnormalityEntry(
            index,
            lineIndex,
            item.Span,
            item.Span.Of(input),
            AbnormalityKind.InSituRegion,
            GainLoss.None,
            null,
            null,
            chromosome is null ? [] : [chromosome],
            [.. breakpoints.Select(AbnormalityList.Summary)],
            WithoutCells(input, item),
            [],
            [],
            flags,
            [],
            [.. region.Kb.Concat(region.Groups.SelectMany(g => g.DescendantsAndSelf()).SelectMany(n => n.Kb)).Concat(item.Kb).Distinct()]);
    }

    /// <summary>Built from recovered or skipped input anywhere in the item.</summary>
    public static bool Partial(FishItem item) => item.DescendantsAndSelf().Any(n => n.IsPartial);

    private static LocusGroupSummary Group(string input, LocusGroup g) => new(
        g.Span,
        g.Span.Of(input),
        [.. g.Loci.Select(Locus)],
        [.. g.Separators.Select(SeparatorText)],
        g.IsPosition,
        g.SharedCount?.Text,
        [.. g.Qualifiers.Select(q => q.Word)],
        [.. g.Breakpoints.Select(AbnormalityList.Summary)]);

    private static LocusSummary Locus(Locus l)
    {
        var start = l.LeadingSignsSpan?.Start ?? l.NameSpan.Start;
        var end = new[] { l.NameSpan.End, l.StatusSpan?.End ?? 0, l.Count?.Span.End ?? 0 }
            .Concat(l.Qualifiers.Select(q => q.Span.End))
            .Max();
        return new LocusSummary(
            TextSpan.FromBounds(start, end),
            l.Name,
            l.LeadingSigns + l.Status,
            l.Count?.Text,
            l.Count?.Low,
            l.Count?.High,
            l.Count?.IsOpenEnded ?? false,
            [.. l.Qualifiers.Select(q => q.Word)]);
    }

    private static string SeparatorText(LocusSeparator s) => s switch
    {
        LocusSeparator.Comma => ",",
        LocusSeparator.Semicolon => ";",
        LocusSeparator.Slash => "/",
        LocusSeparator.Con => "con",
        LocusSeparator.Sep => "sep",
        _ => string.Empty,
    };
}
