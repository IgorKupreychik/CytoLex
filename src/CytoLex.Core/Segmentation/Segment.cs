using System.Collections.Immutable;
using CytoLex.Core.Text;

namespace CytoLex.Core.Segmentation;

/// <summary>
/// One technique segment of a notation string (docs/design/core.md §3.1). Segments and their
/// separators together cover the input exactly, in order.
/// </summary>
/// <param name="Index">Position of the segment in the string, from 0.</param>
/// <param name="Span">The segment text between separators, including any surrounding whitespace.</param>
/// <param name="Type">The detected notation type (§3.2).</param>
/// <param name="Keyword">The leading technique keyword (<c>nuc ish</c>, <c>arr</c>, …); <c>null</c> for a karyotype.</param>
/// <param name="KeywordVocabRefs">The ch. 3 vocab items the keyword is made of, as <c>KbRef</c> strings (<c>vocab:symbols/nuc</c>, <c>vocab:symbols/ish</c>); empty without a keyword.</param>
/// <param name="Separator">The period (<c>.</c>) before this segment; <c>null</c> for the first segment.</param>
public sealed record Segment(
    int Index,
    TextSpan Span,
    NotationType Type,
    TextSpan? Keyword,
    ImmutableArray<string> KeywordVocabRefs,
    TextSpan? Separator);
