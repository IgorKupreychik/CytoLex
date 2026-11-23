using System.Globalization;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;
using CytoLex.Core.Tests.KbExamples;

namespace CytoLex.Core.Tests.Parsing;

/// <summary>Parses notation strings the way the validator will: split into segments, karyotype segments parsed (T-027).</summary>
internal static class ParseHelper
{
    public static Parser Parser2016 => Parser.ForEdition("2016");

    /// <summary>The notation types the parser implements (karyotype, T-027; arr, T-034; the in situ types, T-033).</summary>
    public static bool IsParsed(NotationType type) => type is NotationType.Karyotype or NotationType.Arr
        or NotationType.Ish or NotationType.NucIsh or NotationType.FibIsh or NotationType.RevIsh or NotationType.Cgh;

    /// <summary>The syntax trees of the parsed segments of <paramref name="text"/> (other types are not parsed yet).</summary>
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
    /// The distinct KB examples of edition 2016 (valid, invalid and corrected) of rules for a parsed type (or general rules)
    /// that have a segment the parser implements, with their fragment flag.
    /// </summary>
    public static IEnumerable<(string Text, bool Fragment, ExampleKind Kind, string RuleId)> KbExamples() =>
        KbCatalog.Default.Cases
            .Where(c => c.Rule.Edition == "2016" && c.Rule.AppliesTo.Any(a => a is "karyotype" or "arr" or "general" or "ish" or "nuc_ish" or "fib_ish" or "rev_ish" or "cgh"))
            .Where(c => Segmenter.Split(c.CheckedText).Any(s => IsParsed(s.Type)))
            .Select(c => (Text: c.CheckedText, Fragment: KbCatalog.IsFragment(c.CheckedText), c.Kind, RuleId: c.Rule.Id))
            .DistinctBy(x => (x.Text, x.Kind == ExampleKind.Invalid));
}
