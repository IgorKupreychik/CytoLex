using System.Collections.Immutable;

namespace CytoLex.Core.Validation;

/// <summary>What a <see cref="DiffPart"/> is in a before/after diff.</summary>
public enum DiffKind
{
    /// <summary>Text the edits keep.</summary>
    Same,

    /// <summary>Text of the input the edits remove.</summary>
    Removed,

    /// <summary>Text the edits add.</summary>
    Added,
}

/// <summary>One run of a before/after diff.</summary>
/// <param name="Kind">Kept, removed or added.</param>
/// <param name="Text">The text of the run (never empty).</param>
public sealed record DiffPart(DiffKind Kind, string Text);

/// <summary>
/// The before/after diff of a fix (T-040, docs/design/core.md §10), built from its edits rather than by a generic diff:
/// each edit becomes a removed and an added run, trimmed by the prefix and suffix the replaced and new text share (so a reorder
/// <c>t(5;2)</c> → <c>t(2;5)</c> marks <c>5;2</c>, not the whole symbol). The <see cref="DiffKind.Same"/> and
/// <see cref="DiffKind.Removed"/> runs give back the input; the <see cref="DiffKind.Same"/> and <see cref="DiffKind.Added"/>
/// runs give <see cref="Fixer.Apply(string, IEnumerable{TextEdit})"/>.
/// </summary>
public static class FixDiff
{
    /// <summary>The diff of <paramref name="edits"/> against <paramref name="input"/>.</summary>
    /// <exception cref="ArgumentException">An edit lies outside the input, or two edits overlap (as in <see cref="Fixer.Apply(string, IEnumerable{TextEdit})"/>).</exception>
    public static ImmutableArray<DiffPart> Build(string input, IEnumerable<TextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(edits);

        var parts = new List<DiffPart>();
        var position = 0;
        foreach (var edit in edits.OrderBy(e => e.Span.Start).ThenBy(e => e.Span.Length))
        {
            if (edit.Span.Start < 0 || edit.Span.End > input.Length)
            {
                throw new ArgumentException($"Edit {edit.Span} lies outside the input (length {input.Length}).", nameof(edits));
            }

            if (edit.Span.Start < position)
            {
                throw new ArgumentException($"Edit {edit.Span} overlaps an earlier edit.", nameof(edits));
            }

            var old = edit.Span.Of(input);
            var added = edit.NewText;
            var prefix = 0;
            while (prefix < old.Length && prefix < added.Length && old[prefix] == added[prefix])
            {
                prefix++;
            }

            var suffix = 0;
            while (suffix < old.Length - prefix && suffix < added.Length - prefix
                && old[old.Length - 1 - suffix] == added[added.Length - 1 - suffix])
            {
                suffix++;
            }

            Add(parts, DiffKind.Same, input[position..(edit.Span.Start + prefix)]);
            Add(parts, DiffKind.Removed, old[prefix..(old.Length - suffix)]);
            Add(parts, DiffKind.Added, added[prefix..(added.Length - suffix)]);
            Add(parts, DiffKind.Same, old[(old.Length - suffix)..]);
            position = edit.Span.End;
        }

        Add(parts, DiffKind.Same, input[position..]);
        return [.. parts];
    }

    /// <summary>The diff of the edits of <paramref name="fix"/> (an explanation gives the input unchanged).</summary>
    public static ImmutableArray<DiffPart> Build(string input, Fix fix)
    {
        ArgumentNullException.ThrowIfNull(fix);
        return Build(input, fix.Edits.IsDefault ? [] : fix.Edits);
    }

    // Appends a run, merging it into the previous run of the same kind; empty runs are dropped.
    private static void Add(List<DiffPart> parts, DiffKind kind, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (parts.Count > 0 && parts[^1].Kind == kind)
        {
            parts[^1] = parts[^1] with { Text = parts[^1].Text + text };
        }
        else
        {
            parts.Add(new DiffPart(kind, text));
        }
    }
}
