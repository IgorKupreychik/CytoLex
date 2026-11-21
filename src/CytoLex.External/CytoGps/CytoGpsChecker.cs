using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CytoLex.Core.Kb;

namespace CytoLex.External.CytoGps;

/// <summary>
/// The CytoGPS adapter (T-049): runs a locally compiled CytoGPS on a batch (one JVM per batch, batch file → JSON) and
/// normalizes its results. Local build only, no network (decision T-048); setup in <see cref="CytoGpsConfig"/>.
/// Results are cached in memory per instance and, only with <c>disk_cache: true</c>, under
/// <c>build/cache/cytogps/results/</c> (synthetic/book corpora only).
/// </summary>
public sealed class CytoGpsChecker : IExternalChecker
{
    /// <summary>The ISCN edition CytoGPS follows (docs/research/cytogps.md §4).</summary>
    public const string CytoGpsEdition = "2016";

    private readonly CytoGpsConfig _config;
    private readonly TypedSymbolForms _forms;
    private readonly IProcessRunner _runner;
    private readonly string _expectedJarSha1;
    private readonly ConcurrentDictionary<(string Fingerprint, string Sent), CytoGpsEntry> _cache = new();
    private readonly ConcurrentDictionary<string, ImmutableArray<string>> _bands = new(StringComparer.Ordinal);

    /// <summary>Creates the adapter; symbol forms come from the embedded KB's 2016 vocabulary unless given.</summary>
    public CytoGpsChecker(CytoGpsConfig config, TypedSymbolForms? forms = null)
        : this(config, forms, ProcessRunner.Instance)
    {
    }

