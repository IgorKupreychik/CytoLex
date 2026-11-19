using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>TAS.FORMAT</c> (§9.2.16): the chain starts with the end chromosome that is a sex chromosome or has the lowest number
/// (<see cref="ChromosomeOrder"/>), and the associated chromosomes are counted separately: a count that fits only with
/// the association counted as one chromosome (<c>45,XX,tas(12;13)(q24.3;q34)</c>) is reported here, not as a count mismatch
/// (<see cref="ChromosomeCount"/>).
/// </summary>
[KbRule("TAS.FORMAT")]
internal sealed class TasFormatCheck() : StructuralCheck(Analyze)
{
    private static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var finding in ChromosomeOrder.Analyze(model, input))
        {
            yield return finding;
        }

        foreach (var r in ChromosomeCount.Analyze(model).Where(r => r.Verdict == CountVerdict.TasCountedOnce))
        {
            var tas = r.Line.Items.OfType<Abnormality>().First(a => a.Kind == "tas");
            yield return new StructuralFinding(
                ChromosomeOrder.TasFormatRule,
                r.CountSpan,
                $"The chromosomes of a telomeric association are counted separately: '{tas.Span.Of(input)}' does not lower the count.",
                [tas.Span]);
        }
    }
}
