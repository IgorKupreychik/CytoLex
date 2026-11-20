using System.Globalization;
using CytoLex.Core.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SEX.ACQUIRED_GAIN_LOSS</c> (§8.2 ¶3): acquired sex chromosome changes are written with + and –. The sex field then
/// holds the constitutional sex chromosomes: after a loss the ones that remain (<c>45,X,–X</c>), with a gain the
/// constitutional ones (<c>47,XX,+X</c>). So the letters of the field, plus the lost X/Y, plus the X/Y in rearrangements
/// (<see cref="ChromosomeCount.RearrangedSexChromosomes"/>) make one per haploid set (k). Not checked in a line that uses
/// <c>c</c> anywhere (the field then follows §8.2 ¶4; the book also prints <c>48,XXY,+mar c</c>, E20, with an unmarked
/// XXY), without a known level, in lines the count check skips, or where the count shows another cause
/// (<c>NUM.PLOIDY_REFERENCE</c>, <c>SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST</c>). The KB context (neoplastic) makes it
/// conditional (<c>47,XXX</c>: error if acquired).
/// </summary>
[KbRule("SEX.ACQUIRED_GAIN_LOSS")]
internal sealed class AcquiredGainLossCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        if (context.Model.IsFragment)
        {
            return;
        }

        var explained = ChromosomeCount.Analyze(context.Model)
            .Where(r => r.Verdict is CountVerdict.OtherPloidyLevel or CountVerdict.RearrangedSexInField)
            .Select(r => r.Line)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var line in context.Model.CellLines)
        {
            if (line.Sex is not { } sex || explained.Contains(line) || line.DescendantsAndSelf().Any(n => n is ConstitutionalMark) || ChromosomeCount.IsUncountable(line)
                || ChromosomeCount.ReferenceLevel(line) is not { } level)
            {
                continue;
            }

            var losses = line.Items.OfType<WholeChromosomeChange>()
                .Where(w => w.Chromosome.IsSexChromosome && w.Sign == GainLoss.Loss)
                .Sum(w => w.Copies?.Count ?? 1);
            var rearranged = ChromosomeCount.RearrangedSexChromosomes(line).Count();
            var total = sex.Chromosomes.Length + losses + rearranged;
            if (total != level)
            {
                var detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"A {ChromosomeCount.LevelName(level)} cell line has {level} constitutional sex chromosome(s), but the field, the lost and the rearranged ones make {total}; an acquired gain or loss is written with + or –.");
                context.ReportWithFix(sex.Span, detail, total > level && losses == 0 && rearranged == 0 ? GainFix(line, sex, total - level) : null);
            }
        }
    }

    /// <summary>
    /// <c>47,XXX</c> → <c>47,XX,+X</c>: only when every letter of the field is the same, so the gained one is certain
    /// (<c>47,XXY</c> could be <c>XY,+X</c> or <c>XX,+Y</c>: no single fix).
    /// </summary>
    private static FixProposal? GainFix(CellLine line, SexComplement sex, int excess)
    {
        if (!FixHelpers.IsPlainSexField(sex, line) || sex.Chromosomes.Distinct().Count() != 1)
        {
            return null;
        }

        var letter = sex.Chromosomes[0];
        var kept = string.Concat(Enumerable.Repeat(letter, sex.Chromosomes.Length - excess));
        var gains = string.Concat(Enumerable.Repeat(",+" + letter, excess));
        return FixProposal.Replace(sex.Span, kept + gains);
    }
}
