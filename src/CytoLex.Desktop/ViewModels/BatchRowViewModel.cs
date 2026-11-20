using System.Collections.Immutable;
using CytoLex.Core.Batch;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>One string of the batch table (T-038): where it came from, one verdict cell per edition and its first problem.</summary>
public sealed class BatchRowViewModel
{
    public BatchRowViewModel(BatchItemResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
        Line = result.Item.Line;
        Id = result.Item.Id ?? string.Empty;
        Input = result.Item.Input.ReplaceLineEndings(" ");
        Cells = [.. result.Report.Results.Select(r => new BatchCellViewModel(r))];

        var implemented = result.Report.Results.Where(r => r.Status == EditionStatus.Implemented).ToList();
        IsNotValid = implemented.Any(r => !r.Validity.IsValid);
        IsValid = implemented.Count > 0 && !IsNotValid;
        IsIncomplete = implemented.Any(r => !r.Validity.IsComplete);
        var first = implemented
            .SelectMany(r => r.Diagnostics)
            .Where(d => d.Severity >= Severity.Warning)
            .OrderByDescending(d => d.Severity)
            .FirstOrDefault();
        FirstProblem = first is null ? string.Empty : $"{first.Message} ({first.RuleId})";
    }

    /// <summary>The string's batch result.</summary>
    public BatchItemResult Result { get; }

    /// <summary>The line in the input file.</summary>
    public int Line { get; }

    /// <summary>The ID from the input file, or empty.</summary>
    public string Id { get; }

    /// <summary>The string (line breaks shown as spaces).</summary>
    public string Input { get; }

    /// <summary>One cell per edition, in the batch's edition order.</summary>
    public ImmutableArray<BatchCellViewModel> Cells { get; }

    /// <summary>Not valid in at least one implemented edition.</summary>
    public bool IsNotValid { get; }

    /// <summary>Valid in every implemented edition (and at least one is implemented).</summary>
    public bool IsValid { get; }

    /// <summary>A segment or construct was not validated in some implemented edition.</summary>
    public bool IsIncomplete { get; }

    /// <summary>The most severe error or warning, e.g. <c>Comma after the chromosome count (KARYO.COUNT_SEX_ORDER)</c>.</summary>
    public string FirstProblem { get; }
}

/// <summary>The verdict of one string in one edition: <c>✓</c>, <c>✓*</c> (as far as implemented), <c>✗ 2E 1W</c> or <c>–</c>.</summary>
public sealed class BatchCellViewModel
{
    public BatchCellViewModel(EditionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Edition = result.Edition;
        IsNotImplemented = result.Status == EditionStatus.NotImplemented;
        IsValid = !IsNotImplemented && result.Validity.IsValid;
        IsInvalid = !IsNotImplemented && !IsValid;
        var c = result.Counts;
        Text = IsNotImplemented ? "–"
            : IsValid ? (result.Validity.IsComplete ? "✓" : "✓*")
            : $"✗ {c.Errors}E" + (c.Warnings > 0 ? $" {c.Warnings}W" : string.Empty);
        Tip = IsNotImplemented ? result.Reason ?? $"ISCN {Edition}: not implemented"
            : $"ISCN {Edition}: {(IsValid ? "valid" : "not valid")}"
                + (result.Validity.IsComplete ? string.Empty : " as far as implemented")
                + $" · {c.Errors} error(s), {c.Warnings} warning(s), {c.Infos} info";
    }

    /// <summary>The edition.</summary>
    public string Edition { get; }

    /// <summary>The cell text.</summary>
    public string Text { get; }

    /// <summary>The tooltip: verdict and counts, or why the edition was not validated.</summary>
    public string Tip { get; }

    /// <summary>Valid (green).</summary>
    public bool IsValid { get; }

    /// <summary>Not valid in an implemented edition (red).</summary>
    public bool IsInvalid { get; }

    /// <summary>The edition is not implemented (grey).</summary>
    public bool IsNotImplemented { get; }
}
