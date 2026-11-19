namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DER.ABERRATION_ORDER</c> (§4.4): the aberrations of a derivative follow their breakpoints from pter to qter (<see cref="Derivatives"/>).</summary>
[KbRule("DER.ABERRATION_ORDER")]
internal sealed class DerAberrationOrderCheck() : StructuralCheck(Derivatives.Analyze);
