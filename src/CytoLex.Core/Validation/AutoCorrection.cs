using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>The overall verdict of one edition (T-096).</summary>
public enum EditionVerdict
{
    /// <summary>Valid as written.</summary>
    Valid,

    /// <summary>Not book-conformant as written, but valid after autocorrection (<see cref="EditionResult.AutoCorrection"/>).</summary>
    ValidAfterAutoCorrection,

    /// <summary>Not valid (with autocorrection on: not valid even after it).</summary>
    NotValid,

    /// <summary>The edition was not validated.</summary>
    NotImplemented,
}

/// <summary>One edit applied by autocorrection (T-096), against the original input.</summary>
/// <param name="RuleId">The KB rule whose fix it is (a rule with <c>fix.auto</c>).</param>
/// <param name="OriginalSpan">The span of the original input the edit changed; empty = an insertion point.</param>
/// <param name="OriginalText">The original text of <paramref name="OriginalSpan"/>.</param>
/// <param name="NewText">The text written instead; empty = a deletion.</param>
/// <param name="Description">The fix's wording (KB hint).</param>
/// <param name="Citations">The KB citations of the rule.</param>
/// <param name="Round">The autocorrection round that applied it (1 = first).</param>
public sealed record AppliedCorrection(
    string RuleId,
    TextSpan OriginalSpan,
    string OriginalText,
    string NewText,
    string Description,
    ImmutableArray<Citation> Citations,
    int Round);

/// <summary>
/// What autocorrection did in one edition (T-096; <see cref="ValidationOptions.AutoCorrect"/>). The edition's
/// diagnostics, segments, abnormalities and translation refer to <paramref name="Corrected"/>; <paramref name="Map"/>
/// maps their spans back to the original input (<see cref="ValidationReport.Input"/>).
/// </summary>
/// <param name="Corrected">The input after every applied correction; equal to the input when none applied.</param>
/// <param name="Corrections">The applied edits, in round order, then by position.</param>
/// <param name="Map">Offsets of <paramref name="Corrected"/> → offsets of the original input.</param>
public sealed record AutoCorrection(string Corrected, ImmutableArray<AppliedCorrection> Corrections, OffsetMap Map)
{
    /// <summary>Whether any correction was applied.</summary>
    public bool IsChanged => !Corrections.IsDefaultOrEmpty;

    /// <summary>The span of the original input that <paramref name="span"/> of <see cref="Corrected"/> comes from.</summary>
    public TextSpan ToOriginal(TextSpan span) => Map.ToOriginal(span);
}
