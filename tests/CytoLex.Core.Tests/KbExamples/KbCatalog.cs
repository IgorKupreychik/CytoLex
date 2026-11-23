using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CytoLex.Core.Tests.KbExamples;

/// <summary>A KB rule as the harness and the coverage report need it.</summary>
internal sealed record KbRuleInfo(
    string Edition,
    string Id,
    string Section,
    string File,
    IReadOnlyList<string> AppliesTo,
    IReadOnlyList<string> Context,
    Severity Severity,
    bool NeedsReview,
    int? FixLevel,
    string? FixKind,
    int ValidCount,
    int InvalidCount,
    int CorrectedCount)
{
    /// <summary>The sample contexts the rule is limited to (empty = every context). <c>meiotic</c> is a notation type, not a sample context.</summary>
    public IReadOnlyList<SampleContext> SampleContexts { get; } =
    [
        .. Context.Select(c => c switch
            {
                "constitutional" => (SampleContext?)SampleContext.Constitutional,
                "neoplastic" => SampleContext.Acquired,
                _ => null,
            })
            .OfType<SampleContext>()
            .Distinct(),
    ];
}

/// <summary>One test case built from a KB example.</summary>
/// <param name="Rule">The rule the example belongs to.</param>
/// <param name="Kind">What the case checks.</param>
/// <param name="Index">Index of the example in the rule's <c>valid</c> or <c>invalid</c> list (corrected: the invalid one).</param>
/// <param name="Text">The example text; for <see cref="ExampleKind.Corrected"/> the invalid text the fix starts from.</param>
/// <param name="Corrected">The <c>corrected</c> form (only for <see cref="ExampleKind.Corrected"/>).</param>
/// <param name="IsFragment">A book fragment rather than a whole notation string (see <see cref="KbCatalog.IsFragment"/>).</param>
internal sealed record KbExampleCase(KbRuleInfo Rule, ExampleKind Kind, int Index, string Text, string? Corrected, bool IsFragment)
{
    /// <summary>Serialisable test key, e.g. <c>2016|TRANSLOC.CHROMOSOME_ORDER|invalid|0</c>.</summary>
    public string Key => KbCatalog.MakeKey(Rule.Edition, Rule.Id, Kind, Index);

    /// <summary>The string that must be valid: the example (valid) or the corrected form.</summary>
    public string CheckedText => Kind == ExampleKind.Corrected ? Corrected! : Text;

    public string DisplayName => string.Create(
        CultureInfo.InvariantCulture,
        $"[{Rule.Edition}] {Rule.Id} {Kind.ToString().ToLowerInvariant()}#{Index + 1}: {(Kind == ExampleKind.Corrected ? $"{Text} => {Corrected}" : Text)}");
}

/// <summary>
/// The examples of every rule in the full KB (<c>build/kb.json</c>, embedded in the test assembly as
/// <c>CytoLex.Tests.kb.json</c>), as test cases (T-025).
/// </summary>
internal sealed partial class KbCatalog
{
    public const string ResourceName = "CytoLex.Tests.kb.json";

    private static readonly Lazy<KbCatalog> _default = new(LoadEmbedded);
    private readonly Dictionary<string, KbExampleCase> _byKey;

    private KbCatalog(int formatVersion, IReadOnlyList<string> editions, IReadOnlyList<KbRuleInfo> rules, IReadOnlyList<KbExampleCase> cases)
    {
        FormatVersion = formatVersion;
        Editions = editions;
        Rules = rules;
        Cases = cases;
        _byKey = cases.ToDictionary(c => c.Key, StringComparer.Ordinal);
    }

    /// <summary>The catalog of the embedded <c>build/kb.json</c>.</summary>
    public static KbCatalog Default => _default.Value;

    public int FormatVersion { get; }

    public IReadOnlyList<string> Editions { get; }

    /// <summary>All rules, per edition in KB order.</summary>
    public IReadOnlyList<KbRuleInfo> Rules { get; }

    /// <summary>All cases, in rule order; per rule valid, then invalid, then corrected.</summary>
    public IReadOnlyList<KbExampleCase> Cases { get; }

    public KbExampleCase Find(string key) =>
        _byKey.TryGetValue(key, out var c) ? c : throw new KeyNotFoundException($"No KB example case '{key}'.");

