using System.Collections.Immutable;

namespace CytoLex.Core.Context;

/// <summary>
/// The sample context of a string: the caller's hint, what the markers say, and the effective context
/// (docs/design/core.md §3.3, §9 <c>ContextInfo</c>).
/// </summary>
/// <param name="Hint">The explicit context hint, if any; it overrides inference.</param>
/// <param name="Inferred">The context the markers point to; <c>null</c> = unknown (no markers, or contradicting ones).</param>
/// <param name="Effective">The context validation uses: <paramref name="Hint"/> if given, else <paramref name="Inferred"/>; <c>null</c> = unknown.</param>
/// <param name="Markers">Every marker found, in input order (they decide <paramref name="Inferred"/>).</param>
/// <param name="IsContradictory">Markers point to both contexts; <paramref name="Inferred"/> is then unknown and the engine reports an info diagnostic naming them (T-028).</param>
public sealed record ContextInfo(
    SampleContext? Hint,
    SampleContext? Inferred,
    SampleContext? Effective,
    ImmutableArray<ContextMarker> Markers,
    bool IsContradictory);
