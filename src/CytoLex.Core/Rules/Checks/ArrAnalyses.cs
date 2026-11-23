using System.Collections.Immutable;
using System.Globalization;
using CytoLex.Core.Composing;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>A fault of an arr result, with the KB rule it breaks (one fault, one finding).</summary>
internal sealed record ArrFinding(string Rule, TextSpan Span, string Detail, FixProposal? Fix = null)
{
    public ImmutableArray<TextSpan> SecondarySpans { get; init; } = [];
}

/// <summary>
/// The analyses of an arr result (ISCN 2016 ch. 14, T-034), shared by <see cref="ArrRulesCheck"/>: each fault is found
/// once and tagged with its rule, so the rules never report the same text twice.
/// </summary>
internal static class ArrAnalyses
{
    public const string GenomeBuild = "GEN.GENOME_BUILD_REQUIRED";
    public const string NucleotideFormat = "ARR.NUCLEOTIDE_FORMAT";
    public const string NormalResult = "ARR.NORMAL_RESULT";
    public const string AberrationsOnly = "ARR.ABERRATIONS_ONLY";
    public const string AberrationOrder = "ARR.ABERRATION_ORDER";
    public const string RegionFormat = "ARR.REGION_FORMAT";
    public const string PterToQter = "ARR.PTER_TO_QTER";
    public const string CopyNumber = "ARR.COPY_NUMBER";
    public const string MosaicProportion = "ARR.MOSAIC_PROPORTION";
    public const string InheritanceSpacing = "ARR.INHERITANCE_SPACING";
    public const string Zygosity = "ARR.ZYGOSITY";
    public const string ComplexCx = "ARR.COMPLEX_CX";
    public const string Chromothripsis = "ARR.CHROMOTHRIPSIS";

    /// <summary>Every fault of <paramref name="model"/>, in input order per analysis.</summary>
    public static IEnumerable<ArrFinding> Findings(ArrModel model, string input) =>
        Build(model, input)
            .Concat(Positions(model))
            .Concat(Order(model, input))
            .Concat(NormalGroups(model, input))
            .Concat(Regions(model, input))
            .Concat(Results(model))
            .Concat(Qualifiers(model));

    // ── Genome build (GEN.GENOME_BUILD_REQUIRED, §4.3 ¶2, §14.1 ¶2) ──────────────────────────────────

    private static IEnumerable<ArrFinding> Build(ArrModel model, string input)
    {
        if (model.Build is null)
        {
            if (model.HasPositions)
            {
                yield return new(GenomeBuild, model.Keyword, "Nucleotide positions are given without the genome build (e.g. arr[GRCh38]).");
            }
        }
        else if (!model.Build.FollowedBySpace && !model.Build.IsPartial && model.Build.CloseBracket.End < model.Span.End)
        {
            yield return new(GenomeBuild, model.Build.CloseBracket, $"A space follows the genome build '{model.Build.Span.Of(input)}'.");
        }
    }

    // ── Nucleotide positions (ARR.NUCLEOTIDE_FORMAT, §14.1 ¶2) ───────────────────────────────────────

    private static IEnumerable<ArrFinding> Positions(ArrModel model)
    {
        foreach (var item in model.DescendantsAndSelf().OfType<ArrPositionItem>().Where(i => !i.IsPartial))
        {
            var edits = item.GroupSeparators.Select(s => new TextEdit(s, string.Empty)).ToList();
            var faults = new List<string>();
            if (edits.Count > 0)
            {
                faults.Add("positions are written without thousands separators");
            }

            if (item is { Joiner: not null and not "_", JoinerSpan: { } joiner })
            {
                edits.Add(new TextEdit(joiner, "_"));
                faults.Add($"a span is joined with an underscore (_), not '{item.Joiner}'");
            }

            if (faults.Count > 0)
            {
                yield return new(NucleotideFormat, item.Span, Capitalize(string.Join("; ", faults)) + ".", new FixProposal([.. edits]));
            }
        }
    }

    // ── Order (ARR.NORMAL_RESULT, ARR.ABERRATION_ORDER, §14.2 ¶1, ¶3) ────────────────────────────────

    /// <summary>
    /// A normal result (§14.2 ¶1): whole chromosomes only, every autosome once at ×2, and the sex chromosomes X×2 or
    /// X,Y×1, with no proportion or qualifier.
    /// </summary>
    public static bool IsNormalResult(ArrModel model)
    {
        if (model.Aberrations.IsEmpty || model.Aberrations.Any(a => !a.IsWholeChromosomes || a.IsPartial || a.Proportion is not null || !a.Qualifiers.IsEmpty
            || a.Copy is not { Kind: ArrResultKind.CopyNumber, IsRange: false, Min: { } }))
        {
            return false;
        }

        var copies = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var a in model.Aberrations)
        {
            foreach (var entry in a.Chromosomes)
            {
                if (entry.First.Arm is not null)
                {
                    return false;
                }

                foreach (var name in entry.Names)
                {
                    if (!copies.TryAdd(name, a.Copy!.Min!.Value))
                    {
                        return false;
                    }
                }
            }
        }

