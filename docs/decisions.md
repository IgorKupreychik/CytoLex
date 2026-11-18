# CytoLex — Decisions

Decisions made during planning (2026-11-16) and later `discussion` tasks. Each new decision gets a dated entry under **Decision log**.

## Product & architecture
- **Core library + desktop app.** All ISCN parsing/validation lives in a UI-free core library. A desktop app is the pilot UI. A web service is planned later (T-044) and reuses the same library.
- **App tech stack (T-020):** C# on **.NET 10 LTS**. Core library: a hand-written lexer and recursive-descent parser. Desktop app: **Avalonia + AvaloniaEdit** with CommunityToolkit.Mvvm. Tests: **xUnit v3**. Web service later: ASP.NET Core. Script tooling (extraction, KB build, bands): **Python 3.14 + uv** (T-001), independent of the app stack.

## Version-aware validation
- ISCN 2016 in the pilot; 2020/2024 later (T-045/T-046). Editions are not backward compatible.
- The user selects a single version (one result) or all versions (an array with one result per version; unimplemented versions return `not-implemented`).
- Each diagnostic has: severity (error/warning/info), character span, rule ID, citation — format `ISCN 2016 §9.2.17.1, p.75, ¶1` (book page numbers).
- Each per-version result has a validity (valid = no errors; per context when the context is unknown) and counts per severity (T-022).
- Input type detection (`ish`, `nuc ish`, `arr[...]`, …). Combined strings are split on `.` and each segment is typed.
- The parser must recover from errors and report all problems, not stop at the first.

## Pilot scope (T-021)
- **Implemented:** karyotypes from ch. 2–9, constitutional and single-line acquired, numerical and structural, in the short and detailed systems, including mos/chi cell lines (`[n]`, `/`).
- **Detected but `not-implemented`** (per segment): neoplastic clones (idem/sl/sdl, `//`, composite karyotypes), ish/nuc ish/other ch. 13 notations, arr, ch. 10 and 12 (breakage, meiotic), and underlined homologues (T-042).
- **The core is designed for all types and implements a subset:** the data model covers every type, so later types extend it rather than requiring rework.
- **Context (constitutional vs acquired) is never a required input:**
  - It is inferred from clear markers only.
  - Otherwise, context-dependent rules give conditional diagnostics, with fixes per context.
  - An optional context hint makes the result firm.
- **After the pilot:** clones, then arr, then FISH.

## Strictness & severity (T-022)
- **Valid = no errors** in that edition. Warnings and info never affect validity. Each edition is judged on its own. The result carries counts per severity ("valid, 2 warnings").
- **Severity from the book's wording:**

  | Book wording | Severity |
  |---|---|
  | Mandatory: "is", "must", "always", a forbidding "should not", the printed format | `error` |
  | Preference: "recommended", "preferred", "discouraged", "may be simplified" | `warning` |
  | Explanation or convention | `info` |

- **Non-canonical but unambiguous input** (stray or missing spaces, wrong letter case) → `error` with a level-1 fix, as the book prescribes the form. The parser recovers, reads the intended meaning and keeps running every other rule. (ASCII `-`/`x`/`->`: T-053.)
- **Book-ambiguous rules** (`review.needs_review: true`): a violation is reported **at most as a `warning`**, marked "book ambiguous" with the review note, so it never invalidates a string. Once the rule is reviewed and resolved, its own severity applies.
- **Uncertain checks:** when the engine can't decide a violation with certainty (e.g. the chromosome count with dic/psu dic, ploidy ranges, `?`/`~`, uncertain mar counts), it reports a `warning` instead of an `error`. Example: `NUM.COUNT_MATCHES_CHANGES` is an error when the arithmetic is certain, else a warning.
- **Conditional diagnostics** (context unknown, see Pilot scope) carry a **severity per context** ("error if acquired"). The string is **valid if at least one context has no errors**. The result says in which context(s) it is valid ("valid as constitutional only"). A context hint makes the result firm.
- **Rules a single string can't verify** (they need the report or the family, e.g. `KARYO.ABBREVIATED_FORM`, `REC.KNOWN_PARENTAL_REARRANGEMENT`) → an `info` note **only when the string uses the notation concerned**: "can't be verified from the string: <what to check>", with a citation. Otherwise silent.
- **Band names in the genome assemblies but not in the book's idiograms** (`book: false`, e.g. `9q34.12`) → `info` in karyotype and ish strings: accepted, with a note naming the source. No note in arr/seq, where genome bands are expected. Names that exist nowhere stay errors (`BAND.MUST_EXIST`).
- Applied to the KB rules in T-056.

## Fix suggestions
- **Pilot:** level 1 (deterministic fix, shown as a before/after diff with an "Apply" button) and level 3 (plain-language explanation with citation when no single fix exists).
- **Later:** level 2, "did you mean" (typos, nearest band) — T-041.
- **Highlighting:** wrong range underlined; missing text shown as an insertion marker with grey expected text; extra text struck through.
- Fixes are per version.

## Literal translation (T-054)
- **What:** a plain-language rendering of what a string literally says (`46,XY,del(5)(q13q33)` → "46 chromosomes; sex chromosomes XY; … interstitial deletion of the long arm of chromosome 5 between bands 5q13 and 5q33"). No clinical interpretation.
- **When:** designed now (the T-023 model keeps the meaning), built after the pilot rules (T-057–T-059, Pri 2). Covers karyotype ch. 2–9 first; other types arrive with their parsers.
- **Strings with errors:** every part the parser understood is translated; parts with errors are marked "not translated, see diagnostic …", under a "not valid ISCN <edition>; partial translation" banner. Per edition.
- **Shape:** a summary line (count, sex chromosomes, number of abnormalities) plus one sentence per item, each tied to its character span.
- **References:** the translation links to **KB entries** (a rule ID or a vocab item such as a symbol or band), shown by title with the ID on hover. The link opens the version-specific entry in the rule browser, where the book citation is shown. The translation itself shows **no book citations**; diagnostics keep their inline citation. Exports carry the KB IDs.
- **Context:** neutral literal wording by default (never a syndrome name). Where the context changes the meaning, both readings are given ("if constitutional / if acquired"). A hint or a clear marker gives one reading.
- **Genomic detail:** optional, off by default: approximate GRCh37/38 coordinates and sizes from `kb/bands`, labelled as approximate; book-only bands show "no coordinates". LGF (T-050) linked later.
- **Language:** English in the pilot, from template data files per edition keyed by KB entry (proposed `kb/translation/<version>/`, schema-checked, in the bundle), so other languages are a new template set.
- **Tests:** a per-edition golden file (string → summary, items, spans, KB IDs), seeded from book examples (compared with the printed explanation, cited not quoted) plus non-book strings; entries count only after a person's review.

