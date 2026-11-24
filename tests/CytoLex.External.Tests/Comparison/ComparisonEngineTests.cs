using System.Collections.Immutable;
using CytoLex.Core.Batch;
using CytoLex.Core.Validation;
using CytoLex.External.Comparison;

namespace CytoLex.External.Tests.Comparison;

/// <summary>The comparison engine and report (T-051) with the real validator and a fake checker.</summary>
public sealed class ComparisonEngineTests
{
    private const string _bothValid = "46,XY,t(9;22)(q34;q11.2)";
    private const string _order = "47,XY,+21,del(5)(q13q33)";       // CytoLex: KARYO.ABNORMALITY_ORDER; CytoGPS doesn't check order
    private const string _band = "46,XY,del(5)(q13q99)";             // both reject (no band q99)
    private const string _bandOnlyCytoLex = "46,XX,del(5)(q13q98)";  // fake: CytoGPS accepts → no suggestion (band rules are checked)
    private const string _times = "46,XX,del(6)(q13q23)×2";          // CytoLex valid; fake: CytoGPS rejects
    private const string _warned = "46,XX,dl(5)(q13q33)";          // CytoLex: SYM.LISTED_ONLY warning only (valid); fake: CytoGPS rejects
    private const string _arr = "arr[GRCh38] 9p24.3(1310386_1709409)×1 mat";
    private const string _detailed = "46,XX,add(19)(?::p13.3→qter)";

    private static readonly string[] _rejectedByChecker = [_band, _times, _warned, _arr, _detailed];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ImmutableArray<BatchItem> Items(params string[] inputs) =>
        [.. inputs.Select((s, i) => new BatchItem(i + 2, $"K{i + 1:D2}", s))];

    private static FakeChecker Checker(ExternalBatchStatus status = ExternalBatchStatus.Completed) =>
        new(s => _rejectedByChecker.Contains(s) ? ExternalVerdict.Invalid : ExternalVerdict.Valid, status);

    private static Task<ComparisonResult> Compare(ImmutableArray<BatchItem> items, IExternalChecker checker, TriageFile? triage = null) =>
        ComparisonEngine.CompareAsync(items, Validator.Default, checker, CytoGpsCoverage.Instance, triage, cancellationToken: Ct);

    [Fact]
    public async Task Classifies_EveryCell_AndKeepsUnsupportedOutOfTheMatrix()
    {
        var checker = Checker();
        var result = await Compare(Items(_bothValid, _order, _band, _bandOnlyCytoLex, _times, _arr, _detailed), checker);

        Assert.Equal(1, checker.Calls);
        Assert.Equal("2016", result.Edition);
        Assert.Equal(ExternalBatchStatus.Completed, result.CheckerStatus);
        Assert.Equal(
            [Agreement.BothValid, Agreement.OnlyCytoLexInvalid, Agreement.BothInvalid, Agreement.OnlyCytoLexInvalid,
             Agreement.OnlyCheckerInvalid, Agreement.NotSupported, Agreement.NotSupported],
            result.Items.Select(i => i.Agreement));
        Assert.Equal(new AgreementMatrix(1, 1, 2, 1, 2, 0), result.Matrix);
        Assert.Equal(5, result.Matrix.Compared);
        Assert.Equal(["K01", "K02", "K03", "K04", "K05", "K06", "K07"], result.Items.Select(i => i.Item.Id));
        Assert.Equal("arr segment", result.Items[5].Reason);
        Assert.Equal("detailed system", result.Items[6].Reason);
        Assert.Contains("KARYO.ABNORMALITY_ORDER", result.Items[1].CytoLexErrors);
        Assert.True(result.Items[0].CytoLexValid);
        Assert.False(result.Items[1].CytoLexValid);
    }

    [Fact]
    public async Task SuggestsCheckerLimitation_OnlyWhenEveryErrorIsUnchecked()
    {
        var result = await Compare(Items(_order, _bandOnlyCytoLex, _times), Checker());

        Assert.Equal(TriageLabel.CheckerLimitation, result.Items[0].SuggestedLabel);
        Assert.Null(result.Items[1].SuggestedLabel);
        Assert.Null(result.Items[2].SuggestedLabel); // only CytoGPS rejects, no CytoLex warnings: no suggestion
    }

