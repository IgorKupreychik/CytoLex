using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CytoLex.Desktop.Rules;

/// <summary>The rule database could not be opened (missing, unreadable or of another format).</summary>
public sealed class RuleStoreException : Exception
{
    public RuleStoreException()
    {
    }

    public RuleStoreException(string message)
        : base(message)
    {
    }

    public RuleStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Where a search hit matched.</summary>
[Flags]
public enum RuleMatch
{
    /// <summary>No query (the rule is listed by the filters only).</summary>
    None = 0,

    /// <summary>ID, title, statement or example texts (<c>rules_fts</c>).</summary>
    Rule = 1,

    /// <summary>A verbatim source quote (<c>quotes_fts</c>).</summary>
    Quote = 2,

    /// <summary>The label, a token or a meaning of a vocab item (T-059).</summary>
    Vocab = 4,
}

/// <summary>What a hit of the rule browser is (T-059: vocab items are listed next to the rules).</summary>
public enum KbEntryKind
{
    /// <summary>A KB rule.</summary>
    Rule,

    /// <summary>A vocab item (symbol, abbreviation, banding code).</summary>
    Vocab,
}

/// <summary>One rule or vocab item in the result list of the rule browser.</summary>
/// <param name="Edition">The ISCN edition.</param>
/// <param name="Id">The rule ID, or <c>&lt;vocabulary&gt;/&lt;label&gt;</c> of a vocab item.</param>
/// <param name="Title">The rule title, or the vocab item's first meaning.</param>
/// <param name="Section">The ISCN section (of the vocab file for a vocab item).</param>
/// <param name="Severity">The KB severity (<c>error</c>, <c>warning</c>, <c>info</c>); the entry kind of a vocab item (<c>abbreviation</c>, <c>code</c>, …).</param>
/// <param name="Snippet">The matching text with the match context (the tokens of a vocab item), or <c>null</c>.</param>
/// <param name="Match">Where the query matched.</param>
/// <param name="Kind">Rule or vocab item.</param>
public sealed record RuleHit(string Edition, string Id, string Title, string Section, string Severity, string? Snippet, RuleMatch Match, KbEntryKind Kind = KbEntryKind.Rule);

/// <summary>A valid or invalid KB example of a rule.</summary>
/// <param name="IsValid">Whether it is a valid example.</param>
/// <param name="Text">The example string as printed.</param>
/// <param name="Why">Why it is invalid, if stated.</param>
/// <param name="Corrected">The corrected form, if stated.</param>
/// <param name="Note">A note, if any.</param>
/// <param name="Citation">The rendered citation of the example, if any.</param>
public sealed record RuleExample(bool IsValid, string Text, string? Why, string? Corrected, string? Note, string? Citation);

/// <summary>A verbatim source quote of a rule (optional KB part; removable before a public release, T-043).</summary>
/// <param name="Citation">The rendered citation.</param>
/// <param name="Text">The quote text.</param>
public sealed record RuleQuote(string Citation, string Text);

/// <summary>Everything the KB database holds about one rule in one edition; <c>FixAuto</c> = the KB <c>fix.auto</c> flag (autocorrection, T-097).</summary>
public sealed record RuleDetail(
    string Edition,
    string Id,
    string Title,
    string Section,
    string Severity,
    string Statement,
    string File,
    int? FixLevel,
    string? FixKind,
    string? FixHint,
    bool ParaphraseChecked,
    bool NeedsReview,
    string? ReviewNote,
    ImmutableArray<string> AppliesTo,
    ImmutableArray<string> Contexts,
    ImmutableArray<string> Citations,
    ImmutableArray<string> Related,
    ImmutableArray<RuleExample> Examples,
    ImmutableArray<RuleQuote> Quotes,
    bool FixAuto = false);

/// <summary>
/// Read-only access to <c>build/kb.sqlite</c> (written by <c>kb build</c>; tables and FTS5 indexes in kb/README.md) for the
/// rule browser (T-039), rules and vocab items (T-059, via <see cref="VocabStore"/>), and of the Bands tab (T-084, via <see cref="BandStore"/>). User queries are turned into safe FTS5 queries: every whitespace-separated word becomes a prefix
/// phrase of its letter/digit tokens (<c>t(9;22)</c> → <c>"t 9 22"*</c>), words are ANDed. Quotes are optional: an empty
/// <c>quotes</c> table is a normal KB build without <c>kb/source-quotes/</c>.
/// </summary>
public sealed class RuleStore : IDisposable
{
    /// <summary>The <c>meta.format_version</c> this reader understands.</summary>
    public const int FormatVersion = 2;

    /// <summary>The database file next to the app (copied there by the desktop build).</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "kb", "kb.sqlite");

