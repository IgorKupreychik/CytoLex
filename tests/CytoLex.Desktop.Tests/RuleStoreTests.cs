using CytoLex.Core.Kb;
using CytoLex.Desktop.Rules;
using Microsoft.Data.Sqlite;

namespace CytoLex.Desktop.Tests;

/// <summary>T-039: the rule browser's read-only access to <c>build/kb.sqlite</c> (search, detail, optional quotes).</summary>
public sealed class RuleStoreTests : IDisposable
{
    private readonly RuleStore _store = RuleStore.Open(RuleStore.DefaultPath);

    public void Dispose() => _store.Dispose();

    [Fact]
    public void Database_IsCopiedNextToTheApp_AndListsTheKbEditions()
    {
        Assert.True(File.Exists(RuleStore.DefaultPath));
        Assert.Contains("2016", _store.Editions);
    }

    [Fact]
    public void EveryRuntimeRule_IsListedAndOpens_WithTheSameMetadata()
    {
        var kb = KnowledgeBase.LoadEmbedded();
        foreach (var edition in kb.Editions)
        {
            var ekb = kb.ForEdition(edition)!;
            var listed = _store.Search(edition, query: null).Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(ekb.Rules.Count, listed.Count);
            foreach (var rule in ekb.Rules)
            {
                Assert.Contains(rule.Id, listed);
                var detail = _store.Rule(rule.Edition, rule.Id);
                Assert.NotNull(detail);
                Assert.Equal(rule.Title, detail.Title);
                Assert.Equal(rule.Section, detail.Section);
                Assert.Equal(rule.Statement, detail.Statement);
                Assert.Equal(rule.Severity.ToString(), detail.Severity, ignoreCase: true);
                Assert.Equal(rule.Citations.Select(c => c.Rendered), detail.Citations);
            }
        }
    }

    [Fact]
    public void BlankQuery_ListsRulesInBookOrder()
    {
        var hits = _store.Search("2016", "  ");
        var sections = hits.Select(h => h.Section).ToList();
        Assert.Equal(sections.Order(Comparer<string>.Create(RuleStore.CompareSections)), sections);
        Assert.All(hits, h => Assert.Equal(RuleMatch.None, h.Match));
    }

    [Fact]
    public void Search_ByExactId_PutsThatRuleFirst()
    {
        var hits = _store.Search("2016", "TRANSLOC.FORMAT");
        Assert.Equal("TRANSLOC.FORMAT", hits[0].Id);
        Assert.Equal("TRANSLOC.FORMAT", _store.Search("2016", "transloc.format")[0].Id);
    }

    [Fact]
    public void Search_ByIdPrefix_FindsTheFamily()
    {
        var hits = _store.Search("2016", "TRANSLOC");
        Assert.Contains(hits, h => h.Id == "TRANSLOC.FORMAT");
        Assert.Contains(hits, h => h.Id == "TRANSLOC.JUMPING");
    }