    [Fact]
    public async Task SuggestsSeverityPolicy_WhenOnlyTheCheckerRejectsAndCytoLexWarns()
    {
        var result = await Compare(Items(_warned), Checker());

        var item = Assert.Single(result.Items);
        Assert.Equal(Agreement.OnlyCheckerInvalid, item.Agreement);
        Assert.Empty(item.CytoLexErrors);
        Assert.Contains("SYM.LISTED_ONLY", item.CytoLexNotes);
        Assert.Equal(TriageLabel.SeverityPolicy, item.SuggestedLabel);
        Assert.Contains("| SYM.LISTED_ONLY | severity-policy (suggested) |", ComparisonReport.Markdown(result), StringComparison.Ordinal);
        Assert.Contains("CytoLex warnings/infos: SYM.LISTED_ONLY", ComparisonReport.TriageDraft(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ExternalBatchStatus.NotConfigured, "CytoGPS not configured: test")]
    [InlineData(ExternalBatchStatus.Failed, "CytoGPS failed: test")]
    public async Task CheckerWithoutVerdicts_GivesNotCompared(ExternalBatchStatus status, string reason)
    {
        var result = await Compare(Items(_bothValid, _order, _arr), Checker(status));

        Assert.Equal(status, result.CheckerStatus);
        Assert.Equal([Agreement.NotCompared, Agreement.NotCompared, Agreement.NotSupported], result.Items.Select(i => i.Agreement));
        Assert.All(result.Items.Take(2), i => Assert.Equal(reason, i.Reason));
        Assert.Equal(0, result.Matrix.Compared);
        Assert.Empty(result.StaleTriage);
        Assert.Contains("not compared", ComparisonReport.Markdown(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MatchesTriage_AndListsStaleEntries()
    {
        var triage = new TriageFile("CytoGPS", "2016",
        [
            new TriageEntry(_order, TriageLabel.CheckerLimitation, true, "order", null, null, 6),
            new TriageEntry(_bothValid, TriageLabel.OurBug, true, null, null, null, 10),
        ]);

        var result = await Compare(Items(_bothValid, _order), Checker(), triage);

        Assert.Equal(TriageLabel.CheckerLimitation, result.Items[1].Triage?.Label);
        Assert.Null(result.Items[1].SuggestedLabel);
        Assert.Null(result.Items[0].Triage); // not a disagreement
        Assert.Equal(_bothValid, Assert.Single(result.StaleTriage).Iscn);
        var md = ComparisonReport.Markdown(result);
        Assert.Contains("Stale entries (no longer a disagreement in this batch): 1.", md, StringComparison.Ordinal);
        Assert.Contains("line 10:", md, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithTriage_RecomputesTriageSuggestionsAndStale_WithoutRunningAgain()
    {
        var checker = Checker();
        var result = await Compare(Items(_bothValid, _order), checker);
        Assert.Equal(TriageLabel.CheckerLimitation, result.Items[1].SuggestedLabel);

        var triage = TriageFile.Empty("CytoGPS", "2016")
            .With(new TriageEntry(_order, TriageLabel.OurBug, true, null, null, null))
            .With(new TriageEntry(_bothValid, TriageLabel.OurBug, true, null, null, null));
        var retriaged = result.WithTriage(triage, CytoGpsCoverage.Instance);

        Assert.Equal(1, checker.Calls);
        Assert.Equal(TriageLabel.OurBug, retriaged.Items[1].Triage?.Label);
        Assert.Null(retriaged.Items[1].SuggestedLabel);
        Assert.Equal(_bothValid, Assert.Single(retriaged.StaleTriage).Iscn);
        Assert.Equal(result.Matrix, retriaged.Matrix);

        var back = retriaged.WithTriage(TriageFile.Empty("CytoGPS", "2016"), CytoGpsCoverage.Instance);
        Assert.Equal(TriageLabel.CheckerLimitation, back.Items[1].SuggestedLabel);
        Assert.Empty(back.StaleTriage);
        Assert.Throws<ArgumentException>(() => result.WithTriage(TriageFile.Empty("Other", "2016"), CytoGpsCoverage.Instance));
    }

    [Fact]
    public async Task AutoCorrect_CountsValidAfterAutocorrectionAsValid_CheckerGetsTheStringAsWritten()
    {
        const string cased = "46,xy";
        var checker = Checker();
        var asWritten = await Compare(Items(cased), checker);
        Assert.False(asWritten.AutoCorrect);
        Assert.Equal(Agreement.OnlyCytoLexInvalid, asWritten.Items[0].Agreement);
        Assert.False(asWritten.Items[0].CytoLexAutoCorrected);
        Assert.Contains("- CytoLex: as written", ComparisonReport.Markdown(asWritten), StringComparison.Ordinal);

        var corrected = await ComparisonEngine.CompareAsync(Items(cased), Validator.Default, checker, CytoGpsCoverage.Instance,
            autoCorrect: true, cancellationToken: Ct);
        Assert.True(corrected.AutoCorrect);
        var item = corrected.Items[0];
        Assert.Equal(Agreement.BothValid, item.Agreement);
        Assert.True(item.CytoLexAutoCorrected);
        Assert.Equal("46,XY", item.CytoLexCorrected);
        Assert.Equal(cased, item.External.Input);
        Assert.Contains("- CytoLex: with autocorrection (T-096): valid after autocorrection counts as valid (1 strings)",
            ComparisonReport.Markdown(corrected), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsValidatedCount()
    {
        var counts = new List<int>();
        var validated = new SyncProgress(counts);
        await ComparisonEngine.CompareAsync(Items(_bothValid, _order, _band), Validator.Default, Checker(), CytoGpsCoverage.Instance,
            validated: validated, cancellationToken: Ct);

        lock (counts)
        {
            Assert.Equal(3, counts.Max());
        }
    }

    private sealed class SyncProgress(List<int> counts) : IProgress<int>
    {
        public void Report(int value)
        {
            lock (counts)
            {
                counts.Add(value);
            }
        }
    }

    [Fact]
    public async Task TriageForAnotherChecker_Throws()
    {
        var triage = TriageFile.Empty("Other", "2016");
        await Assert.ThrowsAsync<ArgumentException>(() => Compare(Items(_bothValid), Checker(), triage));
    }

    [Fact]
    public async Task Report_IsDeterministicWithLf()
    {
        var items = Items(_bothValid, _order, _band, _bandOnlyCytoLex, _times, _arr, "46,XY,a|b");
        var first = await Compare(items, Checker());
        var second = await Compare(items, Checker());

        var md = ComparisonReport.Markdown(first);
        var csv = ComparisonReport.Csv(first);
        Assert.Equal(md, ComparisonReport.Markdown(second));
        Assert.Equal(csv, ComparisonReport.Csv(second));
        Assert.DoesNotContain('\r', md + csv);
        Assert.Contains("| **CytoLex valid** | 1 | 1 |", md, StringComparison.Ordinal);
        Assert.Contains("| **CytoLex invalid** | 3 | 1 |", md, StringComparison.Ordinal);
        Assert.Contains("| not supported by CytoGPS | arr segment | 1 |", md, StringComparison.Ordinal);
        Assert.Contains("#### KARYO.ABNORMALITY_ORDER (1)", md, StringComparison.Ordinal);
        Assert.Contains("checker-limitation (suggested)", md, StringComparison.Ordinal);
        Assert.Contains("`46,XY,a\\|b`", md, StringComparison.Ordinal);
        Assert.Contains("rejected \\| by fake", md, StringComparison.Ordinal);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(string.Join(',', ComparisonReport.CsvColumns), lines[0]);
        Assert.Equal(items.Length + 1, lines.Length);
        Assert.StartsWith("K02,\"47,XY,+21,del(5)(q13q33)\",\"47,XY,+21,del(5)(q13q33)\",only-cytolex-invalid,false,true,", lines[2], StringComparison.Ordinal);
        Assert.EndsWith(",Success,true,,,,checker-limitation", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TriageDraft_ListsUntriagedDisagreementsOnce_AndLoadsAfterReview()
    {
        var triage = new TriageFile("CytoGPS", "2016", [new TriageEntry(_times, TriageLabel.CheckerLimitation, true, null, null, null)]);
        var result = await Compare(Items(_order, _order, _bandOnlyCytoLex, _times, _bothValid), Checker(), triage);

        var draft = ComparisonReport.TriageDraft(result);

        Assert.Equal(1, CountOf(draft, _order));
        Assert.DoesNotContain(_times, draft, StringComparison.Ordinal);
        Assert.Contains("label: checker-limitation", draft, StringComparison.Ordinal);
        Assert.Contains("label: TODO", draft, StringComparison.Ordinal);
        Assert.Contains("note: \"CytoLex: KARYO.ABNORMALITY_ORDER\"", draft, StringComparison.Ordinal);
        Assert.Throws<TriageFileException>(() => TriageFile.Parse(draft)); // TODO must be decided first

        var reviewed = TriageFile.Parse(draft.Replace("label: TODO", "label: our-bug", StringComparison.Ordinal));
        Assert.Equal(2, reviewed.Entries.Length);
        Assert.All(reviewed.Entries, e => Assert.False(e.Confirmed));
    }

    [Fact]
    public void KbExampleDraft_GroupsConfirmedEntriesByRule()
    {
        var triage = new TriageFile("CytoGPS", "2016",
        [
            new TriageEntry("46,XY,b", TriageLabel.OurBug, true, "fixed in T-999", ExpectedVerdict.Valid, "Z.RULE"),
            new TriageEntry("46,XY,a", TriageLabel.OurBug, true, null, ExpectedVerdict.Invalid, "A.RULE"),
            new TriageEntry("46,XY,c", TriageLabel.OurBug, true, null, ExpectedVerdict.Invalid, "Z.RULE"),
            new TriageEntry("46,XY,unconfirmed", TriageLabel.OurBug, false, null, ExpectedVerdict.Invalid, "A.RULE"),
            new TriageEntry("46,XY,nokb", TriageLabel.CheckerLimitation, true, null, null, null),
        ]);

        var draft = ComparisonReport.KbExampleDraft(triage);

        var body = string.Join('\n', draft.Split('\n').Where(l => !l.StartsWith('#')));
        Assert.Equal(
            """
            A.RULE:
              invalid:
                - text: "46,XY,a"
                  cite: TODO
                  note: "our-bug (CytoGPS comparison)"
            Z.RULE:
              valid:
                - text: "46,XY,b"
                  cite: TODO
                  note: "our-bug (CytoGPS comparison): fixed in T-999"
              invalid:
                - text: "46,XY,c"
                  cite: TODO
                  note: "our-bug (CytoGPS comparison)"

            """.ReplaceLineEndings("\n"),
            body);
        Assert.Contains("{}", ComparisonReport.KbExampleDraft(TriageFile.Empty("CytoGPS", "2016")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAll_WritesTheFourFiles()
    {
        var dir = Directory.CreateTempSubdirectory("cytolex-comparison-");
        try
        {
            var result = await Compare(Items(_bothValid, _order), Checker());
            var paths = ComparisonReport.WriteAll(dir.FullName, result, TriageFile.Empty("CytoGPS", "2016"));

            Assert.Equal(
                ["cytogps-2016.md", "cytogps-2016.csv", "cytogps-2016-triage-draft.yaml", "cytogps-2016-kb-examples-draft.yaml"],
                paths.Select(Path.GetFileName));
            Assert.All(paths, p => Assert.True(File.Exists(p)));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static int CountOf(string text, string part)
    {
        var n = 0;
        for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
        {
            n++;
        }

        return n;
    }
}
