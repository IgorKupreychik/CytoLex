using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CytoLex.Core.Context;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>One fault of an in situ result, tagged with its rule.</summary>
internal sealed record FishFinding(string Rule, TextSpan Span, string Detail, FixProposal? Fix = null);

/// <summary>
/// The in situ rules (ISCN 2016 ch. 13, T-033) that read one segment's <see cref="FishModel"/>: the keyword spacing, the
/// ish abnormalities and normal chromosomes, the loci, fib/rev/cgh, and the nuc ish analyses (<see cref="NucIshAnalyses"/>).
/// Each fault is found once and tagged with its rule. The order of loci from pter to qter (ISH.LOCUS_ORDER_PTER_QTER) and
/// of chromosomes in nuc ish (NUC_ISH.CHROMOSOME_ORDER) needs probe positions: <see cref="ProbeOrder"/> (T-103).
/// </summary>
internal static class FishAnalyses
{
    public const string CombinedWithKaryotype = "ISH.COMBINED_WITH_KARYOTYPE";
    public const string LocusDesignation = "ISH.LOCUS_DESIGNATION";
    public const string LocusList = "ISH.LOCUS_LIST";
    public const string StructuralFormat = "ISH.STRUCTURAL_FORMAT";
    public const string LocusStatus = "ISH.LOCUS_STATUS";
    public const string AbnormalSignalCount = "ISH.ABNORMAL_SIGNAL_COUNT";
    public const string NormalChromosomeFormat = "ISH.NORMAL_CHROMOSOME_FORMAT";
    public const string InversionSeparateBreakpoints = "ISH.INVERSION_SEPARATE_BREAKPOINTS";
    public const string CellLines = "ISH.CELL_LINES";
    public const string SignalQualifiers = "ISH.SIGNAL_QUALIFIERS";
    public const string PartialPaint = "ISH.PARTIAL_PAINT";
    public const string FibFormat = "FIB_ISH.FORMAT";
    public const string RevFormat = "REV_ISH.FORMAT";
    public const string CghFormat = "CGH.FORMAT";

    /// <summary>Abnormalities that point to a cancer specimen, where ×n on an abnormal chromosome is the rule (§13.2 ¶4); <c>subtel</c> counts probes.</summary>
    private static readonly FrozenSet<string> _countedSymbols = new[] { "dmin", "ider", "hsr", "subtel" }.ToFrozenSet(StringComparer.Ordinal);

    public static IEnumerable<FishFinding> Findings(FishModel model, string input, SampleContext? context) =>
    [
        .. Keyword(model),
        .. model.Type == NotationType.Ish ? Ish(model, input, context) : [],
        .. model.Type == NotationType.NucIsh ? NucIshAnalyses.Findings(model, input) : [],
        .. model.Type == NotationType.FibIsh ? Fib(model) : [],
        .. Loci(model),
        .. Cells(model),
    ];

    // ── Keyword ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A space follows the technique symbols before the result (ish, rev ish, cgh; nuc ish only before a band).</summary>
    private static IEnumerable<FishFinding> Keyword(FishModel model)
    {
        if (model.KeywordFollowedBySpace || model.CellLines.IsEmpty || model.Keyword.Length == 0)
        {
            yield break;
        }

        var rule = model.Type switch
        {
            NotationType.Ish => CombinedWithKaryotype,
            NotationType.RevIsh => RevFormat,
            NotationType.Cgh => CghFormat,
            NotationType.NucIsh when model.CellLines[0].Items is [{ Region: { Band: not null } }, ..] => NucIshAnalyses.DetailedFormat,
            _ => null,
        };
        if (rule is not null)
        {
            yield return new FishFinding(rule, new TextSpan(model.Keyword.End, 0), $"A space follows '{model.Keyword}' before the result.",
                FixProposal.Insert(model.Keyword.End, " "));
        }
    }

