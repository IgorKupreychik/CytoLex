using CytoLex.External.CytoGps;

namespace CytoLex.External.Tests;

/// <summary>Saving the <c>cytogps</c> section of <c>config/local.yaml</c> line by line (T-089).</summary>
public sealed class ConfigFileTests : IDisposable
{
    private const string _template = """
        # Copy to config/local.yaml (gitignored) and adjust paths for your machine.
        iscn_pdf:
          "2016": "C:/path/to/ISCN_2016.pdf"
          # "2020": "C:/path/to/ISCN_2020.pdf"

        # Optional: local CytoGPS build for comparisons.
        # Each key can be overridden by an environment variable CYTOLEX_CYTOGPS_<KEY>, e.g. CYTOLEX_CYTOGPS_JAVA_HOME.
        # cytogps:
        #   source_dir: "C:/path/to/CytoGPS"                  # unpacked github.com/i2-wustl/CytoGPS (src/, target/, resource/)
        #   java_home: "C:/path/to/jdk-21"                    # a JDK: bin/java and bin/javac
        #   javax_json_jar: "C:/path/to/javax.json-1.1.4.jar" # Maven Central org.glassfish:javax.json:1.1.4 (SHA-1 checked)
        #   disk_cache: false                                 # true: also cache results in build/cache/cytogps/results/
        #   timeout_seconds: 600                              # limit for one CytoGPS run

        """;

    private readonly DirectoryInfo _repo = Directory.CreateTempSubdirectory("cytolex-configfile-");

    public ConfigFileTests() => Directory.CreateDirectory(Path.Combine(_repo.FullName, "config"));

    private string ConfigPath => CytoGpsConfigFile.PathOf(_repo.FullName);

