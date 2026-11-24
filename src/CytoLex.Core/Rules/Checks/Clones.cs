using System.Collections.Immutable;
using System.Text;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>What a cell line is for the clone-order rules (§11.1.6 ¶1, §11.3 ¶1).</summary>
internal enum CloneLineKind
{
    /// <summary>An abnormal clone.</summary>
    Clone,

    /// <summary>A normal diploid line: <c>46,XX</c> or <c>46,XY</c> with no changes and no reference.</summary>
    NormalDiploid,

    /// <summary>A line with only constitutional (<c>c</c>) anomalies and no acquired change (§11.3 ¶1).</summary>
    ConstitutionalOnly,
}

/// <summary>One cell line as the clone rules see it.</summary>
/// <param name="Index">Its index in <see cref="KaryotypeModel.CellLines"/>.</param>
/// <param name="Line">The cell line.</param>
/// <param name="Kind">Clone, normal diploid or constitutional-only.</param>
/// <param name="Changes">
/// Its acquired changes, references expanded (the target's changes plus its own, as named: no <c>×n</c>, no cancellation;
/// what a line contains is <see cref="CloneExpansion"/>, T-070).
/// </param>
/// <param name="HasOmitted">The line (or a clone it refers to) has <c>…</c>: its changes are not all known.</param>
internal sealed record CloneLine(int Index, CellLine Line, CloneLineKind Kind, ImmutableArray<KaryotypeItem> Changes, bool HasOmitted)
{
    /// <summary>The number of cells (<c>[n]</c>, <c>[cpN]</c>); <c>null</c> when not given.</summary>
    public int? Cells => Line.CellCount?.Cells;

    /// <summary>Written with <c>idem</c>/<c>sl</c>/<c>sdl</c>.</summary>
    public bool IsReference => Line.BasedOn is not null;
}

/// <summary>
/// Shared analysis of the clone rules (§11.1, T-032): per cell line its kind and acquired changes; per side of <c>//</c>
/// (recipient and donor lines are ordered separately, §4.1 ¶10) the related groups; the tie key of §11.1.6 ¶1 and §4.1 ¶9;
/// and line reorder fixes that keep every <c>idem</c>/<c>sl</c>/<c>sdl</c> pointing to the same clone.
/// </summary>
internal sealed class Clones
{
    private readonly string _input;
    private readonly KaryotypeModel _model;
    private readonly ImmutableArray<CloneLine> _lines;

    private Clones(string input, KaryotypeModel model)
    {
        _input = input;
        _model = model;
        var built = new CloneLine[model.CellLines.Length];
        for (var i = 0; i < built.Length; i++)
        {
            built[i] = Build(i, built);
        }

        _lines = [.. built];
        Sides = [.. SplitSides()];
    }

    /// <summary>The lines, in input order.</summary>
    public ImmutableArray<CloneLine> Lines => _lines;

    /// <summary>The lines of each side of <c>//</c> (one side without it), in input order.</summary>
    public ImmutableArray<ImmutableArray<CloneLine>> Sides { get; }

    /// <summary>The analysis of <paramref name="model"/>.</summary>
    public static Clones Of(string input, KaryotypeModel model) => new(input, model);

    /// <summary>
    /// Whether the neoplastic clone rules leave the karyotype alone: a <c>mos</c>/<c>chi</c> karyotype is a constitutional
    /// mosaic or chimera (§4.1 ¶9, <c>KARYO.MOSAIC_CHIMERA_PREFIX</c>), not a tumour.
    /// </summary>
    public static bool IsConstitutionalMosaicOrChimera(KaryotypeModel model) =>
        model.Relation is CellLineRelation.Mosaic or CellLineRelation.Chimera;

    /// <summary>Whether a side has recovered or skipped input (its order is left alone).</summary>
    public static bool IsPartial(ImmutableArray<CloneLine> side) =>
        side.Any(l => l.Line.DescendantsAndSelf().Any(n => n.IsPartial));

    /// <summary>The text of a line in the input.</summary>
    public string Text(CloneLine line) => line.Line.Span.Of(_input);

