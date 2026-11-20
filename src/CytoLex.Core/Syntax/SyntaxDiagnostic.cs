using System.Collections.Immutable;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Syntax;

/// <summary>How the parser recovered from a syntax error.</summary>
public enum SyntaxDiagnosticKind
{
    /// <summary>A required token was missing; a zero-width token was inserted (e.g. the comma in <c>46XX</c>).</summary>
    Missing,

    /// <summary>The text is out of place or malformed but was understood (e.g. <c>21+</c>, <c>del4(p14)</c>).</summary>
    Unexpected,

    /// <summary>The text could not be understood and was skipped up to the next sync point.</summary>
    Skipped,
}

/// <summary>
/// A syntax error found by the parser: the facts only (docs/design/core.md §8.4). Every recovery names the KB rule
/// that defines the expected form; <see cref="CoreCodes.Unparsed"/> is the safety net when none does.
/// T-028 turns these into <c>Diagnostic</c>s (origin parser) with the KB severity and citations.
/// </summary>
/// <param name="RuleId">The KB rule ID (e.g. <c>KARYO.COUNT_SEX_ORDER</c>) or <see cref="CoreCodes.Unparsed"/>.</param>
/// <param name="Span">The offending text in the whole input; zero-length for a missing token (the insertion point).</param>
/// <param name="Kind">How the parser recovered.</param>
/// <param name="Expected">For a missing token: the text to insert (level-1 fix material, T-035).</param>
/// <param name="Message">A short English description.</param>
public sealed record SyntaxDiagnostic(
    string RuleId,
    TextSpan Span,
    SyntaxDiagnosticKind Kind,
    string? Expected,
    string Message)
{
    /// <summary>
    /// Explicit fix edits (T-035), for recoveries that <see cref="Expected"/> alone can't express (reorders such as
    /// <c>21+</c> → <c>+21</c>). Empty = derive the fix from <see cref="Expected"/>: insert it for a missing token, else
    /// replace <see cref="Span"/> with it.
    /// </summary>
    public ImmutableArray<TextEdit> Edits { get; init; } = [];

    /// <summary>The fix edits: <see cref="Edits"/>, or the ones <see cref="Expected"/> implies; empty = no deterministic fix.</summary>
    public ImmutableArray<TextEdit> FixEdits =>
        !Edits.IsDefaultOrEmpty ? Edits
        : Expected is null || Kind == SyntaxDiagnosticKind.Skipped ? []
        : [new TextEdit(Kind == SyntaxDiagnosticKind.Missing ? new TextSpan(Span.Start, 0) : Span, Expected)];
}

/// <summary>Codes the core uses that are not KB rules (docs/design/core.md §8.4).</summary>
public static class CoreCodes
{
    /// <summary>Text the parser could not attribute to any KB rule; on a KB example it is a KB or parser gap.</summary>
    public const string Unparsed = "CORE.UNPARSED";

    /// <summary>
    /// The context markers contradict (e.g. <c>rec</c> and <c>idem</c>), so the context is unknown (§3.3). An info from the
    /// engine, not an ISCN rule (decided in T-028): CytoLex's own context inference, so it has no book citation.
    /// </summary>
    public const string ContextConflict = "CORE.CONTEXT_CONFLICT";
}
