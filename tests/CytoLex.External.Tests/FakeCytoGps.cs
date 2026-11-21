using System.Text;
using System.Text.Json;
using CytoLex.External.CytoGps;

namespace CytoLex.External.Tests;

/// <summary>
/// A fake CytoGPS setup in a temp directory (source tree, JDK, jar files with dummy content) and a fake process
/// runner that "compiles" and writes CytoGPS-shaped JSON. No Java runs.
/// </summary>
internal sealed class FakeCytoGps : IDisposable
{
    public static readonly string[] Bands = ["1p36.33", "1p36.32", "1p36.31"];

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("cytolex-fake-cytogps-");

    public FakeCytoGps()
    {
        Write("cytogps/src/toolkit/AggregateJsonForBatchFile.java", "class AggregateJsonForBatchFile {}");
        Write("cytogps/src/main/KaryotypeRunner.java", "class KaryotypeRunner {}");
        Write("cytogps/target/generated-sources/antlr4/compiler/KaryotypeParser.java", "class KaryotypeParser {}");
        Write("cytogps/" + CytoGpsBuild.AntlrJarPath, "antlr");
        var exe = OperatingSystem.IsWindows() ? ".exe" : "";
        Write("jdk/bin/java" + exe, "");
        Write("jdk/bin/javac" + exe, "");
        Write("javax.json-1.1.4.jar", "javax.json");
        Directory.CreateDirectory(RepoRoot);
    }

    public string Root => _root.FullName;

    public string RepoRoot => Path.Combine(Root, "repo");

    public string JarSha1 => Convert.ToHexStringLower(
#pragma warning disable CA5350 // test fixture: matches the adapter's SHA-1 integrity check
        System.Security.Cryptography.SHA1.HashData(File.ReadAllBytes(Path.Combine(Root, "javax.json-1.1.4.jar"))));
#pragma warning restore CA5350

    public FakeRunner Runner { get; } = new();

    public CytoGpsConfig Config(bool diskCache = false) => new(
        RepoRoot,
        Path.Combine(Root, "cytogps"),
        Path.Combine(Root, "jdk"),
        Path.Combine(Root, "javax.json-1.1.4.jar"),
        diskCache,
        TimeSpan.FromSeconds(30));

    public CytoGpsChecker Checker(bool diskCache = false) => new(Config(diskCache), null, Runner, JarSha1);

    public void Write(string relative, string content)
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        try
        {
            _root.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>Fakes javac (creates a class file) and the CytoGPS wrapper (writes JSON for each input line).</summary>
internal sealed class FakeRunner : IProcessRunner
{
    public int JavacCalls { get; private set; }

    public int JavaCalls { get; private set; }

    public List<string> LastBatch { get; } = [];

    /// <summary>Entries to drop from the output (simulates a mismatched output).</summary>
    public int DropLast { get; set; }

    public int ExitCode { get; set; }

    public Task<ProcessOutcome> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Path.GetFileNameWithoutExtension(fileName) == "javac")
        {
            JavacCalls++;
            var classes = arguments[arguments.ToList().IndexOf("-d") + 1];
            File.WriteAllText(Path.Combine(classes, "CytoGpsCli.class"), "");
            return Task.FromResult(new ProcessOutcome(0, "", "", false));
        }

        JavaCalls++;
        if (ExitCode != 0)
        {
            return Task.FromResult(new ProcessOutcome(ExitCode, "", "Exception in thread \"main\"", false));
        }

        var input = arguments[^2];
        var output = arguments[^1];
        var lines = File.ReadAllText(input, Encoding.UTF8).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        LastBatch.Clear();
        LastBatch.AddRange(lines);
        var entries = lines.Take(lines.Length - DropLast).Select(Entry).ToList();
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["producer"] = "CytoGPS",
            ["date"] = "2026-11-21",
            ["iscn2016_bands"] = FakeCytoGps.Bands,
            ["output"] = entries,
        });
        File.WriteAllText(output, json);
        return Task.FromResult(new ProcessOutcome(0, "", "", false));
    }

    /// <summary>A rejected entry for strings with "q99", else an accepted one with a loss on the first band.</summary>
    private static Dictionary<string, object> Entry(string karyotype) => karyotype.Contains("q99", StringComparison.Ordinal)
        ? new()
        {
            ["karyotype"] = karyotype,
            ["status"] = "Validation error",
            ["validation_error"] = new[] { "not a valid chromosome breakpoint" },
        }
        : new()
        {
            ["karyotype"] = karyotype,
            ["status"] = "Success",
            ["parsing_result"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["loss_gain_fusion_computing"] = new Dictionary<string, int[]>
                    {
                        ["loss"] = [1, 0, 0],
                        ["gain"] = [0, 0, 0],
                        ["fusion"] = [0, 0, 0],
                    },
                },
            },
        };
}
