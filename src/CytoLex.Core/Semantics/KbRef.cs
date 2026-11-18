namespace CytoLex.Core.Semantics;

/// <summary>What a <see cref="KbRef"/> points at.</summary>
public enum KbRefKind
{
    /// <summary>A KB rule ID (<c>rule:DEL.FORMAT</c>).</summary>
    Rule,

    /// <summary>A vocab item, <c>&lt;vocabulary&gt;/&lt;label&gt;</c> (<c>vocab:symbols/del</c>).</summary>
    Vocab,

    /// <summary>A band name of the edition's band set (<c>band:5q13</c>).</summary>
    Band,
}

/// <summary>
/// A typed reference to a KB entry, stable across the app and exports (docs/design/core.md §5): a rule, a vocab item or
/// a band. Its text form is <c>rule:ID</c>, <c>vocab:key/label</c> or <c>band:name</c>.
/// </summary>
/// <param name="Kind">What the reference points at.</param>
/// <param name="Id">The rule ID, <c>key/label</c> of the vocab item, or the band name.</param>
public readonly record struct KbRef(KbRefKind Kind, string Id)
{
    /// <summary>A rule reference.</summary>
    public static KbRef Rule(string id) => new(KbRefKind.Rule, id);

    /// <summary>A band reference.</summary>
    public static KbRef Band(string name) => new(KbRefKind.Band, name);

    /// <summary>A vocab reference, e.g. <c>Vocab("symbols", "del")</c>.</summary>
    public static KbRef Vocab(string vocabulary, string label) => new(KbRefKind.Vocab, $"{vocabulary}/{label}");

    /// <summary>Parses the text form (<c>rule:DEL.FORMAT</c>, <c>vocab:symbols/del</c>, <c>band:5q13</c>).</summary>
    /// <exception cref="FormatException">The text has no known prefix or an empty ID.</exception>
    public static KbRef Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && colon < text.Length - 1)
        {
            var id = text[(colon + 1)..];
            switch (text[..colon])
            {
                case "rule":
                    return Rule(id);
                case "vocab":
                    return new KbRef(KbRefKind.Vocab, id);
                case "band":
                    return Band(id);
                default:
                    break;
            }
        }

        throw new FormatException($"'{text}' is not a KbRef (rule:…, vocab:…, band:…).");
    }

    public override string ToString() => Kind switch
    {
        KbRefKind.Rule => "rule:" + Id,
        KbRefKind.Vocab => "vocab:" + Id,
        _ => "band:" + Id,
    };
}