## Knowledge base (KB)
The KB is the single source for all validation decisions.

| Layer | Content | Location | In git |
|---|---|---|---|
| 1. Source text | Per-section chunks extracted from the PDF by a deterministic, non-AI script (~3–5k tokens max per chunk) | `kb/source-text/<version>/` | No (can be rebuilt) |
| 2. Rules | YAML, checked against a JSON Schema; stable concept IDs with a block per version | `kb/rules/<version>/` | Yes |
| 2b. Quotes | Verbatim text + location, linked to rules by ID; removable before any public release | `kb/source-quotes/<version>/` | Yes (private project) |
| 3. Generated | SQLite (FTS5) + JSON bundle | `build/` | No |

- **Rule fields:** `id`, `applies_to`, `context`, `statement` (paraphrase), `cite` {version, section, page, paragraph}, `examples` {valid, invalid with `why` and corrected form}, optional `fix` hint, `review.paraphrase_checked`, `needs_review`.
- KB examples double as the validator/fixer test suite.
- **Notation types (`applies_to`):** `general`, `karyotype`, `breakage`, `meiotic`, `ish`, `nuc_ish`, `fib_ish`, `rev_ish`, `cgh`, `arr`, `rsa`, `seq`.
- **Context:** `constitutional`, `neoplastic`, `meiotic`.
- **Type assignment:** the chapter sets the default; explicit text overrides it; ambiguous cases get `needs_review`.
- YAML was chosen over SQLite or JSON as the editable source; this may be revisited.
- **Chapter order:** core ch. 2–9 → neoplasia ch. 11 → FISH ch. 13 → microarray ch. 14 → ch. 10, 12, 15, 16.

## Source PDF
- Kept **outside the repo** and never committed. Path set in gitignored `config/local.yaml` (template: `config/local.example.yaml`); env var override.
- **ISCN 2016 findings:**
  - 145 pages with a real text layer (no OCR); `pdftotext` (mingw64) works.
  - **Book page = PDF page − 6.** TOC on PDF pp. 3–6.
  - Extraction problems:
    - `→` extracts as `]`.
    - Stray spaces (`t(2; 5)`).
    - Underlining (used to tell homologues apart) is lost, so affected rules are flagged `needs_review`.
    - Margin change markers are lost.
  - Ideograms (book pp. 14–33) are images.

## Band data
- **Hybrid approach:**
  - A script (`kb bands`) generates bands from the NCBI Genome Decoration Page ideograms (real 400/550/850 sets) and UCSC cytoBand (850, cross-check), GRCh37/38; every nesting ancestor is a valid name (T-006 entry below).
  - The book's idiograms are decoded from the PDF (`kb ideograms`) and compared in full; every difference is decided in a cited YAML exceptions file, and 300/700 come from the book (T-007 entry below).
- Stored per ISCN version. Coordinates are reused for `arr` checks.

## Workflow
- **Tasks are tracked in `TASKS.md`:**
  - A summary table (`ID | Status | Pri | Depends | Title`), then a section per task.
  - Statuses: `todo`, `in_progress`, `blocked`, `done`, `discussion`.
- **`/next-task [T-xxx]`** (`.claude/skills/next-task/SKILL.md`):
  - Picks the given task, or else the first startable `todo`/`discussion` by priority then ID.
  - Plans in plan mode. Status → `in_progress` on approval.
  - Marks `done` only after the user confirms. Never commits; it suggests a commit message.
  - Discussion tasks are interviews, one question at a time.
- **Token economy:** KB tasks read extracted chunks, one chapter group per session. Later work reads the compact rules, not the book.

## Desktop app (pilot features)
- Single-string check with type detection, highlighting and cited diagnostics.
- Version selector.
- Batch validation (text/CSV, CSV/JSON export).
- Rule browser.
- Fix UI.

## External comparison (CytoGPS)
- Compare our results with CytoGPS for single strings and batches:
  - Validity agreement (2×2 matrix, disagreements highlighted).
  - Interpretation agreement (Loss–Gain–Fusion per band).
  - Triage of each disagreement (our bug / CytoGPS limitation / version difference). Confirmed cases become KB examples.
- A dev/QA tool first, exposed in the desktop app later.
- Pluggable `ExternalChecker` interface; cache; throttling; compare against CytoGPS's ISCN version only.
- **Privacy:**
  - Prefer a local CytoGPS (source code or container).
  - The online adapter is off by default.
  - Data policy to be decided in T-048, after research (T-047).
  - CytoGPS facts are unverified (web access failed during planning).

## Copyright
- Private, personal project for now: verbatim quotes are kept (in separate, removable files) to audit validator decisions.
- Before any public deployment: paraphrase-only KB — T-043.

## Decision log
_(entries added by `discussion` tasks: date — task — decision — rationale)_

- **2026-11-16 — T-001 — Script tooling: Python 3.14, uv, pdfplumber, single `kb` CLI.**
  - Language: Python 3.14 (`requires-python >=3.14`, `.python-version`).
  - Environment: uv; `tools/pyproject.toml` + committed `tools/uv.lock`; `tools/.venv/` is local.
  - Libraries: `pdfplumber` (PDF text with char-level font/position data), `ruamel.yaml` (round-trip YAML), `jsonschema` (draft 2020-12), stdlib `sqlite3` (FTS5) and `urllib` (UCSC download), `argparse` (CLI).
  - Layout: a self-contained project in `tools/`, kept apart from the future app code at the repo root. The package `tools/src/cytolex_tools/` exposes the `kb` command with subcommands `extract` (T-002), `build` (T-005), `bands` (T-006). Tests are in `tools/tests/`. Run with `uv run --directory tools …`.
  - Dev tools: pytest + ruff (lint + format).
  - PDF path: `config/local.yaml` → `iscn_pdf.<version>`; env var `CYTOLEX_ISCN_PDF_<VERSION>` overrides it.
  - Rationale: the strongest PDF extraction libraries (heading detection needs font data), FTS5 in the stdlib, mature YAML/JSON Schema with clear error paths, already installed on Windows. The scripts hand off to the app only through files (chunks, SQLite, JSON bundle, band YAML), so T-020 stays open. pdfplumber (MIT) was chosen over PyMuPDF (AGPL) to avoid licence questions; pdftotext has no font info.

