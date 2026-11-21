using System.Collections.Immutable;
using System.Text.Json;

namespace CytoLex.External.CytoGps;

/// <summary>One entry of CytoGPS's <c>output[]</c>, kept with its raw JSON (for the opt-in disk cache).</summary>
/// <param name="Karyotype">The line CytoGPS read.</param>
/// <param name="Status">CytoGPS's status (one of <see cref="CytoGpsJson.Statuses"/>).</param>
/// <param name="RevisedKaryotype">The repaired string, if any.</param>
/// <param name="Messages">Grammar and validation messages.</param>
/// <param name="Clones">Clones with LGF counts.</param>
/// <param name="RawJson">The entry as CytoGPS wrote it.</param>
internal sealed record CytoGpsEntry(
    string Karyotype,
    string Status,
    string? RevisedKaryotype,
    ImmutableArray<ExternalMessage> Messages,
    ImmutableArray<ExternalClone> Clones,
    string RawJson);

/// <summary>A parsed <c>CytoGPS_Result.json</c>.</summary>
internal sealed record CytoGpsOutput(ImmutableArray<string> Bands, ImmutableArray<CytoGpsEntry> Entries);

/// <summary>
/// Reads CytoGPS's batch JSON (<c>CytoGPS_Result.json</c> schema, docs/research/cytogps.md §3) and maps its
/// statuses to verdicts (§8).
/// </summary>
internal static class CytoGpsJson
{
    public const string Success = "Success";
    public const string FixableSuccess = "Fixable grammar error and success";
    public const string FixableInvalid = "Fixable grammar error but containing validation error";
    public const string Nonfixable = "Nonfixable grammar error";
    public const string ValidationError = "Validation error";

    /// <summary>The five statuses CytoGPS writes.</summary>
    public static readonly ImmutableArray<string> Statuses = [Success, FixableSuccess, FixableInvalid, Nonfixable, ValidationError];

    /// <summary>
    /// The normalized verdict: <c>Success</c> and <c>Fixable grammar error and success</c> are valid, the other
    /// three invalid; an unknown status is a failure.
    /// </summary>
    public static ExternalVerdict VerdictOf(string status) => status switch
    {
        Success or FixableSuccess => ExternalVerdict.Valid,
        FixableInvalid or Nonfixable or ValidationError => ExternalVerdict.Invalid,
        _ => ExternalVerdict.Failed,
    };

    /// <summary>Parses a whole result file.</summary>
    /// <exception cref="FormatException">The JSON does not have the expected shape.</exception>
    public static CytoGpsOutput Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var bands = ParseBands(Required(root, "iscn2016_bands"));
            var output = Required(root, "output");
            if (output.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("'output' is not an array.");
            }

            var entries = output.EnumerateArray().Select(e => ParseEntry(e, bands.Length)).ToImmutableArray();
            return new CytoGpsOutput(bands, entries);
        }
        catch (JsonException ex)
        {
            throw new FormatException("CytoGPS output is not valid JSON: " + ex.Message, ex);
        }
    }

    /// <summary>Parses a band list (the <c>iscn2016_bands</c> array).</summary>
    public static ImmutableArray<string> ParseBands(JsonElement bands)
    {
        if (bands.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("'iscn2016_bands' is not an array.");
        }

        return [.. bands.EnumerateArray().Select(b => b.GetString() ?? throw new FormatException("Band is not a string."))];
    }

    /// <summary>Parses one <c>output[]</c> entry; LGF arrays must have <paramref name="bandCount"/> values.</summary>
    public static CytoGpsEntry ParseEntry(JsonElement e, int bandCount)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Output entry is not an object.");
        }

        var karyotype = Required(e, "karyotype").GetString() ?? throw new FormatException("'karyotype' is not a string.");
        var status = Required(e, "status").GetString() ?? throw new FormatException("'status' is not a string.");
        var revised = e.TryGetProperty("revised_karyotype", out var r) ? r.GetString() : null;

        var messages = ImmutableArray.CreateBuilder<ExternalMessage>();
        messages.AddRange(Strings(e, "grammar_error").Select(t => new ExternalMessage(ExternalMessageKind.Grammar, t)));
        messages.AddRange(Strings(e, "validation_error").Select(t => new ExternalMessage(ExternalMessageKind.Validation, t)));

        var clones = ImmutableArray.CreateBuilder<ExternalClone>();
        if (e.TryGetProperty("parsing_result", out var pr) && pr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in pr.EnumerateArray())
            {
                var lgf = Required(c, "loss_gain_fusion_computing");
                clones.Add(new ExternalClone(
                    c.TryGetProperty("cell_number", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : null,
                    c.TryGetProperty("relationship", out var rel) ? rel.GetString() : null,
                    Strings(c, "uncertain_events"),
                    Strings(c, "derivative_chromosome_detailed_systems"),
                    Counts(lgf, "loss", bandCount),
                    Counts(lgf, "gain", bandCount),
                    Counts(lgf, "fusion", bandCount)));
            }
        }

        return new CytoGpsEntry(karyotype, status, revised, messages.ToImmutable(), clones.ToImmutable(), e.GetRawText());
    }

    private static JsonElement Required(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v : throw new FormatException($"Missing '{name}'.");

    private static ImmutableArray<string> Strings(JsonElement e, string name) =>
        e.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array
            ? [.. a.EnumerateArray().Select(x => x.GetString() ?? "")]
            : [];

    private static ImmutableArray<int> Counts(JsonElement lgf, string name, int bandCount)
    {
        var values = Required(lgf, name);
        if (values.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"'{name}' is not an array.");
        }

        ImmutableArray<int> counts = [.. values.EnumerateArray().Select(x => x.GetInt32())];
        return counts.Length == bandCount
            ? counts
            : throw new FormatException($"'{name}' has {counts.Length} values, expected {bandCount} (one per band).");
    }
}
