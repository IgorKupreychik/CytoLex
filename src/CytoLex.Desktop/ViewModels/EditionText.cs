using System.Collections.Immutable;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The texts of one edition result (T-097): the input as typed (what the editor shows) and the text the result's spans
/// refer to (the corrected string with autocorrection, T-096). View models read excerpts from <see cref="Text"/> and hand
/// spans to the editor through <see cref="ToEditor"/>.
/// </summary>
/// <param name="Original">The input as typed.</param>
/// <param name="AutoCorrection">The edition's autocorrection; <c>null</c> when it was off or the edition not implemented.</param>
public sealed record EditionText(string Original, AutoCorrection? AutoCorrection)
{
    /// <summary>The texts of <paramref name="result"/> of <paramref name="report"/>.</summary>
    public static EditionText Of(ValidationReport report, EditionResult result)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(result);
        return new EditionText(report.Input, result.AutoCorrection);
    }

    /// <summary>A text without autocorrection: spans refer to it as they are.</summary>
    public static EditionText Plain(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new EditionText(input, null);
    }

    /// <summary>The text the result's spans refer to.</summary>
    public string Text => AutoCorrection?.Corrected ?? Original;

    /// <summary>Whether autocorrection changed the input (spans then need mapping).</summary>
    public bool IsCorrected => AutoCorrection is { IsChanged: true };

    /// <summary>The editor span (into <see cref="Original"/>) of <paramref name="span"/> of <see cref="Text"/>.</summary>
    public TextSpan ToEditor(TextSpan span) => IsCorrected ? AutoCorrection!.ToOriginal(span) : span;

    /// <summary>
    /// Where the fix <paramref name="edits"/> (against <see cref="Text"/>) apply: onto the typed text when they map back
    /// (decided with the user, T-097), else onto the corrected string (an edit touches autocorrected text).
    /// </summary>
    public EditTarget Target(IEnumerable<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var list = edits.ToImmutableArray();
        if (!IsCorrected)
        {
            return new EditTarget(Original, list, OnTypedText: true);
        }

        return AutoCorrection!.TryToOriginal(list, out var mapped)
            ? new EditTarget(Original, mapped, OnTypedText: true)
            : new EditTarget(Text, list, OnTypedText: false);
    }
}

/// <summary>Fix edits and the text they apply to (T-097).</summary>
/// <param name="Base">The text the edits apply to.</param>
/// <param name="Edits">The edits, against <paramref name="Base"/>.</param>
/// <param name="OnTypedText">Whether <paramref name="Base"/> is the typed text; <c>false</c> = the corrected string, so applying writes the autocorrections in too.</param>
public sealed record EditTarget(string Base, ImmutableArray<TextEdit> Edits, bool OnTypedText)
{
    /// <summary>The note added to a fix that also writes in the autocorrections.</summary>
    public const string WritesCorrectionsNote = "also writes in the autocorrections";

    /// <summary><see cref="Base"/> with the edits applied.</summary>
    public string Apply() => Fixer.Apply(Base, Edits);
}