    // ── Relation ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether <paramref name="sub"/> is a subclone of <paramref name="clone"/>: it refers to it (directly or through a
    /// chain), or both are written in full with known changes and <paramref name="sub"/> has every change of
    /// <paramref name="clone"/> and more (§11.1.4 ¶1).
    /// </summary>
    public bool IsSubclone(CloneLine sub, CloneLine clone)
    {
        var seen = new HashSet<int>();
        for (var l = sub; l.Line.BasedOn?.TargetIndex is { } t && seen.Add(t);)
        {
            if (t == clone.Index)
            {
                return true;
            }

            l = _lines[t];
        }

        return IsStrictSuperset(sub, clone);
    }

    /// <summary>Both written in full with known changes, and <paramref name="sub"/> has every change of <paramref name="clone"/> and more.</summary>
    public bool IsStrictSuperset(CloneLine sub, CloneLine clone) =>
        !sub.IsReference && !clone.IsReference && !sub.HasOmitted && !clone.HasOmitted
        && sub.Kind == CloneLineKind.Clone && clone.Kind == CloneLineKind.Clone
        && clone.Changes.Length > 0 && sub.Changes.Length > clone.Changes.Length
        && clone.Changes.All(c => sub.Changes.Any(s => SameChange(s, c)));

    /// <summary>The clone lines of a side, grouped: lines related to another line (stemline and subclones) and the rest (unrelated singletons).</summary>
    public (ImmutableArray<CloneLine> Related, ImmutableArray<CloneLine> Unrelated) Groups(ImmutableArray<CloneLine> side)
    {
        var clones = side.Where(l => l.Kind == CloneLineKind.Clone).ToList();
        var related = clones.Where(a => clones.Any(b => !ReferenceEquals(a, b) && (IsSubclone(a, b) || IsSubclone(b, a)))).ToHashSet();
        return ([.. clones.Where(related.Contains)], [.. clones.Where(l => !related.Contains(l))]);
    }

    /// <summary>
    /// Two changes are the same: equal text, or the same symbols and chromosomes with one written without breakpoints (an
    /// abbreviated repeat, §11.1.3 E2 <c>der(2)t(2;5)</c>).
    /// </summary>
    private bool SameChange(KaryotypeItem a, KaryotypeItem b)
    {
        if (string.Equals(a.Span.Of(_input), b.Span.Of(_input), StringComparison.Ordinal))
        {
            return true;
        }

        return a is Abnormality x && b is Abnormality y && x.Sign == y.Sign
            && (!x.Breakpoints.Any() || !y.Breakpoints.Any())
            && string.Equals(CloneExpansion.Skeleton(x), CloneExpansion.Skeleton(y), StringComparison.Ordinal);
    }

    // ── Tie keys ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A sex chromosome abnormality: a change of X/Y, or at the diploid level a sex field other than XX/XY (§11.1.6 ¶1, §4.1 ¶9).</summary>
    public static bool HasSexAbnormality(CloneLine line, IEnumerable<KaryotypeItem> changes) =>
        changes.Any(c => Chromosomes(c).Any(ch => ch.IsSexChromosome))
        || (line.Line.Sex is { } sex && ChromosomeCount.ReferenceLevel(line.Line) == 2
            && string.Join(string.Empty, sex.Chromosomes) is not ("XX" or "XY"));

    /// <summary>The lowest autosome number among the changes; <see cref="int.MaxValue"/> without one.</summary>
    public static int LowestAutosome(IEnumerable<KaryotypeItem> changes) =>
        changes.SelectMany(Chromosomes).Where(c => !c.IsSexChromosome && c.Number is not null).Select(c => c.Number!.Value).DefaultIfEmpty(int.MaxValue).Min();

    /// <summary>Only whole-chromosome changes (and a sex field): a numerical abnormality (§4.1 ¶9).</summary>
    public static bool IsNumerical(IEnumerable<KaryotypeItem> changes) => changes.All(c => c is WholeChromosomeChange);

    /// <summary>The chromosomes a change names.</summary>
    public static IEnumerable<ChromosomeRef> Chromosomes(KaryotypeItem item) => item switch
    {
        WholeChromosomeChange w => [w.Chromosome],
        Abnormality a when !a.Parts.IsEmpty => a.Parts[0].Chromosomes,
        _ => [],
    };