- **2026-11-16 — T-002 — Source-text extraction conventions.**
  - **Role:** deterministic extraction (no AI) of faithful, stable source text. AI interprets the chunks into rules later (T-008+), with human review, and `kb build` can check AI-written quotes against the chunks (proposed for T-005).
  - **Chunks:** one Markdown file per numbered heading (own content only), ch. 1–16, with YAML front matter. Cap: 4,000 tokens, estimated as ceil(chars/4) over the whole file. Oversized sections are split at block, then line, then sentence boundaries into `-pN` parts.
  - **Citation labels:** `¶n` counts prose paragraphs (and small print not under an example) per numbered section; `En` counts example blocks; both restart at every numbered heading. A label's page is the book page of its first line. Cite as `§9.2.17.1, p.75, ¶1` or `… ¶2, ex. E3`.
  - **Font-aware glyph map:** `GeneralSymbolsPB ]`→`→`, `MathTechnicalPD ~`→`×`, `8`→`±`, `MathTechnicalPA !`/`1`→`<`/`>`, `MathematicalPi-Three ,`→`~`, Pi ``→`′`, `a`→`α`. `MathTechnicalPA ^`→`≤` is uncertain (T-003). Unmapped symbol glyphs are listed in the manifest. The en dash used as a minus sign is kept verbatim.
  - **Zero-width spaces dropped (deviation from the T-002 description, which said stray spaces would be noted, not removed):** the "stray spaces" (`t(2; 5)`, `in volving`, `)  ×2`) are space characters in the PDF text layer that take up no room on the page (gap between neighbouring glyphs < 0.8 pt, vs ≥ 1.5 pt for real spaces). Dropping them reproduces the printed text; 813 were removed. Visible odd spaces are kept and listed per chunk (`stray_spaces`).
  - **Layout repairs:** footnote markers become `^n`; Cambria `′` glyphs with broken vertical metrics are moved back onto their line (9); ideogram label glyphs (font TTE2340D80t00) are dropped and their pages listed.

- **2026-11-16 — T-003 — Extraction review: layout recovery, tables and floats (extractor version 2).**
  - **Review method:** the PDF text layer supplies every character; rendered page images are used only to verify; a person signs off tables. A table is verified when all three agree. Report: `docs/extraction-review-2016.md`.
  - **Underlining is recovered, not just flagged.** Underlines are drawn rules in the PDF; an underlined character is written followed by U+0332 COMBINING LOW LINE (`der(1̲)`), and chunk front matter `underlined` lists the labels. Chosen over Markdown-style `_…_` because `_` is ISCN/HGVS syntax (nucleotide ranges). Validator handling stays with T-042.
  - **Margin change bars are recovered** as front matter `changed` (labels beside a bar). Per ISCN 2016 §1.6 they mark changes against the previous edition, which is useful for the 2020/2024 version profiles.
  - **Floats get their own labels.** Captions are `[Fig. N p.X]` / `[Table N p.X]` (uncaptioned tables by name, e.g. `[Ploidy table p.90]`) and are not counted as `¶`; a table, its footnotes and diagram legends belong to the float. This renumbers some `¶` labels against version 1 (no rules cited them yet). Paragraphs that a float or footnote interrupts mid-sentence are rejoined.
  - **Tables come from a committed per-edition layout file** (`tools/src/cytolex_tools/extract/tables/<version>.yaml`: page area, column x positions, row keys, review status) and are rendered as Markdown tables. Hand-measured columns were chosen over automatic table detection because the tables have few ruling lines; the characters still come from the text layer. `kb tables` renders the regions to PNG for sign-off.
  - **Backup plan for tables** (none needed so far): B1 `pdftotext -layout` as a third channel; B2 a hand-transcribed `kb/source-tables/<version>/<table>.yaml` whose tokens are machine-checked against the page's text layer; B3 caption only + `needs_review`, transcribing just the rows a rule needs.
  - **Glyphs:** `≤` verified; `GeneralSymbolsPB s` → `→`; FISH legend symbols `GeneralSymbolsPE P/S` → `●`/`○`. `–`/`×` vs `-`/`x` acceptance is open (T-053).

- **2026-11-16 — T-004 — KB file format: JSON Schemas, one file per section, structured citations.**
  - **Schemas:** JSON Schema draft 2020-12, written as JSON, in `kb/schema/` (`common` = rule ID, citation, vocabularies; `rules`; `quotes`), linked by `$ref`. `additionalProperties: false` everywhere so typos fail. Patterns carry an `x-expected` hint used for readable error messages. Field reference and conventions: `kb/README.md`.
  - **Files:** one rule file per numbered section, named like its chunk (`kb/rules/2016/ch09/09.02.17.01-reciprocal-translocations.yaml`), holding a list of rules; quotes for that section in the same relative path under `kb/source-quotes/`. Keeps per-task reads small and maps 1:1 to chunks.
  - **"Block per version"** = the same rule ID in each edition directory (`kb/rules/2016/`, `kb/rules/2020/`); the file's `version` must equal its directory (checked in T-005).
  - **Rule fields:** `id`, `title`, `applies_to` (≥1), optional `context` (omitted = all), `severity` (error/warning/info), `statement` (paraphrase), `cite` (≥1), `examples.valid` (book examples with their `En` cite) / `examples.invalid` (`why` required, `corrected` optional), optional `fix` (`level: 1` + `kind` replace/insert/delete/reorder, or `level: 3` explanation), `review` (`paraphrase_checked`, `needs_review`; `note` required when `needs_review`), optional `related`. Added beyond the planning list: `title`, `severity`, `related`.
  - **Rule ID:** dot-separated UPPER_SNAKE segments, ≥2, first = readable topic prefix (`TRANSLOC.CHROMOSOME_ORDER`); never renamed.
  - **Citation:** structured `{section, page, para?, example?, float?}` (at least one anchor; labels exactly as in the chunks: `¶n`, `En`, `Fig. 6a`, `Table 1`, `Ploidy table`); the edition comes from the file. Rendered `ISCN 2016 §9.2.17.1, p.75, ¶2, ex. E3`. Structured rather than a string so the build can check labels against chunk front matter.
  - **`kb check [PATH …]`:** schema-only check of rule/quote files (default: all of `kb/rules/` and `kb/source-quotes/`), errors as `file:rules[0].cite[0].para: …`, exit 1 on any problem. `kb build` (T-005) reuses `cytolex_tools.kb.schema`. Duplicate YAML keys are errors.

