using CytoLex.Core.Text;

namespace CytoLex.Core.Context;

/// <summary>The clear markers that decide the sample context (decisions T-021; docs/design/core.md §3.3).</summary>
public enum ContextMarkerKind
{
    /// <summary><c>idem</c>, <c>sl</c> or <c>sdl</c> (§11.1.4) → acquired.</summary>
    StemlineReference,

    /// <summary>Clone structure: cell lines separated by <c>/</c> or <c>//</c> with <c>[n]</c> cell counts and no <c>mos</c>/<c>chi</c> prefix (§11.1) → acquired.</summary>
    CloneStructure,

    /// <summary>The constitutional-anomaly marker <c>c</c> inside an acquired karyotype (§11.3) → acquired.</summary>
    ConstitutionalAnomaly,

    /// <summary>A ploidy level in angle brackets, e.g. <c>&lt;3n&gt;</c> (§8.1) → acquired.</summary>
    PloidyLevel,

    /// <summary>A recombinant chromosome, <c>rec</c> (§4.5) → constitutional.</summary>
    Recombinant,
}

/// <summary>One context marker found in the input.</summary>
/// <param name="Kind">What was found.</param>
/// <param name="Indicates">The context it points to.</param>
/// <param name="Span">Where it is in the input.</param>
public sealed record ContextMarker(ContextMarkerKind Kind, SampleContext Indicates, TextSpan Span);
