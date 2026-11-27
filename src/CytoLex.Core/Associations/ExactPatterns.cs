using System.Collections.Concurrent;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Associations;

/// <summary>
/// The <c>exact</c> forms of association rules read by the edition's own parser and binder (T-109): each form becomes
/// the <see cref="AbnormalityEntry"/> it would be in a string. Parsed once per form; thread-safe.
/// </summary>
internal sealed class ExactPatterns(Parser parser, Binder binder)
{
    private readonly ConcurrentDictionary<string, AbnormalityEntry?> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// The entry of <paramref name="form"/>; <c>null</c> when the form is not one clean abnormality (recovered input,
    /// an unrecognised item, an <c>or</c> or a clone reference). Such a leaf never matches.
    /// </summary>
    public AbnormalityEntry? Get(string form) => _cache.GetOrAdd(form, Parse);

    private AbnormalityEntry? Parse(string form)
    {
        if (Segmenter.Split(form) is not [var segment] || segment.Type != NotationType.Karyotype)
        {
            return null;
        }

        var tree = parser.Parse(form, segment, fragment: true);
        if (binder.BindSegment(tree) is not KaryotypeModel model)
        {
            return null;
        }

        var list = new AbnormalityList(form);
        list.Add(segment.Index, model);
        var (_, entries) = list.Build([]);
        return entries is [var entry]
            && !entry.Flags.IsPartial
            && entry.Kind is not (AbnormalityKind.Unrecognised or AbnormalityKind.CloneReference or AbnormalityKind.Alternatives)
                ? entry
                : null;
    }
}
