namespace CytoLex.Core.Tests.KbExamples;

/// <summary>What a KB example case checks (T-025).</summary>
internal enum ExampleKind
{
    /// <summary>A valid example: no errors; its rule does not fire.</summary>
    Valid,

    /// <summary>An invalid example: its rule fires with the expected severity.</summary>
    Invalid,

    /// <summary>The <c>corrected</c> form of an invalid example: valid, and the fixer reproduces it.</summary>
    Corrected,
}

/// <summary>Sample context (docs/design/core.md §3.3). KB <c>neoplastic</c> maps to <see cref="Acquired"/>.</summary>
internal enum SampleContext
{
    Constitutional,
    Acquired,
}

/// <summary>Diagnostic severity, ordered so that a larger value is more severe.</summary>
internal enum Severity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// The harness's view of one validation run, mirroring the result API (docs/design/core.md §8–§10).
/// The adapter (<see cref="ValidatorAdapter"/>) maps the real <c>ValidationReport</c> of one edition onto it.
/// </summary>
/// <param name="IsValid">No (firm) errors in the edition (§9 validity).</param>
/// <param name="ValidContexts">Contexts in which the string has no errors.</param>
/// <param name="Diagnostics">All diagnostics of the edition.</param>
internal sealed record ExampleRun(
    bool IsValid,
    IReadOnlyList<SampleContext> ValidContexts,
    IReadOnlyList<ExampleDiagnostic> Diagnostics);

/// <param name="RuleId">KB rule ID (or <c>CORE.UNPARSED</c>).</param>
/// <param name="Severity">Effective severity; for a conditional diagnostic the worst over the contexts.</param>
/// <param name="Conditional">Depends on an unknown context (§8.3).</param>
/// <param name="Uncertain">The check could not decide with certainty (§8.2 step 3).</param>
/// <param name="FixPreviews">The full corrected string of each offered fix (§10 <c>Fix.Preview</c>).</param>
internal sealed record ExampleDiagnostic(
    string RuleId,
    Severity Severity,
    bool Conditional = false,
    bool Uncertain = false,
    IReadOnlyList<string>? FixPreviews = null)
{
    public IReadOnlyList<string> Previews => FixPreviews ?? [];
}

/// <summary>Seam between the harness and the validator (plugged in by T-028, fixes by T-035).</summary>
internal interface IExampleValidator
{
    /// <summary>Whether diagnostics carry fixes with a <c>Preview</c> (T-035).</summary>
    bool SupportsFixes { get; }

    /// <summary>Validates <paramref name="text"/> for one edition.</summary>
    /// <param name="edition">ISCN edition, e.g. <c>"2016"</c>.</param>
    /// <param name="text">The example string as printed in the KB.</param>
    /// <param name="hint">Context hint; <c>null</c> = infer (§3.3).</param>
    /// <param name="isFragment">The example is a book fragment (an abnormality, a symbol, or a string with <c>…</c>), not a whole notation string.</param>
    ExampleRun Run(string edition, string text, SampleContext? hint, bool isFragment);
}
