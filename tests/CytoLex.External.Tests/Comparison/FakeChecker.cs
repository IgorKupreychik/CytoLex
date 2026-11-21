using System.Collections.Immutable;

namespace CytoLex.External.Tests.Comparison;

/// <summary>An external checker whose verdicts come from a function; no process runs.</summary>
internal sealed class FakeChecker(Func<string, ExternalVerdict> verdict, ExternalBatchStatus status = ExternalBatchStatus.Completed) : IExternalChecker
{
    public string Name => "CytoGPS";

    public string Edition => "2016";

    public int Calls { get; private set; }

    public ExternalAvailability CheckAvailability() =>
        status == ExternalBatchStatus.NotConfigured ? ExternalAvailability.NotConfigured("CytoGPS not configured: test") : ExternalAvailability.Configured;

    public Task<ExternalBatchResult> CheckAsync(IReadOnlyList<string> inputs, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        var message = status switch
        {
            ExternalBatchStatus.NotConfigured => "CytoGPS not configured: test",
            ExternalBatchStatus.Failed => "CytoGPS failed: test",
            _ => null,
        };
        var items = inputs.Select(s =>
        {
            var v = status switch
            {
                ExternalBatchStatus.NotConfigured => ExternalVerdict.NotConfigured,
                ExternalBatchStatus.Failed => ExternalVerdict.Failed,
                _ => verdict(s),
            };
            var tool = v switch
            {
                ExternalVerdict.Valid => "Success",
                ExternalVerdict.Invalid => "Validation error",
                _ => message ?? "failed",
            };
            ImmutableArray<ExternalMessage> messages = v == ExternalVerdict.Invalid
                ? [new ExternalMessage(ExternalMessageKind.Validation, "rejected | by fake")]
                : [];
            return new ExternalItemResult(s, s.Replace('×', 'x'), v, tool, null, messages, []);
        });
        return Task.FromResult(new ExternalBatchResult(Name, Edition, status, message, [], [.. items]));
    }
}
