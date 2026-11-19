using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>KARYO.REPLACED_NORMAL_NOT_MISSING</c> (§4.1 ¶6, §9.1 ¶2): a normal chromosome replaced by a rearranged one is not
/// written as missing. Fires on (a) <c>–n</c> together with <c>+R</c>, R a rearrangement of the single chromosome n
/// (<c>–3,+inv(3)</c>: write <c>inv(3)</c>; not <c>fis</c>, whose products are written separately, §9.2.6), and (b) a count
/// that fits only without losses of chromosomes that an unsigned rearrangement replaces (<c>–4,del(4)</c>,
/// <c>–1,–3,der(1;3)</c>; <see cref="ChromosomeCount"/>).
/// </summary>
[KbRule("KARYO.REPLACED_NORMAL_NOT_MISSING")]
internal sealed class ReplacedNormalNotMissingCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        var reported = new HashSet<WholeChromosomeChange>(ReferenceEqualityComparer.Instance);
        foreach (var line in context.Model.CellLines)
        {
            foreach (var loss in line.Items.OfType<WholeChromosomeChange>().Where(w => w.Sign == GainLoss.Loss && !w.Chromosome.IsSexChromosome && !w.IsPartial))
            {
                var gain = line.Items.OfType<Abnormality>().FirstOrDefault(a =>
                    a.Sign == GainLoss.Gain && !a.IsPartial && !a.Parts.IsEmpty && a.Kind is not ("fis" or "mar" or "r" or "dmin")
                    && a.Parts[0].Chromosomes is [{ } only] && only.Name == loss.Chromosome.Name);
                if (gain is not null && reported.Add(loss))
                {
                    context.Report(
                        loss.Span,
                        $"'{gain.Span.Of(context.Input)}' takes the place of a normal chromosome {loss.Chromosome.Name}: write it without a sign and drop '{loss.Span.Of(context.Input)}'.",
                        [gain.Span]);
                }
            }
        }

        foreach (var r in ChromosomeCount.Analyze(context.Model).Where(r => r.Verdict == CountVerdict.ReplacedNormalLost))
        {
            foreach (var (loss, replacement) in r.ReplacedLosses.Where(x => reported.Add(x.Loss)))
            {
                context.Report(
                    loss.Span,
                    $"'{replacement.Span.Of(context.Input)}' replaces a normal chromosome {loss.Chromosome.Name}, which is not written as missing.",
                    [replacement.Span]);
            }
        }
    }
}