    public void Dispose()
    {
        try
        {
            _repo.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static Dictionary<string, string?> Values(params (string Key, string? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value);

    private CytoGpsConfig Load() => CytoGpsConfig.Load(_repo.FullName, _ => null);

    [Fact]
    public void Save_OneKey_KeepsEveryOtherLineByteIdentical()
    {
        var original = """
            iscn_pdf:
              "2016": "D:/books/ISCN 2016.pdf"   # my copy
            # comment between sections
            cytogps:
              source_dir: "C:/old"     # unpacked source
              java_home: "C:/jdk"
              disk_cache: false
            other:
              key: 1

            """;
        File.WriteAllText(ConfigPath, original);

        CytoGpsConfigFile.Save(_repo.FullName, Values(("source_dir", "C:/new/CytoGPS")));

        var before = original.Split('\n');
        var after = File.ReadAllText(ConfigPath).Split('\n');
        Assert.Equal(before.Length, after.Length);
        for (var i = 0; i < before.Length; i++)
        {
            if (before[i].Contains("source_dir", StringComparison.Ordinal))
            {
                Assert.Equal("""  source_dir: "C:/new/CytoGPS" # unpacked source""", after[i]);
            }
            else
            {
                Assert.Equal(before[i], after[i]);
            }
        }

        Assert.Equal("C:/new/CytoGPS", Load().SourceDir);
        Assert.Equal("C:/jdk", Load().JavaHome);
    }

    [Fact]
    public void Save_ShortValue_KeepsCommentColumn()
    {
        File.WriteAllText(ConfigPath, "cytogps:\n  java_home: \"C:/a/very/long/path/to/jdk\"   # a JDK\n");

        CytoGpsConfigFile.Save(_repo.FullName, Values(("java_home", "C:/jdk")));

        Assert.Equal("cytogps:\n  java_home: \"C:/jdk\"                       # a JDK\n", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void Save_NoFile_CreatesItFromTheTemplate_UncommentingTheSection()
    {
        File.WriteAllText(CytoGpsConfigFile.TemplatePathOf(_repo.FullName), _template);

        CytoGpsConfigFile.Save(_repo.FullName, Values(
            ("source_dir", "C:/CytoGPS"), ("java_home", "C:/jdk-21"), ("javax_json_jar", "C:/jars/javax.json-1.1.4.jar"),
            ("disk_cache", "true"), ("timeout_seconds", null)));

        // Every line stays as it was, except the header and the set keys: uncommented ("# " goes), the new value, and the
        // inline comment at the column it had on the uncommented line.
        var before = _template.Split('\n');
        var after = File.ReadAllText(ConfigPath).Split('\n');
        Assert.Equal(before.Length, after.Length);
        string[] setKeys = ["source_dir", "java_home", "javax_json_jar", "disk_cache"];
        for (var i = 0; i < before.Length; i++)
        {
            var key = setKeys.FirstOrDefault(k => before[i].StartsWith("#   " + k + ":", StringComparison.Ordinal));
            if (before[i] == "# cytogps:")
            {
                Assert.Equal("cytogps:", after[i]);
            }
            else if (key is not null)
            {
                Assert.StartsWith("  " + key + ": ", after[i], StringComparison.Ordinal);
                var comment = before[i][before[i].IndexOf(" # ", StringComparison.Ordinal)..];
                Assert.EndsWith(comment, after[i], StringComparison.Ordinal);
                Assert.Equal(before[i].IndexOf(" # ", StringComparison.Ordinal) - 2, after[i].IndexOf(" # ", StringComparison.Ordinal));
            }
            else
            {
                Assert.Equal(before[i], after[i]);
            }
        }

        Assert.Contains(after, l => l.StartsWith("  disk_cache: true ", StringComparison.Ordinal));

        var config = Load();
        Assert.Equal("C:/CytoGPS", config.SourceDir);
        Assert.Equal("C:/jars/javax.json-1.1.4.jar", config.JavaxJsonJar);
        Assert.True(config.DiskCache);
        Assert.Equal(CytoGpsConfig.DefaultTimeout, config.Timeout);
        Assert.Equal("C:/path/to/ISCN_2016.pdf", ReadIscnPdf());
    }

    [Fact]
    public void Save_ClearedKey_IsCommentedOut_AndCanBeSetAgain()
    {
        File.WriteAllText(ConfigPath, "cytogps:\n  source_dir: \"C:/a\"  # note\n  java_home: \"C:/jdk\"\n");

        CytoGpsConfigFile.Save(_repo.FullName, Values(("source_dir", null)));
        Assert.Equal("cytogps:\n#   source_dir: \"C:/a\"  # note\n  java_home: \"C:/jdk\"\n", File.ReadAllText(ConfigPath));
        Assert.Null(Load().SourceDir);

        CytoGpsConfigFile.Save(_repo.FullName, Values(("source_dir", "C:/b")));
        Assert.Equal("cytogps:\n  source_dir: \"C:/b\"  # note\n  java_home: \"C:/jdk\"\n", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void Save_MissingKeyAndSection_AreAdded()
    {
        File.WriteAllText(ConfigPath, "iscn_pdf:\n  \"2016\": \"C:/x.pdf\"\n");

        CytoGpsConfigFile.Save(_repo.FullName, Values(("java_home", "C:/jdk")));
        Assert.Equal("iscn_pdf:\n  \"2016\": \"C:/x.pdf\"\n\ncytogps:\n  java_home: \"C:/jdk\"\n", File.ReadAllText(ConfigPath));

        CytoGpsConfigFile.Save(_repo.FullName, Values(("timeout_seconds", "90")));
        Assert.Equal(
            "iscn_pdf:\n  \"2016\": \"C:/x.pdf\"\n\ncytogps:\n  java_home: \"C:/jdk\"\n  timeout_seconds: 90\n",
            File.ReadAllText(ConfigPath));
        Assert.Equal(TimeSpan.FromSeconds(90), Load().Timeout);
    }

    [Theory]
    [InlineData(@"C:\Program Files\Java\jdk-21")]
    [InlineData("D:/odd \"quoted\" path: with # hash")]
    [InlineData("/home/user/jdk")]
    public void Save_PathsRoundTrip(string path)
    {
        CytoGpsConfigFile.Save(_repo.FullName, Values(("java_home", path)));
        Assert.Equal(path, Load().JavaHome);
    }

    [Fact]
    public void Save_WritesLf_NoBom_NoTempFileLeft()
    {
        File.WriteAllText(ConfigPath, "iscn_pdf:\r\n  \"2016\": \"C:/x.pdf\"\r\ncytogps:\r\n  java_home: \"C:/a\"\r\n");

        CytoGpsConfigFile.Save(_repo.FullName, Values(("java_home", "C:/b")));

        var bytes = File.ReadAllBytes(ConfigPath);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(["local.yaml"], Directory.GetFiles(Path.Combine(_repo.FullName, "config")).Select(Path.GetFileName));
    }

    [Fact]
    public void Save_MalformedFile_Throws_AndKeepsIt()
    {
        const string broken = "cytogps:\n  java_home: \"unclosed\n";
        File.WriteAllText(ConfigPath, broken);

        Assert.Throws<InvalidOperationException>(() => CytoGpsConfigFile.Save(_repo.FullName, Values(("disk_cache", "true"))));
        Assert.Equal(broken, File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void ReadValues_IgnoresEnvironment()
    {
        File.WriteAllText(ConfigPath, "cytogps:\n  java_home: \"C:/jdk\"\n  disk_cache: true\n");

        var values = CytoGpsConfigFile.ReadValues(_repo.FullName);

        Assert.Equal("C:/jdk", values["java_home"]);
        Assert.Equal("true", values["disk_cache"]);
        Assert.Equal("CYTOLEX_CYTOGPS_JAVAX_JSON_JAR", CytoGpsConfigFile.EnvironmentVariable("javax_json_jar"));
    }

    private string? ReadIscnPdf()
    {
        var stream = new YamlDotNet.RepresentationModel.YamlStream();
        using var reader = new StreamReader(ConfigPath);
        stream.Load(reader);
        var root = (YamlDotNet.RepresentationModel.YamlMappingNode)stream.Documents[0].RootNode;
        var section = (YamlDotNet.RepresentationModel.YamlMappingNode)root.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("iscn_pdf")];
        return ((YamlDotNet.RepresentationModel.YamlScalarNode)section.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("2016")]).Value;
    }
}