    // ── Metaphase ish ────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<FishFinding> Ish(FishModel model, string input, SampleContext? context)
    {
        foreach (var item in model.CellLines.SelectMany(l => l.Items))
        {
            if (item.Region is { } region)
            {
                foreach (var f in NormalChromosome(region))
                {
                    yield return f;
                }
            }
            else if (item.Abnormality is Abnormality abnormality)
            {
                foreach (var f in AbnormalChromosome(item, abnormality, input, context))
                {
                    yield return f;
                }
            }
        }
    }

    private static IEnumerable<FishFinding> NormalChromosome(FishRegion region)
    {
        if (region.BandInParentheses)
        {
            yield return new FishFinding(NormalChromosomeFormat, region.Band?.Span ?? region.Span, "On a normal chromosome the band designation is not in parentheses.");
        }

        foreach (var locus in region.Groups.SelectMany(g => g.Loci).Where(l => l.Status.Length > 0 && l.Count is null))
        {
            yield return new FishFinding(NormalChromosomeFormat, locus.StatusSpan!.Value,
                $"On a normal chromosome the signals of {locus.Name} are counted with × and a number, not with + or –.");
        }
    }

    private static IEnumerable<FishFinding> AbnormalChromosome(FishItem item, Abnormality abnormality, string input, SampleContext? context)
    {
        var symbols = abnormality.Parts.SelectMany(p => p.Symbols).Select(s => s.Symbol).ToHashSet(StringComparer.Ordinal);
        var groups = item.Groups;

        for (var p = 0; p < abnormality.Parts.Length; p++)
        {
            var part = abnormality.Parts[p];
            var partGroups = groups.Where(g => g.Part == p).ToList();
            var isInversion = part.Symbols.Any(s => s.Symbol == "inv");

            // del(22)(D22S75–)(q11.2q11.2): the breakpoints come before the loci.
            for (var i = 0; i + 1 < partGroups.Count; i++)
            {
                if (!isInversion && partGroups[i].Kind == FishGroupKind.Loci && partGroups[i + 1].Kind == FishGroupKind.Breakpoints)
                {
                    var (lociSpan, bpSpan) = (partGroups[i].Span, partGroups[i + 1].Span);
                    yield return new FishFinding(StructuralFormat, lociSpan, "The breakpoints come before the loci.",
                        FixProposal.Replace(TextSpan.FromBounds(lociSpan.Start, bpSpan.End), bpSpan.Of(input) + lociSpan.Of(input)));
                }
            }

            // dic(14;21)(p11.2p11.2): one breakpoint group per chromosome, separated by a semicolon.
            if (part.Chromosomes.Length >= 2 && part.Groups is [{ Structure: null } only] && only.Breakpoints.Length == part.Chromosomes.Length)
            {
                var edits = only.Breakpoints.Take(only.Breakpoints.Length - 1).Select(b => new TextEdit(new TextSpan(b.Span.End, 0), ";"));
                yield return new FishFinding(StructuralFormat, only.Span, "Each chromosome has its own breakpoint group, separated by a semicolon.", new FixProposal([.. edits]));
            }

            // inv(16)(p13.1q22)(MYH11+,CBFB+;MYH11+,CBFB+): each breakpoint is followed by its own loci.
            if (isInversion && InversionLoci(partGroups) is ({ } bpGroup, { } lociGroup) && part.Groups is [{ Breakpoints: [var b1, var b2] }]
                && lociGroup.Loci is { } loci && loci.Separators.Count(s => s == LocusSeparator.Semicolon) == 1)
            {
                var semi = loci.SeparatorSpans[loci.Separators.IndexOf(LocusSeparator.Semicolon)];
                var first = TextSpan.FromBounds(loci.Open.End, semi.Start).Of(input);
                var second = TextSpan.FromBounds(semi.End, loci.Close.Start).Of(input);
                var replacement = $"({b1.Span.Of(input)})({first})({b2.Span.Of(input)})({second})";
                yield return new FishFinding(InversionSeparateBreakpoints, TextSpan.FromBounds(bpGroup.Span.Start, lociGroup.Span.End),
                    "Each inversion breakpoint is written in its own parentheses, followed by the loci found there.",
                    FixProposal.Replace(TextSpan.FromBounds(bpGroup.Span.Start, lociGroup.Span.End), replacement));
            }
        }

        var counted = context == SampleContext.Acquired || symbols.Overlaps(_countedSymbols);
        // D14Z1/D22Z1+: the members of a contig share the status written after the last one.
        var contigMembers = item.LocusGroups.SelectMany(g => g.Loci.Take(g.Separators.Length).Where((_, i) => g.Separators[i] == LocusSeparator.Slash)).ToHashSet();
        foreach (var locus in item.LocusGroups.SelectMany(g => g.Loci))
        {
            if (locus.LeadingSigns.Length > 0 && locus.LeadingSignsSpan is { } signs)
            {
                yield return new FishFinding(LocusStatus, signs, $"The status follows the locus {locus.Name}.",
                    new FixProposal([new TextEdit(signs, ""), new TextEdit(new TextSpan(locus.NameSpan.End, 0), locus.LeadingSigns)]));
            }
            else if (locus.Status.Length == 0 && locus.Count is null && locus.Qualifiers.IsEmpty && !locus.IsBand && locus.Name.Length > 0 && !IsListedNumberLocus(locus) && !contigMembers.Contains(locus))
            {
                yield return new FishFinding(LocusStatus, locus.NameSpan, $"The locus {locus.Name} on an abnormal chromosome needs its status (+ or –).");
            }

            if (!counted && locus.Count is { Low: { } n, High: null } count)
            {
                yield return new FishFinding(AbnormalSignalCount, count.Span,
                    $"Outside cancer, the {n} signals of {locus.Name} on an abnormal chromosome are shown by repeating the + sign.",
                    n is > 0 and <= 6 ? FixProposal.Replace(count.Span, new string('+', n)) : null);
            }
        }
    }