        var autosomes = Enumerable.Range(1, 22).Select(i => i.ToString(CultureInfo.InvariantCulture));
        var sex = copies.Where(c => c.Key is "X" or "Y").ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
        return copies.Count == 22 + sex.Count
            && autosomes.All(n => copies.GetValueOrDefault(n) == 2)
            && (sex.Count == 1 ? sex.GetValueOrDefault("X") == 2 : sex.Count == 2 && sex["X"] == 1 && sex["Y"] == 1);
    }

    private static IEnumerable<ArrFinding> Order(ArrModel model, string input)
    {
        var rule = IsNormalResult(model) ? NormalResult : AberrationOrder;
        var aberrations = model.Aberrations.Where(a => !a.IsPartial).ToList();
        if (aberrations.Count != model.Aberrations.Length)
        {
            yield break; // recovered input: the order can't be judged
        }

        foreach (var group in aberrations.Where(a => a.Chromosomes.Length > 1))
        {
            var entries = group.Chromosomes.ToList();
            var sorted = StableSort(entries, (x, y) => Key(x.Names[0]).CompareTo(Key(y.Names[0])));
            if (FirstMisplaced(entries, sorted) is { } i)
            {
                var detail = rule == NormalResult
                    ? "In a normal result the autosomes come first and the sex chromosomes after them."
                    : $"'{sorted[i].Span.Of(input)}' belongs before '{entries[i].Span.Of(input)}': chromosomes from the lowest number, sex chromosomes last.";
                yield return new(rule, group.RegionSpan, detail, Reorder(input, entries, sorted)) { SecondarySpans = [sorted[i].Span] };
            }
        }

        var order = StableSort(aberrations, Compare);
        if (FirstMisplaced(aberrations, order) is { } k)
        {
            var detail = rule == NormalResult
                ? "In a normal result the autosomes come first and the sex chromosomes after them."
                : $"'{order[k].Span.Of(input)}' belongs before '{aberrations[k].Span.Of(input)}': by chromosome number (sex chromosomes last), then from pter to qter.";
            yield return new(rule, order[k].Span, detail, Reorder(input, aberrations, order)) { SecondarySpans = [aberrations[k].Span] };
        }
    }

    /// <summary>The sort key of a chromosome: 1–22, then X (23) and Y (24); unknown last.</summary>
    public static int Key(string name) =>
        name switch
        {
            "X" => 23,
            "Y" => 24,
            _ => int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : int.MaxValue,
        };

    /// <summary>Order of two results: by lowest chromosome, then from pter to qter on one chromosome; 0 when they can't be ordered.</summary>
    private static int Compare(ArrAberration a, ArrAberration b)
    {
        var (ka, kb) = (a.ChromosomeNames.Select(Key).DefaultIfEmpty(int.MaxValue).Min(), b.ChromosomeNames.Select(Key).DefaultIfEmpty(int.MaxValue).Min());
        if (ka != kb)
        {
            return ka.CompareTo(kb);
        }

        if (a.Regions.IsEmpty || b.Regions.IsEmpty)
        {
            return 0;
        }

        var (ra, rb) = (a.Regions[0], b.Regions[0]);
        if (ra.Positions?.Items.FirstOrDefault()?.Start is { } sa && rb.Positions?.Items.FirstOrDefault()?.Start is { } sb)
        {
            return sa.CompareTo(sb);
        }

        return ra.First is { } fa && rb.First is { } fb && BandPosition.Of(fa) is { } pa && BandPosition.Of(fb) is { } pb
            ? pa.CompareAlong(pb) ?? 0
            : 0;
    }

    /// <summary>An insertion sort that keeps items the comparison can't order (0) in written order.</summary>
    private static List<T> StableSort<T>(List<T> items, Func<T, T, int> compare)
    {
        var result = new List<T>(items.Count);
        foreach (var item in items)
        {
            var at = result.Count;
            while (at > 0 && compare(result[at - 1], item) > 0)
            {
                at--;
            }

            result.Insert(at, item);
        }

        return result;
    }

    private static int? FirstMisplaced<T>(List<T> written, List<T> sorted)
        where T : class
    {
        for (var i = 0; i < written.Count; i++)
        {
            if (!ReferenceEquals(written[i], sorted[i]))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>The items' text in the new order, the separators between them kept as written.</summary>
    private static FixProposal Reorder<T>(string input, List<T> written, List<T> order)
        where T : MeaningNode
    {
        var separators = written.Zip(written.Skip(1), (x, y) => TextSpan.FromBounds(x.Span.End, y.Span.Start).Of(input)).ToList();
        var text = string.Concat(order.Select((n, i) => n.Span.Of(input) + (i < separators.Count ? separators[i] : "")));
        return FixProposal.Replace(TextSpan.FromBounds(written[0].Span.Start, written[^1].Span.End), text);
    }

    // ── Normal chromosomes listed with aberrations (ARR.ABERRATIONS_ONLY, §14.2 ¶3) ───────────────────

    private static IEnumerable<ArrFinding> NormalGroups(ArrModel model, string input)
    {
        if (IsNormalResult(model) || model.Aberrations.Length < 2)
        {
            yield break;
        }

        static bool PlainTwo(ArrAberration a) =>
            a is { IsWholeChromosomes: true, IsPartial: false, Proportion: null, Qualifiers.IsEmpty: true, Copy: { Kind: ArrResultKind.CopyNumber, IsRange: false, Min: 2 } };
        static bool IsAutosome(string name) => Key(name) <= 22;

        var normal = model.Aberrations.Where(a => PlainTwo(a) && a.ChromosomeNames.Any(IsAutosome)).ToList();
        var others = model.Aberrations.Except(normal).Where(a => !(a.IsWholeChromosomes && a.ChromosomeNames.All(n => !IsAutosome(n)))).ToList();
        if (normal.Count == 0 || others.Count == 0)
        {
            yield break;
        }

        // A polyploid or haploid baseline (most autosomes at another copy number, §14.2.1 E9) makes ×2 an aberration.
        var shifted = model.Aberrations
            .Where(a => a.IsWholeChromosomes && a.Copy is { Kind: ArrResultKind.CopyNumber, Min: { } m } && m != 2)
            .SelectMany(a => a.ChromosomeNames.Where(IsAutosome))
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (shifted >= 11)
        {
            yield break;
        }

        var all = model.Aberrations;
        foreach (var a in normal)
        {
            var i = all.IndexOf(a);
            var removal = i + 1 < all.Length
                ? TextSpan.FromBounds(a.Span.Start, all[i + 1].Span.Start)
                : TextSpan.FromBounds(all[i - 1].Span.End, a.Span.End);
            yield return new(AberrationsOnly, a.Span, $"'{a.Span.Of(input)}' is a normal result; an abnormal result lists only the aberrations.", FixProposal.Delete(removal));
        }
    }

    // ── Regions (ARR.REGION_FORMAT, ARR.PTER_TO_QTER, §14.2 ¶3) ─────────────────────────────────────

    private static IEnumerable<ArrFinding> Regions(ArrModel model, string input)
    {
        foreach (var region in model.Aberrations.SelectMany(a => a.Regions).Where(r => !r.IsPartial))
        {
            foreach (var reading in (ImmutableArray<ArrRegion>)[region, .. region.Alternatives])
            {
                if (!reading.BandHyphens.IsEmpty)
                {
                    yield return new(RegionFormat, reading.BandSpan, "The first and last band are run together, without a hyphen.");
                }

                if (reading.Bands.Length > 2)
                {
                    yield return new(RegionFormat, reading.BandSpan, "A region gives the chromosome once, then its first and last band.");
                }
            }

            if (region.Positions is null)
            {
                yield return new(RegionFormat, region.Span, "The nucleotide positions follow the bands in parentheses.");
            }
            else if (!region.Alternatives.IsEmpty && !region.Positions.IsDetailed
                && region.Positions.Items.Any(i => i.Alternatives.Length != region.Alternatives.Length))
            {
                yield return new(RegionFormat, region.Span, "Two homologous regions joined by 'or' give both the bands and the positions with 'or'.");
            }

            if (Reversed(region, input) is { } reversed)
            {
                yield return reversed;
            }
        }
    }

    /// <summary>Bands or a span written from qter to pter (ARR.PTER_TO_QTER), with a fix that swaps them.</summary>
    private static ArrFinding? Reversed(ArrRegion region, string input)
    {
        var edits = new List<TextEdit>();
        if (region is { First: { } first, Last: { } last } && region.Bands.Length == 2
            && BandPosition.Of(first) is { } pf && BandPosition.Of(last) is { } pl && pf.CompareAlong(pl) > 0)
        {
            var firstSpan = first.ChromosomeWritten && first.Chromosome is { } c ? TextSpan.FromBounds(c.Span.End, first.Span.End) : first.Span;
            edits.Add(new TextEdit(firstSpan, last.Span.Of(input)));
            edits.Add(new TextEdit(last.Span, firstSpan.Of(input)));
        }

        foreach (var item in region.Positions?.Items ?? [])
        {
            if (item is { Start: { } s, End: { } e, EndSpan: { } endSpan } && s > e)
            {
                edits.Add(new TextEdit(item.StartSpan, endSpan.Of(input)));
                edits.Add(new TextEdit(endSpan, item.StartSpan.Of(input)));
            }
        }

        return edits.Count == 0
            ? null
            : new(PterToQter, region.Span, "The bands and positions run from pter to qter (the smaller position first).", new FixProposal([.. edits.OrderBy(e => e.Span.Start)]));
    }

    // ── Copy number, proportion, cx/cth (ARR.COPY_NUMBER, ARR.MOSAIC_PROPORTION, ARR.COMPLEX_CX, ARR.CHROMOTHRIPSIS) ──

    private static IEnumerable<ArrFinding> Results(ArrModel model)
    {
        foreach (var a in model.Aberrations.Where(a => !a.IsPartial))
        {
            if (a.Copy is null && !a.IsDetailed)
            {
                yield return new(CopyNumber, a.Span, "The copy number (×n) follows the region.");
            }

            foreach (var copy in a.DescendantsAndSelf().OfType<ArrCopyNumber>().Where(c => c.RangeSign is not null and not "~"))
            {
                yield return new(CopyNumber, copy.Span, "A copy-number range uses the approximate sign (~).");
            }

            if (a.Results.Length > 1)
            {
                var rule = a.Results.FirstOrDefault(r => r.Kind == ArrResultKind.Complex) is not null ? ComplexCx
                    : a.Results.FirstOrDefault(r => r.Kind == ArrResultKind.Chromothripsis) is not null ? Chromothripsis
                    : CopyNumber;
                yield return new(rule, a.Results[1].Span, "Each result has one copy number (or amp, cx, cth).");
            }

            foreach (var r in a.Results.Where(r => r.Kind is ArrResultKind.Complex or ArrResultKind.Chromothripsis && !a.IsWholeChromosomes))
            {
                yield return r.Kind == ArrResultKind.Complex
                    ? new(ComplexCx, r.Span, "cx follows the chromosomes concerned in parentheses (arr(1–22,X)cx).")
                    : new(Chromothripsis, r.Span, "cth follows the chromosome or arm in parentheses ((1p)cth).");
            }

            if (a.Proportion is { } p)
            {
                if (!p.IsFraction)
                {
                    yield return new(MosaicProportion, p.Span, $"The proportion of cells is a fraction such as [0.6], not '{p.Text}'.");
                }

                if (p.BeforeCopyNumber && a.Copy is not null)
                {
                    yield return new(MosaicProportion, p.Span, "The proportion of cells follows the copy number (×1[0.6]).");
                }
            }
        }
    }

    // ── Qualifiers (ARR.INHERITANCE_SPACING, ARR.ZYGOSITY, §14.2 ¶4, §14.2.1) ───────────────────────

    private static IEnumerable<ArrFinding> Qualifiers(ArrModel model)
    {
        foreach (var a in model.Aberrations.Where(a => !a.IsPartial && !a.Qualifiers.IsEmpty))
        {
            for (var i = 0; i < a.Qualifiers.Length; i++)
            {
                var q = a.Qualifiers[i];
                var afterDetailed = q.AfterParenthesis && a.IsDetailed;
                if (afterDetailed && q.SpaceBefore && q.IsInheritance && i == 0)
                {
                    yield return new(InheritanceSpacing, q.Span, $"No space between the closing parenthesis of the detailed description and '{q.Word}'.", FixProposal.Delete(q.Gap));
                }
                else if (!afterDetailed && !q.SpaceBefore)
                {
                    yield return i == 0 && q.IsInheritance
                        ? new(InheritanceSpacing, q.Span, $"A space separates the copy number and '{q.Word}'.", FixProposal.Insert(q.Span.Start, " "))
                        : new(Zygosity, q.Span, $"A space separates '{q.Word}' from the text before it.");
                }
            }

            static int Rank(ArrQualifier q) => q.Word switch
            {
                "mos" => 0,
                "htz" or "hmz" => 1,
                _ => 2,
            };

            for (var i = 1; i < a.Qualifiers.Length; i++)
            {
                if (Rank(a.Qualifiers[i]) < Rank(a.Qualifiers[i - 1]))
                {
                    yield return new(Zygosity, a.Qualifiers[i].Span, $"'{a.Qualifiers[i].Word}' comes before '{a.Qualifiers[i - 1].Word}': mos, then htz/hmz, then the origin.")
                    {
                        SecondarySpans = [a.Qualifiers[i - 1].Span],
                    };
                    break;
                }
            }
        }
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
