using System.Text;

namespace CytoLex.Core.Batch;

/// <summary>
/// A small RFC 4180 reader and writer for batch files (T-038): quoted fields, <c>""</c> for a quote, delimiters and
/// line breaks inside quotes. Lenient on input (problems become warnings), strict on output.
/// </summary>
internal static class Csv
{
    /// <summary>One record and the 1-based line where it starts.</summary>
    internal sealed record Record(int Line, IReadOnlyList<string> Fields)
    {
        /// <summary>Whether the record is a blank line (one empty field).</summary>
        public bool IsBlank => Fields.Count == 1 && Fields[0].Length == 0;
    }

    /// <summary>Splits <paramref name="text"/> into records; <paramref name="warnings"/> gets the quoting problems.</summary>
    public static List<Record> Parse(string text, char delimiter, List<string> warnings)
    {
        var records = new List<Record>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordLine = 1;
        var inQuotes = false;
        var afterQuote = false; // the field's closing quote was read
        var i = 0;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            afterQuote = false;
        }

        void EndRecord()
        {
            EndField();
            records.Add(new Record(recordLine, [.. fields]));
            fields.Clear();
            recordLine = line;
        }

        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    afterQuote = true;
                }
                else
                {
                    if (c == '\n' || (c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
                    {
                        line++;
                    }

                    field.Append(c);
                }

                i++;
                continue;
            }

            if (c == delimiter)
            {
                EndField();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                EndRecord();
            }
            else if (c == '"' && field.Length == 0 && !afterQuote)
            {
                inQuotes = true;
            }
            else
            {
                if (afterQuote)
                {
                    warnings.Add($"line {line}: text after a closing quote; kept as it is");
                    afterQuote = false;
                }

                field.Append(c);
            }

            i++;
        }

        if (inQuotes)
        {
            warnings.Add($"line {recordLine}: a quoted field is not closed; read to the end of the file");
        }

        // A final line break ends the last record; anything after it is a last record without one.
        if (field.Length > 0 || fields.Count > 0 || afterQuote || inQuotes)
        {
            EndRecord();
        }

        return records;
    }

    /// <summary>One field, quoted when it holds the delimiter, a quote, a line break or edge whitespace.</summary>
    public static string Field(string value, char delimiter)
    {
        var quote = value.Length > 0
            && (value.Contains(delimiter, StringComparison.Ordinal)
                || value.AsSpan().IndexOfAny("\"\r\n") >= 0
                || char.IsWhiteSpace(value[0])
                || char.IsWhiteSpace(value[^1]));
        return quote ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }

    /// <summary>One record with a trailing LF.</summary>
    public static string Row(IEnumerable<string> values, char delimiter) =>
        string.Join(delimiter, values.Select(v => Field(v, delimiter))) + "\n";
}
