using System.Collections.Immutable;
using System.Text;

namespace CytoLex.Core.Semantics;

/// <summary>A change of an expanded cell line: the item, the line it is written in, and how many copies the line has.</summary>
/// <param name="Item">The item as written (in <paramref name="SourceLine"/>).</param>
/// <param name="SourceLine">The index of the cell line the item is written in.</param>
/// <param name="Copies">The copies in this line: the written <c>×k</c> (1 without one) times every <c>×n</c> of the references on the way, less the copies cancelled.</param>
internal sealed record ExpandedChange(KaryotypeItem Item, int SourceLine, int Copies);

/// <summary>An inherited change that an own item of the line removes (<c>–inv(6)</c> on top of <c>inv(6)</c>, <c>+3</c> on top of <c>–3</c>).</summary>
/// <param name="Item">The inherited item as written.</param>
/// <param name="SourceLine">The cell line it is written in.</param>
/// <param name="RemovedBy">The own item that removes it.</param>
internal sealed record RemovedChange(KaryotypeItem Item, int SourceLine, KaryotypeItem RemovedBy);

/// <summary>One cell line with its clone reference resolved.</summary>
/// <param name="Index">Its index in <see cref="KaryotypeModel.CellLines"/>.</param>
/// <param name="Line">The cell line.</param>
/// <param name="Changes">Its changes: the inherited ones (from the line referred to) and its own, net of cancellations, in input order of the chain.</param>
/// <param name="Removed">Inherited changes removed by an own item.</param>
/// <param name="Cancelling">Own items used up by cancelling an inherited change (they are not in <paramref name="Changes"/>).</param>
/// <param name="Sex">The sex chromosomes of the line (as written, or derived for a reference line); empty when unknown.</param>
/// <param name="SexMark">The constitutional mark of the sex field (of the line or the line it refers to); <c>null</c> without one.</param>
/// <param name="HasOmitted">The line or a line it refers to has <c>…</c>.</param>
/// <param name="IsPartial">The line or a line it refers to has recovered or skipped input, or a reference without a target or multiplier.</param>
internal sealed record ExpandedLine(
    int Index,
    CellLine Line,
    ImmutableArray<ExpandedChange> Changes,
    ImmutableArray<RemovedChange> Removed,
    ImmutableArray<KaryotypeItem> Cancelling,
    ImmutableArray<string> Sex,
    ConstitutionalMark? SexMark,
    bool HasOmitted,
    bool IsPartial)
{
    /// <summary>The changes inherited from the line referred to.</summary>
    public IEnumerable<ExpandedChange> Inherited => Changes.Where(c => c.SourceLine != Index);
}

/// <summary>
/// Expands the clone references of a karyotype (§11.1.4, CLONE.SUBCLONE_REFERENCE, CLONE.POLYPLOID_SUBCLONE; T-070): a line
/// written with <c>idem</c>/<c>sl</c>/<c>sdl</c> has the changes of the line it refers to (<see cref="CellLine.BasedOn"/>,
/// recursively for chains), each multiplied by the <c>×n</c> of a polyploid reference, plus its own. Its signs are relative
/// to the clone referred to: an own item of the opposite sense cancels one copy of a matching inherited change
/// (<c>–inv(6)</c> removes <c>inv(6)</c> without repeating the breakpoints, E14; <c>+3</c> undoes an inherited <c>–3</c>, E13).
/// The sex chromosomes are the referred line's, repeated <c>×n</c>; a net own loss of X or Y removes it from the field and
/// an own gain that cancels an inherited loss puts it back (after a loss the field lists the chromosomes that remain,
/// SEX.ACQUIRED_GAIN_LOSS). Constitutional items are inherited too. Unlike <c>Clones.Changes</c> of the clone checks
/// (the acquired changes named, for order keys), this is what the line contains.
/// </summary>
internal static class CloneExpansion
{
    /// <summary>The expanded lines of <paramref name="model"/>, in input order.</summary>
    public static ImmutableArray<ExpandedLine> Of(KaryotypeModel model)
    {
        var built = new ExpandedLine[model.CellLines.Length];
        for (var i = 0; i < built.Length; i++)
        {
            built[i] = Build(i, model.CellLines[i], built);
        }

        return [.. built];
    }

    /// <summary>
    /// An abnormality without its breakpoints: <c>der(2)t(2;5)</c>. Two changes with the same skeleton, one written
    /// without breakpoints, are the same change (an abbreviated repeat, §11.1.3 E2; a missing stemline abnormality, §11.1.4 E14).
    /// </summary>
    public static string Skeleton(Abnormality a)
    {
        var sb = new StringBuilder();
        foreach (var p in a.Parts)
        {
            sb.Append(string.Join(' ', p.Symbols.Select(s => s.Symbol))).Append(p.SymbolNumber)
                .Append('(').Append(string.Join(';', p.Chromosomes.Select(c => c.Name))).Append(')');
        }

        return sb.ToString();
    }

