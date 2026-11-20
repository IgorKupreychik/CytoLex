using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Rules.Checks;

/// <summary>Which rule explains a cell line's count (first match wins, see <see cref="ChromosomeCount"/>).</summary>
internal enum CountVerdict
{
    /// <summary>The count agrees with the listed changes.</summary>
    Fits,

    /// <summary>The changes are stated relative to another ploidy level than the count's (<c>NUM.PLOIDY_REFERENCE</c>).</summary>
    OtherPloidyLevel,

    /// <summary>The count fits only with the rearranged X/Y also in the sex field (<c>SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST</c>).</summary>
    RearrangedSexInField,

    /// <summary>The count fits only without losses of replaced chromosomes (<c>KARYO.REPLACED_NORMAL_NOT_MISSING</c>).</summary>
    ReplacedNormalLost,

    /// <summary>
    /// The count fits only with a lost X/Y still in the sex field (<c>45,XY,–Y</c>): the field convention of
    /// <c>SEX.ACQUIRED_GAIN_LOSS</c> (acquired) or <c>SEX.CONSTITUTIONAL_COMPLEMENT</c> (constitutional), not the count.
    /// </summary>
    LostSexInField,

    /// <summary>The count fits only with each telomeric association counted as one chromosome (<c>TAS.FORMAT</c>, §9.2.16).</summary>
    TasCountedOnce,

    /// <summary>
    /// The count fits only with the copies of an unsigned rearrangement beyond the normal homologues counted as gains
    /// (<c>47,XX,del(6)(q13q23)×3</c>): reported by <c>KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY</c> at the item (§9.3, T-068).
    /// </summary>
    ExcessReplacingCopies,

    /// <summary>No reading fits (<c>NUM.COUNT_MATCHES_CHANGES</c>).</summary>
    Mismatch,
}

/// <summary>A rearrangement that involves a sex chromosome (<c>t(X;8)</c> → X).</summary>
internal sealed record RearrangedSex(Abnormality Item, string Letter);

/// <summary>A loss of a normal autosome that an unsigned rearrangement of the same line replaces (<c>–4</c> with <c>del(4)</c>).</summary>
internal sealed record ReplacedLoss(WholeChromosomeChange Loss, Abnormality Replacement)
{
    public const string GeneralRule = "KARYO.REPLACED_NORMAL_NOT_MISSING";

    /// <summary>
    /// The rule that reports the loss (one fault, one finding; T-068): the replacement's own format rule for a dicentric
    /// (<c>DIC.FORMAT</c>), a Robertsonian translocation (<c>ROB.FORMAT</c>) and a whole-arm <c>der(a;b)</c>
    /// (<c>DER.WHOLE_ARM_TWO_CHROMOSOMES</c>); else <c>KARYO.REPLACED_NORMAL_NOT_MISSING</c>.
    /// </summary>
    public string Rule => Replacement.Parts switch
    {
        [{ Head: "dic" or "idic" }, ..] => "DIC.FORMAT",
        [{ Head: "rob" }] => "ROB.FORMAT",
        [{ Head: "der", Chromosomes.Length: 2 }] => "DER.WHOLE_ARM_TWO_CHROMOSOMES",
        _ => GeneralRule,
    };
}

/// <summary>The count analysis of one cell line.</summary>
internal sealed record CountResult(
    CellLine Line,
    CountVerdict Verdict,
    bool Certain,
    int Level,
    ImmutableArray<int> Expected,
    int? OtherLevel,
    ImmutableArray<RearrangedSex> RearrangedSex,
    ImmutableArray<ReplacedLoss> ReplacedLosses)
{
    /// <summary>The count (and ploidy) span, where count findings are reported.</summary>
    public TextSpan CountSpan => Line.Ploidy is { } p ? TextSpan.FromBounds(Line.Count!.Span.Start, p.Span.End) : Line.Count!.Span;
}

