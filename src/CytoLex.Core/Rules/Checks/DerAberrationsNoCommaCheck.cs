namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DER.ABERRATIONS_NO_COMMA</c> (§4.4): no comma between the aberrations of a derivative (<see cref="Derivatives"/>).</summary>
[KbRule("DER.ABERRATIONS_NO_COMMA")]
internal sealed class DerAberrationsNoCommaCheck() : StructuralCheck(Derivatives.Analyze);
