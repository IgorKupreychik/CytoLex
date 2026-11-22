using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;

namespace CytoLex.Core.Translation;

/// <summary>Why a translation gives one reading instead of both (decisions → Literal translation, T-054).</summary>
public enum ReadingBasis
{
    /// <summary>The context is unknown: neutral wording, both readings where the context changes the meaning.</summary>
    Unknown,

    /// <summary>The caller gave a context hint.</summary>
    Hint,

    /// <summary>The context was inferred from a marker in the string (<c>c</c>, <c>idem</c>, <c>cp</c>, …).</summary>
    Inferred,
}

/// <summary>A link from a phrase to a KB entry: shown by title, with the ID on hover (T-054); opens the rule browser (T-059).</summary>
/// <param name="Ref">The KB entry (rule, vocab item or band).</param>
/// <param name="Title">The rule title, the vocab item's first meaning, or the band name.</param>
public sealed record KbLink(KbRef Ref, string Title);

/// <summary>The two readings of a phrase whose meaning depends on the sample context.</summary>
/// <param name="Constitutional">The constitutional reading.</param>
/// <param name="Acquired">The acquired (neoplastic) reading.</param>
public sealed record ContextReadings(string Constitutional, string Acquired);

/// <summary>The approximate coordinates of a band (optional genomic detail, T-054; off by default).</summary>
/// <param name="Band">The band name, e.g. <c>5q13</c>.</param>
/// <param name="Grch37">GRCh37 coordinates; <c>null</c> = no coordinates.</param>
/// <param name="Grch38">GRCh38 coordinates; <c>null</c> = no coordinates.</param>
/// <param name="IsBookOnly">The name is printed in the book but has no NCBI coordinates ("no coordinates").</param>
public sealed record BandCoordinates(string Band, GenomicRange? Grch37, GenomicRange? Grch38, bool IsBookOnly);

/// <summary>The approximate size of a segment on one chromosome, from its outermost band coordinates.</summary>
/// <param name="From">The first band of the segment.</param>
/// <param name="To">The last band of the segment.</param>
/// <param name="Grch37Bases">The size in GRCh37 bases; <c>null</c> when a band has no coordinates.</param>
/// <param name="Grch38Bases">The size in GRCh38 bases; <c>null</c> when a band has no coordinates.</param>
public sealed record SegmentSize(string From, string To, long? Grch37Bases, long? Grch38Bases);

/// <summary>The optional genomic detail of one item: its bands and the sizes of its segments.</summary>
/// <param name="Bands">Each named band of the item, once, in input order.</param>
/// <param name="Segments">The segments between two bands of one chromosome (deletion, duplication, inversion, …).</param>
public sealed record ItemCoordinates(ImmutableArray<BandCoordinates> Bands, ImmutableArray<SegmentSize> Segments);

/// <summary>The summary line of a translation.</summary>
/// <param name="Text">The summary, e.g. <c>46 chromosomes; sex chromosomes XY; 1 abnormality.</c></param>
/// <param name="Readings">Both readings when the context is unknown and changes the meaning; else <c>null</c>.</param>
/// <param name="Links">The KB entries the summary renders.</param>
/// <param name="IsDraft">Built from a template not yet reviewed (T-082).</param>
public sealed record TranslationSummary(string Text, ContextReadings? Readings, ImmutableArray<KbLink> Links, bool IsDraft);

/// <summary>One translated part of the string: a cell line's header or an item, with its sentence.</summary>
/// <param name="Span">Where the part is in the input.</param>
/// <param name="Text">The part as written.</param>
/// <param name="Sentence">The sentence (one reading when the context is known); for an untranslated part, why not.</param>
/// <param name="Readings">Both readings when the context is unknown and changes the meaning; else <c>null</c>.</param>
/// <param name="Links">The KB entries of the part, in first-seen order.</param>
/// <param name="IsTranslated"><c>false</c> for a part with recovered or skipped input ("not translated, see diagnostic …").</param>
/// <param name="IsDraft">Built from a template not yet reviewed (T-082).</param>
/// <param name="DiagnosticIndices">The indices (into <c>EditionResult.Diagnostics</c>) of the diagnostics inside the part.</param>
/// <param name="Coordinates">The optional genomic detail; <c>null</c> unless requested.</param>
public sealed record TranslationItem(
    TextSpan Span,
    string Text,
    string Sentence,
    ContextReadings? Readings,
    ImmutableArray<KbLink> Links,
    bool IsTranslated,
    bool IsDraft,
    ImmutableArray<int> DiagnosticIndices,
    ItemCoordinates? Coordinates);

/// <summary>
/// The literal translation of a string in one edition (decisions → Literal translation, T-054; docs/design/core.md §11):
/// a summary line and one sentence per part, each with its span and KB links. Plain data, serialisable with the report.
/// </summary>
/// <param name="Language">The template language, e.g. <c>en</c>.</param>
/// <param name="Summary">The summary line.</param>
/// <param name="Items">The parts, in input order.</param>
/// <param name="IsPartial">Some part was not translated, or the string is not valid in the edition.</param>
/// <param name="Banner">"Not valid ISCN &lt;edition&gt;; partial translation." when the edition has errors; else <c>null</c>.</param>
/// <param name="ReadingBasis">Why one reading was given instead of both.</param>
/// <param name="IsDraft">Some phrase comes from a template not yet reviewed (T-082).</param>
public sealed record TranslationResult(
    string Language,
    TranslationSummary Summary,
    ImmutableArray<TranslationItem> Items,
    bool IsPartial,
    string? Banner,
    ReadingBasis ReadingBasis,
    bool IsDraft);
