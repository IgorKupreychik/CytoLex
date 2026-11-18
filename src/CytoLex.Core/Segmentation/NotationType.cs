namespace CytoLex.Core.Segmentation;

/// <summary>
/// The notation type of a segment: the KB <c>applies_to</c> vocabulary (kb/schema/common.schema.json)
/// without <c>general</c> (docs/design/core.md §3.2).
/// </summary>
public enum NotationType
{
    /// <summary>Karyotype (ch. 4–9, 11); the default when a segment has no technique keyword.</summary>
    Karyotype,

    /// <summary>Chromosome breakage (ch. 10). Not detected yet: recognised by markers inside a karyotype-shaped segment (T-018).</summary>
    Breakage,

    /// <summary>Meiotic notation (ch. 12). Not detected yet (T-018).</summary>
    Meiotic,

    /// <summary>Metaphase in situ hybridization, <c>ish</c> (§13.2).</summary>
    Ish,

    /// <summary>Interphase in situ hybridization, <c>nuc ish</c> (§13.3).</summary>
    NucIsh,

    /// <summary>Fiber FISH, <c>fib ish</c> (§13.4).</summary>
    FibIsh,

    /// <summary>Reverse in situ hybridization, <c>rev ish</c> (§13.5).</summary>
    RevIsh,

    /// <summary>Comparative genomic hybridization, <c>cgh</c> (§13.6).</summary>
    Cgh,

    /// <summary>Microarray, <c>arr</c> (ch. 14).</summary>
    Arr,

    /// <summary>Region-specific assay, <c>rsa</c> (ch. 15).</summary>
    Rsa,

    /// <summary>Sequence-based notation, <c>seq</c> (ch. 16).</summary>
    Seq,
}

/// <summary>Mapping between <see cref="NotationType"/> and the KB <c>applies_to</c> names.</summary>
public static class NotationTypeExtensions
{
    /// <summary>The KB <c>applies_to</c> name, e.g. <c>nuc_ish</c>.</summary>
    public static string ToKbName(this NotationType type) => type switch
    {
        NotationType.Karyotype => "karyotype",
        NotationType.Breakage => "breakage",
        NotationType.Meiotic => "meiotic",
        NotationType.Ish => "ish",
        NotationType.NucIsh => "nuc_ish",
        NotationType.FibIsh => "fib_ish",
        NotationType.RevIsh => "rev_ish",
        NotationType.Cgh => "cgh",
        NotationType.Arr => "arr",
        NotationType.Rsa => "rsa",
        NotationType.Seq => "seq",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