    private const string _rebuildHint = "Run 'uv run --directory tools kb build' from the repo root, then 'dotnet build'.";
    private const int _maxHits = 500;

    private static int _initialized;
    private readonly SqliteConnection _connection;
    private readonly VocabStore _vocab;
    private readonly BandStore? _bands;

    private readonly bool _hasFixAuto;

    private RuleStore(SqliteConnection connection, ImmutableArray<string> editions)
    {
        _connection = connection;
        _hasFixAuto = HasColumn(connection, "rules", "fix_auto");
        _vocab = new VocabStore(connection);
        _bands = HasTable(connection, "bands") && HasTable(connection, "band_exceptions") ? new BandStore(connection) : null;
        Editions = editions;
    }

    /// <summary>The editions in the database, e.g. <c>[2016]</c>.</summary>
    public ImmutableArray<string> Editions { get; }

    /// <summary>Opens the database at <paramref name="path"/> read-only.</summary>
    /// <exception cref="RuleStoreException">The file is missing, not a KB database, or of another format version.</exception>
    public static RuleStore Open(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new RuleStoreException($"KB database not found: {path}. {_rebuildHint}");
        }

        if (Interlocked.Exchange(ref _initialized, 1) == 0)
        {
            SQLitePCL.Batteries_V2.Init();
        }

        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        var connection = new SqliteConnection(builder.ConnectionString);
        try
        {
            connection.Open();
            var meta = ReadMeta(connection);
            if (!meta.TryGetValue("format_version", out var format) || format != FormatVersion.ToString(CultureInfo.InvariantCulture))
            {
                throw new RuleStoreException($"KB database {path} has format version {format ?? "?"}, expected {FormatVersion}. {_rebuildHint}");
            }

            var editions = meta.TryGetValue("editions", out var list)
                ? [.. list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
                : ImmutableArray<string>.Empty;
            return new RuleStore(connection, editions);
        }
        catch (SqliteException ex)
        {
            connection.Dispose();
            throw new RuleStoreException($"KB database {path} could not be read: {ex.Message} {_rebuildHint}", ex);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>The sections that state rules or print a vocab list in <paramref name="edition"/>, in book order (<c>2.1</c> before <c>9.2.10</c>).</summary>
    public ImmutableArray<string> Sections(string edition)
    {
        using var cmd = Command("SELECT DISTINCT section FROM rules WHERE version = $v", ("$v", edition));
        var sections = new List<string>();
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                sections.Add(r.GetString(0));
            }
        }

        sections.AddRange(_vocab.Sections(edition).Where(s => s.Length > 0 && !sections.Contains(s)));
        sections.Sort(CompareSections);
        return [.. sections];
    }

    /// <summary>
    /// The rules and vocab items of <paramref name="edition"/> (optionally of one <paramref name="section"/>) that match
    /// <paramref name="query"/>: a vocab item whose label or token equals the query and an exact rule ID first, then the
    /// rules by relevance, then the other vocab items. A blank query (or one without letters or digits) lists every rule
    /// in book order, then every vocab item (a vocab item named exactly by a symbol-only query first). Bands are not listed
    /// here: they have their own tab (<see cref="Bands"/>, T-084).
    /// </summary>
    public ImmutableArray<RuleHit> Search(string edition, string? query, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(edition);
        var fts = ToFtsQuery(query);
        if (fts is null)
        {
            // A symbol-only query (→, ::, ;) has no FTS words but can still name a vocab item exactly: list that first.
            var exactSymbols = string.IsNullOrWhiteSpace(query) ? [] : _vocab.Search(edition, query, section).Exact;
            return
            [
                .. exactSymbols.Select(r => r.ToHit(edition, RuleMatch.Vocab)),
                .. ListAll(edition, section),
                .. _vocab.Rows(edition, section).Where(r => !exactSymbols.Contains(r)).Select(r => r.ToHit(edition, RuleMatch.None)),
            ];
        }

        var (exactVocab, otherVocab) = _vocab.Search(edition, query!, section);
        var rules = SearchRules(edition, query!, fts, section);
        return
        [
            .. exactVocab.Select(r => r.ToHit(edition, RuleMatch.Vocab))
                .Concat(rules)
                .Concat(otherVocab.Select(r => r.ToHit(edition, RuleMatch.Vocab)))
                .Take(_maxHits),
        ];
    }

    /// <summary>Every band of <paramref name="edition"/> in band order (pter → qter per chromosome); empty for a database written before T-084.</summary>
    public ImmutableArray<CytoLex.Core.Kb.Band> Bands(string edition)
    {
        ArgumentNullException.ThrowIfNull(edition);
        return _bands?.Rows(edition) ?? [];
    }

    /// <summary>The band <paramref name="name"/> of <paramref name="edition"/> (exact name), or <c>null</c>.</summary>
    public BandDetail? Band(string edition, string name)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(name);
        return _bands?.Detail(edition, name);
    }

