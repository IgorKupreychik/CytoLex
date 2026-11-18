using System.Collections.Immutable;
using CytoLex.Core.Segmentation;

namespace CytoLex.Core.Syntax;

/// <summary>
/// The parse of one karyotype segment: a lossless tree (its full text is the segment text, exactly), the syntax
/// errors and the not-implemented constructs (docs/design/core.md §3.2, §4, §8.4). Spans are offsets in the whole input.
/// </summary>
/// <param name="Input">The whole input string.</param>
/// <param name="Segment">The segment parsed.</param>
/// <param name="Root">A <see cref="SyntaxKind.Karyotype"/> or <see cref="SyntaxKind.Fragment"/> node.</param>
/// <param name="Diagnostics">Syntax errors, in input order.</param>
/// <param name="Constructs">Not-implemented constructs, in input order.</param>
public sealed record SyntaxTree(
    string Input,
    Segment Segment,
    SyntaxNode Root,
    ImmutableArray<SyntaxDiagnostic> Diagnostics,
    ImmutableArray<NotImplementedConstruct> Constructs)
{
    /// <summary>The segment text rebuilt from the tree; always equal to the segment's text in <see cref="Input"/>.</summary>
    public string ToFullString() => Root.ToFullString();
}
