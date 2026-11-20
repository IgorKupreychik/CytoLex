using System.Globalization;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SEX.UNEVEN_PLOIDY_MALE</c> (§11.2 ¶4): in a male tumour at an uneven ploidy level k, sex chromosome changes are
/// stated relative to X (k = 1), XXY (3), XXXYY (5) …, i.e. (k + 1)/2 X and (k − 1)/2 Y. The field plus the lost minus
/// the gained X/Y must give that reference. Fires only when it does not, but does with X and Y swapped in the signed sex
/// chromosome changes (<c>68&lt;3n&gt;,XY,–Y</c> → <c>–X</c>): that swap is the fix. Male = a Y in the field or in a change.
/// Not checked in lines with <c>c</c>, written with a reference, or that the count check skips.
/// </summary>
[KbRule("SEX.UNEVEN_PLOIDY_MALE")]
internal sealed class UnevenPloidyMaleCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var line in context.Model.CellLines)
        {
            if (line.Sex is not { } sex || line.BasedOn is not null || ChromosomeCount.IsUncountable(line)
                || line.DescendantsAndSelf().Any(n => n is ConstitutionalMark)
                || ChromosomeCount.ReferenceLevel(line) is not { } level || level % 2 == 0)
            {
                continue;
            }

            var changes = line.Items.OfType<WholeChromosomeChange>()
                .Where(w => w.Chromosome.IsSexChromosome && w.Sign is GainLoss.Gain or GainLoss.Loss && !w.IsPartial)
                .ToList();
            if (changes.Count == 0 || !(sex.Chromosomes.Contains("Y") || changes.Any(c => c.Chromosome.Name == "Y")))
            {
                continue;
            }

            var reference = ((level + 1) / 2, (level - 1) / 2);
            if (Original(sex, changes, swap: false) == reference || Original(sex, changes, swap: true) != reference)
            {
                continue;
            }

            var edits = changes.Select(c => new TextEdit(c.Chromosome.Span, Swap(c.Chromosome.Name))).ToList();
            context.ReportWithFix(
                changes[0].Span,
                string.Create(CultureInfo.InvariantCulture, $"In a male tumour at an uneven ploidy level ({level}n), sex chromosome changes are stated relative to {Reference(level)}: '{sex.Span.Of(context.Input)}' is reached with {string.Join(",", changes.Select(c => (c.Sign == GainLoss.Gain ? "+" : "–") + Swap(c.Chromosome.Name)))}."),
                new FixProposal([.. edits]),
                [sex.Span]);
        }
    }

    /// <summary>The X and Y counts before the changes: the field, plus the lost, minus the gained.</summary>
    private static (int X, int Y) Original(SexComplement sex, List<WholeChromosomeChange> changes, bool swap)
    {
        var x = sex.Chromosomes.Count(c => c == "X");
        var y = sex.Chromosomes.Count(c => c == "Y");
        foreach (var c in changes)
        {
            var copies = c.Copies?.Count ?? 1;
            var delta = c.Sign == GainLoss.Loss ? copies : -copies;
            if ((c.Chromosome.Name == "X") != swap)
            {
                x += delta;
            }
            else
            {
                y += delta;
            }
        }

        return (x, y);
    }

    private static string Swap(string letter) => letter == "X" ? "Y" : "X";

    private static string Reference(int level) => new string('X', (level + 1) / 2) + new string('Y', (level - 1) / 2);
}
