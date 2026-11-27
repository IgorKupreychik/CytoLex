using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Batch;

/// <summary>One row of the CSV export: one string in one edition (T-038).</summary>
/// <param name="Line">The string's line in the input file.</param>
/// <param name="Id">Its ID, if the input had one.</param>
/// <param name="Input">The string.</param>
/// <param name="Edition">The edition.</param>
/// <param name="Status">Whether the edition was validated.</param>
/// <param name="Valid">Valid in the edition (<c>false</c> when not implemented).</param>
/// <param name="ValidContexts">The contexts without errors, <c>;</c>-separated (e.g. <c>Constitutional;Acquired</c>).</param>
/// <param name="Complete">Every segment and construct was validated.</param>
/// <param name="Errors">Error count.</param>
/// <param name="Warnings">Warning count.</param>
/// <param name="Infos">Info count.</param>
/// <param name="Conditional">Context-dependent diagnostics (also counted by their worst severity).</param>
/// <param name="Rules">The diagnostics, <c>;</c>-separated, as <c>Severity:RULE.ID@start-end</c> (spans in <paramref name="Corrected"/> when it is set).</param>
/// <param name="Verdict">The verdict (T-096); for a pre-T-096 export read back, derived from <paramref name="Status"/> and <paramref name="Valid"/>.</param>
/// <param name="Corrected">The string after autocorrection when autocorrection changed it (T-096); else empty.</param>
/// <param name="AutoCorrections">The applied corrections, <c>;</c>-separated, as <c>RULE.ID@start-end=new text</c> (spans in <paramref name="Input"/>).</param>
public sealed record BatchCsvRow(
    int Line,
    string? Id,
    string Input,
    string Edition,
    EditionStatus Status,
    bool Valid,
    string ValidContexts,
    bool Complete,
    int Errors,
    int Warnings,
    int Infos,
    int Conditional,
    string Rules,
    EditionVerdict Verdict,
    string Corrected,
    string AutoCorrections);

/// <summary>The JSON export document.</summary>
/// <param name="Format">Always <see cref="BatchExport.JsonFormat"/>.</param>
/// <param name="FormatVersion">The format version (<see cref="BatchExport.JsonFormatVersion"/>).</param>
/// <param name="Editions">The editions of the run.</param>
/// <param name="Items">The strings with their full reports.</param>
public sealed record BatchJsonDocument(string Format, int FormatVersion, ImmutableArray<string> Editions, ImmutableArray<BatchItemResult> Items);

/// <summary>
/// Batch exports (T-038). JSON keeps every report in full (spans, KB IDs, citations, fixes) and reads back into a
/// <see cref="BatchResult"/>. CSV is a summary with one row per string and edition (yes/no validity per edition for
/// comparisons, T-051), UTF-8 with a BOM so spreadsheet programs show <c>×</c>/<c>–</c>; it reads back into
/// <see cref="BatchCsvRow"/>s, and as batch input into the original strings (<see cref="BatchReader"/>).
/// Values are written as they are: prefixing <c>'</c> against spreadsheet formulas would break the round-trip, and ISCN
/// strings start with a digit or a technique keyword.
/// </summary>
public static class BatchExport
{
    /// <summary>The <c>Format</c> value of a JSON export.</summary>
    public const string JsonFormat = "cytolex-batch";

    /// <summary>
    /// The current JSON format version. 2 (T-096): an edition result may carry <c>AutoCorrection</c>, and then its spans
    /// refer to the corrected string. 3 (T-070): a cell line written with <c>idem</c>/<c>sl</c>/<c>sdl</c> carries
    /// <c>Reference</c>, <c>Inherited</c> and <c>FullForm</c>. 4 (T-101): an edition result carries <c>Loci</c>.
    /// 5 (T-103): a locus placed from the probe table carries <c>ProbeRow</c> (source, reviewed).
    /// Older files read as they are (no loci, no probe rows).
    /// </summary>
    public const int JsonFormatVersion = 5;

    /// <summary>The CSV header (T-096 appended <c>verdict</c>, <c>corrected</c>, <c>autocorrections</c>).</summary>
    public static ImmutableArray<string> CsvColumns { get; } =
    [
        "line", "id", "input", "edition", "status", "valid", "valid_contexts", "complete", "errors", "warnings", "infos", "conditional", "rules",
        "verdict", "corrected", "autocorrections",
    ];

    /// <summary>The header of exports written before T-096; still read.</summary>
    public static ImmutableArray<string> CsvColumnsV1 { get; } = [.. CsvColumns.Take(13)];

    private static readonly UTF8Encoding _utf8Bom = new(encoderShouldEmitUTF8Identifier: true);