    [Fact]
    public void Search_ByTitleAndStatementWords()
    {
        Assert.Contains(_store.Search("2016", "whole-arm translocation"), h => h.Id == "TRANSLOC.WHOLE_ARM");

        var statement = _store.Rule("2016", "TRANSLOC.JUMPING")!.Statement;
        var word = statement.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.All(char.IsLetter)).MaxBy(w => w.Length)!;
        var hits = _store.Search("2016", word);
        Assert.Contains(hits, h => h.Id == "TRANSLOC.JUMPING");
        Assert.All(hits, h => Assert.NotNull(h.Snippet));
    }

    [Fact]
    public void Search_ByExampleString_MatchesTheTokenSequence()
    {
        var hits = _store.Search("2016", "t(9;22)");
        Assert.Contains(hits, h => h.Id == "KARYO.TRANSPLANT_CHIMERA");
        Assert.Equal("\"t 9 22\"*", RuleStore.ToFtsQuery("t(9;22)"));
    }

    [Fact]
    public void Search_ByQuoteText_MarksQuoteMatches()
    {
        var quote = _store.Rule("2016", "CHROM.IDENTIFIERS")!.Quotes[0].Text;
        Assert.Contains("decreasing length", quote, StringComparison.Ordinal);
        var hit = Assert.Single(_store.Search("2016", "decreasing length"), h => h.Id == "CHROM.IDENTIFIERS");
        Assert.True(hit.Match.HasFlag(RuleMatch.Quote));
    }

    [Fact]
    public void Search_SectionFilter_KeepsOnlyThatSection()
    {
        var section = _store.Rule("2016", "TRANSLOC.FORMAT")!.Section;
        Assert.Contains(section, _store.Sections("2016"));
        var hits = _store.Search("2016", null, section);
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal(section, h.Section));
        Assert.All(_store.Search("2016", "translocation", section), h => Assert.Equal(section, h.Section));
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("(")]
    [InlineData("AND")]
    [InlineData("OR NOT")]
    [InlineData("*")]
    [InlineData("-")]
    [InlineData("^x NEAR(a b)")]
    [InlineData("id:foo")]
    [InlineData("{title}")]
    [InlineData("46,XX,del(5)(q13q33)")]
    [InlineData("×→–")]
    public void Search_AnyInput_DoesNotThrow(string query) => _ = _store.Search("2016", query);

    [Fact]
    public void UnknownRuleOrEdition_ReturnsNothing()
    {
        Assert.Null(_store.Rule("2016", "NO.SUCH_RULE"));
        Assert.Null(_store.Rule("1999", "TRANSLOC.FORMAT"));
        Assert.Empty(_store.Search("1999", null));
    }

    [Fact]
    public void Sections_AreInBookOrder()
    {
        Assert.True(RuleStore.CompareSections("2.1", "2.10") < 0);
        Assert.True(RuleStore.CompareSections("9.2.9", "9.2.10") < 0);
        Assert.True(RuleStore.CompareSections("9", "9.1") < 0);
        var sections = _store.Sections("2016");
        Assert.Equal(sections.Order(Comparer<string>.Create(RuleStore.CompareSections)), sections);
    }

    [Fact]
    public void WithoutQuotes_SearchAndDetailWork()
    {
        using var db = NoQuotesCopy.Create();
        using var store = RuleStore.Open(db.Path);
        Assert.Empty(store.Rule("2016", "CHROM.IDENTIFIERS")!.Quotes);
        Assert.Contains(store.Search("2016", "TRANSLOC"), h => h.Id == "TRANSLOC.FORMAT");
        Assert.All(store.Search("2016", "length"), h => Assert.False(h.Match.HasFlag(RuleMatch.Quote)));
    }

    [Fact]
    public void MissingOrForeignDatabase_RaisesRuleStoreException()
    {
        var missing = Path.Combine(Path.GetTempPath(), "cytolex-no-such-" + Guid.NewGuid().ToString("N") + ".sqlite");
        Assert.Contains("kb build", Assert.Throws<RuleStoreException>(() => RuleStore.Open(missing)).Message, StringComparison.Ordinal);

        using var db = NoQuotesCopy.Create("UPDATE meta SET value = '1' WHERE key = 'format_version'");
        Assert.Contains("format version 1", Assert.Throws<RuleStoreException>(() => RuleStore.Open(db.Path)).Message, StringComparison.Ordinal);

        var garbage = Path.Combine(Path.GetTempPath(), "cytolex-garbage-" + Guid.NewGuid().ToString("N") + ".sqlite");
        File.WriteAllText(garbage, "not a database");
        try
        {
            Assert.Throws<RuleStoreException>(() => RuleStore.Open(garbage));
        }
        finally
        {
            File.Delete(garbage);
        }
    }
}

/// <summary>
/// A temp copy of the KB database without source quotes, as <c>kb build</c> writes it when <c>kb/source-quotes/</c> is gone
/// (T-043 cleanup); optionally with more SQL applied.
/// </summary>
internal sealed class NoQuotesCopy : IDisposable
{
    private NoQuotesCopy(string path) => Path = path;

    public string Path { get; }

    public static NoQuotesCopy Create(string? extraSql = null)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cytolex-kb-" + Guid.NewGuid().ToString("N") + ".sqlite");
        SQLitePCL.Batteries_V2.Init();
        File.Copy(RuleStore.DefaultPath, path);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM quotes; DELETE FROM quotes_fts;" + (extraSql is null ? string.Empty : extraSql + ";");
            cmd.ExecuteNonQuery();
        }

        return new NoQuotesCopy(path);
    }

    public void Dispose() => File.Delete(Path);
}
