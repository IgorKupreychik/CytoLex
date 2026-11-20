using System.Globalization;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;
using CytoLex.Core.Tests.KbExamples;

namespace CytoLex.Core.Tests.Parsing;

/// <summary>Parses notation strings the way the validator will: split into segments, karyotype segments parsed (T-027).</summary>
internal static class ParseHelper
{
    public static Parser Parser2016 => Parser.ForEdition("2016");

    /// <summary>The notation types the parser implements (karyotype, T-027; arr, T-034).</summary>
    public static bool IsParsed(NotationType type) => type is NotationType.Karyotype or NotationType.Arr;

    /// <summary>The syntax trees of the karyotype and arr segments of <paramref name="text"/> (other types are not parsed yet).</summary>
    public static IReadOnlyList<SyntaxTree> Parse(string text, bool fragment = false) =>
    [
        .. Segmenter.Split(text)
            .Where(s => IsParsed(s.Type))
            .Select(s => Parser2016.Parse(text, s, fragment)),
    ];

    /// <summary>All syntax diagnostics of <paramref name="text"/>, in input order.</summary>
    public static IReadOnlyList<SyntaxDiagnostic> Diagnostics(string text, bool fragment = false) =>
        [.. Parse(text, fragment).SelectMany(t => t.Diagnostics).OrderBy(d => d.Span.Start)];

    /// <summary><c>RULE 'span text'</c> per diagnostic, for readable assertions.</summary>
    public static string Describe(string text, SyntaxDiagnostic d) =>
        string.Create(CultureInfo.InvariantCulture, $"{d.RuleId} '{d.Span.Of(text)}'@{d.Span.Start}");

    /// <summary>
    /// The distinct KB karyotype/arr/general examples of edition 2016 (valid, invalid and corrected) that have a segment the
    /// parser implements, with their fragment flag (ish-only strings of general rules have nothing to parse yet).
    /// </summary>
    public static IEnumerable<(string Text, bool Fragment, ExampleKind Kind, string RuleId)> KbExamples() =>
        KbCatalog.Default.Cases
            .Where(c => c.Rule.Edition == "2016" && c.Rule.AppliesTo.Any(a => a is "karyotype" or "arr" or "general"))
            .Where(c => Segmenter.Split(c.CheckedText).Any(s => IsParsed(s.Type)))
            .Select(c => (Text: c.CheckedText, Fragment: KbCatalog.IsFragment(c.CheckedText), c.Kind, RuleId: c.Rule.Id))
            .DistinctBy(x => (x.Text, x.Kind == ExampleKind.Invalid));
}
