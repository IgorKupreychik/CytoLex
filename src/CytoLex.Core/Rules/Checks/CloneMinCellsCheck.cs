using System.Globalization;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>CLONE.MIN_CELLS</c> (§11.1.1 ¶1, a warning: the author may define the criteria): a clone needs two cells with the
/// same gain or structural change, three with the same loss; two cells sharing a loss together with a gain or structural
/// change are accepted. Fires on (a) a clone of two cells whose changes (with those of the clone it refers to) are all
/// losses, and (b) in a composite karyotype, a per-abnormality count of one cell, or of two for a loss (§11.1.5). A clone
/// of one cell is <c>CLONE.SINGLE_CELL_REPORTING</c>'s. Lines with <c>…</c> and constitutional-only lines are skipped.
/// Not checked in <c>mos</c>/<c>chi</c> karyotypes.
/// </summary>
[KbRule("CLONE.MIN_CELLS")]
internal sealed class CloneMinCellsCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (Clones.IsConstitutionalMosaicOrChimera(context.Model))
        {
            return;
        }

        foreach (var l in Clones.Of(context.Input, context.Model).Lines.Where(l => l.Kind == CloneLineKind.Clone && !l.Line.IsPartial))
        {
            if (l.Line.CellCount is { IsComposite: true })
            {
                foreach (var item in l.Line.Items.OfType<SuffixedItem>().Where(i => i.CellCount?.Cells is { } n && (n == 1 || (n == 2 && IsLoss(i)))))
                {
                    var n = item.CellCount!.Cells!.Value;
                    context.Report(item.CellCount.Span, string.Create(CultureInfo.InvariantCulture, $"'{item.Span.Of(context.Input)}' is seen in {n} cell(s): each abnormality of a composite karyotype must be clonal ({(IsLoss(item) ? "three cells for a loss" : "two cells")})."), [item.Span]);
                }

                continue;
            }

            if (l.Cells == 2 && !l.HasOmitted && l.Changes.Length > 0 && l.Changes.All(IsLoss))
            {
                context.Report(l.Line.CellCount!.Span, $"'{l.Line.Span.Of(context.Input)}' has only losses, seen in two cells: a clone with a chromosome loss needs at least three cells.");
            }
        }
    }

    private static bool IsLoss(KaryotypeItem item) => item is WholeChromosomeChange { Sign: GainLoss.Loss };
}
