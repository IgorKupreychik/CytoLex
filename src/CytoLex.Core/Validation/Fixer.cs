using System.Collections.Immutable;
using System.Text;
using CytoLex.Core.Context;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>The outcome of <see cref="Fixer.ApplyAll"/>: the new text, the fixes applied and those left for the next round.</summary>
/// <param name="Text">The input with the applied fixes' edits.</param>
/// <param name="Applied">The fixes whose edits were applied.</param>
/// <param name="Skipped">Level-1 fixes left out because their edits overlap an applied fix; re-validate and fix again.</param>
public sealed record FixAllResult(string Text, ImmutableArray<Fix> Applied, ImmutableArray<Fix> Skipped);

/// <summary>
/// Applies fixes to the original input (docs/design/core.md §10). Apply = edit the text, then re-validate: the tree is
/// never patched, so callers validate the returned string again.
/// </summary>
public static class Fixer
{
    /// <summary>The input with <paramref name="edits"/> applied.</summary>
    /// <exception cref="ArgumentException">An edit lies outside the input, or two edits overlap.</exception>
    public static string Apply(string input, IEnumerable<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(edits);

        var ordered = edits.OrderBy(e => e.Span.Start).ThenBy(e => e.Span.Length).ToList();
        var sb = new StringBuilder(input.Length + 16);
        var position = 0;
        foreach (var edit in ordered)
        {
            if (edit.Span.Start < 0 || edit.Span.End > input.Length)
            {
                throw new ArgumentException($"Edit {edit.Span} lies outside the input (length {input.Length}).", nameof(edits));
            }

            if (edit.Span.Start < position)
            {
                throw new ArgumentException($"Edit {edit.Span} overlaps an earlier edit.", nameof(edits));
            }

            sb.Append(input, position, edit.Span.Start - position).Append(edit.NewText);
            position = edit.Span.End;
        }

        return sb.Append(input, position, input.Length - position).ToString();
    }

    /// <summary>The input with the edits of <paramref name="fix"/> applied (an explanation leaves it unchanged).</summary>
    public static string Apply(string input, Fix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);
        return Apply(input, fix.Edits.IsDefault ? [] : fix.Edits);
    }

    /// <summary>
    /// "Fix all" (§10): applies every level-1 fix of <paramref name="result"/> whose edits don't overlap an already chosen
    /// fix, in span order. A diagnostic with several alternatives contributes the one for <paramref name="context"/> (or
    /// its context-free fix); conditional diagnostics that have no finding in <paramref name="context"/> are left out.
    /// </summary>
    /// <param name="input">The string <paramref name="result"/> was validated from.</param>
    /// <param name="result">One edition's result.</param>
    /// <param name="context">The context to fix for; <c>null</c> = only fixes that don't depend on a context.</param>
    /// <param name="include">Which diagnostics take part; <c>null</c> = all (autocorrection: the rules with <c>fix.auto</c>, T-096).</param>
    public static FixAllResult ApplyAll(string input, EditionResult result, SampleContext? context = null, Func<Diagnostic, bool>? include = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(result);

        var candidates = new List<Fix>();
        foreach (var d in result.Diagnostics)
        {
            if (include is not null && !include(d))
            {
                continue;
            }

            if (d.Condition is { } condition && (context is not { } c || condition.In(c) is null))
            {
                continue;
            }

            var fix = d.Fixes.FirstOrDefault(f => f.IsEdit && context is not null && f.ForContext == context)
                ?? d.Fixes.FirstOrDefault(f => f.IsEdit && f.ForContext is null);
            if (fix is not null)
            {
                candidates.Add(fix);
            }
        }

        var applied = ImmutableArray.CreateBuilder<Fix>();
        var skipped = ImmutableArray.CreateBuilder<Fix>();
        var taken = new List<TextSpan>();
        foreach (var fix in candidates.OrderBy(f => f.Edits.Min(e => e.Span.Start)))
        {
            if (fix.Edits.Any(e => taken.Any(t => Overlaps(t, e.Span))))
            {
                skipped.Add(fix);
                continue;
            }

            applied.Add(fix);
            taken.AddRange(fix.Edits.Select(e => e.Span));
        }

        var text = Apply(input, applied.SelectMany(f => f.Edits));
        return new FixAllResult(text, applied.ToImmutable(), skipped.ToImmutable());
    }

    /// <summary>Whether no two of <paramref name="edits"/> conflict (<see cref="Overlaps"/>).</summary>
    internal static bool AreDisjoint(IReadOnlyList<TextEdit> edits)
    {
        for (var i = 0; i < edits.Count; i++)
        {
            for (var j = i + 1; j < edits.Count; j++)
            {
                if (Overlaps(edits[i].Span, edits[j].Span))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Edits conflict when their spans share a character, or when two insertions (or an insertion and a span) meet at the same point.</summary>
    internal static bool Overlaps(TextSpan a, TextSpan b) =>
        a.Start < b.End && b.Start < a.End
        || (a.Length == 0 && b.Start <= a.Start && a.Start <= b.End)
        || (b.Length == 0 && a.Start <= b.Start && b.Start <= a.End);
}