    private static bool IsListedNumberLocus(Locus locus) => locus.Name.All(char.IsAsciiDigit);

    /// <summary>The breakpoint group and the locus group right after it, in an inversion part.</summary>
    private static (FishGroup? Breakpoints, FishGroup? Loci) InversionLoci(List<FishGroup> groups)
    {
        var bps = groups.Where(g => g.Kind == FishGroupKind.Breakpoints).ToList();
        if (bps.Count != 1)
        {
            return (null, null);
        }

        var i = groups.IndexOf(bps[0]);
        return i + 1 < groups.Count && groups[i + 1].Kind == FishGroupKind.Loci ? (bps[0], groups[i + 1]) : (null, null);
    }

    // ── Loci (all in situ types) ─────────────────────────────────────────────────────────────────────

    private static IEnumerable<FishFinding> Loci(FishModel model)
    {
        foreach (var item in model.CellLines.SelectMany(l => l.Items))
        {
            var chromosomes = item.Abnormality is Abnormality a ? a.Parts.Select(p => p.Chromosomes.Length).ToList() : [];
            foreach (var group in item.LocusGroups)
            {
                foreach (var f in Group(item, group, chromosomes))
                {
                    yield return f;
                }
            }
        }
    }

    private static IEnumerable<FishFinding> Group(FishItem item, LocusGroup group, List<int> partChromosomes)
    {
        for (var i = 0; i < group.Separators.Length; i++)
        {
            if (group.Separators[i] != LocusSeparator.None)
            {
                continue;
            }

            var (left, right) = (group.Loci[i], group.Loci[i + 1]);
            if (left.Name == "pcp")
            {
                // pcp 18p11.2+: the chromosome and band follow pcp directly.
                var gap = TextSpan.FromBounds(left.Span.End, right.Span.Start);
                yield return new FishFinding(PartialPaint, gap, "The chromosome and band follow pcp directly.", FixProposal.Delete(gap));
            }
            else
            {
                yield return new FishFinding(LocusList, group.SeparatorSpans[i], $"A comma separates the loci {left.Name} and {right.Name}.",
                    FixProposal.Insert(group.SeparatorSpans[i].Start, ","));
            }
        }

        // del(15)(…)(SNRPN–;D15S10–): a semicolon separates the loci of different chromosomes only.
        var chromosomes = item.Region is not null ? 1
            : item.Groups.FirstOrDefault(g => g.Loci == group) is { } fg && fg.Part < partChromosomes.Count ? partChromosomes[fg.Part] : 0;
        var semicolons = group.Separators.Select((s, i) => (s, i)).Where(x => x.s == LocusSeparator.Semicolon).Select(x => x.i).ToList();
        if (chromosomes >= 1 && semicolons.Count >= chromosomes)
        {
            foreach (var i in semicolons.Skip(chromosomes - 1))
            {
                yield return new FishFinding(LocusList, group.SeparatorSpans[i],
                    chromosomes == 1 ? "The loci are on one chromosome; a semicolon only separates the loci of different chromosomes."
                        : "More semicolon-separated groups than chromosomes.",
                    FixProposal.Replace(group.SeparatorSpans[i], ","));
            }
        }

        foreach (var locus in group.Loci)
        {
            if (IsLowerCaseLocus(locus))
            {
                yield return new FishFinding(LocusDesignation, locus.NameSpan, $"Locus designations are written in capital letters: {locus.Name.ToUpperInvariant()}.",
                    FixProposal.Replace(locus.NameSpan, locus.Name.ToUpperInvariant()));
            }

            foreach (var q in locus.Qualifiers.Where(q => !q.SpaceBefore))
            {
                yield return new FishFinding(SignalQualifiers, q.Span, $"A space separates {q.Word} from the locus.", FixProposal.Insert(q.Span.Start, " "));
            }

            if (GluedQualifier(locus) is { } at)
            {
                // MYCamp: read as one name by the lexer.
                var word = new TextSpan(at, locus.NameSpan.End - at);
                yield return new FishFinding(SignalQualifiers, word, $"A space separates {locus.Name[(at - locus.NameSpan.Start)..]} from the locus.", FixProposal.Insert(at, " "));
            }
        }
    }

