using CytoLex.Core.Text;

namespace CytoLex.Core.Syntax;

/// <summary>Constructs inside a karyotype segment that the pilot recognises but does not validate (docs/design/core.md §3.2).</summary>
public enum ConstructKind
{
    /// <summary>A chromosome breakage symbol (ch. 10; T-018).</summary>
    Breakage,

    /// <summary>A meiotic notation symbol (ch. 12; T-018).</summary>
    Meiotic,

    /// <summary>An underlined character (U+0332) telling homologues apart (T-042).</summary>
    Underlining,
}

/// <summary>A not-implemented construct found in a karyotype segment, with where it is.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Span">Its text in the whole input.</param>
/// <param name="Reason">Why it is not validated, for the segment result.</param>
public sealed record NotImplementedConstruct(ConstructKind Kind, TextSpan Span, string Reason);