    internal CytoGpsChecker(CytoGpsConfig config, TypedSymbolForms? forms, IProcessRunner runner, string expectedJarSha1 = CytoGpsBuild.JavaxJsonSha1)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _forms = forms ?? _defaultForms.Value;
        _runner = runner;
        _expectedJarSha1 = expectedJarSha1;
    }

    private static readonly Lazy<TypedSymbolForms> _defaultForms = new(() =>
        TypedSymbolForms.FromVocabulary(
            (KnowledgeBase.LoadEmbedded().ForEdition(CytoGpsEdition)
                ?? throw new InvalidOperationException($"The embedded KB has no edition {CytoGpsEdition}.")).Vocab));

    /// <summary>The adapter for this machine's <c>config/local.yaml</c> (and environment).</summary>
    public static CytoGpsChecker FromLocalConfig() => new(CytoGpsConfig.Load());

    public string Name => "CytoGPS";

    public string Edition => CytoGpsEdition;

    /// <summary>The setup this adapter uses.</summary>
    public CytoGpsConfig Config => _config;

    public ExternalAvailability CheckAvailability() =>
        CytoGpsBuild.TryResolve(_config, out _, out var reason, _expectedJarSha1)
            ? ExternalAvailability.Configured
            : ExternalAvailability.NotConfigured(reason);

    /// <summary>The string as sent to CytoGPS: printed symbols in their typed ASCII forms (§4).</summary>
    public string ToSent(string input) => _forms.Apply(input);

    public async Task<ExternalBatchResult> CheckAsync(
        IReadOnlyList<string> inputs,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var sent = inputs.Select(ToSent).ToArray();

        if (!CytoGpsBuild.TryResolve(_config, out var setup, out var reason, _expectedJarSha1))
        {
            return Batch(ExternalBatchStatus.NotConfigured, reason, [],
                inputs.Select((s, i) => Item(s, sent[i], ExternalVerdict.NotConfigured, reason)));
        }

        string fingerprint;
        try
        {
            fingerprint = await CytoGpsBuild.EnsureBuiltAsync(setup, _runner, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return Failed(inputs, sent, ex.Message);
        }

        var bands = Bands(fingerprint);
        var misses = sent
            .Where(s => SendProblem(s) is null && (bands.IsDefault || Lookup(fingerprint, s) is null))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        string? failure = null;
        if (misses.Count > 0)
        {
            progress?.Report($"Running CytoGPS on {misses.Count} string(s)");
            failure = await RunAsync(setup, fingerprint, misses, cancellationToken).ConfigureAwait(false);
            bands = Bands(fingerprint);
        }

        var items = inputs.Select((input, i) => Result(input, sent[i], fingerprint, failure)).ToImmutableArray();
        return failure is null
            ? new ExternalBatchResult(Name, Edition, ExternalBatchStatus.Completed, null, bands.IsDefault ? [] : bands, items)
            : new ExternalBatchResult(Name, Edition, ExternalBatchStatus.Failed, failure, bands.IsDefault ? [] : bands, items);
    }

    private ExternalItemResult Result(string input, string sent, string fingerprint, string? failure)
    {
        if (SendProblem(sent) is { } problem)
        {
            return Item(input, sent, ExternalVerdict.Failed, problem);
        }

        if (Lookup(fingerprint, sent) is not { } entry)
        {
            return Item(input, sent, ExternalVerdict.Failed, failure ?? "CytoGPS returned no result for this string.");
        }

        var verdict = CytoGpsJson.VerdictOf(entry.Status);
        return new ExternalItemResult(input, sent, verdict, entry.Status, entry.RevisedKaryotype, entry.Messages, entry.Clones);
    }

    /// <summary>Why a string cannot be sent (CytoGPS reads one string per line), or <c>null</c>.</summary>
    private static string? SendProblem(string sent) =>
        string.IsNullOrWhiteSpace(sent) ? "Empty string: not sent to CytoGPS."
        : sent.AsSpan().IndexOfAny('\r', '\n') >= 0 ? "String contains a line break: not sent to CytoGPS (one string per line)."
        : null;

    /// <summary>Runs CytoGPS on the strings; returns a failure message, or <c>null</c> when every string got a result.</summary>
    private async Task<string?> RunAsync(CytoGpsSetup setup, string fingerprint, List<string> batch, CancellationToken ct)
    {
        // Inputs go to a fresh temp directory (not the repo, T-048) that is deleted afterwards.
        var temp = Directory.CreateTempSubdirectory("cytolex-cytogps-");
        try
        {
            var input = Path.Combine(temp.FullName, "input.txt");
            var output = Path.Combine(temp.FullName, "output.json");
            await File.WriteAllTextAsync(input, string.Join("\n", batch) + "\n", new UTF8Encoding(false), ct).ConfigureAwait(false);

            string[] args = ["-Dfile.encoding=UTF-8", "-cp", setup.ClassPath, "CytoGpsCli", input, output];
            var outcome = await _runner.RunAsync(setup.Java, args, temp.FullName, _config.Timeout, ct).ConfigureAwait(false);
            if (outcome.TimedOut)
            {
                return $"CytoGPS timed out after {_config.Timeout.TotalSeconds:0} s (timeout_seconds).";
            }

            if (outcome.ExitCode != 0 || !File.Exists(output))
            {
                return $"CytoGPS failed (exit {outcome.ExitCode}):\n" + CytoGpsBuild.Tail(outcome.StdErr + outcome.StdOut);
            }

            CytoGpsOutput parsed;
            try
            {
                parsed = CytoGpsJson.Parse(await File.ReadAllTextAsync(output, Encoding.UTF8, ct).ConfigureAwait(false));
            }
            catch (FormatException ex)
            {
                return "CytoGPS output could not be read: " + ex.Message;
            }

            Store(fingerprint, parsed.Bands);
            var missing = 0;
            for (var i = 0; i < batch.Count; i++)
            {
                // CytoGPS writes one entry per input line, in order, echoing the line.
                if (i < parsed.Entries.Length && string.Equals(parsed.Entries[i].Karyotype, batch[i], StringComparison.Ordinal))
                {
                    Store(fingerprint, batch[i], parsed.Entries[i]);
                }
                else
                {
                    missing++;
                }
            }

            return missing == 0 && parsed.Entries.Length == batch.Count
                ? null
                : $"CytoGPS output does not match the input: {parsed.Entries.Length} entries for {batch.Count} strings, {missing} unmatched.";
        }
        finally
        {
            try
            {
                temp.Delete(recursive: true);
            }
            catch (IOException)
            {
                // Best effort; the OS cleans the temp directory.
            }
        }
    }

    private CytoGpsEntry? Lookup(string fingerprint, string sent)
    {
        if (_cache.TryGetValue((fingerprint, sent), out var hit))
        {
            return hit;
        }

        if (!_config.DiskCache)
        {
            return null;
        }

        var file = DiskEntryPath(fingerprint, sent);
        if (!File.Exists(file) || Bands(fingerprint) is not { IsDefault: false } bands)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
            var entry = CytoGpsJson.ParseEntry(doc.RootElement, bands.Length);
            if (!string.Equals(entry.Karyotype, sent, StringComparison.Ordinal))
            {
                return null;
            }

            _cache[(fingerprint, sent)] = entry;
            return entry;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or IOException)
        {
            return null; // unreadable cache file: run CytoGPS again
        }
    }

    private void Store(string fingerprint, string sent, CytoGpsEntry entry)
    {
        _cache[(fingerprint, sent)] = entry;
        if (_config.DiskCache)
        {
            var file = DiskEntryPath(fingerprint, sent);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, entry.RawJson + "\n", new UTF8Encoding(false));
        }
    }

    private ImmutableArray<string> Bands(string fingerprint)
    {
        if (_bands.TryGetValue(fingerprint, out var bands))
        {
            return bands;
        }

        var file = Path.Combine(DiskDir(fingerprint), "bands.json");
        if (_config.DiskCache && File.Exists(file))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
                return _bands[fingerprint] = CytoGpsJson.ParseBands(doc.RootElement);
            }
            catch (Exception ex) when (ex is JsonException or FormatException or IOException)
            {
                return default;
            }
        }

        return default;
    }

    private void Store(string fingerprint, ImmutableArray<string> bands)
    {
        _bands[fingerprint] = bands;
        if (_config.DiskCache)
        {
            Directory.CreateDirectory(DiskDir(fingerprint));
            File.WriteAllText(Path.Combine(DiskDir(fingerprint), "bands.json"), JsonSerializer.Serialize(bands) + "\n", new UTF8Encoding(false));
        }
    }

    private string DiskDir(string fingerprint) => Path.Combine(_config.CacheDir, "results", fingerprint);

    private string DiskEntryPath(string fingerprint, string sent) =>
        Path.Combine(DiskDir(fingerprint), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sent))) + ".json");

    private ExternalBatchResult Failed(IReadOnlyList<string> inputs, string[] sent, string message) =>
        Batch(ExternalBatchStatus.Failed, message, [], inputs.Select((s, i) => Item(s, sent[i], ExternalVerdict.Failed, message)));

    private ExternalBatchResult Batch(ExternalBatchStatus status, string? message, ImmutableArray<string> bands, IEnumerable<ExternalItemResult> items) =>
        new(Name, Edition, status, message, bands, [.. items]);

    private static ExternalItemResult Item(string input, string sent, ExternalVerdict verdict, string status) =>
        new(input, sent, verdict, status, null, [], []);
}