    /// <summary>A locus name with lower-case letters, other than a paint (<c>wcp</c>, <c>pcp</c>), a band or a glued <c>con</c>/<c>sep</c>.</summary>
    private static bool IsLowerCaseLocus(Locus locus) =>
        locus.Name.Any(char.IsAsciiLetterLower) && locus.PaintedChromosome is null && locus.Name != "pcp" && !locus.IsBand
        && !NucIshAnalyses.GluedPosition().IsMatch(locus.Name) && GluedQualifier(locus) is null;

    /// <summary>Where <c>amp</c>, <c>dim</c> or <c>enh</c> glued to the end of an upper-case name starts (<c>MYCamp</c>), or null.</summary>
    private static int? GluedQualifier(Locus locus)
    {
        foreach (var word in LocusGrammar.QualifierWords)
        {
            if (locus.Name.Length > word.Length && locus.Name.EndsWith(word, StringComparison.Ordinal)
                && !locus.Name[..^word.Length].Any(char.IsAsciiLetterLower))
            {
                return locus.NameSpan.End - word.Length;
            }
        }

        return null;
    }

    // ── Cells ────────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<FishFinding> Cells(FishModel model)
    {
        foreach (var cells in model.CellLines.SelectMany(l => l.Items).Select(i => i.Cells).OfType<FishCells>().Where(c => c.IsPercentage))
        {
            yield return new FishFinding(CellLines, cells.Span, "Square brackets hold the number of cells, not a percentage.");
        }
    }

    // ── fib ish ──────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<FishFinding> Fib(FishModel model)
    {
        foreach (var region in model.CellLines.SelectMany(l => l.Items).Select(i => i.Region).OfType<FishRegion>().Where(r => r.Band is null))
        {
            yield return new FishFinding(FibFormat, region.Span, "The chromosomal band of the loci follows fib ish.");
        }
    }
}
