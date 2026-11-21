using System.Collections.Frozen;
using System.Text;
using CytoLex.Core.Kb;

namespace CytoLex.External;

/// <summary>
/// Replaces non-ASCII symbol forms with their ASCII typed equivalents, as listed in the KB vocabulary
/// (entries with equivalent forms, T-063: <c>→</c> → <c>-&gt;</c>, <c>–</c>/<c>−</c> → <c>-</c>, <c>×</c> → <c>x</c>).
/// For checkers that read ASCII only, such as CytoGPS (docs/research/cytogps.md §4).
/// </summary>
public sealed class TypedSymbolForms
{
    private readonly FrozenDictionary<string, string> _map;
    private readonly int _maxLength;

    private TypedSymbolForms(FrozenDictionary<string, string> map)
    {
        _map = map;
        _maxLength = map.Count == 0 ? 0 : map.Keys.Max(k => k.Length);
    }

    /// <summary>Builds the mapping from an edition's vocabulary.</summary>
    public static TypedSymbolForms FromVocabulary(Vocabulary vocab)
    {
        ArgumentNullException.ThrowIfNull(vocab);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in vocab.Entries.Where(e => e.HasEquivalentForms))
        {
            var ascii = entry.Tokens.FirstOrDefault(IsAscii);
            if (ascii is null)
            {
                continue;
            }

            foreach (var token in entry.Tokens.Where(t => !IsAscii(t)))
            {
                map.TryAdd(token, ascii);
            }
        }

        return new TypedSymbolForms(map.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>The mapping from each non-ASCII form to its ASCII typed form.</summary>
    public IReadOnlyDictionary<string, string> Map => _map;

    /// <summary>Returns <paramref name="input"/> with every listed non-ASCII form replaced (longest match first).</summary>
    public string Apply(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_map.Count == 0 || IsAscii(input))
        {
            return input;
        }

        var sb = new StringBuilder(input.Length);
        var i = 0;
        while (i < input.Length)
        {
            var matched = false;
            for (var len = Math.Min(_maxLength, input.Length - i); len > 0; len--)
            {
                if (_map.TryGetValue(input.Substring(i, len), out var typed))
                {
                    sb.Append(typed);
                    i += len;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                sb.Append(input[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private static bool IsAscii(string s) => s.All(char.IsAscii);
}