    public static string MakeKey(string edition, string ruleId, ExampleKind kind, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{edition}|{ruleId}|{kind.ToString().ToLowerInvariant()}|{index}");

    /// <summary>
    /// A book fragment, not a whole notation string: it contains <c>…</c> (omitted abnormalities,
    /// docs/decisions.md T-010), or it does not start like a whole string (a count followed by <c>,</c>, <c>~</c>,
    /// <c>&lt;</c>, <c>X</c>/<c>Y</c> or the end; <c>mos</c>/<c>chi</c>, <c>//</c>, <c>end</c>, or a technique keyword such as
    /// <c>ish</c>/<c>arr</c>, or a leading breakage or meiotic symbol such as <c>chtg</c>/<c>MI</c>/<c>fem</c>, T-018), e.g. <c>inv(2)</c>, <c>+21</c>, <c>GTG</c>, and bare bands or variants such as <c>1p31</c>,
    /// <c>16qh+</c> (T-027).
    /// </summary>
    public static bool IsFragment(string text) => text.Contains('…', StringComparison.Ordinal) || !WholeStringStart().IsMatch(text);

    [GeneratedRegex(@"^(?:\d+(?:[,~<(XY]|$)|//|(?:mos|chi|end|ish|nuc ish|fib ish|rev ish|cgh|arr|rsa|seq|ch[tr][gbe]|cht|sce|pvz|MII|AII|PI|MI|AI|fem|mal|spm|oom|lep|zyg|pac|dip|dit|dia)(?![A-Za-z]))")]
    private static partial Regex WholeStringStart();

    public static KbCatalog Load(Stream json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var formatVersion = root.GetProperty("format_version").GetInt32();
        var editions = new List<string>();
        var rules = new List<KbRuleInfo>();
        var cases = new List<KbExampleCase>();

        foreach (var edition in root.GetProperty("editions").EnumerateObject())
        {
            editions.Add(edition.Name);
            foreach (var r in edition.Value.GetProperty("rules").EnumerateArray())
            {
                var examples = r.GetProperty("examples");
                var valid = Array(examples, "valid");
                var invalid = Array(examples, "invalid");
                var fix = r.TryGetProperty("fix", out var f) ? f : (JsonElement?)null;
                var rule = new KbRuleInfo(
                    edition.Name,
                    r.GetProperty("id").GetString()!,
                    r.GetProperty("section").GetString()!,
                    r.GetProperty("file").GetString()!,
                    Strings(r, "applies_to"),
                    Strings(r, "context"),
                    ParseSeverity(r.GetProperty("severity").GetString()!),
                    r.GetProperty("review").GetProperty("needs_review").GetBoolean(),
                    fix?.GetProperty("level").GetInt32(),
                    fix is { } fx && fx.TryGetProperty("kind", out var k) ? k.GetString() : null,
                    valid.Count,
                    invalid.Count,
                    invalid.Count(e => Corrected(e) is not null));
                rules.Add(rule);

                for (var i = 0; i < valid.Count; i++)
                {
                    var text = valid[i].GetProperty("text").GetString()!;
                    cases.Add(new KbExampleCase(rule, ExampleKind.Valid, i, text, null, IsFragment(text)));
                }

                for (var i = 0; i < invalid.Count; i++)
                {
                    var text = invalid[i].GetProperty("text").GetString()!;
                    cases.Add(new KbExampleCase(rule, ExampleKind.Invalid, i, text, null, IsFragment(text)));
                }

                for (var i = 0; i < invalid.Count; i++)
                {
                    if (Corrected(invalid[i]) is { } corrected)
                    {
                        var text = invalid[i].GetProperty("text").GetString()!;
                        cases.Add(new KbExampleCase(rule, ExampleKind.Corrected, i, text, corrected, IsFragment(text) || IsFragment(corrected)));
                    }
                }
            }
        }

        return new KbCatalog(formatVersion, editions, rules, cases);
    }

    private static KbCatalog LoadEmbedded()
    {
        using var stream = typeof(KbCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing; run 'uv run --directory tools kb build' and rebuild.");
        return Load(stream);
    }

    private static List<JsonElement> Array(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array ? [.. a.EnumerateArray()] : [];

    private static List<string> Strings(JsonElement parent, string name) =>
        [.. Array(parent, name).Select(e => e.GetString()!)];

    private static string? Corrected(JsonElement example) =>
        example.TryGetProperty("corrected", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;

    private static Severity ParseSeverity(string s) => s switch
    {
        "error" => Severity.Error,
        "warning" => Severity.Warning,
        "info" => Severity.Info,
        _ => throw new InvalidDataException($"Unknown severity '{s}'."),
    };
}
