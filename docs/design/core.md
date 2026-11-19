# CytoLex core library — design

Task T-023 · status: **draft for approval** · 2026-11-18

This document designs the UI-free core library (`CytoLex.Core`, C#/.NET 10, T-020). It fixes the data model, version profiles, the result and diagnostic API, the fix model and type detection, for **every** notation type; the pilot implements karyotypes of ISCN 2016 ch. 2–9 (T-021). Decisions it builds on: `docs/decisions.md` → Version-aware validation, Pilot scope (T-021), Strictness & severity (T-022), Fix suggestions, Literal translation (T-054), T-020 log entry.

Code blocks are **illustrative sketches** (names and shapes), not final code; T-024+ may refine signatures without changing the design.

## 1. Principles
1. **KB-traced.** Every diagnostic, and every meaning node, names a KB entry (rule ID or vocab item). Severity, citation, review state and context come from the KB at runtime, never from C# constants. A check with no KB rule is a KB gap: add the rule (with citation) first.
2. **Never stop at the first error.** The parser recovers and keeps going; every rule runs on what was understood.
3. **Designed for all types, implements a subset.** Segments of a type (or an edition) without an implementation return `NotImplemented`, never a guess.
4. **Per edition.** Each ISCN edition is judged on its own; results never mix editions.
5. **Immutable and thread-safe.** Trees, KB objects and results are immutable; one `Validator` instance serves concurrent calls (batches, web service).
6. **Spans over the original text.** All positions are UTF-16 offsets into the exact input string, so the editor can highlight and fixes can edit without re-serialising.

## 2. Pipeline

```
input string
   │
   ▼
Segmenter ──► Segment[] (span, detected type, NotImplemented?)            §3
   │                         ContextInference ──► Inferred context        §3.3
   ▼   (per edition profile, per implemented segment)
Parser (lexer + recursive descent) ──► SyntaxTree (lossless)              §4
   ▼
Binder ──► SemanticModel (meaning nodes, KbRefs, IsPartial)               §5
   ▼
RuleEngine (KB rules of that edition) ──► Diagnostic[] (+ Fix[])          §7–§8, §10
   ▼
EditionResult (validity, counts, diagnostics, fixes, [translation])      §9
   ▼
ValidationReport (one or all editions)
```

The parser emits syntax diagnostics; the binder interprets without reporting (T-065: an unknown band or a group without a chromosome is a fact in the model, reported by a rule); the rule engine runs the rule implementations. All three use the same `Diagnostic` type and the same KB-driven severity resolution (§8.2).

## 3. Segments, type detection, context inference (T-026)

### 3.1 Splitting
- A string is a list of **segments** separated by `.` at the **top level** only. `.` inside brackets (`(q21.1)`), inside a band (`9q34.11`), or as a decimal point is not a separator; the lexer knows these positions (`.` is a `shared_token` in the ch. 3 vocabulary: decimal point vs period).
- Splitting is purely lexical and edition-independent; it never fails. Each segment keeps its span and the separator token.
- **Exact rule (T-026):** a `.` separates segments when it is outside every `( )`, `[ ]`, `{ }` (stray closers clamp the depth at 0) **and** the next non-whitespace character is a letter. This splits `46,XX.ish …`, `…×1.rsa …`, `…[10/30].arr …` and keeps `9q34.11`, `1p36.3.3`, `[0.6]`, `NC_000008.10` and seq's HGVS `g.1287…`/`g.[…]` inside their segment. Segments + separators rebuild the input exactly; an empty input is one empty karyotype segment. Code: `CytoLex.Core.Segmentation.Segmenter`.

### 3.2 Type detection
- Each segment gets a `NotationType` (the KB `applies_to` vocabulary: `karyotype`, `breakage`, `meiotic`, `ish`, `nuc_ish`, `fib_ish`, `rev_ish`, `cgh`, `arr`, `rsa`, `seq`).
- Detection is by the segment's leading keyword (`ish`, `nuc ish`, `fib ish`, `rev ish`, `cgh`, `arr`, `rsa`, `seq`, …, from the vocab) — otherwise `karyotype`. As built (T-026): after leading whitespace, longest keyword first, case-sensitive, not followed by a letter; two-word keywords accept any spacing (`nucish` is still `nuc_ish`, spacing is a parser rule). Each keyword word is a ch. 3 symbol; the segment carries their refs (`vocab:symbols/nuc`, `vocab:symbols/ish`) and a test checks them against the embedded vocab. Whether a type is `NotImplemented` is decided by the edition profile (T-028); construct-level detection inside a karyotype segment needs tokens (T-027). Breakage and meiotic notation are recognised by their markers inside a karyotype-shaped segment (T-018 adds the markers).
- A segment whose type (or construct) has no implementation in the chosen edition becomes a `NotImplemented` segment result with the reason (`"ish: not implemented in the pilot"`). The rest of the string is still validated.
- Pilot constructs detected but `NotImplemented` inside a karyotype segment: neoplastic clone syntax (`idem`, `sl`, `sdl`, `cp`), ch. 10/12 markers, underlined homologues (U+0332, T-042). The segment result says which construct and where (span). `//` is **not** among them (decided in T-027): it is the ch. 4 transplant-chimera separator (`KARYO.TRANSPLANT_CHIMERA`) and parsed as a normal cell-line separator.

### 3.3 Context inference
- `SampleContext` = `Constitutional | Acquired`. KB `context: neoplastic` maps to `Acquired`; KB `meiotic` is a notation type, not a sample context, and is ignored here.
- Inferred **only from clear markers** (T-021): `idem`/`sl`/`sdl`, `c`, or a ploidy `<Nn>` → `Acquired` (a clone structure was dropped in T-028, see below); `rec` → `Constitutional`. Anything else → `Unknown`. Contradicting markers → `Unknown` plus an info diagnostic naming both markers.
- An explicit `ContextHint` overrides inference. The effective context per string is `Constitutional`, `Acquired` or `Unknown`; it drives conditional diagnostics (§8.3).
- The result reports the inferred context and the marker span(s) that decided it.
- **T-028 change (decided with the user):** cell lines with `[n]` counts (`/`, `//`) are **no longer** an acquired marker — the book writes constitutional mosaics that way without `mos` (§4.1 ¶9: `45,X[25]/46,X,i(X)(q10)[25]`), so the shape is not a clear marker. Such strings have an unknown context and context-limited rules report conditionally; a hint or a clear marker makes them firm. The T-026 note below is kept for history.
- **As built (T-026, lexical scan, decided with the user):** words are maximal ASCII letter runs matched exactly (`cen`, `cp`, `idic` never count as `c`). Acquired: `idem`/`sl`/`sdl`, `c`, `<…n…>`, and a **clone structure** = a top-level `/` or `//` in a karyotype segment with a `[n]` cell count and no `mos`/`chi` word (constitutional mosaics and chimeras are not clone markers). Constitutional: `rec`. Only karyotype, ish-family and cgh segments are scanned (arr/rsa/seq skipped: HGVS `c.`, probe and gene names). Contradiction → `ContextInfo.IsContradictory` with all markers; the info diagnostic is emitted by the engine (T-028). Code: `CytoLex.Core.Context.ContextInference`; the parser (T-027) may back the same API with tokens.

## 4. Syntax layer (lossless)

```csharp
readonly record struct TextSpan(int Start, int Length);

sealed record SyntaxToken(TokenKind Kind, TextSpan Span, string Text,
    ImmutableArray<Trivia> Leading, ImmutableArray<Trivia> Trailing,
    bool IsMissing,            // zero-width, inserted by recovery; Text = expected text
    string? VocabRef);         // "vocab:symbols/del" when the token is a listed symbol

abstract record SyntaxNode(SyntaxKind Kind, TextSpan Span, ImmutableArray<SyntaxElement> Children);
// Trivia: Whitespace, Skipped (unparseable text kept as one trivia element with its span)
```

- **Lossless:** concatenating all tokens with their trivia gives back the input exactly. That is what makes spacing rules (`SYM.NO_SPACE_AT_PARENTHESIS`, `SYM.SPACE_BEFORE_CHROMOSOME_COUNT`, `SYM.SPACE_BETWEEN_SYMBOLS`) checkable and level-1 fixes exact.
- **Tokens** come from the edition's vocabularies (symbols, banding codes) plus lexical classes: integer, chromosome id, arm, band, `[n]`, punctuation (`, ; ( ) [ ] < > / . : ~ ? + – ×`, `→`). Printed forms are canonical; ASCII `-`/`x`/`->` are lexed as their own token kinds so T-053 can decide (equivalent / error + fix) without lexer changes.
- **Node kinds** cover every type, even where the pilot only produces a placeholder: `Karyotype`, `CellLine`, `CellCount` (`[n]`), `CellLineSeparator` (`/`, `//`), `Prefix` (`mos`, `chi`), `ChromosomeCount` (incl. ranges `45~48`, ploidy `<2n>`), `SexChromosomes`, `Abnormality` (prefix sign, symbol, chromosome list, breakpoint list, suffixes `×n`, `c`, inheritance), `DerivativeBody`, `BreakpointList` (short and detailed system, `→`, `::`, `:`), `Band`, `Uncertainty` (`?`, `~`, `or`), `CloneRef` (idem/sl/sdl), `IshSegment`/`ArrSegment`/… (opaque until their parsers exist).
- **Parser:** hand-written lexer + recursive descent (T-020). Recovery: on an unexpected token, insert a missing token when exactly one is expected (e.g. the missing `,` in `46XX`) or skip to the next sync point (`,`, `/`, `)`, `]`, `.`) and wrap the skipped text as `Skipped` trivia. Each recovery records **which KB rule defines the expected form** (e.g. `KARYO.COUNT_SEX_ORDER` for the missing comma, `DEL.FORMAT` for `del(5q)`); see §8.4.
- The parser is configured by the edition profile (vocab, feature flags, §7.1) but is a single code base.
- **As built (T-027):** `CytoLex.Core.Syntax` — `Lexer`, `Parser` (`ParseKaryotype`, `ParseFragment`, `Parser.ForEdition`), `SyntaxTree` (root, `SyntaxDiagnostic`s, `NotImplementedConstruct`s), `SyntaxNode`/`SyntaxToken`; vocab from the bundle via `CytoLex.Core.Kb.Vocabulary`.
  - **Lexer:** symbols come from the edition's vocab. A letter run is split into listed symbols and `X`/`Y` with the fewest pieces (`psudic` → `psu` `dic`, `mardn` → `mar` `dn`, `XXYc` → `X X Y c`); the pieces are adjacent tokens without trivia, so the spacing rules still see the missing space. An unsplittable run is an `UnknownWord` (`dl`, `iso`). Typed forms keep their text with `IsTypedForm`. U+0332 stays in its token. Whitespace is trailing trivia (leading only on the first token); a zero-width `EndOfSegment` token closes every segment.
  - **Fragment entry:** `ParseFragment` is the same grammar with the count and sex chromosomes optional, for book fragments (`inv(2)`, `1p31`, `16qh+`, `psu dic`, `(pter→q22::…)`); the harness's `isFragment` picks it (T-028).
  - **Permissive where the tree can hold it:** symbol combinations (`der dic`, `min mar`), group counts (`t(2;5)(q21)`), arms in a chromosome list (`del(5q)`; needed for `rec(2)dup(2p)…`), order, counts, context, spacing and band existence are left to the rules. The parser reports surface errors only: missing comma (`46XX`), swapped header (`XX,46`), chromosome without parentheses (`del4(p14)`), `,` between chromosomes, `21+`, `5q–`, `16hq+`, unlisted words, `P`/`Q` arms, a second decimal point or a comma as decimal point, misplaced `mos`/`chi`/`end`/`mat`, `(15)` cell counts, `,` between cell lines, `X2`, `×2(…)`, ploidy outside `< >`, `dmin14`, `upd15`, a trailing `?`. Each names its KB rule (`Parser.RuleIds`); anything else is `CORE.UNPARSED`, which no KB example triggers.
  - **Deviations from the sketch above:** skipped text is a `Skipped` node in the tree (not trivia), equally lossless; `SyntaxDiagnostic` (rule, span, kind missing/unexpected/skipped, expected text, message) is the parser's raw finding, mapped to `Diagnostic` by T-028. The binder (§5) is T-065.

## 5. Semantic layer (meaning)

The binder walks the syntax tree and builds meaning nodes. Rules and the translation renderer (T-058) work on this layer; they go back to syntax only for spans and spacing.

```csharp
abstract record MeaningNode(TextSpan Span, ImmutableArray<KbRef> Kb, bool IsPartial);

sealed record KaryotypeSegment(..., ImmutableArray<CellLine> CellLines, CellLineRelation Relation /* none|mos|chi|transplant */);
sealed record CellLine(..., ChromosomeCount Count, PloidyLevel? Ploidy, SexComplement Sex,
    ImmutableArray<Abnormality> Abnormalities, int? CellCount /* [n] */,
    CloneRef? BasedOn /* idem/sl/sdl, T-032 */, bool IsConstitutionalMarked);
sealed record Abnormality(..., AbnormalityKind Kind /* gain, loss, del, dup, inv, t, der, dic, i, r, mar, … */,
    ImmutableArray<ChromosomeRef> Chromosomes, ImmutableArray<Breakpoint> Breakpoints,
    DerivativeStructure? Structure, Copies? Copies /* ×n */, Inheritance? Inheritance /* mat/pat/inh/dn */,
    bool ConstitutionalC, Uncertainty Uncertainty);
sealed record Breakpoint(..., ChromosomeRef Chromosome, Arm? Arm, BandName? Band, Terminal? Terminal /* pter/qter/cen */,
    Uncertainty Uncertainty /* ?, ~, or-alternatives */);
sealed record DerivativeStructure(ImmutableArray<SegmentPiece> Pieces); // ordered, each with orientation (from→to)
```

- **Meaning kept** (T-054): event type, chromosome(s), arms, breakpoints/bands, derivative structure (ordered pieces with orientation), sex-chromosome field, count/ploidy, cell lines and their `[n]`, `/`, mos/chi, room for idem/sl/sdl, uncertainty (`?`, `~`, `or`), inheritance (mat/pat/inh/dn), constitutional `c`.
- **`KbRef`** — a typed reference to a KB entry, stable across the app and exports:
  - `rule:DEL.FORMAT` — a rule ID;
  - `vocab:symbols/del` — a vocab item: vocabulary key + entry label (vocab entries have no ID of their own; `<key>/<label>` is unique per edition, which `kb build` already checks via labels);
  - `band:5q13` — a band from `kb/bands/<version>/bands.yaml`.
  Every meaning node carries the refs that define it (the abnormality's format rule, the symbol's vocab item, each band).
- **Partial nodes:** a node built from input with recovered or skipped parts has `IsPartial = true` and the IDs of the diagnostics concerned; rules that need complete data skip it (and say so only if that hides a check), and the renderer prints "not translated, see diagnostic …".
- **Normalisation for rules:** the binder resolves short vs detailed system into the same `Breakpoint`/`DerivativeStructure` shape, so rules don't care which system was written (system-specific rules use the syntax layer).
- **As built (T-065, decided with the user):** `CytoLex.Core.Semantics` — `Binder(IEditionKb)`, `Bind(SyntaxTree)` → `KaryotypeModel`; `KbRef` (`rule:`/`vocab:`/`band:`, `Parse`/`ToString`); meaning nodes in `MeaningNodes.cs`. The validator binds every karyotype segment and checks read `RuleContext.Model`.
  - **No binder diagnostics.** The binder records facts (band found in the band set or not → `Breakpoint.KbBand`; group without a chromosome → `BreakpointGroup.Chromosome = null`); the rules (T-029+) report them.
  - **Breakpoints, both systems:** short system = each band/`?` of a group, group *i* → chromosome *i* of the part's list (the only chromosome when there is one); detailed system = the points at the joins `::`/`:`, minus arm ends (`pter`/`qter`/`ter`) and inserted `hsr`/`neo`, distinct in written order. The breakpoint *sets* of the book's short/detailed pairs are equal (tested). **`DerivativeStructure` is kept as written only** (detailed system, band-composition fragments); computing it from the short system is T-066.
  - **Deviations from the sketch:** `Abnormality` is a list of `AbnormalityPart`s (a `der`/`rec`/`ider` part, then one part per rearrangement); `Kind` = the head (last) symbol of the first part, `Symbols` keep combinations (`psu dic`, `trc r`, `upd der`); breakpoints live in `BreakpointGroup`s (one per `;`), each with an optional `Structure`; uncertainty is on the nodes (`IsQuestioned`, `Breakpoint.RangeEnd` for `~`, `Breakpoint.Alternatives` and `AlternativeItem` for `or`, `NumberRange.IsRange`); further item kinds `WholeChromosomeChange`, `ChromosomeRegion` (variants, bands), `OmittedItems` (`…`), `BandComposition`, `UnboundItem` (skipped/misplaced text); `CellLine.SeparatorBefore` and `KaryotypeModel.Relation` (`//` → transplant, else `mos`/`chi`) instead of one relation field; `BasedOn`/`CloneRef` wait for T-032.
  - **Partial:** `MeaningNode.Issues` holds the `SyntaxDiagnostic`s themselves (no diagnostic IDs exist): those whose missing token lies in the node, or whose span overlaps it; ancestors inherit them, the root has all, a recovered separator marks the following cell line.
  - **KbRefs:** each symbol's vocab item plus its format rule(s) from `Binder.SymbolRules` (each rule's section is one of the symbol's vocab sections — tested); resolved bands; construct rules (`KARYO.MOSAIC_CHIMERA_PREFIX`, `KARYO.TRANSPLANT_CHIMERA`, `STRUCT.DETAILED_SYSTEM`, `STRUCT.BREAKPOINT_GROUPS`, `UNCERTAIN.*`, `KARYO.CONSTITUTIONAL_C`, `KARYO.PARENTAL_ORIGIN`, `VAR.*`, …), attached only when the edition's KB has them.

## 6. KB at runtime

**Decision (this task, with the user):** the validator reads a **lean runtime bundle** into memory, behind an interface; the bundle is **embedded in `CytoLex.Core`**; the rule browser and search use `build/kb.sqlite`.

```csharp
interface IKnowledgeBase { IReadOnlyList<Edition> Editions { get; } IEditionKb? ForEdition(Edition e); }
interface IEditionKb {
    Edition Edition { get; }
    KbRule? Rule(string id);                 // severity, title, statement, citations (rendered), applies_to,
                                             // context, fix {level, kind, hint}, review {needs_review, note}
    IVocabulary Vocab { get; }               // tokens → vocab items (kind, pair, meanings)
    IBandSet Bands { get; }                  // name → levels, book flag, GRCh37/38 coords (nullable)
}
static class KnowledgeBase { IKnowledgeBase LoadEmbedded(); IKnowledgeBase Load(Stream bundle); }
```

- **Bundle content:** per edition, the rules' metadata (no quotes, no example lists), vocabularies, band names with levels/`book` flag/coordinates; later translation templates (T-057). Produced by `kb build` as `build/kb-runtime.json` (T-060; format in `kb/README.md`, its own `format_version` = 1); `format_version` checked at load (mismatch → clear exception).
- **Embedding:** `CytoLex.Core.csproj` embeds the bundle from `build/` as a resource. If it is missing, `dotnet build` fails with "run `uv run --directory tools kb build`". As built in T-024/T-060: MSBuild property `CytoLexKbBundle` (default `build/kb-runtime.json`), resource name `CytoLex.Kb.bundle.json`, read via `CytoLex.Core.Kb.EmbeddedBundle.Open()`. Desktop, tests and web service therefore always carry the KB the code was built with.
- **Override:** `Load(Stream)` lets tests and developers use another bundle (e.g. a fixture KB).
- **Why not a database for the validator:** the engine runs every rule of an edition on every string, so it needs the whole edition in memory anyway; dictionary lookups beat queries, and a few MB of JSON load in tens of ms. Size growth goes mainly into quotes and examples, which stay out of the bundle. Because the core only sees `IKnowledgeBase`, the format can change later (SQLite, binary) in one loader class.
- **SQLite (`build/kb.sqlite`, FTS5)** stays the store for the rule browser (T-039): search, quotes, examples. The desktop app reads it via `Microsoft.Data.Sqlite`; the core does not depend on it.
- **KB examples for tests** (T-025) are read from `build/kb.json` by the test project, not from the runtime bundle.

## 7. Version profiles and the rule engine (T-028)

### 7.1 Edition profile
```csharp
sealed record EditionProfile(Edition Edition, IEditionKb Kb, ParserFeatures Features,
    ImmutableHashSet<NotationType> ImplementedTypes, IReadOnlyList<IRuleCheck> Checks);
```
- One parser for all editions, configured by the profile: vocabulary (which symbols exist), feature flags for syntax that differs between editions, and the implemented notation types.
- The pilot has one profile (2016). An edition in the KB without a profile, or a requested edition without KB data, yields `EditionResult.Status = NotImplemented` (e.g. 2020/2024 until T-045/T-046).
- **Parse sharing:** the parser records which profile inputs (vocab tokens, features) it actually consulted; editions whose profiles agree on all of them share one syntax tree and semantic model. This is an optimisation only; correctness never depends on it.

### 7.2 Rule implementations bound to KB IDs
```csharp
[KbRule("STRUCT.CHROMOSOME_ORDER")]
sealed class ChromosomeOrderCheck : IRuleCheck {
    public void Check(RuleContext ctx, KaryotypeSegment seg) { … ctx.Report(span, args, fix?, certainty?) … }
}
```
- A check reports **only the facts** (span, message arguments, proposed text edits, whether it is certain). Severity, citation, title, `needs_review` cap and context come from `ctx.Rule` (the KB entry) in §8.2.
- A check runs for an edition **only if that edition's KB contains the rule ID**. Edition-specific behaviour: an attribute `[KbRule("X", Editions = "2020+")]` on a variant class; the engine picks the most specific one per edition.
- **Startup checks** (in tests and debug builds; a cheap one at load): every `[KbRule]` ID exists in at least one edition's KB (otherwise fail). A **coverage list** (KB rules with no implementation, implementations per edition) is exposed for the T-025 report.
- `applies_to` filters which segments a rule sees; rules with `general` run on every implemented type.
- Rules with KB `context` run in every context; the engine turns their findings into conditional diagnostics when the effective context is unknown (§8.3), and drops them when the effective context excludes them.

- **As built (T-028):**
  - Edition IDs are strings (`"2016"`), as in `Parser.ForEdition` and `[KbRule(Editions = …)]`. KB: `CytoLex.Core.Kb.KnowledgeBase` (`LoadEmbedded`, `Load(Stream)`) → `IKnowledgeBase.ForEdition` (renamed from `Get`, analyzer CA1716) → `IEditionKb` (`Rules`, `Rule(id)` → `KbRule` with rendered `Citation`s, `Vocab`, `BandSet? Bands`).
  - Engine: `CytoLex.Core.Rules` — `IRuleCheck.Check(RuleContext)` (the context exposes input, segment, syntax tree, `ContextInfo`, `KbRule`, edition KB; `Report(span, detail, secondarySpans, certainty, contexts, cannotVerify)`), `RuleCatalog` (scan, startup check incl. `Parser.RuleIds`, most specific variant: exact edition > range > any; equally specific → exception), `EditionProfile` / `EditionProfiles` (pilot: 2016 → karyotype), `EditionCoverage` (checked rule IDs + parser-reported rule IDs, `Validator.Coverage(edition)`).
  - Parse sharing is not implemented (optimisation only). The semantic model is on `RuleContext.Model` (T-065).
- **As built (T-029):** checks live in `src/CytoLex.Core/Rules/Checks/`: one `internal sealed` class per rule, named `<Rule>Check`, with shared model/token queries in `CheckHelpers` (`Breakpoints()`, `Chromosomes()`, `IsValidIdentifier()`, `BandSpan()`, `PresentTokens()`). A rule the parser already reports gets no duplicate check (`BAND.ARM_SYMBOL`, `BAND.SUBBAND_FORMAT`, `SYM.LISTED_ONLY`, `SYM.TYPED_SYMBOL_FORMS`). Spacing rules read token trivia from the syntax tree or compare node spans of the model. `BandSet.ContainsRegion` supports region-only breakpoints (`1p1`).
- **As built (T-030):** 16 checks for count, sex field and order, one class per rule. Two shared analyses each serve a group of rules, so one fault gives one finding:
  - **`ChromosomeCount`** (per cell line): reference level k from `<Nn>` or the count (≤34 → 1, else ⌊(c+11)/23⌋, §8.1 ¶2, §11.2 ¶2); expected = 22k + sex-field letters + item deltas (gain/loss ±1 × copies; a lost X/Y is already out of a field without `c`; unsigned rearrangements add products − replaced autosomes, `t`/`ins`/`tas` one product per chromosome, every other kind one, so `dic`/`rob`/`der(A;B)` −1 and a rearranged X/Y +1; `dmin` 0). Lines with `…`, `inc`, `idem`/`sl`/`sdl`, `[cp]`, partial input or items of unknown effect (unsigned `mar`/`r`/`fis`) are skipped; count ranges, two-level ranges, `?` chromosomes in a rearrangement and leading-count ranges are uncertain. Verdict, first match wins: fits; fits at another level → `NUM.PLOIDY_REFERENCE`; fits with the rearranged X/Y in the field → `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST`; fits without losses of replaced chromosomes → `KARYO.REPLACED_NORMAL_NOT_MISSING`; fits with a lost X/Y still in the field → left to the sex-field rules; else `NUM.COUNT_MATCHES_CHANGES` (uncertain → warning; the specific verdicts only when certain).
  - **`ItemOrder`** (per cell line): key = (category identified < `der(?)` < `r` < `mar` < `dmin`; chromosome X < Y < 1…22, the derivative's own chromosome for `der`/`rec`/`ider`, else the item's first chromosome; numerical before structural; first symbol word, alphabetically). An item whose key is below an earlier item's is reported once, against the earlier item with the greatest key, by the rule of the first differing key part (`ORDER.UNKNOWN_CENTROMERE_DER`/`ORDER.UNIDENTIFIED_LAST`, `ORDER.X_BEFORE_Y`/`KARYO.ABNORMALITY_ORDER`, `ORDER.NUMERICAL_BEFORE_STRUCTURAL`, `ORDER.HOMOLOGUES_ALPHABETICAL`). Items without a key (`or`, `…`, `inc`, `idem`, `t(?;…)`) take no part.
  - Sex field: `SEX.CONSTITUTIONAL_COMPLEMENT` fires on every signed X/Y (not with a `c` field) and `SEX.ACQUIRED_GAIN_LOSS` when field + lost + rearranged X/Y ≠ k (not in a line with any `c`, or where the count names another cause); their KB contexts make them conditional (§13 `47,XXX`).

## 8. Diagnostics

### 8.1 Shape
```csharp
sealed record Diagnostic(
    string RuleId,                     // always a KB rule ID (§8.4)
    Severity Severity,                 // effective severity (§8.2); for conditional ones: the worst over contexts
    TextSpan Span, ImmutableArray<TextSpan> SecondarySpans,
    string Message,                    // from the rule title + check's arguments
    ImmutableArray<Citation> Citations,// rendered "ISCN 2016 §8.1, p.54, ¶1"
    DiagnosticOrigin Origin,           // Parser | Binder | Rule
    bool BookAmbiguous, string? ReviewNote,  // needs_review → capped at warning (T-022)
    Certainty Certainty,               // Certain | Uncertain (→ capped at warning)
    ContextCondition? Condition,       // §8.3
    bool CannotVerify,                 // info "can't be verified from the string: …"
    ImmutableArray<Fix> Fixes);        // §10
enum Severity { Info, Warning, Error }
```

### 8.2 Severity resolution (T-022 policy, in this order)
1. Start with the KB rule's `severity`.
2. `review.needs_review: true` → at most `Warning`; set `BookAmbiguous` and copy the review note.
3. `Certainty.Uncertain` (the check can't decide with certainty, e.g. counts with dic/psu dic, ploidy ranges, `?`/`~`, uncertain mar) → at most `Warning`. `NUM.COUNT_MATCHES_CHANGES` thus reports an error when the arithmetic is certain, else a warning.
4. Context (§8.3): if the effective context is known, keep the result only when the rule applies to it; if it is unknown, build a per-context severity.
5. "Can't verify from a single string" rules (e.g. `KARYO.ABBREVIATED_FORM`, `REC.KNOWN_PARENTAL_REARRANGEMENT`) → `Info` with `CannotVerify`, emitted **only when the string uses the notation concerned**.
6. Band names with `book: false` → `Info` in karyotype/ish segments (accepted, source named), nothing in arr/seq; names that exist nowhere stay `BAND.MUST_EXIST` errors. As built (T-029): a KB info rule of its own, `BAND.GENOME_ASSEMBLY_ONLY`, whose `applies_to` leaves out arr/seq, so this step needs no code in `DiagnosticFactory`.

### 8.3 Conditional diagnostics
```csharp
sealed record ContextCondition(ImmutableDictionary<SampleContext, Severity?> PerContext); // null = no finding in that context
```
- Used when the effective context is `Unknown` and the rule is context-dependent (KB `context`, or a check that reports differently per context, e.g. `47,XXX` vs `47,XX,+X`).
- Message wording: "error if acquired; valid if constitutional". Fixes carry `ForContext` (§10).
- With a hint or an inferred context the diagnostic becomes firm (no `Condition`).

### 8.4 Parser and binder errors also carry rule IDs
- Each recovery point in the parser names the KB rule that defines the expected form (`KARYO.COUNT_SEX_ORDER`, `DEL.FORMAT`, `SYM.LISTED_ONLY` for an unknown symbol, `CHROM.IDENTIFIERS` for chromosome 23, `BAND.MUST_EXIST` for an unknown band).
- If no KB rule describes an expected form, that is a KB gap to fix before implementing. As a safety net only, an internal `CORE.UNPARSED` code (no citation, severity error, flagged in the coverage report) marks text the parser could not attribute; T-025 treats any occurrence on a KB example as a test failure.

### 8.5 As built (T-028)
- `CytoLex.Core.Validation.Diagnostic` follows §8.1 plus `Detail` (the parser's or check's own message; `Message` is the KB title) and `Origin = Core` for engine codes. `Fixes` stays empty until T-035.
- Severity resolution (§8.2 steps 1–5) and conditions (§8.3) are in `DiagnosticFactory`. A check may narrow the contexts of one finding (`contexts`); otherwise the KB `context` applies. Step 6 (band `book: false`) is the KB info rule `BAND.GENOME_ASSEMBLY_ONLY` (T-029).
- Contradicting context markers → **`CORE.CONTEXT_CONFLICT`** (decided with the user): info, no citation, the first marker as span and the others as secondary spans. It is a core code like `CORE.UNPARSED`, not a KB rule, because it comes from CytoLex's inference, not from the book.

## 9. Results API

```csharp
sealed class Validator {
    Validator(IKnowledgeBase kb);                               // registers profiles and checks
    ValidationReport Validate(string input, ValidationOptions? options = null);
}
sealed record ValidationOptions(
    EditionSelection Editions,          // One(Edition) | All   (default: newest implemented edition)
    SampleContext? ContextHint = null,  // default: infer (§3.3)
    bool IncludeTranslation = false,    // T-058
    bool IncludeCoordinates = false);   // T-054 genomic detail (GRCh37/38), off by default

sealed record ValidationReport(string Input, ImmutableArray<SegmentInfo> Segments,
    ContextInfo Context /* hint, inferred value, deciding marker spans */,
    ImmutableArray<EditionResult> Results);   // one element for One(e); one per known edition for All

sealed record EditionResult(Edition Edition, EditionStatus Status /* Implemented | NotImplemented */,
    Validity Validity, SeverityCounts Counts,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<SegmentResult> Segments,    // per segment: Implemented | NotImplemented(reason, span)
    Translation? Translation);                 // T-058 slot, per edition, per-context readings

sealed record Validity(bool IsValid, ImmutableArray<SampleContext> ValidContexts, bool IsComplete);
```
- **Validity** (T-022): valid = no errors in that edition. With a known context: no firm errors. With an unknown context: valid if at least one context has no errors; `ValidContexts` names them ("valid as constitutional only"). `IsComplete = false` when any segment or construct is `NotImplemented` ("valid as far as implemented").
- **Counts** per severity, conditional diagnostics counted by their worst severity and listed separately (`ConditionalCount`).
- **Ordering:** diagnostics sorted by span start, then severity.
- **Serialisation:** all result types serialise to JSON with System.Text.Json (KB IDs and spans included) for batch export (T-038) and the web service (T-044).
- **As built (T-028):** `Validator(IKnowledgeBase)`, `Validator.Default` (embedded KB), `Validate(input, ValidationOptions)`. `ValidationOptions` has `Editions` (`EditionSelection.One(e) | All | Default`), `ContextHint` and `IsFragment` (book fragments use the parser's fragment entry; for the KB-example harness and the rule browser). Translation and coordinates options come with T-058. `EditionResult` has a `Reason` for not-implemented editions; segments are `SegmentResult(Segment, Status, Reason, Constructs)`. Known editions = KB editions ∪ `Validator.IscnEditions` (2016, 2020, 2024). `Validity.ValidContexts` with a known context is `[effective]` when valid. `Validator.JsonOptions` serialises enums by name (round-trip tested).
- **Messages:** English in the pilot; messages are built from KB titles plus check arguments, so localisation is a data question later.

## 10. Fix model

```csharp
sealed record TextEdit(TextSpan Span, string NewText);   // insert = zero-length span; delete = empty NewText
sealed record Fix(
    FixLevel Level,              // 1 = deterministic edit, 3 = explanation only (level 2 "did you mean": T-041)
    FixKind? Kind,               // replace | insert | delete | reorder (from the KB fix.kind)
    ImmutableArray<TextEdit> Edits,   // non-overlapping, against the original input; empty for level 3
    string Description,          // from the KB fix hint (+ arguments)
    SampleContext? ForContext,   // per-context alternative fixes (T-021/T-035)
    string? Preview);            // full corrected string for the before/after diff
```
- Fixes belong to a diagnostic and therefore to an edition (fixes are per edition).
- **Reorder** is expressed as edits too (replace the covered span with the reordered text); `Kind` tells the UI what happened.
- **Apply = edit text + re-validate.** The tree is never patched; after applying, the caller validates the new string (the desktop app does this automatically, T-040).
- **Fix all:** applies all level-1 fixes of one edition (and one context, if conditional) whose edits don't overlap, then re-validates; overlapping ones are left for the next round.
- **Highlighting** (decisions → Fix suggestions) follows from the edits: a replace underlines the span; an insert shows an insertion marker with grey expected text (`NewText`); a delete strikes the span through. Missing tokens from the parser give inserts directly.
- KB `corrected` forms of invalid examples are the expected `Preview` in tests (T-025/T-035).

## 11. Hooks for the literal translation (T-054, T-058)
The core model guarantees what the renderer needs; the renderer itself is T-058.
- Meaning nodes per §5 with spans and `KbRef`s (rule or vocab item; bands as `band:`).
- `IsPartial` plus diagnostic IDs on nodes from recovered input, so the renderer can mark parts "not translated, see diagnostic …" under a "not valid ISCN <edition>; partial translation" banner.
- The effective context (and `Unknown`) reaches the renderer, which gives both readings where the context changes the meaning.
- Band coordinates are available from `IBandSet` for the optional genomic detail.
- `EditionResult.Translation` is the slot per edition; templates come from the bundle (T-057).

## 12. Extensibility
- **New notation type** (ish, arr, …): a segment parser + binder producing its meaning nodes (subclasses of `MeaningNode`), rule checks tagged with KB IDs, and the type added to the profile's `ImplementedTypes`. Segmenter, engine, diagnostics, fixes and results are unchanged.
- **Clones** (T-032): `CellLine.BasedOn` (`CloneRef` → idem/sl/sdl) and the `//` relation already exist in the model; the binder resolves the reference to the earlier cell line.
- **New edition** (T-045/T-046): a KB edition plus an `EditionProfile` (vocab, features); checks run where the edition's KB has their IDs; edition-specific variants only where behaviour differs.
- **External checkers** (T-049): consume `ValidationReport` and the semantic model; no change to the core API.

## 13. Worked examples (ISCN 2016, pilot)

**`46,XY,del(5)(q13q33)`** — tokens `46` `,` `XY` `,` `del` `(` `5` `)` `(` `q13` `q33` `)`; meaning: count 46, sex XY, `Abnormality{Kind=del, Chromosomes=[5], Breakpoints=[5q13, 5q33]}` with `rule:DEL.FORMAT`, `vocab:symbols/del`, `band:5q13`, `band:5q33`. No diagnostics → `Validity(IsValid: true, ValidContexts: [Constitutional, Acquired])`.

**`47,XXX`** (no hint, context `Unknown`) — `SEX.ACQUIRED_GAIN_LOSS` (KB context neoplastic) reports a conditional diagnostic: error if acquired, nothing if constitutional; fix `ForContext = Acquired`: `47,XX,+X`. Since T-056 the rule is no longer `needs_review`, so the acquired severity is a full error. Result: "valid as constitutional only" (the constitutional context has no errors), with the conditional note "acquired: write 47,XX,+X". With hint `Constitutional` → no diagnostic. The mirror case `47,XY,+X` gives `SEX.CONSTITUTIONAL_COMPLEMENT` as error if constitutional → "valid as acquired only".

**`46,XX,del (5)(q13q33)`** — the space is trailing trivia of `del`. `SYM.NO_SPACE_AT_PARENTHESIS`: error, span = the space, level-1 fix `delete` (`TextEdit(span, "")`), preview `46,XX,del(5)(q13q33)`. The deletion is still bound and every other rule runs (`BAND.MUST_EXIST` etc.).

**`46XX`** — the parser inserts a missing `,` token (rule `KARYO.COUNT_SEX_ORDER`): error, insertion marker at offset 2 with grey `,`; fix `insert`.

**`46,XX.ish 5p15.2(D5S23×2)`** — two segments: `karyotype` (validated) and `ish` (`NotImplemented`, "ish: not implemented in the pilot"). Result for 2016: valid, `IsComplete = false`. All-editions call: 2016 as above, 2020/2024 `Status = NotImplemented`.

## 14. Open points and follow-ups
- **T-060** (done 2026-11-18): `kb build` writes the lean runtime bundle `build/kb-runtime.json` (rules metadata, vocab, bands); `CytoLex.Core` embeds it. Proposed as a dependency of T-028.
- **T-053** (decided 2026-11-18): typed `-`/`x`/`->` are equivalent to `–`/`×`/`→` in ISCN 2016, with no diagnostic (vocab `equivalent_forms`, rule `SYM.TYPED_SYMBOL_FORMS`, T-063). The lexer may still keep the typed form on the token so fixes preserve the user's symbol forms.
- **T-056:** severities in the KB are applied to the policy; until then results use the current KB severities.
- **`CORE.UNPARSED`** (§8.4): kept only as a safety net; every occurrence on a KB example is a KB/parser gap.
- **Default edition** for `EditionSelection` when none is given: the newest implemented edition (2016 in the pilot).
- **T-028** (done 2026-11-18): engine, KB loader and result API as built (§7.2, §8.5, §9); clone structure dropped as a context marker (§3.3).
- **T-065** (done 2026-11-18): the binder (syntax tree → semantic model) as built in §5; no binder diagnostics; short-system band compositions are T-066.
