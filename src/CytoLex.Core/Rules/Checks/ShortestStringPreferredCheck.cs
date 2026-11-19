using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>ORDER.SHORTEST_STRING_PREFERRED</c> (§6 E6, warning): <c>+n</c> plus an unsigned rearrangement of the single
/// chromosome n (<c>+3,inv(3)(q21q26.2)</c>) is better written as the gain of the rearranged chromosome
/// (<c>+inv(3)(q21q26.2)</c>). Autosomes only; rearrangements of several chromosomes (<c>t(2;5)</c>, <c>dic(13;15)</c>,
/// <c>der(1;3)</c>) and copies (<c>×2</c>) are left alone.
/// </summary>
[KbRule("ORDER.SHORTEST_STRING_PREFERRED")]
internal sealed class ShortestStringPreferredCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var line in context.Model.CellLines)
        {
            foreach (var gain in line.Items.OfType<WholeChromosomeChange>()
                .Where(w => w.Sign == GainLoss.Gain && w.Chromosome.Number is not null && w.Copies is null && w.Constitutional is null && !w.IsPartial))
            {
                var changed = line.Items.OfType<Abnormality>().FirstOrDefault(a =>
                    a.Sign == GainLoss.None && !a.IsPartial && a.Copies is null && a.Constitutional is null && !a.Parts.IsEmpty
                    && a.Kind is not ("fis" or "mar" or "r" or "dmin" or "upd" or "inc")
                    && a.Parts[0].Chromosomes is [{ } only] && only.Name == gain.Chromosome.Name);
                if (changed is not null)
                {
                    context.Report(
                        gain.Span,
                        $"'{gain.Span.Of(context.Input)}' and '{changed.Span.Of(context.Input)}' are shorter as '+{changed.Span.Of(context.Input)}'.",
                        [changed.Span]);
                }
            }
        }
    }
}