    // ── Reorder fixes ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A fix that writes the lines of <paramref name="side"/> in <paramref name="order"/> (each line's text unchanged, the
    /// separators kept in place); <c>null</c> when the order is unchanged, or when it would change what an
    /// <c>idem</c>/<c>sl</c>/<c>sdl</c> refers to (the KB hint is given instead, T-032).
    /// </summary>
    public FixProposal? Reorder(ImmutableArray<CloneLine> side, IReadOnlyList<CloneLine> order)
    {
        if (order.Count != side.Length || order.Select(l => l.Index).SequenceEqual(side.Select(l => l.Index)) || !PreservesReferences(side, order))
        {
            return null;
        }

        var sb = new StringBuilder();
        for (var k = 0; k < order.Count; k++)
        {
            sb.Append(Text(order[k]));
            if (k + 1 < side.Length)
            {
                sb.Append(_input, side[k].Line.Span.End, side[k + 1].Line.Span.Start - side[k].Line.Span.End);
            }
        }

        return FixProposal.Replace(TextSpan.FromBounds(side[0].Line.Span.Start, side[^1].Line.Span.End), sb.ToString());
    }

    /// <summary>Whether every reference still points to the same line after <paramref name="side"/> is reordered.</summary>
    private bool PreservesReferences(ImmutableArray<CloneLine> side, IReadOnlyList<CloneLine> order)
    {
        if (!_lines.Any(l => l.IsReference))
        {
            return true;
        }

        var newOrder = new List<CloneLine>(_lines.Length);
        var start = side[0].Index;
        newOrder.AddRange(_lines.Take(start));
        newOrder.AddRange(order);
        newOrder.AddRange(_lines.Skip(start + side.Length));
        var targets = CloneReferences.Resolve([.. newOrder.Select(l => CloneReferences.Of(l.Line))]);
        for (var k = 0; k < newOrder.Count; k++)
        {
            var before = newOrder[k].Line.BasedOn?.TargetIndex;
            var after = targets[k] is { } t ? newOrder[t].Index : (int?)null;
            if (before != after)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A stable sort of <paramref name="lines"/> by <paramref name="key"/>.</summary>
    public static List<CloneLine> StableSort<TKey>(IEnumerable<CloneLine> lines, Func<CloneLine, TKey> key, IComparer<TKey> comparer) =>
        [.. lines.Select((l, i) => (l, i)).OrderBy(x => key(x.l), comparer).ThenBy(x => x.i).Select(x => x.l)];

    // ── Building ──────────────────────────────────────────────────────────────────────────────────────

    private CloneLine Build(int index, CloneLine?[] built)
    {
        var line = _model.CellLines[index];
        var reference = line.BasedOn;
        var own = line.Items
            .Where(i => !ReferenceEquals(i, reference?.Item) && i is not OmittedItems && !(i is SuffixedItem { Constitutional: not null }))
            .ToList();
        var omitted = line.Items.Any(i => i is OmittedItems);
        var changes = new List<KaryotypeItem>();
        if (reference?.TargetIndex is { } t && t < index && built[t] is { } target)
        {
            changes.AddRange(target.Changes);
            omitted |= target.HasOmitted;
        }

        changes.AddRange(own);
        var kind = CloneLineKind.Clone;
        if (reference is null && own.Count == 0 && !omitted)
        {
            var normalField = line.Sex is { Constitutional: null } sex && string.Join(string.Empty, sex.Chromosomes) is "XX" or "XY";
            if (line.Items.IsEmpty && normalField && line.Count is { From: 46, IsRange: false })
            {
                kind = CloneLineKind.NormalDiploid;
            }
            else if (!line.Items.IsEmpty || line.Sex?.Constitutional is not null)
            {
                kind = CloneLineKind.ConstitutionalOnly;
            }
        }

        return new CloneLine(index, line, kind, [.. changes], omitted);
    }

    private IEnumerable<ImmutableArray<CloneLine>> SplitSides()
    {
        var current = ImmutableArray.CreateBuilder<CloneLine>();
        foreach (var l in _lines)
        {
            if (l.Line.SeparatorBefore == CellLineSeparator.DoubleSlash && current.Count > 0)
            {
                yield return current.ToImmutable();
                current.Clear();
            }

            current.Add(l);
        }

        if (current.Count > 0)
        {
            yield return current.ToImmutable();
        }
    }
}
