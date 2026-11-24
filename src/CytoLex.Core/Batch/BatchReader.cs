using System.Collections.Immutable;
using System.Text;

namespace CytoLex.Core.Batch;

/// <summary>
/// Reads the strings of a batch file (T-038): plain text (one string per line) or CSV/TSV with a header row. The string
/// column is the first header named <c>iscn</c>, <c>karyotype</c>, <c>input</c> or <c>string</c> (case-insensitive), else
/// the first column; an <c>id</c> column gives each string's ID. A CSV export of this library
/// (<see cref="BatchExport.CsvColumns"/>, one row per string and edition) reads back as its original strings, lines and IDs.
/// Never throws on content: problems become <see cref="BatchReadResult.Warnings"/>.
/// </summary>
public static class BatchReader
{
    private static readonly string[] _stringColumns = ["iscn", "karyotype", "input", "string"];

    /// <summary>The format of <paramref name="path"/> by its extension: <c>.csv</c>, <c>.tsv</c>, anything else text.</summary>
    public static BatchFormat FormatOf(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".CSV" => BatchFormat.Csv,
        ".TSV" => BatchFormat.Tsv,
        _ => BatchFormat.Text,
    };

    /// <summary>Reads the file at <paramref name="path"/> in the format of its extension.</summary>
    public static BatchReadResult ReadFile(string path) => Read(File.ReadAllBytes(path), FormatOf(path));

    /// <summary>Reads <paramref name="stream"/> to its end.</summary>
    public static BatchReadResult Read(Stream stream, BatchFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Read(buffer.ToArray(), format);
    }

    /// <summary>Reads <paramref name="bytes"/>: UTF-8 (BOM optional) or UTF-16 with a BOM; other bytes are read as Latin-1 with a warning.</summary>
    public static BatchReadResult Read(byte[] bytes, BatchFormat format)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var warnings = new List<string>();
        return Parse(Decode(bytes, warnings), format, warnings);
    }

    /// <summary>Reads already decoded <paramref name="text"/>.</summary>
    public static BatchReadResult Read(string text, BatchFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Parse(text, format, []);
    }

    private static string Decode(byte[] bytes, List<string> warnings)
    {
        ReadOnlySpan<byte> span = bytes;
        if (span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            span = span[3..];
        }
        else if (span.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return Encoding.Unicode.GetString(span[2..]);
        }
        else if (span.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return Encoding.BigEndianUnicode.GetString(span[2..]);
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(span);
        }
        catch (DecoderFallbackException)
        {
            warnings.Add("the file is not valid UTF-8; read as Latin-1, so check symbols such as ×, – and →");
            return Encoding.Latin1.GetString(span);
        }
    }

    private static BatchReadResult Parse(string text, BatchFormat format, List<string> warnings)
    {
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var items = format == BatchFormat.Text
            ? ParseText(text)
            : ParseTable(text, format == BatchFormat.Tsv ? '\t' : ',', warnings);
        return new BatchReadResult(items, [.. warnings]);
    }

    private static ImmutableArray<BatchItem> ParseText(string text)
    {
        var items = ImmutableArray.CreateBuilder<BatchItem>();
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var s = lines[i].Trim();
            if (s.Length > 0)
            {
                items.Add(new BatchItem(i + 1, null, s));
            }
        }

        return items.ToImmutable();
    }

    private static ImmutableArray<BatchItem> ParseTable(string text, char delimiter, List<string> warnings)
    {
        var records = Csv.Parse(text, delimiter, warnings).Where(r => !r.IsBlank).ToList();
        if (records.Count == 0)
        {
            return [];
        }

        var header = records[0].Fields.Select(f => f.Trim()).ToList();
        if (BatchExport.IsExportHeader(header, StringComparer.OrdinalIgnoreCase))
        {
            return ParseExport(records, warnings);
        }

        var column = _stringColumns
            .Select(name => header.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(i => i >= 0, 0);
        var idColumn = header.FindIndex(h => string.Equals(h, "id", StringComparison.OrdinalIgnoreCase));

        var items = ImmutableArray.CreateBuilder<BatchItem>();
        foreach (var record in records.Skip(1))
        {
            var input = FieldOf(record, column);
            if (input.Length == 0)
            {
                warnings.Add($"line {record.Line}: no string in column '{header[column]}'; row skipped");
                continue;
            }

            var id = idColumn >= 0 ? FieldOf(record, idColumn) : string.Empty;
            items.Add(new BatchItem(record.Line, id.Length > 0 ? id : null, input));
        }

        return items.ToImmutable();
    }

    /// <summary>A CSV export: one row per string and edition; each string once, with its original line and ID.</summary>
    private static ImmutableArray<BatchItem> ParseExport(List<Csv.Record> records, List<string> warnings)
    {
        var items = ImmutableArray.CreateBuilder<BatchItem>();
        BatchItem? last = null;
        foreach (var record in records.Skip(1))
        {
            var input = FieldOf(record, 2);
            if (input.Length == 0 || !int.TryParse(FieldOf(record, 0), out var line))
            {
                warnings.Add($"line {record.Line}: no string or line number; row skipped");
                continue;
            }

            var id = FieldOf(record, 1);
            var item = new BatchItem(line, id.Length > 0 ? id : null, input);
            if (item != last)
            {
                items.Add(item);
                last = item;
            }
        }

        return items.ToImmutable();
    }

    private static string FieldOf(Csv.Record record, int column) =>
        column < record.Fields.Count ? record.Fields[column].Trim() : string.Empty;
}