/// <summary>
/// The chromosome count arithmetic of <c>NUM.COUNT_MATCHES_CHANGES</c> (§8.1 ¶2–3) and the rules that explain a
/// mismatch more precisely. Per cell line:
/// <list type="bullet">
/// <item>Reference level k: <c>&lt;Nn&gt;</c>, else from the count: ≤34 → 1, else ⌊(count + 11) / 23⌋ (§8.1 ¶2, §11.2 ¶2:
/// n ± n/2 with n/2 = 11; the Ploidy table).</item>
/// <item>Expected = 22k + the letters of the sex field + Σ item deltas. Gains and losses ±1 (× copies). A lost X/Y is
/// already left out of a sex field without <c>c</c> (the field lists what remains, <c>45,X,–X</c>, <c>SEX.ACQUIRED_GAIN_LOSS</c>),
/// but not out of a constitutional field (<c>46,XXYc,–X</c>). An unsigned rearrangement adds its products minus the
/// autosomes it replaces: <c>t</c>, <c>ins</c>, <c>tas</c> make one product per chromosome, every other kind one, so
/// <c>dic(13;15)</c>, <c>der(1;3)</c> and <c>rob</c> count −1 (<c>KARYO.REPLACED_NORMAL_NOT_MISSING</c>) and a rearranged X/Y
/// +1 (it is not in the sex field, <c>SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST</c>). <c>dmin</c> is not counted (<c>DMIN.FORMAT</c>).</item>
/// <item>Skipped lines (no finding): fragments, partial input, no count, <c>…</c>, <c>inc</c>, <c>idem</c>/<c>sl</c>/<c>sdl</c>,
/// <c>[cp]</c>, and items whose effect on the count is unknown (unsigned <c>mar</c>, unidentified <c>r</c>, <c>fis</c>, …).</item>
/// <item>Uncertain (→ at most warning): a count range, a range whose ends lie on two levels, <c>?</c> chromosomes in a
/// rearrangement, a leading count range (<c>+1~5mar</c>).</item>
/// </list>
/// </summary>
internal static class ChromosomeCount
{
    private const int _maxLevel = 8;