    private static readonly JsonSerializerOptions _json = new(Validator.JsonOptions)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep ×, –, § readable; the file is not embedded in HTML
        WriteIndented = true,
    };

    /// <summary>Writes the JSON export of <paramref name="result"/>.</summary>
    public static void WriteJson(Stream stream, BatchResult result)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(result);
        JsonSerializer.Serialize(stream, new BatchJsonDocument(JsonFormat, JsonFormatVersion, result.Editions, result.Items), _json);
    }

    /// <summary>Reads a JSON export.</summary>
    /// <exception cref="InvalidDataException">Not a CytoLex batch export, or a newer format version.</exception>
    public static BatchResult ReadJson(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        BatchJsonDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<BatchJsonDocument>(stream, _json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Not a CytoLex batch export: " + ex.Message, ex);
        }

        if (doc is null || doc.Format != JsonFormat)
        {
            throw new InvalidDataException($"Not a CytoLex batch export (Format must be '{JsonFormat}').");
        }

        if (doc.FormatVersion > JsonFormatVersion)
        {
            throw new InvalidDataException($"Batch export format version {doc.FormatVersion} is newer than this version reads ({JsonFormatVersion}).");
        }

        return new BatchResult(doc.Editions, doc.Items);
    }

    /// <summary>The CSV rows of <paramref name="result"/>: per string, one row per edition.</summary>
    public static ImmutableArray<BatchCsvRow> CsvRows(BatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return
        [
            .. result.Items.SelectMany(i => i.Report.Results.Select(r => new BatchCsvRow(
                i.Item.Line,
                i.Item.Id,
                i.Item.Input,
                r.Edition,
                r.Status,
                r.Status == EditionStatus.Implemented && r.Validity.IsValid,
                string.Join(';', r.Validity.ValidContexts),
                r.Validity.IsComplete,
                r.Counts.Errors,
                r.Counts.Warnings,
                r.Counts.Infos,
                r.Counts.Conditional,
                string.Join(';', r.Diagnostics.Select(d => $"{d.Severity}:{d.RuleId}@{d.Span.Start}-{d.Span.End}")),
                r.Verdict,
                r.AutoCorrection is { IsChanged: true } a ? a.Corrected : string.Empty,
                r.AutoCorrection is { } c
                    ? string.Join(';', c.Corrections.Select(x => $"{x.RuleId}@{x.OriginalSpan.Start}-{x.OriginalSpan.End}={x.NewText}"))
                    : string.Empty))),
        ];
    }

    /// <summary>Writes the CSV export of <paramref name="result"/> (UTF-8 with BOM, LF line ends).</summary>
    public static void WriteCsv(Stream stream, BatchResult result)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var writer = new StreamWriter(stream, _utf8Bom, leaveOpen: true);
        writer.Write(Csv.Row(CsvColumns, ','));
        foreach (var row in CsvRows(result))
        {
            writer.Write(Csv.Row(
                [
                    Num(row.Line), row.Id ?? string.Empty, row.Input, row.Edition, row.Status.ToString(), Bool(row.Valid), row.ValidContexts,
                    Bool(row.Complete), Num(row.Errors), Num(row.Warnings), Num(row.Infos), Num(row.Conditional), row.Rules,
                    row.Verdict.ToString(), row.Corrected, row.AutoCorrections,
                ],
                ','));
        }
    }

    /// <summary>Reads a CSV export back into its rows.</summary>
    /// <exception cref="InvalidDataException">The header is not <see cref="CsvColumns"/>, or a value does not parse.</exception>
    public static ImmutableArray<BatchCsvRow> ReadCsv(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var warnings = new List<string>();
        var records = Csv.Parse(reader.ReadToEnd(), ',', warnings).Where(r => !r.IsBlank).ToList();
        if (warnings.Count > 0)
        {
            throw new InvalidDataException(string.Join("; ", warnings));
        }

        if (records.Count == 0 || !IsExportHeader(records[0].Fields, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Not a CytoLex batch CSV export (header: " + string.Join(',', CsvColumns) + ").");
        }

        var columns = records[0].Fields.Count;
        var rows = ImmutableArray.CreateBuilder<BatchCsvRow>();
        foreach (var r in records.Skip(1))
        {
            if (r.Fields.Count != columns)
            {
                throw new InvalidDataException($"line {r.Line}: {r.Fields.Count} fields, expected {columns}.");
            }

            var f = r.Fields;
            try
            {
                var status = Enum.Parse<EditionStatus>(f[4]);
                var valid = bool.Parse(f[5]);
                var hasAutoCorrection = columns == CsvColumns.Length;
                rows.Add(new BatchCsvRow(
                    int.Parse(f[0], CultureInfo.InvariantCulture),
                    f[1].Length > 0 ? f[1] : null,
                    f[2],
                    f[3],
                    status,
                    valid,
                    f[6],
                    bool.Parse(f[7]),
                    int.Parse(f[8], CultureInfo.InvariantCulture),
                    int.Parse(f[9], CultureInfo.InvariantCulture),
                    int.Parse(f[10], CultureInfo.InvariantCulture),
                    int.Parse(f[11], CultureInfo.InvariantCulture),
                    f[12],
                    hasAutoCorrection ? Enum.Parse<EditionVerdict>(f[13]) : VerdictOf(status, valid),
                    hasAutoCorrection ? f[14] : string.Empty,
                    hasAutoCorrection ? f[15] : string.Empty));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            {
                throw new InvalidDataException($"line {r.Line}: {ex.Message}", ex);
            }
        }

        return rows.ToImmutable();
    }

    /// <summary>Writes the export of <paramref name="result"/> to <paramref name="path"/>: JSON for <c>.json</c>, else CSV.</summary>
    public static void WriteFile(string path, BatchResult result)
    {
        using var stream = File.Create(path);
        if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(stream, result);
        }
        else
        {
            WriteCsv(stream, result);
        }
    }

    /// <summary>Whether <paramref name="header"/> is the header of a CSV export (<see cref="CsvColumns"/> or <see cref="CsvColumnsV1"/>).</summary>
    internal static bool IsExportHeader(IReadOnlyList<string> header, StringComparer comparer) =>
        header.SequenceEqual(CsvColumns, comparer) || header.SequenceEqual(CsvColumnsV1, comparer);

    private static EditionVerdict VerdictOf(EditionStatus status, bool valid) =>
        status == EditionStatus.NotImplemented ? EditionVerdict.NotImplemented : valid ? EditionVerdict.Valid : EditionVerdict.NotValid;

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";
}
