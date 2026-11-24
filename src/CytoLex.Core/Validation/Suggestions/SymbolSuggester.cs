using System.Collections.Immutable;
using System.Globalization;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation.Suggestions;

/// <summary>
/// "Did you mean" options for an unlisted word (<c>SYM.LISTED_ONLY</c>, level 2, T-041): listed ch. 3 abbreviations
/// whose meaning starts with the word (<c>trans</c> → <c>t</c>, Translocation) or that are a small typo away from it
/// (<c>dl</c> → <c>del</c>). Every candidate is checked by re-parsing the segment with it: it is kept only when the
/// parser then reports fewer faults and none at the replaced word (so <c>dl(5)</c> gets <c>del</c>, not <c>dn</c>).
/// </summary>
internal sealed class SymbolSuggester
{
    private readonly ImmutableArray<(string Token, VocabEntry Entry)> _tokens;
    private readonly Parser _parser;

    public SymbolSuggester(IEditionKb kb, Parser parser)
    {
        ArgumentNullException.ThrowIfNull(kb);
        _parser = parser;
        _tokens =
        [
            .. kb.Vocab.Entries
                .Where(e => e.Vocabulary == "symbols" && e.Kind == "abbreviation" && e.Tokens.Length > 0)
                .Select(e => (Token: e.Tokens[0], Entry: e))
                .Where(t => t.Token.All(char.IsAsciiLetter)),
        ];
    }

    /// <summary>
    /// The options for the unlisted word at <paramref name="span"/> of <paramref name="segment"/>, best first, at most
    /// <see cref="DiagnosticFactory.MaxSuggestions"/>; empty when nothing plausible parses.
    /// </summary>
    public ImmutableArray<SuggestionProposal> Suggest(string input, Segment segment, bool isFragment, TextSpan span)
    {
        var word = input.Substring(span.Start, span.Length);
        if (word.Length == 0 || !word.All(char.IsAsciiLetter))
        {
            return [];
        }

        var baseline = _parser.Parse(input, segment, isFragment).Diagnostics.Length;
        var result = ImmutableArray.CreateBuilder<SuggestionProposal>();
        foreach (var (token, entry) in Candidates(word))
        {
            var edit = new TextEdit(span, token);
            if (Parses(input, segment, isFragment, edit, baseline))
            {
                result.Add(new SuggestionProposal(edit, $"Did you mean '{token}' ({entry.Title})?"));
                if (result.Count == DiagnosticFactory.MaxSuggestions)
                {
                    break;
                }
            }
        }

        return result.ToImmutable();
    }

    /// <summary>The candidate tokens for <paramref name="word"/>, best first: meaning matches, then by edit distance.</summary>
    internal IEnumerable<(string Token, VocabEntry Entry)> Candidates(string word)
    {
        var lower = word.ToLowerInvariant();
        var maxTypo = word.Length <= 4 ? 1 : 2;
        return _tokens
            .Select(t => (t.Token, t.Entry, Score: Score(lower, t.Token, t.Entry, maxTypo)))
            .Where(t => t.Score is not null && !string.Equals(t.Token, word, StringComparison.Ordinal))
            .OrderBy(t => t.Score!.Value)
            .ThenBy(t => t.Token, StringComparer.Ordinal)
            .Select(t => (t.Token, t.Entry))
            .DistinctBy(t => t.Token, StringComparer.Ordinal);
    }

    /// <summary>
    /// 0 = the word starts the first word of a meaning (<c>trans</c>: Translocation); 1–2 = a near typo of that word
    /// (the distance; <c>inversoin</c>); 2–3 = a typo of the token (1 + the distance; <c>dl</c>: del); <c>null</c> = no match.
    /// </summary>
    private static int? Score(string word, string token, VocabEntry entry, int maxTypo)
    {
        int? best = null;
        if (word.Length >= 3)
        {
            foreach (var first in entry.Meanings.Select(FirstWord))
            {
                if (first.Length >= word.Length && first.StartsWith(word, StringComparison.Ordinal))
                {
                    return 0;
                }

                if (word.Length >= 6 && Distance(word, first) is var d and <= 2)
                {
                    best = Math.Min(best ?? d, d);
                }
            }
        }

        var typo = Distance(word, token.ToLowerInvariant());
        return typo <= maxTypo ? Math.Min(best ?? int.MaxValue, 1 + typo) : best;
    }

    private static string FirstWord(string meaning)
    {
        var end = 0;
        while (end < meaning.Length && char.IsAsciiLetter(meaning[end]))
        {
            end++;
        }

        return meaning[..end].ToLower(CultureInfo.InvariantCulture);
    }

    private bool Parses(string input, Segment segment, bool isFragment, TextEdit edit, int baseline)
    {
        var text = Fixer.Apply(input, [edit]);
        var delta = edit.NewText.Length - edit.Span.Length;
        var shifted = segment with { Span = new TextSpan(segment.Span.Start, segment.Span.Length + delta) };
        var replaced = new TextSpan(edit.Span.Start, edit.NewText.Length);
        var diagnostics = _parser.Parse(text, shifted, isFragment).Diagnostics;
        return diagnostics.Length < baseline
            && !diagnostics.Any(d => d.Span.Start < replaced.End && replaced.Start < d.Span.End);
    }

    /// <summary>Optimal string alignment distance (Levenshtein with adjacent transpositions).</summary>
    internal static int Distance(string a, string b)
    {
        var w = b.Length + 1;
        var d = new int[(a.Length + 1) * w];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i * w] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var v = Math.Min(Math.Min(d[((i - 1) * w) + j] + 1, d[(i * w) + j - 1] + 1), d[((i - 1) * w) + j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    v = Math.Min(v, d[((i - 2) * w) + j - 2] + 1);
                }

                d[(i * w) + j] = v;
            }
        }

        return d[(a.Length * w) + b.Length];
    }
}
