using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;

namespace CytoLex.Core.Validation;

/// <summary>Whether an edition (or a segment) was validated.</summary>
public enum EditionStatus
{
    /// <summary>Validated.</summary>
    Implemented,

    /// <summary>Not validated: no profile, no KB data, or a notation type without an implementation (§3.2).</summary>
    NotImplemented,
}

/// <summary>Validity of a string in one edition (decisions T-022; docs/design/core.md §9).</summary>
/// <param name="IsValid">No errors: with a known context, no firm errors; with an unknown one, at least one context without errors.</param>
/// <param name="ValidContexts">The contexts in which the string has no errors.</param>
/// <param name="IsComplete"><c>false</c> when a segment or construct was not validated ("valid as far as implemented").</param>
public sealed record Validity(bool IsValid, ImmutableArray<SampleContext> ValidContexts, bool IsComplete);

/// <summary>Diagnostic counts per severity; conditional diagnostics count by their worst severity and also in <see cref="Conditional"/>.</summary>
public sealed record SeverityCounts(int Errors, int Warnings, int Infos, int Conditional)
{
    /// <summary>No diagnostics.</summary>
    public static SeverityCounts None { get; } = new(0, 0, 0, 0);
}

/// <summary>What happened to one segment in one edition.</summary>
/// <param name="Segment">The segment.</param>
/// <param name="Status"><see cref="EditionStatus.NotImplemented"/> when its notation type has no implementation.</param>
/// <param name="Reason">Why it was not validated, e.g. <c>ish: not implemented in the pilot</c>.</param>
/// <param name="Constructs">Constructs inside a validated segment that were not validated (<c>idem</c>, <c>cp</c>, U+0332, …).</param>
public sealed record SegmentResult(
    Segment Segment,
    EditionStatus Status,
    string? Reason,
    ImmutableArray<NotImplementedConstruct> Constructs);

/// <summary>The result of one edition (docs/design/core.md §9). Results never mix editions.</summary>
/// <param name="Edition">The ISCN edition, e.g. <c>2016</c>.</param>
/// <param name="Status">Whether the edition was validated at all.</param>
/// <param name="Reason">Why not, for a <see cref="EditionStatus.NotImplemented"/> edition.</param>
/// <param name="Validity">Validity in this edition.</param>
/// <param name="Counts">Diagnostic counts.</param>
/// <param name="Diagnostics">Diagnostics, sorted by span start, then most severe first.</param>
/// <param name="Segments">One result per segment; empty for a not-implemented edition.</param>
/// <param name="CellLines">The cell lines of the karyotype segments, in input order (T-069).</param>
/// <param name="Abnormalities">The abnormalities of the karyotype segments, in input order; each names its cell line (T-069).</param>
public sealed record EditionResult(
    string Edition,
    EditionStatus Status,
    string? Reason,
    Validity Validity,
    SeverityCounts Counts,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<SegmentResult> Segments,
    ImmutableArray<CellLineSummary> CellLines,
    ImmutableArray<AbnormalityEntry> Abnormalities);

/// <summary>The result of <see cref="Validator.Validate"/>: the segments, the context, and one result per edition requested.</summary>
/// <param name="Input">The input string; every span refers to it.</param>
/// <param name="Segments">Its segments (§3.1).</param>
/// <param name="Context">The hint, the inferred context and its markers (§3.3).</param>
/// <param name="Results">One element for one edition; one per known edition for <see cref="EditionSelection.All"/>.</param>
public sealed record ValidationReport(
    string Input,
    ImmutableArray<Segment> Segments,
    ContextInfo Context,
    ImmutableArray<EditionResult> Results);
