using CytoLex.Core.Semantics;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>SEX.CONSTITUTIONAL_COMPLEMENT</c> (§8.1 ¶1, §8.2 ¶1–2): a constitutional sex chromosome change is written in the sex
/// field, without + or –. Fires on every signed X/Y (<c>47,XY,+X</c>, <c>45,X,–X</c>); the KB context (constitutional)
/// makes it conditional. Not when the sex field carries <c>c</c>: that marks the string as acquired (§8.2 ¶4).
/// </summary>
[KbRule("SEX.CONSTITUTIONAL_COMPLEMENT")]
internal sealed class ConstitutionalComplementCheck : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var line in context.Model.CellLines.Where(l => l.Sex?.Constitutional is null))
        {
            foreach (var w in line.Items.OfType<WholeChromosomeChange>()
                .Where(w => w.Chromosome.IsSexChromosome && w.Sign != GainLoss.None && w.Constitutional is null && !w.IsPartial))
            {
                context.ReportWithFix(w.Span, $"A constitutional change of {w.Chromosome.Name} is written in the sex chromosome field, without a sign.", Fix(line, w));
            }
        }
    }

    /// <summary>
    /// <c>47,XY,+X</c> → <c>47,XXY</c> (the gained copies join the field, X before Y); <c>45,X,–X</c> → <c>45,X</c> (the field
    /// already shows what is present). <c>null</c> when the field is not plain letters or the copies are unknown.
    /// </summary>
    private static FixProposal? Fix(CellLine line, WholeChromosomeChange w)
    {
        var remove = FixHelpers.Remove(line, w);
        if (w.Sign == GainLoss.Loss)
        {
            return remove;
        }

        if (line.Sex is not { } sex || !FixHelpers.IsPlainSexField(sex, line) || w.Copies is { Count: null })
        {
            return null;
        }

        var letters = sex.Chromosomes.Concat(Enumerable.Repeat(w.Chromosome.Name, w.Copies?.Count ?? 1)).OrderBy(c => c == "Y" ? 1 : 0);
        return new FixProposal([new TextEdit(sex.Span, string.Concat(letters)), .. remove.Edits]);
    }
}
