using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>KARYO.REPLACED_NORMAL_NOT_MISSING</c> (§4.1 ¶6, §9.1 ¶2): a normal chromosome replaced by a rearranged one is not
/// written as missing. Fires on (a) <c>–n</c> together with <c>+R</c>, R a rearrangement of the single chromosome n
/// (<c>–3,+inv(3)</c>: write <c>inv(3)</c>; not <c>fis</c>, whose products are written separately, §9.2.6), and (b) a count
/// that fits only without losses of chromosomes that an unsigned rearrangement replaces (<c>–4,del(4)</c>;
/// <see cref="ChromosomeCount"/>). Losses replaced by a dicentric, a Robertsonian translocation or a
/// whole-arm <c>der(a;b)</c> go to those symbols' rules (T-068).
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
                    // –3,+inv(3) → inv(3): drop the loss and the gain's sign.
                    var fix = gain.Syntax?.PresentTokens().FirstOrDefault() is { } sign && sign.Span.Start == gain.Span.Start
                        ? new FixProposal([.. FixHelpers.Remove(line, loss).Edits, new TextEdit(sign.Span, string.Empty)])
                        : null;
                    context.Report(
                        loss.Span,
                        $"'{gain.Span.Of(context.Input)}' takes the place of a normal chromosome {loss.Chromosome.Name}: write it without a sign and drop '{loss.Span.Of(context.Input)}'.",
                        [gain.Span],
                        fixes: fix is null ? [] : [fix]);
                }
            }
        }

        foreach (var f in ReplacedLosses.Analyze(context.Model, context.Input).Where(f => f.Rule == ReplacedLoss.GeneralRule && reported.Add(f.Loss)))
        {
            context.Report(f.Finding.Span, f.Finding.Detail, f.Finding.SecondarySpans, fixes: f.Finding.Fix is null ? [] : [f.Finding.Fix]);
        }
    }
}

/// <summary>
/// The losses of replaced chromosomes that the count shows (<see cref="CountVerdict.ReplacedNormalLost"/>), each addressed
/// to <see cref="ReplacedLoss.Rule"/>: <c>DIC.FORMAT</c>, <c>ROB.FORMAT</c>, <c>DER.WHOLE_ARM_TWO_CHROMOSOMES</c> or
/// <c>KARYO.REPLACED_NORMAL_NOT_MISSING</c> (T-068).
/// </summary>
internal static class ReplacedLosses
{
    /// <remarks>The fix of each loss removes every loss its replacement accounts for (<c>–13,–15,dic(13;15)</c> → <c>dic(13;15)</c>).</remarks>
    public static IEnumerable<(string Rule, WholeChromosomeChange Loss, StructuralFinding Finding)> Analyze(KaryotypeModel model, string input) =>
        ChromosomeCount.Analyze(model)
            .Where(r => r.Verdict == CountVerdict.ReplacedNormalLost)
            .SelectMany(r => r.ReplacedLosses.Select(x => (r.Line, x, Same: r.ReplacedLosses.Where(o => ReferenceEquals(o.Replacement, x.Replacement)).Select(o => o.Loss))))
            .Select(t => (t.x.Rule, t.x.Loss, new StructuralFinding(
                t.x.Rule,
                t.x.Loss.Span,
                $"'{t.x.Replacement.Span.Of(input)}' replaces a normal chromosome {t.x.Loss.Chromosome.Name}, which is not written as missing.",
                [t.x.Replacement.Span],
                Fix: FixHelpers.Remove(t.Line, t.Same))));

    /// <summary>The findings of the T-068 symbol rules (not the general rule).</summary>
    public static IEnumerable<StructuralFinding> SymbolFindings(KaryotypeModel model, string input) =>
        Analyze(model, input).Where(x => x.Rule != ReplacedLoss.GeneralRule).Select(x => x.Finding);
}