- **2026-11-16 — T-005 — KB build (`kb build`): checks, outputs, quote verification.**
  - **Quotes are verified against the chunks (error).** Each quote's `text` must occur, whitespace-normalised, in the block of one of its cited labels (`¶n`/`En`/float) in `kb/source-text/<version>/`. A mismatch fails the build: this guards against AI-invented or edited quotes. Without chunks (`kb extract` not run) the chunk checks are skipped with a warning, so the KB still builds on a machine without the PDF.
  - **Cites are checked against chunk front matter (error):** section exists, label exists, `page` = the label's page. Multi-part chunks (`-p1`/`-p2`) are merged per section.
  - **Valid book examples** with an `En` cite are looked up in that block, ignoring all whitespace; a miss is only a **warning** (stray spaces, future ASCII input forms, T-053).
  - **Other errors:** schema; `version` ≠ directory; file name ≠ `section`; duplicate rule IDs per edition; unknown `related` IDs; quote file without a rule file at the same path, different `section`, or quote `rule` not in that rule file. Cross-file checks run only once every file passes the schema.
  - **Outputs:** one `build/kb.sqlite` and one `build/kb.json` for all editions (keyed by `version`), so the app can give multi-version results from one load; plus `build/review/kb-review.md` (rules with `needs_review: true` / `paraphrase_checked: false`, also printed). `kb build` builds every edition by default; `--version` restricts it. On any error nothing is written (files are replaced atomically), so a previous good build stays usable. No timestamps in the outputs. `format_version: 1`.
  - **SQLite layout:** normalised tables (`rules`, `citations`, `examples`, `quotes`, `rule_applies_to`, `rule_context`, `rule_related`, `meta`) plus FTS5 `rules_fts` (id, title, statement, example texts) and `quotes_fts`, `unicode61` tokenizer. Stdlib `sqlite3` (FTS5 available in Python 3.14's SQLite 3.50).

- **2026-11-16 — T-006 — Band data: NCBI GDP + UCSC, pinned, committed output.**
  - **Sources (deviation from the original "UCSC cytoBand" plan):** UCSC `cytoBand` only has the ~850 level, so the 400/550/850 band sets come from the NCBI Genome Decoration Page ideograms (`ftp.ncbi.nlm.nih.gov/pub/gdp`, `ideogram_9606_GCF_000001305.13_*` = GRCh37, `.16_*` = GRCh38; `.14`/`.15` are byte-identical to `.16`). UCSC hg19/hg38 `cytoBand` cross-checks names and coordinates and gives the canonical 850-level coordinates (0-based → 1-based closed). The 862 UCSC primary-chromosome names equal the NCBI 850 set. Both are public data.
  - **300 and 700 levels:** no public source has them. Recorded as `levels_unavailable` in the output; whether to derive them from the book's ideograms is left to T-007.
  - **Names:** the 400 ∪ 550 ∪ 850 sets plus every nesting ancestor (9q34.11 → 9q34.1 → 9q34), 1,150 names; 52 are ancestor-only (`derived: true`). An ancestor's coordinates are the union of its 850 sub-bands. Arm-level terms (pter, cen, p10) are notation rules for the KB, not band data.
  - **Checks (any failure → exit 1, nothing written):** same names per level in both assemblies; NCBI 850 = UCSC names; NCBI vs UCSC coordinates within 1 bp (NCBI 850 files mix 0/1-based starts); every 400/550 name nests over 850 names and its coordinates equal the union of its sub-bands.
  - **Repeatability:** source URLs and sha256 pins in `tools/src/cytolex_tools/bands/sources/<version>.yaml`; a changed upstream file fails the run until re-pinned with `--update-pins`. Downloads are cached in `build/cache/bands/`. The output `kb/bands/<version>/bands.yaml` is committed (no network needed after a clone) and byte-identical across runs.
  - **Format:** YAML, one flow-style line per band (diff-friendly); stdlib `urllib` download, no new dependencies.

- **2026-11-17 — T-007 — Book idiograms decoded from the PDF; full comparison; book wins with cited exceptions.**
  - **Source (beyond the original "spot-check a sample" plan, decided with the user):** the Fig. 5 idiograms (book pp.16–29) are vector drawings whose band labels are text in a figure-only font (`TTE2340D80t00`) with private-use glyphs. `kb ideograms` decodes all 24 chromosomes × 5 levels (2,894 labels) deterministically:
    - The glyph map (13 glyphs: digits, `.`, the range dash `–`, `p`, `q`) is in `bands/sources/2016.yaml` → `ideograms`.
    - Glyphs are joined per baseline and adjacency, because word grouping merges stacked leader-line labels.
    - Each label is assigned to the nearest idiogram to its right (columns from the drawn outlines; left to right = 300/400/550/700/850).
    - Arms are split where the label order turns (p descending, q ascending). Where that is ambiguous (`p11 | q11.1`, 31 idiograms), the drawn centromere decides.
  - **Output committed:** `kb/bands/2016/ideograms.yaml` (band labels only: factual names, no drawings), so `kb bands` runs without the PDF. Revisit under T-043 before any public release.
  - **Spot-check:** the rendered crops of chr1, 9, 22 and X (`build/review/bands/2016/`) were compared label by label with the decoded lists at all five levels: all match. Signed off by the user on 2026-11-17 (`ideograms.spot_check[].human: ok`).
  - **Ranges:** 300-level (and two 400-level) labels such as `22–24` are merged bands; they count for every band they span at the same depth (`22`, `23`, `24`).
  - **Comparison:** at 400/550/850 the book and NCBI differ in 21 regions, grouped by the shortest differing name, e.g. 850 `9q34.1`: book [9q34.1] vs NCBI [9q34.11–13].
    - Most are level moves only.
    - 15 book-only 850 sub-bands have no public coordinates: 1q32.11–13, 2p21.1–3, 5q13.21–23, 6p22.31–33, 6q21.1–3.
    - 6 NCBI 850 names appear in no idiogram: 6p24.1–3, 9q34.11–13.
  - **Policy:** the book wins (`resolution: book`), each difference listed with citation (`§2.5, p.N, Fig. 5`) and note in `kb/bands/2016/exceptions.yaml` (schema `kb/schema/band-exceptions.schema.json`).
    - `kb bands` fails on an unexplained or stale difference and writes a draft of all differences.
    - The six NCBI-only names are **kept** (`keep:`) with `book: false`, because ISCN 2016 itself uses genome-assembly bands in array/sequence notation (§15.3 `9q34.11q34.3`). Whether to warn on them in karyotype/ISH context is for T-022/T-029.
  - **bands.yaml (generator version 2):** levels 300–850, a `book` flag per name, book-only names with `null` coordinates and stain; 1,165 names.

- **2026-11-17 — T-008 — Ch. 3 symbols and Table 2 as vocabulary files; sign-off before planning.**
  - **Sign-off:** the user signed off Table 2 (22 rows, §2.4 p.14) and the ch. 3 symbol list (131 rows, pp.34–36; the earlier "134" counted header lines) against the page images (`review.human: ok`). From now on `/next-task` does any pending table sign-off **before** planning (`.claude/skills/next-task/SKILL.md`, step 2).
  - **Format (decided with the user):** a separate vocabulary file per printed list, `kb/vocab/<version>/chNN/<section>-<name>.yaml`, schema `kb/schema/vocab.schema.json`, instead of one rule per symbol. Each entry separates the book's row label (coverage only) from the `tokens` that appear in a string (`plus sign, single (+)` → `+`). Bracket pairs carry `pair`; several tokens per row are allowed (Roman numerals, `→`/`->` as the book gives both). Underlining is `kind: typographic` with no token (T-042). `–`/`×` stay as printed (T-053).
  - **Checks in `kb build`:** labels must equal the first column of the cited chunk tables (no missing or extra rows); tokens unique except those listed in `shared_tokens` (`.` = decimal point and period, told apart by position). Outputs: SQLite `vocab` + `vocab_tokens`, `vocab` per edition in `kb.json`; `format_version` 2.
  - **Review:** meanings are paraphrases with one `review` block per file (131 entries are approved as a whole, not one by one).
  - **Rules (19):** ch. 2–3 rules with prefixes `CHROM`, `BAND`, `SYM`. Two are inferences: `BAND.MUST_EXIST` (the idiograms define the valid bands; the user accepted it in review on 2026-11-17, since the idiograms are the book's only definition of band names and assembly bands such as 9q34.11 are covered by the cited exceptions) and `SYM.LISTED_ONLY` (the ch. 3 list is closed; marked `needs_review`). Table 1 (heteromorphisms) is left to T-010.

- **2026-11-17 — T-009 — Ch. 4 rules (karyotype designation).**
  - **Sign-off:** the user signed off Table 4 (§4.3.2.2 p.44, 16 rows; `review.human: ok`) before planning.
  - **Rules (44, all paraphrases approved by the user):** new topic prefixes `KARYO` (karyotype skeleton, clones, mos/chi, `//` transplant chimerism, end, inh/mat/pat/dn, c), `STRUCT` (chromosome/breakpoint order and separators, short vs detailed system, band composition), `BREAK` (breakpoint specification), `DER`, `REC`, and `GEN.GENOME_BUILD_REQUIRED` (§4.3 ¶2, applies to arr/rsa/seq). General ordering rules that ch. 4 states for every rearrangement use `STRUCT.*` IDs; rearrangement-specific prefixes (`TRANSLOC`, `INS`, …) are left for ch. 9 (T-012/T-013).
  - **Severity (provisional, policy is T-022):** "always / is / must / should not" → `error`; descriptive or uncheckable statements → `info`; `REC.KNOWN_PARENTAL_REARRANGEMENT` → `warning`.
  - **Deferred to T-022 (`needs_review`):** `KARYO.ABBREVIATED_FORM` (a string such as `46,XX,der(5)mat` is valid only after the full form appeared earlier in the report, which a single string can't show) and `REC.KNOWN_PARENTAL_REARRANGEMENT` (whether to warn on `rec` without mat/pat/inh).
  - **Fig. 8/9 (pp.43, 45):** checked against the printed book by the user. They add no rule content beyond the text, so the rules citing §4.3.2–4.4 keep `needs_review: false` with a `review.note`. The line-wrap spaces in §4.5 ¶4's inline strings (`rec(2) dup(2p)` …) are line breaks on printed p.46; the examples are written without them.
  - **Symbols:** every symbol ch. 4 uses is in the ch. 3 vocabulary (noted on `SYM.LISTED_ONLY`, which stays `needs_review` until T-019). `SYM.SPACE_BEFORE_CHROMOSOME_COUNT` also cites §4.1 ¶9.

- **2026-11-17 — T-010 — Ch. 5–7 rules (uncertainty, order of abnormalities, normal variable features).**
  - **Sign-off:** the user signed off Table 1 (heteromorphisms, §2.3.2 p.12, 2 parts, 14 rows + footnotes; `review.human: ok`) before planning.
  - **Rules (19 new):** new topic prefixes `UNCERTAIN` (§5: `?`, `~`, `or`, `inc`), `ORDER` (§6), `VAR` (§7.1) and `FRA` (§7.2). The general abnormality order stays `KARYO.ABNORMALITY_ORDER` (now also citing §6 ¶1); the ORDER rules add X before Y, numerical before structural per chromosome, alphabetical for homologues, der(?) then r, mar, dmin last. §6 E8 (one derivative: pter→qter, not alphabetical) is an example on `DER.ABERRATION_ORDER`.
  - **Table 1 (decided with the user):** not a vocabulary (the book lists only the commonest heteromorphisms). One info rule, `VAR.POPULATION_INVERSIONS` (§7.1.2 ¶5 + Table 1), holds its 7 inversion variants as full strings; the other cells stay in the chunk. A validator may use it for an info note only.
  - **`BREAK.ALTERNATIVE_BANDS` changed (decided with the user):** its invalid example `1q21 or q24` (non-adjacent bands) was dropped, because §5.3 E1 prints `add(19)(p13 or q13)` as valid; adjacency is the typical case, not a requirement. The reworded paraphrase was re-approved by the user.
  - **Partial strings:** book examples with `…` (§5.2 E4, §6 E9/E10) are kept as printed with a note; the test harness (T-025) must treat them as fragments.
  - **Deferred:** `ORDER.SHORTEST_STRING_PREFERRED` (T-022) and `FRA.HOMOLOGUE_UNDERLINING` (underlining, T-042) are `needs_review`. Every symbol used in ch. 5–7 is in the ch. 3 vocabulary (noted on `SYM.LISTED_ONLY`).

- **2026-11-17 — T-011 — Ch. 8 rules (numerical abnormalities).**
  - **Rules (8 new):** new topic prefixes `NUM` (`PLOIDY_REFERENCE`, `PLOIDY_IN_ANGLE_BRACKETS`, `COUNT_MATCHES_CHANGES`), `SEX` (`CONSTITUTIONAL_COMPLEMENT`, `ACQUIRED_GAIN_LOSS`), `UPD` (`FORMAT`), `MAR` (`CONSTITUTIONAL_C_SPACE`), plus `KARYO.UNCERTAIN_CONSTITUTIONAL` (`?c`). Concepts already in ch. 4 get cites and examples instead of new rules: `KARYO.GAIN_LOSS_SIGN` (§8.1 ¶1, §8.3 ¶1 + E1–E4), `KARYO.CONSTITUTIONAL_C` (§8.2 ¶4, §8.3 ¶2 + 7 examples; statement now names the `mar c` exception, re-approved); all paraphrases approved by the user, `KARYO.CLONE_ORDER_CONSTITUTIONAL` (§8.1 E4, §8.4 E2). §8.3 therefore has no rule file of its own.
  - **Count arithmetic (decided with the user):** `NUM.COUNT_MATCHES_CHANGES` (count = reference level + gains − losses) is recorded although the book never states it (all ch. 8 examples obey it): severity `warning`, `needs_review`; the exact logic is for T-022/T-030 (notes added there).
  - **Context-dependent strings:** `47,XXX` / `45,X,–X` are valid constitutional resp. acquired forms and invalid in the other context; `SEX.ACQUIRED_GAIN_LOSS` is `needs_review` (also for the sex-field convention after losses), handed to T-022.
  - **Symbols:** the ploidy levels `n`, `2n`, `4n` in `< >` (§8.1 ¶3) are not in the ch. 3 list; noted on `SYM.LISTED_ONLY` (still `needs_review`). `UPD.FORMAT` is `needs_review` (is mat/pat required?, T-022).

- **2026-11-17 — T-012 — Ch. 9.1–9.2.10 rules (structural, part 1).**
  - **Rules (26 new):** new topic prefixes `ADD` (`FORMAT`, `INSERTED_UNKNOWN_USE_INS`), `DEL.FORMAT`, `DIC` (`FORMAT`, `NO_DER_DIC`, `PSEUDODICENTRIC_ORDER`), `DUP.FORMAT`, `FIS.FORMAT`, `HSR` (`FORMAT`, `REPLACED_SEGMENT_PROXIMAL`, `INTERFACE_TWO_CHROMOSOMES`, `NO_ABR_SYMBOL`), `INS.ORIENTATION`, `INV.FORMAT`; plus `FRA.FORMAT`, `REC.NOT_ACQUIRED`, `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST` and nine `DER.*` rules for §9.2.3 (`MULTIPLE_ABERRATIONS_ONE_CHROMOSOME`, `NO_PH_SYMBOL`, `INSERTION_POINT_CONVENTION`, `ISODERIVATIVE`, `DICENTRIC_FORMAT`, `UNKNOWN_CENTROMERE`, `HOMOLOGUE_NOT_SPECIFIED`, `HOMOLOGUE_UNDERLINING`, `COMPLEX_USE_DETAILED`). All paraphrases approved by the user.
  - **Consolidation:** "several aberrations in one chromosome → der", stated in §9.2.1 ¶3, §9.2.2 ¶2, §9.2.3 ¶4, §9.2.4 ¶2 and §9.2.8 ¶2, is one rule (`DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME`) with five cites. Concepts already in ch. 4–6 get §9 cites and examples instead of new rules (`DER.*`, `REC.*`, `STRUCT.*`, `KARYO.*`, `ORDER.UNKNOWN_CENTROMERE_DER`).
  - **Statements changed (re-approved):** `KARYO.PLOIDY_RELATIVE` (structural aberrations relative to the ploidy level, §9.1 ¶1), `KARYO.REPLACED_NORMAL_NOT_MISSING` (the loss is written when a further homologue is missing, §9.1 E11), `BREAK.NOT_REPEATED_FOR_COPIES` (also derivatives of one rearrangement, §9.2.3 ¶14), `STRUCT.PROXIMAL_BREAKPOINT_FIRST` (dup and inserted segments are an exception: their band order gives the orientation, §9.2.5/§9.2.9).
  - **Inferred from examples:** `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST` (only normal sex chromosomes in the sex field) rests on the §9.1 E8 explanation and every ch. 9 example with a rearranged X/Y.
  - **Printed oddities:** §9.2.5 E2 has two detailed forms joined by "or" (second kept as a fragment); §9.2.3 E22 wraps over two lines (joined); §9.2.3 ¶6 `der(22) t(9;22)` has a line-wrap space (written without it); §9.2.3 E17 `+mos der(8)…` kept as printed.
  - **Deferred:** `DER.HOMOLOGUE_UNDERLINING` is `needs_review` (underlining, T-042). Severity questions (`ADD.FORMAT` warning, context-dependent `REC.*`) noted on T-022. Every symbol used in §9.1–9.2.10 is in the ch. 3 list; `Ph` is listed but barred from karyotypes (`DER.NO_PH_SYMBOL`) and the book rules out a symbol for abnormally banded regions (`HSR.NO_ABR_SYMBOL`).
- **2026-11-17 — T-013 — Ch. 9.2.11–9.3 rules (structural, part 2).**
  - **Rules (24 new):** new topic prefixes `ISO.FORMAT`, `MAR` (`PLUS_SIGN`, `NO_SIZE_MORPHOLOGY`, `NUMBERED`), `DMIN.FORMAT`, `ACE.NOT_IN_KARYOTYPE`, `NEO.FORMAT`, `QDP.FORMAT`, `RING` (`FORMAT`, `MONOCENTRIC_AS_DER`, `DIC_TRC_ORDER`, `UNIDENTIFIED`), `TAS.FORMAT`, `TRANSLOC` (`FORMAT`, `HOMOLOGUE_UNDERLINING`, `WHOLE_ARM`, `JUMPING`), `ROB` (`FORMAT`, `NOT_ACQUIRED`, `DICENTRIC_USE_DIC`), `TRC.FORMAT`, `TRP.FORMAT`, plus `DER.WHOLE_ARM_TWO_CHROMOSOMES` and `KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY` (§9.3). No rule file for §9.2.17 (heading only).
  - **Consolidation:** translocation chromosome order stays in `STRUCT.CHROMOSOME_ORDER` / `STRUCT.MULTI_CHROMOSOME_ORDER` (§9.2.17.1 ¶1 cites and examples added), not a new `TRANSLOC.*` order rule. Other §9.2.11–9.3 content already stated earlier gets cites and examples: `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST` (now explicit in §9.2.17.1 E3, "46,X,t(X;13) and not 46,XX"), `KARYO.REPLACED_NORMAL_NOT_MISSING`, `KARYO.ABNORMALITY_ORDER`, `ORDER.UNIDENTIFIED_LAST`, `ORDER.X_BEFORE_Y`, `DER.DEFINITION`, `DER.UNKNOWN_CENTROMERE`, `DER.ISODERIVATIVE`, `DER.UNBALANCED_USE_DER`.
  - **Statements changed (re-approved):** `STRUCT.PROXIMAL_BREAKPOINT_FIRST` and `STRUCT.SHORT_ARM_BREAKPOINT_FIRST`: wherever the band order shows a segment's orientation (dup, trp, inserted segment, segments of complex translocations and rings; band groups in multicentric rings, tas and trc), the distal or long-arm band may come first. Book examples that need it: `trp(1)(q32q21)`, `t(5;14;9)(q13q23;q24q21;p12p23)`, `der(1)r(1;3)(p36.1q23;q27q21)`, `trc r(1;3;12)(…;q26.3p24;…)`, `tas(1;X;12;7)(…;q28p22.3;…)`, `trc(4;12;9)(…;q22p13;…)`.
  - **Book contradiction:** §9.2.19 ¶1 says the short system cannot show a triplication's orientation, yet E2 writes `trp(1)(q32q21)` for the opposite orientation. Both examples are kept valid; `TRP.FORMAT` is `needs_review` (T-022/T-031).
  - **Inferred invalid examples** (stated as such in `review.note`): `rob(9;22)` (rob is for acrocentrics only), `der(1)t(1;3)(p10;q10)` for a whole-arm derivative, `t(1;3)(p11.1;q11.1)` for a whole-arm translocation, `del(6)(q13q23)×3` where only two copies can replace normal homologues (§9.3 E3 caption).
  - **Printed forms:** multi-line detailed forms in §9.2.17.1 (E5–E12) are joined without spaces; partial strings with `…` (§9.2.12 E7–E9, §9.2.15 E10–E11) are kept as printed; inline forms from the §9.2.13 E1 explanation are valid examples.
  - **Deferred:** `TRANSLOC.HOMOLOGUE_UNDERLINING` is `needs_review` (T-042). Severities: `MAR.NO_SIZE_MORPHOLOGY` warning ("discouraged"), `NEO.FORMAT` info, context-only `ROB.NOT_ACQUIRED`/`TRANSLOC.JUMPING` (T-022). Every symbol used in §9.2.11–9.3 is listed in ch. 3 (`acro`, in the discouraged "acro mar", is not).

- **2026-11-17 — T-020 — App stack: C#/.NET 10, hand-written parser, Avalonia + AvaloniaEdit, CommunityToolkit.Mvvm, xUnit v3.**
  - **Core library:** C# in a UI-free .NET class library. Rationale: the user's preferred language. It is typed and fast (batches), native on Windows, and reused unchanged by ASP.NET Core; browser-side checking stays possible later via Blazor WASM.
  - **Target:** .NET 10 LTS (C# 14), supported until Nov 2028; retarget to the next LTS (.NET 12) when it ships. .NET 11 (STS, shorter support) was rejected.
  - **Parser:** a hand-written lexer plus a recursive-descent parser, with no parser library. Rationale: full control over error recovery (report every problem, not just the first), character spans and messages, so each recovery decision can be traced to a KB rule. Rejected: ANTLR4 (generic recovery and messages, awkward for ISCN's context-sensitive segments, Java build dependency); Superpower/Pidgin combinators (they stop at the first failure).
  - **Desktop app:** Avalonia + AvaloniaEdit. Rationale: XAML/MVVM in the WPF style and cross-platform (Windows now, macOS/Linux possible later); AvaloniaEdit's text decorations cover the planned highlighting (underlined ranges, insertion marker with grey expected text, strikethrough). Rejected: WPF (Windows only), Blazor Hybrid (WebView2 dependency, mixes web and .NET UI skills), WinUI 3 (younger, no AvalonEdit equivalent).
  - **MVVM:** CommunityToolkit.Mvvm (source generators, lightweight). ReactiveUI was rejected for its steeper learning curve.
  - **Tests:** xUnit v3. `[Theory]` + `MemberData` gives one named test case per KB example (T-025); `dotnet test --filter` runs a single test. NUnit and MSTest are equivalent but less common.
  - **Web service (T-044):** ASP.NET Core (minimal API) over the same core library. This was not asked; it is the default for the stack.
  - **KB at runtime:** the app reads the `kb build` output (`build/kb.json` via `System.Text.Json`; `build/kb.sqlite` via `Microsoft.Data.Sqlite` where FTS5 search is needed, e.g. in the rule browser). The Python tooling and the app still exchange only files (T-001). How the bundle is located or embedded is decided in T-023.
  - **Other languages considered:** TypeScript (best for in-browser checking, but the user prefers C#), Python (would share the tooling language, but slower batches and awkward desktop packaging), Rust (fastest, but a steep learning curve and slower rule iteration).
  - **Proposed layout for T-024:** app code at the repo root, apart from `tools/`: `CytoLex.slnx`, `src/CytoLex.Core/`, `src/CytoLex.Desktop/`, `tests/CytoLex.Core.Tests/`, shared settings in `Directory.Build.props`, and `global.json` pinning the .NET 10 SDK.

- **2026-11-18 — T-021 — Pilot scope: karyotype ch. 2–9; context inferred, with conditional diagnostics; then clones → arr → FISH.**
  - **Scope:** the pilot fully implements karyotypes from ch. 2–9: constitutional and single-line acquired, numerical and structural, short and detailed systems, and mos/chi cell lines with `[n]` and `/` (already in the ch. 4 KB). Rationale: it uses the KB that already exists (T-008–T-013) and is the fastest route to a working end-to-end pilot.
  - **Out of the pilot:** neoplastic clones (idem/sl/sdl, `//`, composite karyotypes), ish/nuc ish/fib ish/cgh/painting, arr, ch. 10 and 12, and underlined homologues (T-042). These are detected and returned as `not-implemented` per segment.
  - **Extension, not rework:** T-023 designs the data model for all types and the pilot implements a subset. A string is a list of typed segments (ish/arr then add sub-parsers and rule sets behind the same interface). A karyotype is one or more cell lines (optional `[n]`), with room for references to earlier clones. The one risk of rework, a "one karyotype = one abnormality list" model that clones would break, is avoided this way.
  - **Context (constitutional vs acquired):** the user's goal is to submit only the ISCN string, with no extra context.
    - Context is inferred from clear markers only: clones/`idem`/`sl`/`sdl`, a `c` marker or a ploidy `<Nn>` mean acquired; `rec` means constitutional. It is never guessed from weak hints.
    - When context is still unknown, context-dependent rules (e.g. `47,XXX` vs `47,XX,+X`, `45,X` vs `45,X,–X`, `REC.*`, `ROB.NOT_ACQUIRED`, `KARYO.CONSTITUTIONAL_C`) give **conditional diagnostics** (a condition field: "valid if constitutional; if acquired write `47,XX,+X`") and fixes per context, never a firm error.
    - An optional context hint (API parameter; an advanced app setting, default auto) makes the results firm.
    - Accepted side effects: a real mistake in a context-dependent rule surfaces only as a note when context is unknown; fixes may be per context; the literal translation (T-054) uses neutral wording.
    - Rejected: a required context input (contradicts the string-only goal); inference without a hint (lab systems that know the sample type couldn't use that knowledge).
  - **Order after the pilot: clones → arr → FISH.** Clones extend the karyotype model directly (the mos/chi groundwork). Microarray is very common in labs and has a compact grammar. FISH is the largest grammar. T-014/T-032 and T-017/T-034 stay Pri 2; T-015, T-016 and T-033 move to Pri 3.
  - Requirements were handed on as notes to T-022 (severity of conditional diagnostics), T-023 (model), T-025 (harness expectations), T-026 (context inference), T-035 (per-context fixes) and T-054 (wording, scope).

- **2026-11-18 — T-022 — Strictness & severity: valid = no errors; severity from the book's wording; ambiguous rules capped at warning; conditional validity per context.**
  - **Validity:** valid = no errors in that edition; warnings and info are listed with counts. Rejected: "no errors and no warnings" (preferences in the book would make correct strings invalid) and a three-state result (batch exports and the CytoGPS 2×2 comparison need yes/no; the counts carry the same information).
  - **Non-canonical input** (spaces, letter case) → `error` + level-1 fix. Rationale: the book prescribes the form (`SYM.SPACE_BETWEEN_SYMBOLS`, `SYM.SPACE_BEFORE_CHROMOSOME_COUNT`, `SYM.NO_SPACE_AT_PARENTHESIS` stay `error`); with error recovery and a one-click fix this costs the user little. Rejected: warnings (a validator that calls `46, xx` valid isn't checking ISCN) and a split by kind (one rule is simpler). ASCII forms are left to T-053, which should follow this principle unless the book allows the ASCII form (e.g. "→ or ->", p.34).
  - **Wording map:** mandatory wording → error, preference wording → warning, explanations → info. It replaces the provisional ch. 4 mapping from T-009; T-056 re-checks every rule against it. Rejected: preferences as info (users wouldn't see them) and per-rule decisions without a map (inconsistent).
  - **Book-ambiguous rules** (`needs_review: true`) are capped at warning and marked "book ambiguous" until reviewed. Rationale: an unresolved reading of the book must not make a string invalid. Rejected: the rule's own severity (false errors) and info only (the issue is hidden).
  - **Uncertain checks** are warnings, not errors (decided on `NUM.COUNT_MATCHES_CHANGES`; a general principle). Rationale: no false errors while the counting logic can't fully interpret dic/rob/mar, ploidy ranges or `?`/`~`.
  - **Conditional diagnostics** (T-021) carry a severity per context; the string is valid if one context has no errors, and the result names that context. Rejected: plain warnings while the context is unknown (the context-specific outcome is lost) and invalid unless valid in all contexts (most correct constitutional strings would fail without a hint).
  - **Rules a string can't verify** give an info note only when the notation concerned is present. Rejected: warnings (noise on correct strings) and not reporting them (the user isn't told what to check).
  - **`book: false` bands** give info in karyotype/ish (accepted, source named) and are silent in arr/seq. Rejected: warning (they are real bands of the assemblies) and error (strict book-only names would reject current lab practice).
  - **Follow-up:** T-056 applies the policy to the KB. The dispositions from the T-022 notes:
    - Info when triggered: `KARYO.ABBREVIATED_FORM`, `REC.KNOWN_PARENTAL_REARRANGEMENT`, `ADD.INSERTED_UNKNOWN_USE_INS`, `DER.INSERTION_POINT_CONVENTION`, `DIC.PSEUDODICENTRIC_ORDER`.
    - Conditional: `SEX.ACQUIRED_GAIN_LOSS`, `SEX.CONSTITUTIONAL_COMPLEMENT`, `REC.NOT_ACQUIRED`, `ROB.NOT_ACQUIRED`, `TRANSLOC.JUMPING`, `KARYO.CONSTITUTIONAL_C`.
    - Warning (preference wording): `ORDER.SHORTEST_STRING_PREFERRED`, `ADD.FORMAT`, `MAR.NO_SIZE_MORPHOLOGY`.
    - Capped at warning while `needs_review`: `UPD.FORMAT` (no mat/pat), `TRP.FORMAT`.
    - Info: `NEO.FORMAT`.
    - Error when certain, else warning: `NUM.COUNT_MATCHES_CHANGES`.
    - To re-check against the map: `INS.ORIENTATION`, `ACE.NOT_IN_KARYOTYPE`.
  - Requirements were handed on as notes to T-023 (result API), T-025 (harness expectations), T-029 (band notes) and T-053 (ASCII forms).

- **2026-11-18 — T-054 — Literal translation: KB-linked, partial for invalid strings, per-context readings; built after the pilot rules.**
  - **Timing:** decided before T-023 so the model keeps the meaning (event type, chromosomes, arms, bands, derivative structure, sex field, count/ploidy, cell lines/clones, uncertainty, inheritance; spans and KB entry IDs per node; partial ASTs marked). Implementation after T-030/T-031. Rejected: building it in the pilot (delays the validator) and leaving it until after the desktop pilot (the model requirements would be forgotten).
  - **Invalid strings:** partial translation with the bad parts marked. Rejected: only valid strings (no help exactly where users need it) and translating the auto-fixed form (hides what the user actually wrote).
  - **Shape:** summary + per-item sentences with spans. Rejected: breakdown only (no overview) and one paragraph (can't link phrases to spans).
  - **References:** KB entries, not book citations. Rationale: the translation stays readable; traceability is one click away through the rule browser; the book is cited in one place (the KB), which also helps the copyright cleanup (T-043); citations never get copied into templates. Diagnostics keep their book citation because they assert an error that users check against the book. Vocab items count as KB entries, because many phrases come from symbols and bands rather than rules.
  - **Context:** neutral wording, both readings where the meaning changes, firm with a hint (follows T-021). Rejected: neutral only (loses meaning, e.g. `–X`) and requiring a hint (contradicts the string-only goal).
  - **Coordinates:** optional, off by default. Rejected: always shown (clutter, and approximate band boundaries could be mistaken for measured breakpoints) and never (useful, and the data already exists).
  - **Language:** English templates in data files per edition. Rejected: strings in C# (refactor for any other language; templates can't be reviewed like KB files) and localising now (no need yet).
  - **Tests:** a person-reviewed golden file. Rejected: an `explanation` field on KB examples (re-touches six finished KB tasks and mixes test expectations into rules) and snapshot-only tests (no independent expectation).
  - **Follow-up:** T-057 (templates, schema, golden file), T-058 (renderer), T-059 (desktop view); requirements noted on T-023 and T-038.