    /// <summary>The vocab item <paramref name="label"/> of <paramref name="vocabulary"/> in <paramref name="edition"/>, or <c>null</c>.</summary>
    public VocabDetail? Vocab(string edition, string vocabulary, string label)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(label);
        return _vocab.Detail(edition, vocabulary, label);
    }

    private ImmutableArray<RuleHit> SearchRules(string edition, string query, string fts, string? section)
    {
        // Relevance: bm25 (smaller = better) of the rule text, else of the best quote.
        var hits = new Dictionary<string, (double Score, string? Snippet, RuleMatch Match)>(StringComparer.Ordinal);
        using (var cmd = Command(
            "SELECT id, bm25(rules_fts, 0, 10.0, 5.0, 1.0, 2.0), snippet(rules_fts, -1, '', '', '…', 12) "
            + "FROM rules_fts WHERE rules_fts MATCH $q AND version = $v",
            ("$q", fts),
            ("$v", edition)))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                hits[r.GetString(0)] = (r.GetDouble(1), Clean(r.GetString(2)), RuleMatch.Rule);
            }
        }

        using (var cmd = Command(
            "SELECT rule_id, bm25(quotes_fts), snippet(quotes_fts, 2, '', '', '…', 12) "
            + "FROM quotes_fts WHERE quotes_fts MATCH $q AND version = $v",
            ("$q", "{text} : (" + fts + ")"),
            ("$v", edition)))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var id = r.GetString(0);
                var score = r.GetDouble(1);
                var snippet = Clean(r.GetString(2));
                if (hits.TryGetValue(id, out var hit))
                {
                    hits[id] = (Math.Min(hit.Score, score), hit.Snippet, hit.Match | RuleMatch.Quote);
                }
                else
                {
                    hits[id] = (score, snippet, RuleMatch.Quote);
                }
            }
        }

        if (hits.Count == 0)
        {
            return [];
        }

        var rules = RuleRows(edition, section);
        var exact = query.Trim();
        return
        [
            .. hits
                .Where(h => rules.ContainsKey(h.Key))
                .OrderBy(h => string.Equals(h.Key, exact, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(h => h.Value.Score)
                .ThenBy(h => h.Key, StringComparer.Ordinal)
                .Take(_maxHits)
                .Select(h =>
                {
                    var (title, sec, severity) = rules[h.Key];
                    return new RuleHit(edition, h.Key, title, sec, severity, h.Value.Snippet, h.Value.Match);
                }),
        ];
    }

    /// <summary>The rule <paramref name="id"/> of <paramref name="edition"/>, or <c>null</c> if the edition has no such rule.</summary>
    public RuleDetail? Rule(string edition, string id)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(id);
        using var cmd = Command(
            "SELECT title, section, severity, statement, file, fix_level, fix_kind, fix_hint, paraphrase_checked, needs_review, review_note, "
            + (_hasFixAuto ? "fix_auto" : "0") + " FROM rules WHERE version = $v AND id = $id",
            ("$v", edition),
            ("$id", id));
        using var r = cmd.ExecuteReader();
        if (!r.Read())
        {
            return null;
        }

        return new RuleDetail(
            edition,
            id,
            Title: r.GetString(0),
            Section: r.GetString(1),
            Severity: r.GetString(2),
            Statement: r.GetString(3),
            File: r.GetString(4),
            FixLevel: r.IsDBNull(5) ? null : r.GetInt32(5),
            FixKind: NullableString(r, 6),
            FixHint: NullableString(r, 7),
            ParaphraseChecked: r.GetInt32(8) != 0,
            NeedsReview: r.GetInt32(9) != 0,
            ReviewNote: NullableString(r, 10),
            AppliesTo: Strings("SELECT type FROM rule_applies_to WHERE version = $v AND rule_id = $id", edition, id),
            Contexts: Strings("SELECT context FROM rule_context WHERE version = $v AND rule_id = $id", edition, id),
            Citations: Strings("SELECT rendered FROM citations WHERE version = $v AND rule_id = $id ORDER BY ord", edition, id),
            Related: Strings("SELECT related_id FROM rule_related WHERE version = $v AND rule_id = $id", edition, id),
            Examples: Examples(edition, id),
            Quotes: Quotes(edition, id),
            FixAuto: r.GetInt32(11) != 0);
    }

    // A database written before T-084 has no band tables: the browser then lists no bands.
    private static bool HasTable(SqliteConnection connection, string table)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = $t";
        cmd.Parameters.AddWithValue("$t", table);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    // A database written before T-097 has no fix_auto column: read it as "not flagged" rather than failing.
    private static bool HasColumn(SqliteConnection connection, string table, string column)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT count(*) FROM pragma_table_info('{table}') WHERE name = $c";
        cmd.Parameters.AddWithValue("$c", column);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    /// <inheritdoc/>
    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// The FTS5 query for a user query, or <c>null</c> when it has no letters or digits. Each whitespace-separated word
    /// becomes one quoted prefix phrase of its tokens, so no user text is read as FTS5 syntax.
    /// </summary>
    public static string? ToFtsQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var phrases = new List<string>();
        foreach (var word in query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var tokens = Tokens(word);
            if (tokens.Count > 0)
            {
                phrases.Add("\"" + string.Join(' ', tokens) + "\"*");
            }
        }

        return phrases.Count == 0 ? null : string.Join(" AND ", phrases);
    }

    /// <summary>Orders section numbers numerically per part: <c>2.1</c> &lt; <c>2.10</c> &lt; <c>9.2.10</c>.</summary>
    public static int CompareSections(string? a, string? b)
    {
        var pa = (a ?? string.Empty).Split('.');
        var pb = (b ?? string.Empty).Split('.');
        for (var i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            var c = int.TryParse(pa[i], CultureInfo.InvariantCulture, out var x) && int.TryParse(pb[i], CultureInfo.InvariantCulture, out var y)
                ? x.CompareTo(y)
                : string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0)
            {
                return c;
            }
        }

        return pa.Length.CompareTo(pb.Length);
    }

    // Letter/digit runs, like FTS5's unicode61 tokenizer (which also splits on '.', '_', '(' …).
    internal static List<string> Tokens(string word)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in word)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static string? Clean(string snippet)
    {
        var text = string.Join(' ', snippet.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length == 0 ? null : text;
    }

    private static string? NullableString(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private static Dictionary<string, string> ReadMeta(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM meta";
        using var r = cmd.ExecuteReader();
        var meta = new Dictionary<string, string>(StringComparer.Ordinal);
        while (r.Read())
        {
            meta[r.GetString(0)] = r.GetString(1);
        }

        return meta;
    }

    private SqliteCommand Command(string sql, params (string Name, object Value)[] parameters)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        return cmd;
    }

    private ImmutableArray<RuleHit> ListAll(string edition, string? section) =>
    [
        .. RuleRows(edition, section)
            .OrderBy(r => r.Value.Section, Comparer<string>.Create(CompareSections))
            .ThenBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => new RuleHit(edition, r.Key, r.Value.Title, r.Value.Section, r.Value.Severity, null, RuleMatch.None)),
    ];

    private Dictionary<string, (string Title, string Section, string Severity)> RuleRows(string edition, string? section)
    {
        using var cmd = section is null
            ? Command("SELECT id, title, section, severity FROM rules WHERE version = $v", ("$v", edition))
            : Command("SELECT id, title, section, severity FROM rules WHERE version = $v AND section = $s", ("$v", edition), ("$s", section));
        using var r = cmd.ExecuteReader();
        var rows = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal);
        while (r.Read())
        {
            rows[r.GetString(0)] = (r.GetString(1), r.GetString(2), r.GetString(3));
        }

        return rows;
    }

    private ImmutableArray<string> Strings(string sql, string edition, string id)
    {
        using var cmd = Command(sql, ("$v", edition), ("$id", id));
        using var r = cmd.ExecuteReader();
        var list = ImmutableArray.CreateBuilder<string>();
        while (r.Read())
        {
            if (!r.IsDBNull(0))
            {
                list.Add(r.GetString(0));
            }
        }

        return list.ToImmutable();
    }

    private ImmutableArray<RuleExample> Examples(string edition, string id)
    {
        using var cmd = Command(
            "SELECT kind, text, why, corrected, note, cite_rendered FROM examples WHERE version = $v AND rule_id = $id "
            + "ORDER BY CASE kind WHEN 'valid' THEN 0 ELSE 1 END, ord",
            ("$v", edition),
            ("$id", id));
        using var r = cmd.ExecuteReader();
        var list = ImmutableArray.CreateBuilder<RuleExample>();
        while (r.Read())
        {
            list.Add(new RuleExample(r.GetString(0) == "valid", r.GetString(1), NullableString(r, 2), NullableString(r, 3), NullableString(r, 4), NullableString(r, 5)));
        }

        return list.ToImmutable();
    }

    private ImmutableArray<RuleQuote> Quotes(string edition, string id)
    {
        using var cmd = Command("SELECT rendered, text FROM quotes WHERE version = $v AND rule_id = $id ORDER BY ord", ("$v", edition), ("$id", id));
        using var r = cmd.ExecuteReader();
        var list = ImmutableArray.CreateBuilder<RuleQuote>();
        while (r.Read())
        {
            list.Add(new RuleQuote(r.GetString(0), r.GetString(1)));
        }

        return list.ToImmutable();
    }
}
