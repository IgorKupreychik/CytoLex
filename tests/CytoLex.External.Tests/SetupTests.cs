using CytoLex.Core.Kb;
using CytoLex.External.CytoGps;

namespace CytoLex.External.Tests;

/// <summary>Config reading, symbol conversion and the no-network guarantee.</summary>
public sealed class SetupTests
{
    [Fact]
    public void Config_ReadsSection_EnvironmentOverrides()
    {
        var repo = Directory.CreateTempSubdirectory("cytolex-config-");
        try
        {
            Directory.CreateDirectory(Path.Combine(repo.FullName, "config"));
            File.WriteAllText(Path.Combine(repo.FullName, "config", "local.yaml"), """
                iscn_pdf:
                  "2016": "C:/x.pdf"
                cytogps:
                  source_dir: "C:/cytogps"
                  java_home: "C:/jdk"
                  javax_json_jar: "C:/javax.json-1.1.4.jar"
                  disk_cache: true
                  timeout_seconds: 42
                """);

            var config = CytoGpsConfig.Load(repo.FullName, k => k == "CYTOLEX_CYTOGPS_JAVA_HOME" ? "D:/other-jdk" : null);

            Assert.Equal("C:/cytogps", config.SourceDir);
            Assert.Equal("D:/other-jdk", config.JavaHome);
            Assert.Equal("C:/javax.json-1.1.4.jar", config.JavaxJsonJar);
            Assert.True(config.DiskCache);
            Assert.Equal(TimeSpan.FromSeconds(42), config.Timeout);
            Assert.Equal(Path.Combine(repo.FullName, "build", "cache", "cytogps"), config.CacheDir);
        }
        finally
        {
            repo.Delete(recursive: true);
        }
    }

    [Fact]
    public void Config_NoFileOrSection_IsEmptyAndNotConfigured()
    {
        var repo = Directory.CreateTempSubdirectory("cytolex-config-");
        try
        {
            var config = CytoGpsConfig.Load(repo.FullName, _ => null);
            Assert.Null(config.SourceDir);
            Assert.False(config.DiskCache);
            Assert.Equal(CytoGpsConfig.DefaultTimeout, config.Timeout);
            Assert.False(new CytoGpsChecker(config).CheckAvailability().IsConfigured);
        }
        finally
        {
            repo.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("46,XY,t(9;22)(q34;q11.2)×2", "46,XY,t(9;22)(q34;q11.2)x2")]
    [InlineData("45,XX,–7", "45,XX,-7")]
    [InlineData("45,XX,−7", "45,XX,-7")]
    [InlineData("46,XX,der(1)(1pter→1q21::3q21→3qter)", "46,XX,der(1)(1pter->1q21::3q21->3qter)")]
    [InlineData("46,XY", "46,XY")]
    public void TypedSymbolForms_ConvertsPrintedSymbolsFromTheKbVocabulary(string printed, string typed)
    {
        var forms = TypedSymbolForms.FromVocabulary(KnowledgeBase.LoadEmbedded().ForEdition("2016")!.Vocab);
        Assert.Equal(typed, forms.Apply(printed));
        Assert.All(forms.Map, kv => Assert.True(kv.Value.All(char.IsAscii)));
    }

    [Fact]
    public void NoNetwork_ExternalAssemblyReferencesNoNetworkingAssemblies()
    {
        string[] forbidden = ["System.Net.Http", "System.Net.Sockets", "System.Net.Requests", "System.Net.WebClient", "System.Net.WebSockets"];
        foreach (var assembly in new[] { typeof(CytoGpsChecker).Assembly, typeof(YamlDotNet.RepresentationModel.YamlStream).Assembly })
        {
            var names = assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
            Assert.DoesNotContain(names, n => forbidden.Contains(n, StringComparer.Ordinal));
        }
    }
}
