namespace CytoLex.Core.Rules.Checks;

/// <summary><c>HSR.REPLACED_SEGMENT_PROXIMAL</c> (§9.2.8): an hsr replacing a deleted segment is assigned to the proximal deletion breakpoint (<see cref="Hsr"/>).</summary>
[KbRule("HSR.REPLACED_SEGMENT_PROXIMAL")]
internal sealed class HsrReplacedSegmentProximalCheck() : StructuralCheck((m, _) => Hsr.Analyze(m));
