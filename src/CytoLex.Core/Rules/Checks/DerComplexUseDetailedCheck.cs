using CytoLex.Core.Composing;
using CytoLex.Core.Semantics;

namespace CytoLex.Core.Rules.Checks;

/// <summary>
/// <c>DER.COMPLEX_USE_DETAILED</c> (§9.2.3 ¶15, T-086): a derivative in the short system whose aberration names a band
/// the chromosome no longer has after the aberrations before it (<see cref="CompositionGapKind.Unplaceable"/>, found by
/// the <see cref="Composer"/>): a derivative taking part in a new rearrangement, which the short system can't describe.
/// Info: the detailed system is adequate. Derivatives without a composition for other reasons (uncertain breakpoints,
/// shapes not defined yet) are not reported.
/// </summary>
[KbRule(Rule)]
internal sealed class DerComplexUseDetailedCheck() : StructuralCheck(Analyze)
{
    public const string Rule = "DER.COMPLEX_USE_DETAILED";

    public static IEnumerable<StructuralFinding> Analyze(KaryotypeModel model, string input)
    {
        foreach (var a in model.DescendantsAndSelf().OfType<Abnormality>())
        {
            if (a.CompositionGap is { Kind: CompositionGapKind.Unplaceable, Part: { } index } && index > 0 && index < a.Parts.Length)
            {
                var head = a.Parts[0];
                var part = a.Parts[index];
                yield return new StructuralFinding(
                    Rule,
                    part.Span,
                    $"After the aberrations before it, {head.HeadSpan().Of(input)} no longer has the place '{part.Span.Of(input)}' acts on: "
                    + "a derivative taking part in a new rearrangement can't be described in the short system; write it in the detailed system.",
                    [head.HeadSpan()]);
            }
        }
    }
}
