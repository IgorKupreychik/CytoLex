using CytoLex.Core.Text;

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
    string Message);

/// <summary>Codes the core uses that are not KB rules (docs/design/core.md §8.4).</summary>
public static class CoreCodes
{
    /// <summary>Text the parser could not attribute to any KB rule; on a KB example it is a KB or parser gap.</summary>
    public const string Unparsed = "CORE.UNPARSED";
}
