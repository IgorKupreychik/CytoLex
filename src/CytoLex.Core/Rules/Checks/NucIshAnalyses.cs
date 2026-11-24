using System.Text.RegularExpressions;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// The nuc ish rules (ISCN 2016 §13.3–13.3.2, T-033) that read one hybridization or cell line: the short and detailed
/// forms, shared counts, signal positions, the cells scored and the normal cell line. Called by <see cref="FishAnalyses"/>.
/// </summary>
internal static partial class NucIshAnalyses
{
    public const string SignalCountFormat = "NUC_ISH.SIGNAL_COUNT_FORMAT";
    public const string DetailedFormat = "NUC_ISH.DETAILED_FORMAT";
    public const string SharedCount = "NUC_ISH.SHARED_COUNT";
    public const string CellsScored = "NUC_ISH.CELLS_SCORED";
    public const string AbnormalOverTotal = "NUC_ISH.ABNORMAL_OVER_TOTAL";
    public const string SignalPositionFormat = "NUC_ISH.SIGNAL_POSITION_FORMAT";

    /// <summary><c>ABL1conBCR</c>: two loci with <c>con</c>/<c>sep</c> written without spaces (read as one name).</summary>
    [GeneratedRegex(@"^(?<a>.*[A-Z0-9])(?<w>con|sep)(?<b>[0-9′'A-Z].*)$")]
    public static partial Regex GluedPosition();

    public static IEnumerable<FishFinding> Findings(FishModel model, string input)
    {
        var first = model.CellLines.FirstOrDefault()?.Items.FirstOrDefault();
        if (first?.Region is { Band: null, Groups: [{ SpaceBefore: true } group, ..] } && model.KeywordFollowedBySpace)
        {
            yield return new FishFinding(SignalCountFormat, TextSpan.FromBounds(model.Keyword.End, group.Span.Start),
                "In the short form the parentheses follow nuc ish directly, without a space.");
        }

        foreach (var line in model.CellLines)
        {
            foreach (var region in line.Items.Select(i => i.Region).OfType<FishRegion>())
            {
                foreach (var f in Hybridization(region, input))
                {
                    yield return f;
                }
            }

            if (line.Items.All(i => i.Cells is null))
            {
                yield return new FishFinding(CellsScored, line.Span, "In a cancer specimen the number of cells scored is given in square brackets.");
            }
        }

        foreach (var f in NormalLines(model))
        {
            yield return f;
        }
    }

    private static IEnumerable<FishFinding> Hybridization(FishRegion region, string input)
    {
        foreach (var group in region.Groups)
        {
            foreach (var locus in group.Loci)
            {
                if (!group.IsPosition && locus.Status.Length > 0 && locus.Count is null)
                {
                    yield return new FishFinding(SignalCountFormat, locus.StatusSpan!.Value,
                        $"The number of signals of {locus.Name} is given with × and a number, not with + or –.");
                }

                if (GluedPosition().Match(locus.Name) is { Success: true } m)
                {
                    var fixedName = $"{m.Groups["a"].Value} {m.Groups["w"].Value} {m.Groups["b"].Value}";
                    yield return new FishFinding(SignalPositionFormat, locus.NameSpan, $"{m.Groups["w"].Value} is separated from the loci by a space on each side.",
                        FixProposal.Replace(locus.NameSpan, fixedName));
                }
            }

            // (D21S65×3,D21S64×3): the same count for every locus is written once, after the parentheses.
            if (!group.IsPosition && group.SharedCount is null && group.Loci.Length >= 2
                && group.Loci.All(l => l.Count is not null && l.Status.Length == 0 && l.Qualifiers.IsEmpty)
                && group.Loci.Select(l => l.Count!.Text).Distinct(StringComparer.Ordinal).Count() == 1)
            {
                var text = group.Loci[0].Count!.Span.Of(input);
                var edits = group.Loci.Select(l => new TextEdit(l.Count!.Span, "")).Append(new TextEdit(new TextSpan(group.Close.End, 0), text));
                yield return new FishFinding(SharedCount, group.Span, $"Every locus shows {group.Loci[0].Count!.Text} signals: one {text} follows the parentheses.",
                    new FixProposal([.. edits]));
            }
        }

        // (ABL1 con BCR×1)(ABL1,BCR)×2: the parentheses with the number of signals come first.
        for (var i = 0; i + 1 < region.Groups.Length; i++)
        {
            if (region.Groups[i].IsPosition && region.Groups.Skip(i + 1).FirstOrDefault(g => !g.IsPosition) is { } counts)
            {
                var position = region.Groups[i];
                var countsEnd = counts.SharedCount?.Span.End ?? counts.Span.End;
                var span = TextSpan.FromBounds(position.Span.Start, countsEnd);
                var countsText = TextSpan.FromBounds(counts.Span.Start, countsEnd).Of(input);
                var between = TextSpan.FromBounds(position.Span.End, counts.Span.Start).Of(input);
                yield return new FishFinding(SignalPositionFormat, position.Span, "The parentheses with the number of signals come first, then the relative position.",
                    FixProposal.Replace(span, countsText + between + position.Span.Of(input)));
                break;
            }
        }
    }

    /// <summary><c>(D13S319×0)[100/400]/(D13S319×2)[300/400]</c>: the normal cells are the remainder of the total, not a cell line.</summary>
    private static IEnumerable<FishFinding> NormalLines(FishModel model)
    {
        if (model.CellLines.Length < 2)
        {
            yield break;
        }

        for (var i = 0; i < model.CellLines.Length; i++)
        {
            var line = model.CellLines[i];
            if (line.Cells is not { Total: not null } || !IsNormal(line))
            {
                continue;
            }

            var span = i > 0
                ? TextSpan.FromBounds(model.CellLines[i - 1].Span.End, line.Span.End)
                : TextSpan.FromBounds(line.Span.Start, model.CellLines[1].Span.Start);
            yield return new FishFinding(AbnormalOverTotal, line.Span, "The normal cells are not listed; they are the remainder of the total scored.", FixProposal.Delete(span));
        }
    }

    /// <summary>Every locus of the line shows two signals, with no position or qualifier.</summary>
    private static bool IsNormal(FishCellLine line) =>
        FishNormal.AllTwoSignals(line.Items.Select(i => i.Region).OfType<FishRegion>().SelectMany(r => r.Groups));
}
