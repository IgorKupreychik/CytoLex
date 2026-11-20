using System.Collections.Immutable;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Batch;

/// <summary>One string of a batch (T-038).</summary>
/// <param name="Line">The 1-based line of the string in the input file (for CSV: the line where its record starts).</param>
/// <param name="Id">The value of the <c>id</c> column, if the file has one.</param>
/// <param name="Input">The ISCN string, trimmed.</param>
public sealed record BatchItem(int Line, string? Id, string Input);

/// <summary>The format of a batch input file.</summary>
public enum BatchFormat
{
    /// <summary>One string per line; blank lines are skipped.</summary>
    Text,

    /// <summary>Comma-separated values (RFC 4180) with a header row.</summary>
    Csv,

    /// <summary>Tab-separated values with a header row (same quoting as CSV).</summary>
    Tsv,
}

/// <summary>The strings read from a batch file, and what the reader could not take as it was.</summary>
/// <param name="Items">The strings, in file order.</param>
/// <param name="Warnings">Problems found while reading, each naming its line (skipped rows, quoting, encoding).</param>
public sealed record BatchReadResult(ImmutableArray<BatchItem> Items, ImmutableArray<string> Warnings);

/// <summary>The result of one batch string.</summary>
/// <param name="Item">The string and where it came from.</param>
/// <param name="Report">Its validation report (one result per edition requested).</param>
public sealed record BatchItemResult(BatchItem Item, ValidationReport Report);

/// <summary>The result of a batch run (<see cref="BatchRunner"/>).</summary>
/// <param name="Editions">The editions requested, oldest first; every report has one result per edition, in this order.</param>
/// <param name="Items">One result per input string, in input order.</param>
public sealed record BatchResult(ImmutableArray<string> Editions, ImmutableArray<BatchItemResult> Items);
