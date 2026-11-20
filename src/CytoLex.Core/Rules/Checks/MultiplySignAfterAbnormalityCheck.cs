namespace CytoLex.Core.Rules.Checks;

/// <summary><c>KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY</c> (§9.3): <c>×n</c> after the abnormality, not a count before it; extra copies with a plus sign (<see cref="Unidentified"/>); <c>del(6)×2(q13q23)</c> is the parser's.</summary>
[KbRule("KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY")]
internal sealed class MultiplySignAfterAbnormalityCheck() : StructuralCheck(Unidentified.Analyze);