    private static ExpandedLine Build(int index, CellLine line, ExpandedLine?[] built)
    {
        var reference = line.BasedOn;
        var own = line.Items.Where(i => !ReferenceEquals(i, reference?.Item) && i is not OmittedItems).ToList();
        var omitted = line.Items.Any(i => i is OmittedItems);
        var partial = line.DescendantsAndSelf().Any(n => n.IsPartial);
        if (reference is null)
        {
            return new ExpandedLine(
                index, line, [.. own.Select(i => new ExpandedChange(i, index, CopiesOf(i)))], [], [],
                line.Sex?.Chromosomes ?? [], line.Sex?.Constitutional, omitted, partial);
        }

        var target = reference.TargetIndex is { } t && t < index ? built[t] : null;
        var multiplier = reference.Multiplier ?? 1;
        partial |= target is null || reference.Multiplier is null || target.IsPartial;
        omitted |= target?.HasOmitted ?? false;

        var changes = (target?.Changes ?? []).Select(c => c with { Copies = c.Copies * multiplier }).ToList();
        var sex = line.Sex is { } written
            ? [.. written.Chromosomes]
            : (target?.Sex ?? []).SelectMany(s => Enumerable.Repeat(s, multiplier)).ToList();
        var removed = new List<RemovedChange>();
        var cancelling = new List<KaryotypeItem>();
        var inheritedCount = changes.Count;

        foreach (var item in own)
        {
            var k = changes.FindIndex(0, inheritedCount, c => c.Copies > 0 && Cancels(item, c.Item));
            if (k < 0)
            {
                changes.Add(new ExpandedChange(item, index, CopiesOf(item)));
                if (line.Sex is null && item is WholeChromosomeChange { Sign: GainLoss.Loss, Chromosome.IsSexChromosome: true } loss)
                {
                    sex.Remove(loss.Chromosome.Name);
                }

                continue;
            }

            var hit = changes[k];
            changes[k] = hit with { Copies = hit.Copies - 1 };
            cancelling.Add(item);
            if (changes[k].Copies == 0)
            {
                removed.Add(new RemovedChange(hit.Item, hit.SourceLine, item));
            }

            if (line.Sex is null && hit.Item is WholeChromosomeChange { Sign: GainLoss.Loss, Chromosome.IsSexChromosome: true } lost)
            {
                sex.Add(lost.Chromosome.Name);
            }
        }

        return new ExpandedLine(
            index,
            line,
            [.. changes.Where(c => c.Copies > 0)],
            [.. removed],
            [.. cancelling],
            [.. sex.OrderBy(s => s == "X" ? 0 : s == "Y" ? 1 : 2)],
            line.Sex is not null ? line.Sex.Constitutional : target?.SexMark,
            omitted,
            partial);
    }

    /// <summary>
    /// Whether the own item <paramref name="own"/> cancels one copy of the inherited <paramref name="inherited"/>: the same
    /// whole chromosome or the same abnormality (equal skeleton, equal breakpoints or one written without), with the
    /// opposite sense (a loss of a change that is present or gained; a gain of a change that is lost).
    /// </summary>
    private static bool Cancels(KaryotypeItem own, KaryotypeItem inherited)
    {
        if (own is not SuffixedItem o || inherited is not SuffixedItem i
            || !((o.Sign == GainLoss.Loss && i.Sign != GainLoss.Loss) || (o.Sign == GainLoss.Gain && i.Sign == GainLoss.Loss)))
        {
            return false;
        }

        return (o, i) switch
        {
            (WholeChromosomeChange a, WholeChromosomeChange b) =>
                string.Equals(a.Chromosome.Name, b.Chromosome.Name, StringComparison.Ordinal) && a.Chromosome.IsQuestioned == b.Chromosome.IsQuestioned,
            (Abnormality a, Abnormality b) =>
                string.Equals(Skeleton(a), Skeleton(b), StringComparison.Ordinal)
                && (!a.Breakpoints.Any() || !b.Breakpoints.Any() || a.Breakpoints.Select(Text).SequenceEqual(b.Breakpoints.Select(Text), StringComparer.Ordinal)),
            _ => false,
        };
    }

    private static string Text(Breakpoint b) => (b.Chromosome?.Name ?? string.Empty) + b.Text;

    private static int CopiesOf(KaryotypeItem item) => item is SuffixedItem { Copies.Count: { } n } && n > 0 ? n : 1;
}