    /// <summary>Kinds that make one product per chromosome (§9.2.9, §9.2.16, §9.2.17).</summary>
    private static readonly FrozenSet<string> _perChromosomeProducts = new[] { "t", "ins", "tas" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The count analysis of every countable cell line of <paramref name="model"/>.</summary>
    public static IEnumerable<CountResult> Analyze(KaryotypeModel model)
    {
        if (model.IsFragment)
        {
            yield break;
        }

        foreach (var line in model.CellLines)
        {
            if (Analyze(line) is { } result)
            {
                yield return result;
            }
        }
    }

    /// <summary>The ploidy level a count falls in (§8.1 ¶2, §11.2 ¶2).</summary>
    public static int LevelOf(int count) => count <= 34 ? 1 : (count + 11) / 23;

    /// <summary>The name of a ploidy level for messages.</summary>
    public static string LevelName(int level) => level switch
    {
        1 => "near-haploid",
        2 => "near-diploid",
        3 => "near-triploid",
        4 => "near-tetraploid",
        5 => "near-pentaploid",
        6 => "near-hexaploid",
        7 => "near-heptaploid",
        8 => "near-octaploid",
        _ => string.Create(CultureInfo.InvariantCulture, $"{level}n"),
    };

    /// <summary>
    /// The reference level of a line: <c>&lt;Nn&gt;</c>, or the level of a single count or of a count range whose ends lie
    /// on the same level; <c>null</c> otherwise.
    /// </summary>
    public static int? ReferenceLevel(CellLine line)
    {
        if (line.Ploidy is { Level: { } explicitLevel })
        {
            return explicitLevel;
        }

        if (line.Count is not { IsMissing: false, From: { } from } count)
        {
            return null;
        }

        var to = count.To ?? from;
        return LevelOf(from) == LevelOf(to) ? LevelOf(from) : null;
    }

    /// <summary>Whether the line must be left alone by count and sex-field checks (see the class summary).</summary>
    public static bool IsUncountable(CellLine line) =>
        line.IsPartial
        || line.DescendantsAndSelf().Any(n => n.IsPartial)
        || line.Count is not { IsMissing: false, From: not null }
        || line.CellCount is { IsComposite: true }
        || line.Items.Any(i => i is OmittedItems or UnboundItem or BandComposition
            || (i is Abnormality a && a.Kind is "inc" or "idem" or "sl" or "sdl"));

    /// <summary>
    /// The sex chromosomes in unsigned rearrangements of the line (<c>t(X;8)</c> → X, <c>t(X;Y)</c> → X, Y,
    /// <c>der(X)t(X;8)</c> → X; a derivative counts its own chromosome only).
    /// </summary>
    public static IEnumerable<RearrangedSex> RearrangedSexChromosomes(CellLine line) =>
        line.Items.OfType<Abnormality>()
            .Where(a => a.Sign == GainLoss.None && !a.Parts.IsEmpty && !IsUnidentified(a) && a.Kind is not "dmin")
            .SelectMany(a => a.Parts[0].Chromosomes.Where(c => c.IsSexChromosome).Select(c => new RearrangedSex(a, c.Name)));

    private static CountResult? Analyze(CellLine line)
    {
        if (IsUncountable(line))
        {
            return null;
        }

        var count = line.Count!;
        var hasConstitutionalSex = line.Sex?.Constitutional is not null;
        var deltas = new List<ImmutableArray<int>>();
        var certain = !count.IsRange;
        foreach (var item in line.Items)
        {
            if (Delta(item, hasConstitutionalSex) is not { } d)
            {
                return null;
            }

            deltas.Add(d.Values);
            certain &= d.Certain;
        }

        var sum = deltas.Aggregate(ImmutableArray.Create(0), Combine);
        var field = line.Sex?.Chromosomes.Length ?? 0;
        var explicitLevel = line.Ploidy?.Level;
        int[] levels = explicitLevel is { } e
            ? [e]
            : [.. new[] { LevelOf(count.From!.Value), LevelOf(count.To ?? count.From!.Value) }.Distinct()];
        certain &= levels.Length == 1;

        var rearranged = RearrangedSexChromosomes(line).ToImmutableArray();
        var replaced = ReplacedLosses(line).ToImmutableArray();
        var level = levels[0];
        var expected = Shift(sum, (22 * level) + field);

        CountResult Result(CountVerdict verdict, bool isCertain, int? other = null) =>
            new(line, verdict, isCertain, level, expected, other, rearranged, replaced);

        if (levels.Any(k => Fits(Shift(sum, (22 * k) + field), count)))
        {
            return Result(CountVerdict.Fits, certain);
        }

        if (!certain)
        {
            return Result(CountVerdict.Mismatch, false);
        }

        if (explicitLevel is null)
        {
            for (var k = 1; k <= Math.Max(_maxLevel, level + 2); k++)
            {
                if (k != level && Fits(Shift(sum, (22 * k) + field), count))
                {
                    return Result(CountVerdict.OtherPloidyLevel, true, k);
                }
            }
        }

        if (!rearranged.IsEmpty && SexFieldHolds(line.Sex, rearranged) && Fits(Shift(expected, -rearranged.Length), count))
        {
            // A c-marked field gives the constitutional complement, which may hold the rearranged X/Y (§11.3 E9:
            // 45,Xc,t(X;18)(p11.1;q11.1) - the only X is translocated).
            return Result(hasConstitutionalSex ? CountVerdict.Fits : CountVerdict.RearrangedSexInField, true);
        }

        for (var dropped = 1; dropped <= replaced.Length; dropped++)
        {
            if (Fits(Shift(expected, dropped), count))
            {
                return Result(CountVerdict.ReplacedNormalLost, true) with { ReplacedLosses = replaced[..dropped] };
            }
        }

        var sexLosses = line.Items.OfType<WholeChromosomeChange>()
            .Where(w => w.Sign == GainLoss.Loss && w.Chromosome.IsSexChromosome)
            .Sum(w => w.Copies?.Count ?? 1);
        if (sexLosses > 0 && !hasConstitutionalSex && Fits(Shift(expected, -sexLosses), count))
        {
            return Result(CountVerdict.LostSexInField, true);
        }

        var tasExtra = line.Items.OfType<Abnormality>()
            .Where(a => a.Sign == GainLoss.None && a.Parts is [{ Head: "tas", Symbols.Length: 1 } tas] && !tas.Chromosomes.IsEmpty)
            .Sum(a => a.Parts[0].Chromosomes.Length - 1);
        if (tasExtra > 0 && Fits(Shift(expected, -tasExtra), count))
        {
            return Result(CountVerdict.TasCountedOnce, true);
        }

        var excess = line.Items.OfType<Abnormality>().Sum(a => ExcessCopies(a, level));
        if (excess > 0 && Fits(Shift(expected, excess), count))
        {
            return Result(CountVerdict.ExcessReplacingCopies, true);
        }

        return Result(CountVerdict.Mismatch, true);
    }

    /// <summary>
    /// The losses of normal autosomes that an unsigned rearrangement of the same line replaces, each replaced chromosome
    /// of a rearrangement used once (<c>–1,–3,der(1;3)</c> → both losses).
    /// </summary>
    private static IEnumerable<ReplacedLoss> ReplacedLosses(CellLine line)
    {
        var used = new HashSet<ChromosomeRef>(ReferenceEqualityComparer.Instance);
        foreach (var loss in line.Items.OfType<WholeChromosomeChange>().Where(w => w.Sign == GainLoss.Loss && w.Chromosome.Number is not null))
        {
            var match = line.Items.OfType<Abnormality>()
                .Where(a => a.Sign == GainLoss.None && !a.Parts.IsEmpty && !IsUnidentified(a) && a.Kind is not ("dmin" or "upd"))
                .SelectMany(a => a.Parts[0].Chromosomes.Where(c => c.Name == loss.Chromosome.Name && !used.Contains(c)).Select(c => (a, c)))
                .FirstOrDefault();
            if (match.a is not null)
            {
                used.Add(match.c);
                yield return new ReplacedLoss(loss, match.a);
            }
        }
    }

    private static bool SexFieldHolds(SexComplement? sex, ImmutableArray<RearrangedSex> rearranged) =>
        sex is not null
        && rearranged.GroupBy(r => r.Letter).All(g => sex.Chromosomes.Count(c => c == g.Key) >= g.Count());

    private readonly record struct ItemDelta(ImmutableArray<int> Values, bool Certain);

    /// <summary>
    /// The possible count changes of a subclone's own items relative to the clone it refers to (§11.1.4 ¶1: signs are
    /// relative to the stemline, so a lost X/Y counts −1: the subclone has no sex field of its own); <c>null</c> when an
    /// item's effect is unknown.
    /// </summary>
    public static (ImmutableArray<int> Values, bool Certain)? RelativeDelta(IEnumerable<KaryotypeItem> items)
    {
        var sum = ImmutableArray.Create(0);
        var certain = true;
        foreach (var item in items)
        {
            if (Delta(item, hasConstitutionalSex: true) is not { } d)
            {
                return null;
            }

            sum = Combine(sum, d.Values);
            certain &= d.Certain;
        }

        return (sum, certain);
    }

    /// <summary>The possible count changes of one item; <c>null</c> when unknown (the line is skipped).</summary>
    private static ItemDelta? Delta(KaryotypeItem item, bool hasConstitutionalSex)
    {
        switch (item)
        {
            case WholeChromosomeChange w:
                {
                    var copies = w.Copies?.Count ?? 1;
                    return w.Sign switch
                    {
                        GainLoss.Gain => new ItemDelta([copies], true),
                        GainLoss.Loss when w.Chromosome.IsSexChromosome => new ItemDelta([hasConstitutionalSex ? -copies : 0], true),
                        GainLoss.Loss => new ItemDelta([-copies], true),
                        _ => null,
                    };
                }

            case ChromosomeRegion r:
                return r.Sign switch
                {
                    GainLoss.Gain => new ItemDelta([1], true),
                    GainLoss.Loss => new ItemDelta([-1], true),
                    _ => new ItemDelta([0], true),
                };

            case AlternativeItem alt:
                {
                    var readings = alt.Readings.Select(r => Delta(r, hasConstitutionalSex)).ToList();
                    return readings.Any(r => r is null)
                        ? null
                        : new ItemDelta([.. readings.SelectMany(r => r!.Value.Values).Distinct().Order()], readings.All(r => r!.Value.Certain));
                }

            case Abnormality a:
                return AbnormalityDelta(a);

            default:
                return null;
        }
    }

    private static ItemDelta? AbnormalityDelta(Abnormality a)
    {
        if (a.Parts.IsEmpty || a.Parts[0].Head is not { } head)
        {
            return null;
        }

        if (head == "dmin")
        {
            return new ItemDelta([0], true);
        }

        var multiplier = a.Copies?.Count ?? 1;
        var leading = a.LeadingCount;
        int[] leadingValues = leading is null
            ? [1]
            : leading is { From: { } lf, To: { } lt } && lt >= lf ? [.. Enumerable.Range(lf, lt - lf + 1)]
            : leading.From is { } single ? [single] : [];
        if (leadingValues.Length == 0)
        {
            return null;
        }

        var leadingCertain = leadingValues.Length == 1;
        var part = a.Parts[0];
        if (IsUnidentified(a))
        {
            // +mar, +3mar, +r: as many chromosomes as written; unsigned ones have no known effect.
            return a.Sign switch
            {
                GainLoss.Gain => new ItemDelta([.. leadingValues.Select(v => v * multiplier)], leadingCertain),
                GainLoss.Loss => new ItemDelta([.. leadingValues.Select(v => -v * multiplier)], leadingCertain),
                _ => null,
            };
        }

        if (part.Chromosomes.IsEmpty)
        {
            return null;
        }

        var products = _perChromosomeProducts.Contains(head) && !part.Symbols.Any(s => s.Symbol is "der" or "rec" or "ider")
            ? part.Chromosomes.Length
            : 1;
        var hasUnknown = part.Chromosomes.Any(c => c.IsUnknown);
        switch (a.Sign)
        {
            case GainLoss.Gain:
                return new ItemDelta([.. leadingValues.Select(v => v * products * multiplier)], leadingCertain);
            case GainLoss.Loss:
                return new ItemDelta([.. leadingValues.Select(v => -v * products * multiplier)], leadingCertain);
            default:
                if (head is "fis" || leading is not null)
                {
                    return null;
                }

                if (head is "upd")
                {
                    return new ItemDelta([0], true);
                }

                var autosomes = part.Chromosomes.Count(c => !c.IsSexChromosome);
                return new ItemDelta([(products - autosomes) * multiplier], !hasUnknown);
        }
    }

    /// <summary>
    /// The copies of an unsigned rearrangement of one autosome beyond the <paramref name="level"/> normal homologues it can
    /// replace (<c>del(6)(q13q23)×3</c> at the diploid level → 1): extra copies are written with a plus sign (§9.3 ¶1).
    /// </summary>
    public static int ExcessCopies(Abnormality a, int level) =>
        a.Sign == GainLoss.None && a.Copies?.Count is { } copies && copies > level && !IsUnidentified(a)
        && a.Parts is [{ Chromosomes: [{ IsSexChromosome: false, IsUnknown: false }] }, ..] && a.Kind is not ("dmin" or "fis" or "upd")
            ? copies - level
            : 0;

    /// <summary>An unidentified <c>mar</c> or <c>r</c> (no chromosome list), counted only when signed.</summary>
    public static bool IsUnidentified(Abnormality a) =>
        !a.Parts.IsEmpty && a.Parts[0].Chromosomes.IsEmpty && a.Parts[0].Head is "mar" or "r";

    private static ImmutableArray<int> Combine(ImmutableArray<int> left, ImmutableArray<int> right) =>
        [.. left.SelectMany(l => right.Select(r => l + r)).Distinct().Order()];

    private static ImmutableArray<int> Shift(ImmutableArray<int> values, int by) => [.. values.Select(v => v + by)];

    private static bool Fits(ImmutableArray<int> totals, NumberRange count)
    {
        var from = count.From!.Value;
        var to = count.To ?? from;
        return totals.Any(t => t >= from && t <= to);
    }


    /// <summary>The expected total(s) for messages: <c>47</c> or <c>47 or 48</c>.</summary>
    public static string Describe(ImmutableArray<int> totals) =>
        string.Join(" or ", totals.Select(t => t.ToString(CultureInfo.InvariantCulture)));
}
