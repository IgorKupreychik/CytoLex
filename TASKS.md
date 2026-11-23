# CytoLex — Tasks

Statuses: `todo` · `in_progress` · `blocked` · `done` · `discussion` (a decision to make via interview).
Priority: 1 = foundation/core · 2 = pilot · 3 = later · 4 = after the ISCN 2016 core and the desktop app are complete (web service).
Run `/next-task` to pick the next startable task (or `/next-task T-xxx` for a specific one).
A task is startable when its status is `todo` or `discussion` and every task in **Depends** is `done`.
Keep the summary table and the task sections in sync (status changes are made in both places).

## Summary

| ID | Status | Pri | Depends | Title |
|---|---|---|---|---|
| T-001 | done | 1 | – | Choose language/tooling for build & extraction scripts |
| T-002 | done | 1 | T-001 | Source-text extraction script (PDF → section chunks + manifest) |
| T-003 | done | 1 | T-002 | Review extraction quality; list sections affected by underlining/tables |
| T-004 | done | 1 | T-001 | KB JSON Schema (rules, quotes, vocabularies, ID & citation conventions) |
| T-005 | done | 1 | T-004 | KB build script (validate → SQLite FTS5 + JSON bundle; review reports) |
| T-006 | done | 1 | T-001 | Band data generator from UCSC cytoBand (all resolutions, versioned) |
| T-007 | done | 1 | T-006, T-003 | Spot-check bands vs. ISCN 2016 ideograms; exceptions YAML |
| T-008 | done | 1 | T-003, T-005 | KB: ch. 2–3 (band nomenclature, symbols & abbreviated terms) |
| T-009 | done | 1 | T-008 | KB: ch. 4 (karyotype designation, short/detailed systems, derivatives) |
| T-010 | done | 1 | T-008 | KB: ch. 5–7 (uncertainty, order of abnormalities, variable features) |
| T-011 | done | 1 | T-008 | KB: ch. 8 (numerical abnormalities) |
| T-012 | done | 1 | T-008 | KB: ch. 9.1–9.2.10 (structural, part 1) |
| T-013 | done | 1 | T-008 | KB: ch. 9.2.11–9.3 (structural, part 2 incl. translocations) |
| T-014 | done | 2 | T-013 | KB: ch. 11 (neoplasia) |
| T-015 | todo | 3 | T-013 | KB: ch. 13.1–13.2 (metaphase ish) |
| T-016 | todo | 3 | T-015 | KB: ch. 13.3–13.8 (nuc ish, fib/rev ish, cgh, painting) |
| T-017 | done | 2 | T-013 | KB: ch. 14 (microarray) |
| T-018 | todo | 3 | T-013 | KB: ch. 10 & 12 (breakage, meiotic) |
| T-019 | todo | 3 | T-017 | KB: ch. 15 & 16 (RSA, sequence-based) |
| T-020 | done | 1 | T-013 | Tech stack for core library + desktop app |
| T-021 | done | 1 | T-013 | Pilot validator scope (notation types implemented in the pilot) |
| T-022 | done | 1 | T-013 | Strictness & severity policy |
| T-023 | done | 1 | T-020, T-021, T-022 | Core design: data model, version profiles, result/diagnostic API, fix model |
| T-024 | done | 1 | T-023 | Project scaffolding; update CLAUDE.md with commands |
| T-025 | done | 1 | T-024 | KB-example test harness + rule coverage report |
| T-026 | done | 1 | T-024 | String type detection & segment splitting |
| T-027 | done | 1 | T-026 | Karyotype tokenizer/parser with spans and error recovery |
| T-028 | done | 1 | T-027, T-063 | Version-profile rule engine; multi-version results |
| T-029 | done | 1 | T-028, T-007, T-065 | Rules: general + band validation |
| T-030 | done | 1 | T-029, T-065 | Rules: numerical + sex chromosomes + ordering |
| T-031 | done | 1 | T-029, T-065 | Rules: structural rearrangements |
| T-032 | done | 2 | T-031, T-068, T-014 | Rules: neoplastic (clones, idem/sl/sdl, composite) |
| T-033 | todo | 3 | T-031, T-016 | Parser + rules: FISH (ish, nuc ish) |
| T-034 | done | 2 | T-031, T-017 | Parser + rules: microarray (arr) |
| T-035 | done | 1 | T-030, T-031, T-068 | Level 1 deterministic fixes + level 3 explanations |
| T-036 | done | 2 | T-028 | Desktop: app shell + single-string check with highlighting |
| T-037 | done | 2 | T-036 | Desktop: version selector |
| T-038 | done | 2 | T-036 | Desktop: batch validation + export |
| T-039 | done | 2 | T-036, T-005 | Desktop: rule browser |
| T-040 | done | 2 | T-036, T-035 | Desktop: fix UI (diff view, Apply → re-validate) |
| T-041 | todo | 3 | T-035 | Level 2 "did you mean" suggestions |
| T-042 | todo | 3 | T-027 | Handling of underlined homologue notation |
| T-043 | discussion | 3 | – | Copyright cleanup before any public deployment |
| T-044 | todo | 4 | T-028 | Web service wrapping the core library + browser page |
| T-045 | todo | 3 | T-013 | ISCN 2020 KB + version profile |
| T-046 | todo | 3 | T-045 | ISCN 2024 KB + version profile |
| T-047 | done | 2 | – | Research CytoGPS (API, LGF output, ISCN version, source/licence, local options, terms) |
| T-048 | done | 2 | T-047 | Decide CytoGPS integration mode and data policy |
| T-049 | done | 2 | T-028, T-048 | `ExternalChecker` interface + CytoGPS adapter |
| T-050 | todo | 3 | T-031 | LGF computation in the core library |
| T-051 | done | 2 | T-049 | Comparison engine + report (no CLI) |
| T-052 | todo | 3 | T-051, T-038 | Desktop: comparison view |
| T-053 | done | 1 | T-003 | Accept ASCII `-`/`x`/`->` for printed `–`/`×`/`→`? |
| T-054 | done | 2 | T-021 | Literal translation of ISCN strings into plain language: scope, style, architecture |
| T-055 | done | 2 | T-007 | Tooling: LF line endings for committed generated files (CRLF checkout breaks the ideograms test) |
| T-056 | done | 1 | T-022 | KB: apply the strictness & severity policy to the 2016 rules |
| T-057 | done | 2 | T-023 | Translation templates + schema; golden file (ISCN 2016; drafts, review in T-082) |
| T-058 | done | 2 | T-030, T-031, T-057 | Literal translation renderer in the core library |
| T-059 | done | 2 | T-058, T-036, T-039 | Desktop: translation view |
| T-060 | done | 1 | T-023 | `kb build`: lean runtime bundle (rules metadata, vocab, bands) for the core |
| T-061 | discussion | 3 | – | Presentation & documentation material for the pilot: define what to prepare |
| T-062 | done | 2 | T-026 | Test corpus of ISCN strings per notation type (review-flagged) + mutation generator |
| T-063 | done | 1 | T-053 | KB: typed symbol forms (`-`/`x`/`->`) in the ch. 3 vocab + rule `SYM.TYPED_SYMBOL_FORMS` |
| T-064 | done | 3 | – | Merged into T-055 (line endings of generated committed files) |
| T-065 | done | 1 | T-027 | Binder: syntax tree → semantic model (meaning nodes, KbRefs, IsPartial) |
| T-066 | todo | 2 | T-065 | Band composition from the short system (derivative structure for short-form abnormalities) |
| T-067 | discussion | 3 | T-045, T-046 | Revisit genome-assembly-only bands per edition (2016 error vs. later-edition valid?) |
| T-068 | done | 1 | T-031 | Rules: ch. 9 symbol rules (split from T-031) |
| T-069 | done | 2 | T-036 | Abnormality list: core API + desktop panel |
| T-070 | todo | 3 | T-032, T-069 | Abnormality list: inherited changes of idem/sl/sdl clones (expandable reference, full form) |
| T-071 | discussion | 2 | T-034 | Arr: nucleotide positions vs. bands of the stated build (KB rule, GRCh37-under-[GRCh38] book examples) |
| T-072 | todo | 3 | T-034, T-069 | Abnormality list: arr aberrations (core API + desktop panel) |
| T-073 | done | 2 | T-029 | Stray spaces inside a karyotype (`47,XY, +21`, `46,X Y`) are not reported: KB basis and fix |
| T-074 | done | 2 | T-073, T-035 | Checks for `SYM.NO_OTHER_SPACE` and `SYM.LETTER_CASE` (stray spaces, letter case; level-1 fixes) |
| T-075 | discussion | 3 | T-047, T-073 | Severity of stray spaces and letter case (`SYM.NO_OTHER_SPACE`, `SYM.LETTER_CASE`) vs. CytoGPS behaviour |
| T-076 | todo | 3 | T-047 | Ask the CytoGPS authors: licence of the GitHub code, newer parser/server code |
| T-077 | todo | 3 | T-050, T-038 | Batch statistics per band (loss/gain/fusion share of clones, aggregate LGF table) |
| T-078 | todo | 3 | T-050, T-069 | Desktop: ideogram view of loss/gain/fusion per band and clone |
| T-079 | todo | 3 | T-050, T-038 | Export in CytoGPS's JSON result format (RCytoGPS-compatible) |
| T-080 | discussion | 3 | T-075 | Optional lenient mode (stray spaces, letter case as warnings) |
| T-081 | todo | 2 | T-049, T-036 | Desktop: CytoGPS panel in the single-string check (optional, never blocks CytoLex) |
| T-082 | todo | 3 | T-057 | Review the translation templates and golden file (ISCN 2016) |
| T-083 | todo | 3 | – | Refactor: remove hand-written partial classes (`Binder`, `Parser`) per the code standard |
| T-084 | todo | 3 | T-059 | Rule browser: bands (band table in `kb.sqlite`, band links of the translation open it) |
| T-085 | todo | 3 | T-062 | Parser: report truncated input (missing `)`, trailing sign) under a KB rule instead of `CORE.UNPARSED` |

---

## Foundation

### T-001 — Choose language/tooling for build & extraction scripts
**Status:** done
**Description:** Interview to pick the language and libraries for repo tooling: PDF extraction, KB schema validation and build, band-data generation. It doesn't have to match the app stack (T-020). Consider: PDF text libraries (`pdftotext` is available via mingw64), YAML + JSON Schema support, SQLite FTS5, Windows friendliness.
**Acceptance:**
- Decision recorded in `docs/decisions.md` (language, version, key libraries, how scripts are run).
- `CLAUDE.md` updated with how to run scripts.
**Notes:** Decided Python 3.14 + uv, pdfplumber, ruamel.yaml, jsonschema, stdlib sqlite3; single `kb` CLI (`extract`/`build`/`bands` stubs) in `tools/` with pytest + ruff. Files: `tools/` (pyproject, uv.lock, `src/cytolex_tools/{cli,config}.py`, `tests/test_cli.py`), `docs/decisions.md` (decision log), `CLAUDE.md` Commands, new `README.md`. Committed in a047c5a.

### T-002 — Source-text extraction script
**Status:** done
**Description:** A deterministic, non-AI script that reads the PDF path from `config/local.yaml` (env var override) and writes per-section chunks to `kb/source-text/2016/` (gitignored).
- Split by numbered headings; cap of ~3–5k tokens per chunk, splitting at subsections or paragraphs.
- Front-matter on each chunk: section number, title, book page range (book page = PDF page − 6).
- Fix `]` → `→` in derivation notation, record paragraph numbers, and write a manifest.
- Example strings are kept verbatim; the stray-space normalisation (`t(2; 5)`) is noted, not applied, in the source text.
**Acceptance:**
- Every TOC section (ch. 2–16) has at least one chunk.
- Re-running gives byte-identical output.
- No chunk exceeds the cap.
- Manifest lists section → file → book pages.
**Notes:** Done. `kb extract --version 2016 [--pdf] [--out]` → `kb/source-text/2016/` (135 sections, 137 chunks, max 3,936 tokens, byte-identical re-runs; TOC cross-check fails the run on mismatch). Code: `tools/src/cytolex_tools/extract/` (editions, glyphs, layout, toc, blocks, chunker); tests: `tools/tests/test_extract_units.py`, `test_extract_pdf.py` (skipped without the PDF). Citation labels `¶n`/`En` per numbered section; font-aware glyph map; ch. 1–16 extracted. Deviation: invisible zero-width spaces (813) are dropped rather than only noted, since they aren't in the printed text (see `docs/decisions.md`). Open points handed to T-003; quote-check proposal noted on T-005.

### T-003 — Review extraction quality
**Status:** done
**Description:** Spot-check chunks against the PDF. Produce a list of sections where underlining (homologue distinction), tables, figures or margin markers were lost, so those rules get `needs_review`.
**Acceptance:**
- Review report at `docs/extraction-review-2016.md` listing affected sections and pages.
- Extraction fixes applied to the script if needed.
**Notes:** Done. Review report `docs/extraction-review-2016.md`; extractor version 2 (`kb extract`), plus new `kb tables` (table images for sign-off → `build/review/tables/2016/`).
  - Recovered from the PDF's drawn objects: underlining (U+0332 after the character; §7.2, 9.2.3, 9.2.17.1, 16.2, 16.3.7) and margin change bars (front matter `changed`; ISCN 2016 §1.6).
  - Captions are float labels (`[Fig. N p.X]`, `[Table N p.X]`), not ¶; float-interrupted paragraphs rejoined; FISH legends (`●`/`○`) attached to their examples; three example lists reclassified; ` | ` placed at description columns.
  - Tables from a committed layout file `tools/src/cytolex_tools/extract/tables/2016.yaml` (Tables 1–4, ploidy table, ch. 3 symbols, §10.3 list, §16.2 alignment, §13.4 fibre diagram): text layer vs page image agree for all; person sign-off pending per table (reminders in the Notes of T-007/008/009/010/014/016/018/019). Backup plan B1–B3 documented; not needed.
  - Glyphs: `≤` verified; `GeneralSymbolsPB s` → `→`; `GeneralSymbolsPE P/S` → `●`/`○`. `–`/`×` question → T-053. No example is split across pages; wrapped long karyotypes stay as separate lines (documented).
  - Code: `extract/layout.py`, `blocks.py`, `chunker.py`, `tables.py`, `review.py`, `glyphs.py`, `cli.py`; tests `tests/test_extract_review.py` + additions in `test_extract_pdf.py`, `test_cli.py` (46 pass).
  - Deviations from the plan: beyond the planned scope, also recovered change bars (useful for 2020/2024 profiles), added a `figure` region kind, and added `kb tables` as a subcommand (instead of a separate script). Some ¶ labels were renumbered vs. version 1 (no rules cited them yet).

### T-004 — KB JSON Schema
**Status:** done
**Description:** Schemas for rule YAML files (`kb/rules/<version>/…`) and quote files (`kb/source-quotes/<version>/…`).
- Vocabularies: `applies_to`, `context`, severity, `fix` hints.
- Conventions: rule ID format (stable concept IDs) and citation format `ISCN 2016 §x.y.z, p.N, ¶M`.
**Acceptance:**
- Schemas committed.
- Sample rule + quote files validate; a deliberately broken sample fails with a clear message.
- Conventions documented in `docs/decisions.md` or `kb/README.md`.
**Notes:** Done. Schemas (JSON Schema 2020-12) in `kb/schema/` (`common`, `rules`, `quotes`); format and conventions in `kb/README.md`; decision in `docs/decisions.md`. One rule file per section, named like its chunk; quotes at the same relative path under `kb/source-quotes/`; structured citations `{section, page, para|example|float}`. New `kb check [PATH …]` (schema-only, `cytolex_tools/kb/schema.py`, reused by T-005). Samples + 9 broken fixtures in `tools/tests/fixtures/kb/`, tests in `tools/tests/test_kb_schema.py` (70 pass). Deviations: added `title`, `severity`, `related` fields; `referencing` added as a direct dependency (uv.lock changed); `kb check` resolves relative paths from the repo root. Cross-file checks handed to T-005 (see its Notes).

### T-005 — KB build script
**Status:** done
**Description:** Validates all YAML against the schemas, checks quote ↔ rule links and unique IDs, and compiles a SQLite DB (with FTS5 over rules and quotes) and a JSON bundle into `build/` (gitignored). Works with or without the quote files present.
**Acceptance:**
- One command builds everything.
- Reports list rules with `paraphrase_checked: false` and `needs_review: true`.
- Fails on schema errors, duplicate IDs or orphan quotes.
**Notes:** Proposed in T-002, not yet agreed: also check that each verbatim quote occurs in `kb/source-text/<version>/` at its cited section, page and ¶/E label (a guard against AI-invented quotes). This needs the chunks, so it should be skipped with a warning when they're absent.
From T-004: reuse `cytolex_tools.kb.schema` (`validate_file`, also behind `kb check`). Cross-file checks left to the build: file `version` = its directory, rule/quote file pairs at the same relative path, quote `rule` IDs exist, unique rule IDs per version, `related` IDs exist, cite labels/pages match the chunk front matter (when chunks exist), and rendering citations as `ISCN 2016 §…, p.N, ¶n[, ex. En]`.
**Outcome (2026-11-16):** `kb build [--version YYYY]` (default: every edition) in `tools/src/cytolex_tools/kb/build.py`, with `kb/chunks.py` (chunk front matter + label blocks, multi-part sections merged) and `kb/cite.py` (`render_cite`). Errors: schema, `version`/section vs path, wrong file kind per directory, duplicate IDs, unknown `related`, orphan quote files/quotes; with chunks also cite section/label/page and **verbatim quote text** (decided: error; skipped with a warning without chunks). Warning: valid `En` example text not found in its block. Outputs: `build/kb.sqlite` (normalised tables + FTS5 `rules_fts`/`quotes_fts`), `build/kb.json`, `build/review/kb-review.md`; nothing written on errors. Tests: `tools/tests/test_kb_build.py` (+ chunk excerpt fixture `tools/tests/fixtures/kb/source-text/`). Docs: `kb/README.md` (check table), `README.md`, `CLAUDE.md`, `docs/decisions.md`. Deviations: cross-file checks run only after all files pass the schema; empty-KB message added.

### T-006 — Band data generator
**Status:** done
**Description:** A script that downloads or reads UCSC cytoBand (GRCh37 + GRCh38) and generates versioned band data (`kb/bands/2016/…`).
- Records band names at 300/400/550/700/850 resolution (coarser levels derived by nesting) plus coordinates.
- Records the data source and version.
**Acceptance:**
- Repeatable output.
- Known bands resolve (e.g. 9q34, 9q34.1, 22q11.2, 1p36.3).
- Invented bands don't (e.g. 9q35).
**Notes:** Done (2026-11-16). `kb bands [--version] [--out] [--cache] [--cache-only] [--update-pins]` → committed `kb/bands/2016/bands.yaml`: 1,150 names (NCBI 400/553/862 at 400/550/850 + 52 nesting ancestors), GRCh37/38 coordinates (1-based closed), stain, parent, levels; byte-identical re-runs. Code: `tools/src/cytolex_tools/bands/` (`fetch`, `parse`, `generate`, `lookup`, `sources/2016.yaml` with sha256 pins); tests `tools/tests/test_bands.py` + chr22 fixtures (121 pass). Docs: `CLAUDE.md`, `README.md`, `kb/README.md`, `docs/decisions.md`. Deviation (decided with the user): NCBI Genome Decoration Page ideograms are the primary source (UCSC has only the 850 level) and UCSC cytoBand is the cross-check; 300/700 have no public source → `levels_unavailable`, handed to T-007.

### T-007 — Spot-check bands vs. ideograms
**Status:** done
**Description:** Compare generated band data against ISCN 2016 ideograms (book pp. 14–33) for a sample of chromosomes (e.g. 1, 9, 22, X). Record differences in a cited exceptions YAML.
**Acceptance:**
- Sample checked.
- Exceptions file committed, with citations.
- The generator applies the exceptions.
**Notes:** Before starting, person sign-off of **Table 3** (landmarks, p.15): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`. See `docs/extraction-review-2016.md`.
From T-006: `kb/bands/2016/bands.yaml` has the NCBI 400/550/850 band sets plus nesting ancestors; **300 and 700 have no public source** (`levels_unavailable`). Decide here whether to tag them from the book's ideograms (exceptions/level file) or leave them out. The exceptions file needs a format (and a schema), and it plugs into `cytolex_tools/bands/generate.py` (apply before `render`; bump `GENERATOR_VERSION`). Use `cytolex_tools.bands.lookup.load()` for spot checks.
**Outcome:** Done (2026-11-17). Deviation from "sample only" (decided with the user): the Fig. 5 idiogram labels (pp.16–29) are text in a figure-only font, so the new `kb ideograms` decodes all 24 chromosomes × 300/400/550/700/850 (2,894 labels) into committed `kb/bands/2016/ideograms.yaml`, with review crops in `build/review/bands/2016/`. Spot-check of chr1, 9, 22, X signed off by the user (`spot_check` in `bands/sources/2016.yaml`). Full comparison with NCBI found 21 differing regions; all are in cited `kb/bands/2016/exceptions.yaml` (schema `kb/schema/band-exceptions.schema.json`, `kb check` accepts it). Policy: the book wins; 6 NCBI-only names (6p24.1–3, 9q34.11–13) are kept with `book: false` (§15.3 uses `9q34.11`). `kb bands` (generator v2) applies the exceptions, fails on unexplained/stale differences with a draft in `build/review/bands/2016/exceptions-draft.yaml`, and adds levels 300/700 from the book plus a `book` flag. `bands.yaml`: 1,165 names, 15 book-only sub-bands without coordinates. Code: `tools/src/cytolex_tools/bands/` (`ideograms.py`, `book.py`, `common.py`; `generate.py`, `cli.py`, `kb/schema.py` changed); tests `tools/tests/test_ideograms.py` + `test_bands.py` (152 pass). Docs: `CLAUDE.md`, `README.md`, `kb/README.md`, `docs/decisions.md`, `docs/extraction-review-2016.md`; follow-up notes added to T-022 and T-029.

## Knowledge base (ISCN 2016)

All KB tasks share these steps and criteria:
- Read the relevant chunks from `kb/source-text/2016/`, never the whole book.
- Write rules (paraphrase + cite + examples + `applies_to`/`context` + optional `fix` hint) and the matching quote files.
- Mark ambiguous items `needs_review`.
- Check `docs/extraction-review-2016.md` for the chapter: listed sections get `needs_review`, and configured tables must have `review.human: ok` before rules cite them (see each task's Notes).
- Review paraphrases with the user, side by side with the quotes, and set `paraphrase_checked` for approved ones.
- Acceptance: KB build passes; every book example in the section range is captured as a valid example; invalid examples include `why` and the corrected form where one exists.

### T-008 — KB: ch. 2–3 (band nomenclature, symbols & abbreviated terms)
**Status:** done
**Description:** Band and region designation rules; full symbol/abbreviation table (ch. 3) as a vocabulary the parser can use.
**Acceptance:** Shared KB criteria; symbol vocabulary file covers every ch. 3 entry.
**Notes:** Before starting, person sign-off of the **ch. 3 symbol list** (pp.34–36; most important, it is the validator's vocabulary), **Table 2** (p.14) and **Table 3** (p.15): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`.
**Outcome (2026-11-17):** Table 2 and the ch. 3 symbol list signed off (131 rows, not 134: header lines were counted). New vocabulary format: `kb/schema/vocab.schema.json`, `kb/vocab/2016/ch03/03-symbols.yaml` (131 entries) and `ch02/02.04-banding-codes.yaml` (22); `kb build` checks tokens and coverage of the printed tables, writes SQLite `vocab`/`vocab_tokens` and `vocab` in `kb.json` (format_version 2). 19 rules (CHROM/BAND/SYM) + quotes in `kb/rules/2016/ch02–ch03/`, all paraphrases reviewed; `SYM.LISTED_ONLY` stays `needs_review` (check in T-009–T-019); `BAND.MUST_EXIST` accepted. Deviations: sign-off moved before planning (skill updated); vocab files instead of one rule per symbol; book's hypothetical `1p31.11`/`1p31.12` kept as format-only examples. Follow-up: T-055 (CRLF test failure).

### T-009 — KB: ch. 4 (karyotype designation)
**Status:** done
**Description:** General principles, breakpoint specification, short and detailed systems, derivative and recombinant chromosomes (§4.1–4.5).
**Acceptance:** Shared KB criteria.
**Notes:** Before starting, person sign-off of **Table 4** (p.44): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`. §4.3.2–4.4 also depend on the Fig. 8/9 diagrams (images): check the printed page, `needs_review` (`docs/extraction-review-2016.md`).
**Outcome (2026-11-17):** Table 4 (p.44, 16 rows) signed off before planning. 44 rules + quotes in `kb/rules/2016/ch04/` and `kb/source-quotes/2016/ch04/` (12 section files, §4.1–4.5), prefixes `KARYO`, `STRUCT`, `BREAK`, `DER`, `REC`, plus `GEN.GENOME_BUILD_REQUIRED`; all paraphrases approved by the user. All 35 printed example strings (19 example-block lines + 16 Table 4 rows) and the inline e.g. strings are valid examples; `kb build` passes with chunk checks (63 rules, 89 quotes). Fig. 8/9 and the p.46 line-wrap spaces were checked on the printed pages: no rule depends on the diagrams (`docs/extraction-review-2016.md` updated). Also: §4.1 ¶9 cite added to `SYM.SPACE_BEFORE_CHROMOSOME_COUNT`; `SYM.LISTED_ONLY` notes ch. 4 checked (all symbols listed). Decision entry in `docs/decisions.md`. Deviation: `REC.KNOWN_PARENTAL_REARRANGEMENT` is `needs_review` too (with `KARYO.ABBREVIATED_FORM`), both handed to T-022 (note added there). Severities are provisional until T-022.

### T-010 — KB: ch. 5–7
**Status:** done
**Description:** Uncertainty (`?`, `~`, `or`, `inc`), order of abnormalities, normal variable features (heteromorphisms, fragile sites).
**Acceptance:** Shared KB criteria.
**Notes:** Before starting, person sign-off of **Table 1** (heteromorphisms, p.12): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`. §7.2 has underlined homologues (U+0332).
**Outcome (2026-11-17):** Table 1 (p.12, 2 parts, 14 rows + footnotes) signed off before planning. 19 rules + quotes in `kb/rules/2016/ch05–ch07/` and `kb/source-quotes/2016/ch05–ch07/` (9 section files), new prefixes `UNCERTAIN` (?, ~, or, inc), `ORDER` (§6), `VAR` (§7.1), `FRA` (§7.2); all paraphrases approved by the user. Every example block and inline example list in §5.1–7.2 is a valid example; `kb build` passes (82 rules, 113 quotes). Table 1 (decided with the user) is one info rule, `VAR.POPULATION_INVERSIONS`, holding its 7 inversion variants, not a vocabulary. Changes to earlier rules: `BREAK.ALTERNATIVE_BANDS` lost its invalid `1q21 or q24` (§5.3 E1 `add(19)(p13 or q13)` is valid; re-approved); `KARYO.ABNORMALITY_ORDER` cites §6 ¶1 + E2; `DER.ABERRATION_ORDER` gains §6 E8; `KARYO.NO_ARM_LENGTH_SIGN` cites §5.1 E7; `SYM.LISTED_ONLY` notes ch. 5–7 checked. `needs_review`: `ORDER.SHORTEST_STRING_PREFERRED` (T-022, note added there) and `FRA.HOMOLOGUE_UNDERLINING` (T-042). Book fragments with `…` kept as printed (T-025 must treat them as fragments). Notes added to T-012 and T-022; decision entry in `docs/decisions.md`.

### T-011 — KB: ch. 8 (numerical abnormalities)
**Status:** done
**Description:** General principles, sex chromosome and autosomal abnormalities, UPD.
**Acceptance:** Shared KB criteria.
**Notes:**
**Outcome (2026-11-17):** No sign-off needed. 8 new rules + quotes in `kb/rules/2016/ch08/` and `kb/source-quotes/2016/ch08/` (§8.1, §8.2, §8.4), new prefixes `NUM` (PLOIDY_REFERENCE, PLOIDY_IN_ANGLE_BRACKETS, COUNT_MATCHES_CHANGES), `SEX` (CONSTITUTIONAL_COMPLEMENT, ACQUIRED_GAIN_LOSS), `UPD.FORMAT`, `MAR.CONSTITUTIONAL_C_SPACE`, plus `KARYO.UNCERTAIN_CONSTITUTIONAL` (`?c`); all paraphrases approved by the user. All 35 ch. 8 example blocks are valid examples; `kb build` passes (90 rules, 126 quotes). Deviation: §8.3 has no rule file. Its content is already in ch. 4, so `KARYO.GAIN_LOSS_SIGN` (§8.1 ¶1, §8.3 ¶1, E1–E4) and `KARYO.CONSTITUTIONAL_C` (§8.2 ¶4, §8.3 ¶2, 7 examples; statement extended with the `mar c` exception and re-approved) were extended instead; `KARYO.CLONE_ORDER_CONSTITUTIONAL` gains §8.1 E4 and §8.4 E2. `needs_review`: `NUM.COUNT_MATCHES_CHANGES` (an inference, recorded as the user decided), `SEX.ACQUIRED_GAIN_LOSS` and `UPD.FORMAT`; notes added to T-022 and T-030. `SYM.LISTED_ONLY` notes that the ploidy symbols `n`/`2n`/`4n` are not listed. Decision entry in `docs/decisions.md`.

### T-012 — KB: ch. 9.1–9.2.10 (structural, part 1)
**Status:** done
**Description:** General principles; add, del, der, dic, dup, fission, fra, hsr, ins, inv.
**Acceptance:** Shared KB criteria.
**Notes:** From T-010: the general fra format belongs to §9.2.7 (`FRA.*` prefix; `FRA.HERITABLE_VARIANT` and `FRA.HOMOLOGUE_UNDERLINING` from §7.2 exist); §9.2.3 has the `der(?)` examples that §5.1/§6 refer to (`UNCERTAIN.QUESTION_MARK_DESIGNATION`, `ORDER.UNKNOWN_CENTROMERE_DER`).
**Outcome (2026-11-17):** No sign-off needed. 26 new rules + quotes in `kb/rules/2016/ch09/` and `kb/source-quotes/2016/ch09/` (11 section files, §9.1–9.2.10), new prefixes `ADD`, `DEL`, `DIC`, `DUP`, `FIS`, `HSR`, `INS`, `INV`, plus `FRA.FORMAT`, `REC.NOT_ACQUIRED`, `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST` and nine `DER.*` rules; all paraphrases approved by the user. All 90 example blocks (149 printed lines) are valid examples (script-checked); `kb build` passes (116 rules, 181 quotes). "Several aberrations in one chromosome → der" is one rule (`DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME`, 5 cites). 15 earlier rules gained §9 cites/examples; re-approved statement changes: `KARYO.PLOIDY_RELATIVE`, `KARYO.REPLACED_NORMAL_NOT_MISSING`, `BREAK.NOT_REPEATED_FOR_COPIES`, `STRUCT.PROXIMAL_BREAKPOINT_FIRST` (dup/ins exception). `needs_review`: `DER.HOMOLOGUE_UNDERLINING` (T-042). Printed oddities (§9.2.5 E2 "or" form, §9.2.3 E22 line wrap, ¶6 line-wrap space) handled as noted in `docs/decisions.md`. Notes added to T-013, T-022, T-042; `docs/extraction-review-2016.md` updated. No deviations from the plan.

### T-013 — KB: ch. 9.2.11–9.3 (structural, part 2)
**Status:** done
**Description:** i, mar, neo, qdp, r, tas, translocations (reciprocal, whole-arm, Robertsonian, jumping), trc, trp, multiple copies of rearranged chromosomes.
**Acceptance:** Shared KB criteria; rules affected by lost underlining are flagged (see T-003).
**Notes:** From T-012: §9.2.3 ¶8 (ider) points to §9.2.11 - link `DER.ISODERIVATIVE` from the isochromosome rule; the sex-field convention for rearranged X/Y is `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST` (add translocation examples such as `46,Y,t(X;…)`); `DIC.FORMAT` refers to whole-arm/Robertsonian translocations (§9.2.17.2–3) for the missing-chromosome convention; `KARYO.REPLACED_NORMAL_NOT_MISSING` and `STRUCT.PROXIMAL_BREAKPOINT_FIRST` (dup/ins exception) should get the §9.2.11–9.3 examples that apply.
**Outcome (2026-11-17):** No sign-off needed. 24 new rules + quotes in `kb/rules/2016/ch09/` and `kb/source-quotes/2016/ch09/` (13 section files, §9.2.11–9.3; none for the §9.2.17 heading), new prefixes `ISO`, `MAR`, `DMIN`, `ACE`, `NEO`, `QDP`, `RING`, `TAS`, `TRANSLOC`, `ROB`, `TRC`, `TRP`, plus `DER.WHOLE_ARM_TWO_CHROMOSOMES` and `KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY`; all paraphrases approved by the user. All 69 example blocks are valid examples (script-checked); `kb build` passes (140 rules, 220 quotes). Translocation chromosome order stays in `STRUCT.CHROMOSOME_ORDER`/`STRUCT.MULTI_CHROMOSOME_ORDER` (§9.2.17.1 cites added); T-013 notes done (`DER.ISODERIVATIVE` ↔ `ISO.FORMAT`, `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST` now cites the explicit §9.2.17.1 E3 statement, `DIC.FORMAT` linked to the whole-arm/rob rules, `KARYO.REPLACED_NORMAL_NOT_MISSING` gained §9.2.11–9.3 examples). Re-approved statement changes: `STRUCT.PROXIMAL_BREAKPOINT_FIRST` and `STRUCT.SHORT_ARM_BREAKPOINT_FIRST` (band order free where it shows orientation: trp, complex t, rings, tas, trc). `needs_review`: `TRANSLOC.HOMOLOGUE_UNDERLINING` (T-042) and `TRP.FORMAT` (§9.2.19 ¶1 vs E2). Inferred invalid examples are noted per rule. Notes added to T-014, T-022, T-031, T-042; `docs/decisions.md` and `docs/extraction-review-2016.md` updated. No deviations from the plan.

### T-014 — KB: ch. 11 (neoplasia)
**Status:** done
**Description:** Clone definition and size, mainline, stemline/sideline, idem vs. sl/sdl, composite karyotype, unrelated clones, modal number, constitutional karyotype in neoplasia.
**Acceptance:** Shared KB criteria.
**Notes:** Before starting, person sign-off of the **Ploidy table** (p.90; its ranges become numeric limits — check every number): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`. Indentation/bold of the printed table are lost (`docs/extraction-review-2016.md`). From T-013: `TRANSLOC.JUMPING` (§9.2.17.4) points to §11.1.6 for the order of unrelated clones (decreasing frequency); link it to the clone-order rule written here.
**Outcome (2026-11-20):** Ploidy table (p.90, 48 cells) signed off before planning. 18 new rules + quotes in `kb/rules/2016/ch11/` and `kb/source-quotes/2016/ch11/` (8 section files, §11.1.1–11.3), new prefixes `CLONE` (11), `CP` (3), `PLOIDY` (2), plus `SEX.UNEVEN_PLOIDY_MALE` and `KARYO.CONSTITUTIONAL_ALWAYS_GIVEN`; all paraphrases approved by the user. `kb build` passes (160 rules, 262 quotes); `dotnet test` 3633 total, 0 failed. Decided with the user: Ploidy table as the info rule `PLOIDY.LEVELS`; `CLONE.MIN_CELLS` and `CLONE.ORDER_UNRELATED` are warnings (§11.1.6 E3 kept as an invalid warning form). Existing rules extended: `KARYO.CLONE_SEPARATOR`, `KARYO.CONSTITUTIONAL_C`, `MAR.CONSTITUTIONAL_C_SPACE`, `KARYO.ABNORMALITY_ORDER`, `NUM.PLOIDY_REFERENCE`, `KARYO.CLONE_ORDER_CONSTITUTIONAL`, `SYM.LISTED_ONLY`; `TRANSLOC.JUMPING` linked to `CLONE.ORDER_UNRELATED`. Deviations (decided with the user during the work): the §11.1.4 E7 idem form is an invalid example of `KARYO.ABNORMALITY_ORDER` (printed out of order); `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST` gained a cited c-field exception (§11.3 E9, re-approved) with a one-line fix in `ChromosomeCount.cs` and a unit test; new test-side `KbExamples/PendingExamples.cs` skips 3 examples the parser can't read yet (handed to T-032); invalid examples classified in `InvalidExampleExpectations.cs`. Notes added to T-032; `docs/decisions.md`, `docs/extraction-review-2016.md`, `CLAUDE.md`, `README.md` updated.

### T-015 — KB: ch. 13.1–13.2 (metaphase ish)
**Status:** todo
**Description:** In situ hybridization introduction, `ish` notation, `dim`/`enh`, subtelomeric ish.
**Acceptance:** Shared KB criteria.
**Notes:**

### T-016 — KB: ch. 13.3–13.8
**Status:** todo
**Description:** `nuc ish` (signal numbers, relative positions, fusion and break-apart probes), `fib ish`, `rev ish`, `cgh`, multi-color and partial painting.
**Acceptance:** Shared KB criteria.
**Notes:** Before starting, person sign-off of the **Fibre diagram** (§13.4, p.112): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`. Drawn fibre symbols and nucleus diagrams (§13.3.2, pp.109–112) are not in the text: `needs_review`.

### T-017 — KB: ch. 14 (microarray)
**Status:** done
**Description:** `arr` nomenclature, genome build requirement, SNP-array specifics, complex results.
**Acceptance:** Shared KB criteria.
**Notes:** **Outcome (2026-11-20):**
- KB: 17 new `ARR` rules in `kb/rules/2016/ch14/` (§14.1, §14.2, §14.2.1, §14.2.2) with quotes in `kb/source-quotes/2016/ch14/`. All paraphrases were approved by the user. 13 are errors; 4 are info (`SHORT_AND_DETAILED_SYSTEMS`, `RESULT_TABLE`, `STRUCTURE_NOT_SHOWN`, `GENOME_BROWSER_BANDS`). `COMBINED_WITH_OTHER_METHODS` is `applies_to: [general]` and holds the combined karyotype/ish + arr strings.
- All 73 printed strings are valid examples. The exception is §14.2 E27 (misprinted band 18q22.33): its printed form is an invalid example of `BAND.MUST_EXIST`, and the corrected form is a valid example of `ARR.ABERRATION_ORDER`.
- Changes to existing rules:
  - `GEN.GENOME_BUILD_REQUIRED` extended (placement, space, exceptions) and re-approved.
  - `SYM.NO_SPACE_AT_PARENTHESIS` gained an arr example; `SYM.LISTED_ONLY` gained a ch. 14 note.
  - `related` links from `KARYO.PARENTAL_ORIGIN`, `UPD.FORMAT` and `UNCERTAIN.OR_ALTERNATIVE`.
- Printed-form decisions (with the user):
  - the spaces printed in `) ×n` are normalized;
  - line wraps are joined;
  - GRCh37 positions under `[GRCh38]` (E23/E24, E34, E40, E44, E45) are kept as printed and noted for T-034.
- Tests:
  - the harness skips a non-valid case whose string has no implemented segment (`ExampleRun.NotImplemented`, `ValidatorAdapter`);
  - `ParseHelper.KbExamples()` skips strings with no karyotype segment;
  - one `InvalidExampleExpectations` entry added.
- Results: `kb build` 177 rules / 288 quotes; `dotnet test` 3797 total, 0 failed, 81 skipped.
- Docs: `docs/decisions.md`, `docs/extraction-review-2016.md`, `CLAUDE.md`/`README.md` (harness skip), T-034 notes.
- Deviations from the plan:
  - combined strings moved under the general rule;
  - the E27 misprint and GRCh37 findings came from a band-coordinate check during the work.

### T-018 — KB: ch. 10 & 12 (breakage, meiotic)
**Status:** todo
**Description:** Chromatid/chromosome aberrations and scoring; meiotic terminology.
**Acceptance:** Shared KB criteria.
**Notes:** Before starting, person sign-off of **Aberration groups** (§10.3, p.83): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`.

### T-019 — KB: ch. 15 & 16 (RSA, sequence-based)
**Status:** todo
**Description:** Region-specific assays (`rsa`, MLPA) and sequence-based nomenclature (`seq`).
**Acceptance:** Shared KB criteria.
**Notes:** Before starting, person sign-off of the **Alignment figure** (§16.2, p.126): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`. Its shading/bold/italic carry meaning and are lost: `needs_review`.

### T-056 — KB: apply the strictness & severity policy
**Status:** done
**Description:** Apply the T-022 policy (`docs/decisions.md` → "Strictness & severity") to the ISCN 2016 rules:
- Re-check every rule's `severity` against the wording map (mandatory → error, preference → warning, explanation → info).
- Apply the dispositions listed in the T-022 decision log entry (info when triggered, conditional, warning, capped while `needs_review`, `NUM.COUNT_MATCHES_CHANGES` error-when-certain).
- Document in `kb/README.md`: the `needs_review` cap at warning, the "info only when the notation is present" convention for rules a string can't verify, and how a rule states "error when certain, else warning" (add a schema field only if needed).
- Run `kb build`.
**Acceptance:** Every severity matches the wording map or has a recorded reason; `kb build` passes; the changed severities are listed in the task's Notes.
**Notes:** Should be done before T-029 (rule implementation); proposed to add it to T-029's Depends.
Changed severities (ISCN 2016): `NUM.COUNT_MATCHES_CHANGES` warning → error; `ORDER.SHORTEST_STRING_PREFERRED` info → warning; `REC.KNOWN_PARENTAL_REARRANGEMENT` warning → info; `KARYO.MOSAIC_CHIMERA_PREFIX` info → error. `needs_review` cleared (with the user) on `NUM.COUNT_MATCHES_CHANGES`, `ORDER.SHORTEST_STRING_PREFERRED`, `REC.KNOWN_PARENTAL_REARRANGEMENT`, `KARYO.ABBREVIATED_FORM` and `SEX.ACQUIRED_GAIN_LOSS`. Reasons recorded in `review.note` for 16 rules whose severity departs from the literal wording; the `UPD.FORMAT`/`TRP.FORMAT` notes updated (still capped). Details: `docs/decisions.md` (T-056 log entry). Outcome (2026-11-18): done. Files: 19 rule files in `kb/rules/2016/`, `kb/README.md` (new Severity section), `kb/schema/common.schema.json` (description), `docs/decisions.md`, `docs/design/core.md` (47,XXX example). Deviations: the E6 long form of `ORDER.SHORTEST_STRING_PREFERRED` moved to `invalid` (a warning fires on it; also classified in `InvalidExampleExpectations.cs`); the KB-example harness accepts a cannot-verify info on a rule's own valid example (`ExampleDiagnostic.CannotVerify`, new test). T-056 not yet added to T-029's Depends (proposed).

## Decisions before code

### T-020 — Tech stack for core library + desktop app
**Status:** done
**Description:** Interview to choose the language/framework for the core library and desktop app.
- Inputs: an error-recovering parser with spans; desktop now and web service later; loading the KB bundle at runtime; batch performance; Windows environment.
- Postponed during initial planning until scope was clear.
**Acceptance:** Decision and rationale in `docs/decisions.md`.
**Notes:** Decided C#/.NET 10 LTS core library with a hand-written lexer + recursive-descent parser; Avalonia + AvaloniaEdit desktop with CommunityToolkit.Mvvm; xUnit v3; ASP.NET Core for the later web service (default, not asked). Recorded in `docs/decisions.md` (Product & architecture + 2026-11-17 log entry with rejected alternatives and the proposed T-024 layout); `CLAUDE.md`/`README.md` status updated; note added to T-024. No deviations from the plan.

### T-021 — Pilot validator scope
**Status:** done
**Description:** Which notation types and contexts the pilot validator implements (e.g. constitutional karyotype only, or also neoplastic, ish, nuc ish, arr). Informed by the KB built so far.
**Acceptance:** Decision in `docs/decisions.md`; T-032..T-034 priorities adjusted.
**Notes:** Decided: pilot = karyotype ch. 2–9 (constitutional + single-line acquired, numerical + structural, mos/chi), other types `not-implemented` per segment; model designed for all types (T-023). Context never required: inferred from clear markers, else conditional diagnostics with per-context fixes, optional context hint. Order after the pilot: clones → arr → FISH (T-015, T-016, T-033 moved to Pri 3). Recorded in `docs/decisions.md` (Pilot scope section + log entry); README status updated; notes added to T-022, T-023, T-025, T-026, T-035, T-054. No deviations from the plan.

### T-022 — Strictness & severity policy
**Status:** done
**Description:**
- When input that is non-canonical but unambiguous (stray spaces, letter case) is an error vs. a warning.
- How book-ambiguous rules are reported.
- What "valid" means when there are only warnings.
**Acceptance:** Policy in `docs/decisions.md`.
**Notes:** From T-007: include the severity for band names that exist in the genome assemblies but not in the book's idiograms (`book: false` in band data, e.g. 9q34.12 in a karyotype string). From T-009: severities in the ch. 4 rules are provisional ("always/is/must/should not" → error, descriptive → info); decide `KARYO.ABBREVIATED_FORM` (abbreviations such as `46,XX,der(5)mat` and copies without breakpoints are only valid after an earlier full form, which a single string can't show) and `REC.KNOWN_PARENTAL_REARRANGEMENT` (warn on `rec` without mat/pat/inh?), both `needs_review`. From T-010: `ORDER.SHORTEST_STRING_PREFERRED` (§6 E6 note: `+inv(3)(…)` preferred over `+3,inv(3)(…)`; info hint or nothing?), `needs_review`. From T-011: `UPD.FORMAT` (is `upd(n)` without `mat`/`pat` valid? every book example has one), `SEX.ACQUIRED_GAIN_LOSS` / `SEX.CONSTITUTIONAL_COMPLEMENT` (the same string, e.g. `47,XXX` or `45,X,–X`, is valid in one context and not the other: how to report when the context is unknown), severity of `NUM.COUNT_MATCHES_CHANGES` (provisional warning), all `needs_review` where noted. From T-012: `ADD.FORMAT` (add recommended over `t(n;?)`) is a `warning`; `REC.NOT_ACQUIRED` and `REC.KNOWN_PARENTAL_REARRANGEMENT` need the clinical context (constitutional vs neoplastic) or parental information that a string may not carry; several §9 conventions are `info` explanations that cannot be checked from the string (`ADD.INSERTED_UNKNOWN_USE_INS`, `DER.INSERTION_POINT_CONVENTION`, `DIC.PSEUDODICENTRIC_ORDER`, `INS.ORIENTATION` long form recommended). From T-013: decide `MAR.NO_SIZE_MORPHOLOGY` (warning, "discouraged"), `ACE.NOT_IN_KARYOTYPE`, `NEO.FORMAT` (info), the context-only `ROB.NOT_ACQUIRED` and `TRANSLOC.JUMPING`, and whether short-form trp band order carries orientation (`TRP.FORMAT`, `needs_review`: §9.2.19 ¶1 vs E2). From T-021: the context question is settled: when context is unknown, context-dependent rules give **conditional** diagnostics ("valid if constitutional; if acquired …"), never a firm error. Decide their severity/display and how they count towards "valid".
**Outcome (2026-11-18):** Decided: valid = no errors (warnings/info listed with counts); severity from the book's wording (mandatory → error, preference → warning, explanation → info); spaces/case → error + level-1 fix; `needs_review` rules capped at warning ("book ambiguous"); uncertain checks → warning (`NUM.COUNT_MATCHES_CHANGES`: error when certain); conditional diagnostics carry per-context severity, valid if one context has no errors; rules a string can't verify → info only when the notation is present; `book: false` bands → info in karyotype/ish, silent in arr/seq. Recorded in `docs/decisions.md` (new section, result-format bullet, log entry with per-rule dispositions); README sentence; new task T-056 (apply the policy to the KB); notes added to T-023, T-025, T-029, T-053. No deviations from the plan; T-056 not yet added to T-029's Depends (proposed).

### T-053 — Accept ASCII `-`/`x`/`->` for printed `–`/`×`/`→`?
**Status:** done
**Description:** The book prints the minus sign as an en dash (`–5`, `arr(1–22)`), copy numbers with `×` (`×2`) and the arrow as `→`; users usually type `-`, `x` and `->`. The ch. 3 symbol list itself gives "arrow (→ or ->)" (p.34). Decide whether the validator treats the ASCII forms as equivalent, as a warning with a level-1 fix, or as an error, per edition. Found in T-003 (`docs/extraction-review-2016.md`, "Glyphs and characters"). Related: T-022 (strictness policy).
**Acceptance:** Decision in `docs/decisions.md`; the KB rule(s) to write are named.
**Notes:** From T-022: non-canonical but unambiguous input is an `error` with a level-1 fix; apply that here unless the book allows the ASCII form (the ch. 3 list gives "→ or ->", p.34).
Outcome (2026-11-18): Decided that `->`, `-` (and U+2212) and lowercase `x` in multiplier position are equivalent to the printed `→`, `–`, `×` in ISCN 2016, with no diagnostic; uppercase `X` as a multiplier stays a letter-case error with a level-1 fix. Recorded in `docs/decisions.md` (T-053 log entry). KB work named and done in T-063 (vocab tokens + `equivalent_forms`, rule `SYM.TYPED_SYMBOL_FORMS`). No deviations.

### T-054 — Literal translation of ISCN strings into plain language
**Status:** done
**Description:** Render a valid ISCN string as plain-language text describing what it literally says, e.g. `46,XY,del(5)(q13q33)` → "46 chromosomes, XY sex chromosomes. Interstitial deletion of the long arm of chromosome 5 between bands 5q13 and 5q33." **Literal translation only:** no clinical interpretation or disease association (out of scope; a separate decision if ever wanted). Must work for any string the parser accepts, not only the book's examples. Questions to decide:
- Scope: which notation types are translated in the pilot (follows T-021) and how partially valid strings are handled (translate the valid parts + list errors, or refuse).
- Output style: a short summary and/or a per-abnormality breakdown; wording per context (constitutional vs. neoplastic: "46,XY" in a tumour sample is not "a male person"); clones, `idem`/`sl`/`sdl`, uncertainty (`?`, `~`, `or`).
- Optional detail from band data: genomic coordinates and approximate size per segment (GRCh37/38; book-only bands have none); link to LGF (T-050).
- Traceability: each phrase tied to the KB rule ID(s) of the notation it renders, with citation; version-aware like validation.
- Architecture: a renderer in the core library working on the parser's structured output (T-027). T-023 must keep enough meaning in that model (event type, chromosomes, arms, breakpoints, derivative structure, clone membership).
- Language(s): English only for the pilot, or localisable templates.
- Tests: book examples with their printed explanations as expected outputs (KB tasks T-008+ should capture those explanations), plus a reviewed set of non-book strings.
**Acceptance:** Decision in `docs/decisions.md`; follow-up `todo` task(s) for the implementation added with Pri and Depends; T-023 requirements noted.
**Notes:** Raised 2026-11-17 (user). Should be decided before T-023 (core design), so the data model supports it. From T-021: the pilot translation scope is karyotype (ch. 2–9). Use neutral wording when the context is unknown ("three X chromosomes", not a syndrome), or give both readings where context changes the meaning.
**Outcome (2026-11-18):** Decided: designed now, built after the pilot rules (karyotype ch. 2–9 first); strings with errors get a partial translation with the bad parts marked; summary line + one sentence per item with spans; the translation links to KB entries (rule or vocab item, version-specific, via the rule browser) instead of book citations, while diagnostics keep theirs; neutral wording with both readings where context changes the meaning; optional GRCh37/38 coordinates (off by default); English templates in data files per edition; a person-reviewed golden file for tests. Recorded in `docs/decisions.md` (new "Literal translation (T-054)" section + log entry); T-023 requirements added to its Notes; note on T-038; new tasks T-057 (templates, schema, golden file), T-058 (renderer), T-059 (desktop view). Deviation: the interview added the KB-link-instead-of-citation rule (user's proposal).

### T-057 — Translation templates + schema; golden file (drafts)
**Status:** done
**Description:** Data side of the literal translation (T-054, `docs/decisions.md` → "Literal translation"):
- Phrase templates per ISCN edition, keyed by KB entry (rule ID or vocab item), English for the pilot; proposed location `kb/translation/<version>/`.
- A JSON Schema for them in `kb/schema/`; `kb check` validates them, `kb build` checks that every key is an existing KB entry and puts them into the bundle.
- A golden file for ISCN 2016: string → expected summary, items (with spans) and KB IDs. Seeded from book examples by comparing with the printed explanation (cited, not quoted) plus non-book strings; each entry carries a person-review flag and counts only once reviewed.
**Acceptance:** Templates cover every karyotype ch. 2–9 construct of the T-023 model; `kb build` passes with them; golden file has entries for the book examples of ch. 4, 8 and 9 plus at least 20 non-book strings (drafts: the person review moved to T-082, decided with the user 2026-11-21); `CLAUDE.md`/`README.md` updated for the new folder.
**Notes:** Created 2026-11-18 from T-054. 2026-11-21 (user): no review in this task; every entry is a draft (`review.checked: false`), usable by T-058/T-059 as it is; the review is T-082.
**Outcome (2026-11-21):** `kb/translation/2016/templates.en.yaml` (69 templates, one per karyotype construct of ch. 2–9, keyed by vocab item or rule, forms per variant, `readings` only where the context changes the meaning) and `kb/translation/2016/golden.yaml` (398 entries: 372 book examples of ch. 4, 8, 9 paraphrasing the printed explanations + 26 synthetic strings; all `review.checked: false`); schemas `kb/schema/translation-templates.schema.json` and `translation-golden.schema.json`. `kb check`/`kb build` (`tools/src/cytolex_tools/kb/schema.py`, `build.py`) check keys, construct coverage, placeholders, `when`, spans (UTF-16), KB refs and cites; templates go into `kb-runtime.json` and `kb.json`, the golden file into `kb.json` only (additive, no format bump); review report counts unreviewed entries. C#: `tests/CytoLex.Core.Tests/Translation/` (`GoldenFile.cs` item list, `GoldenFileTests` = spans/refs match the binder + synthetic entries valid, opt-in `GoldenDraftTests` skeleton writer with `CYTOLEX_GOLDEN_DRAFT[_INPUTS]`). pytest: 28 new tests. Docs: `kb/README.md` → Translation, `docs/decisions.md` (T-057 log entry), `docs/design/core.md` §6, `CLAUDE.md`, `README.md`, comparison doc. Deviations: review deferred to T-082 (user); golden top-level key `golden` (not `entries`, which marks vocab files); the 4 `arr` examples of ch. 4 left out (karyotype only); some template wordings aligned to the golden sentences.

### T-058 — Literal translation renderer
**Status:** done
**Description:** Renderer in the core library working on the T-023 model: summary line + one sentence per item, each with its span and KB entry links (title shown, ID on hover; no book citation); partial translation of strings with errors (bad parts marked "not translated, see diagnostic"); neutral wording when the context is unknown, both readings where context changes the meaning, a single reading with a hint; optional approximate GRCh37/38 coordinates and sizes from `kb/bands` ("no coordinates" for book-only bands). Per edition.
**Acceptance:** All golden-file entries (T-057, `kb/translation/2016/golden.yaml`; drafts count too) pass in the normal test command; strings with errors give a partial translation.
**Notes:** Created 2026-11-18 from T-054. LGF link (T-050) is a later addition. From T-057 (2026-11-21): read the templates from the runtime bundle (`editions[v].translation.templates.en`, format in `kb/README.md` → Translation) and the golden file from the embedded `kb.json`; reuse `tests/CytoLex.Core.Tests/Translation/GoldenFile.cs` (item list = `GoldenFile.Items`). Pass each phrase's template `review.checked` through, so T-059 can mark draft wording. The clone references `idem`/`sl`/`sdl`/`cp` (ch. 11) and `…` have no construct yet: add schema enum values + templates when the renderer covers them. Where a golden sentence and its template differ in wording, the golden file wins (adjust the template).
**Outcome (2026-11-22):** Renderer in `src/CytoLex.Core/Translation/`, opt-in via `ValidationOptions.IncludeTranslation` (+ `IncludeCoordinates`) → `EditionResult.Translation` (`TranslationResult`: summary, items with span/sentence/readings/KB links with titles/diagnostic indices/coordinates, banner, `ReadingBasis`, draft flags). Templates read by `Kb/TranslationTemplates.cs` (`IEditionKb.Translation`); `VocabEntry` now carries `Meanings`/`Title`. Structure (no partial classes): `KaryotypeRenderer` entry point over `RenderContext` + `HeaderRenderer`, `ItemRenderer`, `SymbolDescriber`, `DerivativeDescriber`, `CompositionRenderer`, `BandPhrases`, `ChromosomePhrases`, `Phrasing`; `TranslationBuilder` builds the item list, links, partial parts and coordinates. Data: `templates.en.yaml` rewritten (forms per construct; new constructs `composite`, `clone_reference`; schema enum extended); `golden.yaml`: 58 draft entries (67 texts) normalized to the renderer's wording, old wording in `review.note`. Tests: `RendererGoldenTests` (exact match on all 398 entries + opt-in `WriteDifferenceReport` with `CYTOLEX_GOLDEN_DIFF`), `TranslationTests` (readings, partial translation, links, coordinates, JSON, every KB example rendered); `GoldenFile.cs` reads sentences/readings. Docs: `CLAUDE.md`, `README.md`, `kb/README.md`, `docs/design/core.md` §11, `docs/decisions.md`, comparison doc; T-059 note. Decisions with the user: exact match + normalize; only syntax-damaged parts untranslated. Deviations: result type named `TranslationResult` (namespace clash); list joining stays in code (English); renderer restructured from partial classes into composed classes, code standard "avoid partial classes" added to `CLAUDE.md`, follow-up T-083 (Binder/Parser partials).

### T-059 — Desktop: translation view
**Status:** done
**Description:** Show the translation next to the diagnostics: hovering an item highlights its span (and vice versa), KB links open the version-specific entry in the rule browser, a coordinates toggle (off by default) with a GRCh37/38 choice, and the partial-translation banner.
**Acceptance:** Book examples show their translation; hover ↔ span highlighting and KB links work.
**Notes:** Created 2026-11-18 from T-054. From T-039 (decided with the user, 2026-11-20): the rule browser lists rules only; add the vocab entries (symbols, banding codes: `vocab`/`vocab_tokens` in `build/kb.sqlite`) to it here, so translation links to vocab items have a target (`RuleStore`, `RuleBrowserViewModel`). From T-057 (decided with the user, 2026-11-21): show a small "draft wording" note on translations built from templates with `review.checked: false` (all of them until T-082). From T-058 (2026-11-22): validate with `ValidationOptions.IncludeTranslation = true` (and `IncludeCoordinates` for the toggle) and read `EditionResult.Translation` (`TranslationResult`: `Summary`, `Items` with `Span`/`Sentence`/`Readings`/`Links` (`KbLink(Ref, Title)`)/`IsTranslated`/`DiagnosticIndices`/`Coordinates` (both GRCh37 and GRCh38; pick one in the UI), `Banner`, `ReadingBasis`, `IsDraft` per summary, item and result); format in `docs/design/core.md` §11 "As built (T-058)".
**Outcome (2026-11-22):** Desktop translation view. The right-hand column of each edition panel has the tabs "Abnormalities" (default) | "Translation" (the chosen tab is kept while typing); both result columns are full-height panels with aligned headers ("Diagnostics (n)", "No findings."). Translation tab: partial-translation banner, draft-wording note, reading note, summary card and one card per part (sentence, both readings, KB links as pills, links to the diagnostics inside the part, coordinate lines). Hover both ways (editor offset → narrowest part + span shading; card → span). Rule and vocab links open the rule browser; band links are tooltip-only (coordinates). "Genomic coordinates" toggle (off by default; re-validates with `IncludeCoordinates`) and GRCh38 (default)/GRCh37 choice (re-renders only). Rule browser: vocab items listed after the rules with their own detail (`Rules/VocabStore.cs`, `RuleHit.Kind`, `RuleStore.Vocab`, `VocabDetailViewModel`, `OpenVocab`/`OpenKbRef`); `kb.sqlite` `vocab.section` column (additive, `tools/src/cytolex_tools/kb/build.py`). Files: `ViewModels/TranslationViewModel.cs`, `TranslationItemViewModel.cs`, `EditionPanelViewModel.cs`, `MainWindowViewModel.cs`, `RuleBrowserViewModel.cs`, `Views/MainWindow.axaml(.cs)`; tests `TranslationViewTests` (every unhinted, non-fragment book golden entry shown exactly; hover, links, coordinates, readings, banner), `TypingTests` (every prefix with translation and hover), updated `RuleStoreTests`/`RuleBrowserTests`, pytest for the new column. Docs: `CLAUDE.md`, `README.md`, `kb/README.md`, `docs/decisions.md`, comparison doc. Deviations, decided with the user during review: Abnormalities is the default tab (plan: Translation); the toggle is named "Genomic coordinates"; result columns restyled as panels. Also fixed a pre-existing T-036 crash: pasting text with a line break set `TextEditor.Text` inside AvaloniaEdit's open paste undo group; line breaks are now replaced after the paste as one undoable document change. Type named `CoordinateBuild` (the core has an arr `GenomeBuild` node). Follow-up: T-084 (bands in the rule browser).

### T-084 — Rule browser: bands
**Status:** todo
**Description:** `kb build` writes the edition's bands (`kb/bands/<version>/bands.yaml`: name, chromosome, arm, parent, levels, GRCh37/38 coordinates, book-only flag) to a `bands` table in `kb.sqlite`; the rule browser lists and searches them and shows a band detail (levels, parent and sub-bands, coordinates, exceptions with their citation); the translation's band links open it instead of being tooltip-only.
**Acceptance:** every band of the runtime bundle opens in the browser with the same coordinates; a band link of the translation opens its band.
**Notes:** Created 2026-11-22 from T-059 (band links tooltip-only for the pilot, decided with the user).

## Core library

### T-055 — Tooling: LF line endings for committed generated files
**Status:** done
**Description:** With `core.autocrlf=true` (Windows), git checks out `kb/bands/2016/ideograms.yaml` with CRLF, while `kb ideograms` writes LF bytes, so `tools/tests/test_ideograms.py::test_pdf_decodes_every_chromosome_and_matches_committed` fails on a byte comparison, even on a clean tree. The same can happen to `bands.yaml` and any other generated, committed file. Options (decide which):
- a `.gitattributes`: `eol=lf` for the generated `kb/**` YAML, or repo-wide `* text=auto eol=lf`, plus a renormalise;
- tests that compare text with normalised line endings.
**Acceptance:** `uv run --directory tools pytest` (the full Python suite) passes on a Windows checkout with `core.autocrlf=true`; the generators and the checkout agree on line endings.
**Notes:** Found during T-008 (2026-11-17), again during T-063 (2026-11-18, filed as T-064); unrelated to either task. T-064 merged into this task on 2026-11-18. After adding `.gitattributes`, re-normalise the working tree (`git add --renormalize .`). Observed in T-056: the working copy has CRLF throughout (KB YAML, docs, C#), so a repo-wide `eol=lf` would touch most files in the renormalise commit.
**Outcome (2026-11-18):** done. That concern didn't apply. Every tracked file was already LF in the index (CRLF only in the working copy), so `git add --renormalize .` changes nothing. Decided `.gitattributes` `* text=auto eol=lf` (repo-wide) over normalising tests; the byte-compare tests stay strict. The local working copy was converted CRLF → LF in place (263 files, content identical to the index). Docs: `docs/decisions.md` (Workflow bullet + log entry), README (Development → line endings, how to convert an old CRLF working copy), CLAUDE.md (convention line). `bands/generate.py`'s `\r\n` stripping kept as a fallback.

### T-023 — Core design
**Status:** done
**Description:** Design doc covering:
- Data model (AST with spans).
- Version profiles (shared parser, per-version rules from the KB).
- Result API (single version vs. array for all versions; `not-implemented`).
- Diagnostics (severity, span, rule ID, citation).
- Fix model (replace/insert/delete/reorder; per version).
- Type detection and segment splitting.
**Acceptance:** `docs/design/core.md` approved by the user.
**Notes:** From T-021: design the model for **all** notation types and implement the pilot subset (karyotype, ch. 2–9). A string is a list of typed segments; types outside the pilot return `not-implemented` per segment. A karyotype is one or more cell lines (optional `[n]`, `/`, `mos`/`chi`), with room for references to earlier clones (`idem`/`sl`/`sdl`, T-032). Diagnostics carry an optional **condition** (context `constitutional`/`acquired`) for context-dependent rules, and fixes may be per context. The API takes an optional context hint (default auto/inferred). From T-022: per-version result = validity (valid = no errors; per context when unknown, naming the context(s) it is valid in) + counts per severity; diagnostics carry a "book ambiguous" flag (rules with `needs_review` are capped at warning) and a per-context severity for conditional rules; checks the engine can't decide with certainty report a warning, not an error. From T-054 (literal translation, `docs/decisions.md` → "Literal translation"): AST nodes keep their meaning (event type, chromosome(s), arm(s), breakpoints/bands, derivative structure with segment order/orientation, sex-chromosome field, count/ploidy, cell-line and clone membership incl. `[n]`, `/`, mos/chi and room for idem/sl/sdl, uncertainty `?`/`~`/`or`, inheritance mat/pat/inh/dn); each node carries its span and the KB entry IDs (rule or vocab item) that define it; partial ASTs from error recovery are marked so the renderer can skip bad parts; the result API offers a translation per edition next to the diagnostics, with per-context readings where needed.
**Outcome (2026-11-18):** `docs/design/core.md` written and approved by the user: pipeline; top-level `.` segment split, keyword type detection, `NotImplemented` segments, marker-only context inference; two layers (lossless syntax tree + semantic model with spans and `KbRef`s `rule:`/`vocab:<key>/<label>`/`band:`, `IsPartial`); lean KB bundle in memory behind `IKnowledgeBase`, embedded in `CytoLex.Core`, `kb.sqlite` for the rule browser; edition profiles and `[KbRule]`-bound checks; diagnostics with T-022 severity resolution, conditional per-context severity, book-ambiguous flag; results API (validity per context, `IsComplete`, counts, translation slot); fix model (text edits, apply = re-validate, fix all, per-context fixes); worked examples. Two design choices decided with the user (two layers; bundle + SQLite, embedded). Recorded in `docs/decisions.md` (Core design section + log entry); `CLAUDE.md`/`README.md` point to the doc; new task T-060 (runtime bundle); notes on T-024, T-026, T-027, T-028, T-035. Deviation: `CORE.UNPARSED` safety-net code (not a KB rule) added to §8.4.

### T-024 — Project scaffolding
**Status:** done
**Description:** Create the solution/packages for the core library, desktop app and tests, using the T-020 stack. Build/test/lint commands work from the command line.
**Acceptance:** Build and an empty test run succeed; `CLAUDE.md` lists the exact commands, including how to run a single test.
**Notes:** Stack from T-020: .NET 10 SDK (pin it in `global.json`), xUnit v3, Avalonia + AvaloniaEdit, CommunityToolkit.Mvvm. Proposed layout (the decision log, T-020 entry): `CytoLex.slnx`, `src/CytoLex.Core/`, `src/CytoLex.Desktop/`, `tests/CytoLex.Core.Tests/`, `Directory.Build.props`. Update `README.md` with `CLAUDE.md` (new top-level folders and commands). From T-023 (`docs/design/core.md` §6): `CytoLex.Core` embeds the runtime bundle from `build/` (T-060) as a resource (until T-060 is done, `build/kb.json` stands in); `dotnet build` fails with a "run `kb build`" message when it is missing. Outcome (2026-11-18): `CytoLex.slnx`, `src/CytoLex.Core/` (embeds `build/kb.json` as `CytoLex.Kb.bundle.json` via property `CytoLexKbBundle`; `Kb/EmbeddedBundle.cs`; missing bundle → build error naming `kb build`), `src/CytoLex.Desktop/` (Avalonia 12.1.6 + AvaloniaEdit 12.0.0 + CommunityToolkit.Mvvm placeholder window), `tests/CytoLex.Core.Tests/` (one embedded-bundle smoke test); `global.json`, `Directory.Build.props` (warnings as errors, analyzers), `Directory.Packages.props` (central versions), `.editorconfig`; commands in `CLAUDE.md`/`README.md`. Deviations: xunit.v3 4.x requires Microsoft.Testing.Platform on the .NET 10 SDK, so `global.json` opts `dotnet test` into MTP and single tests use `--filter-method` (not VSTest `--filter`); Avalonia 12 instead of 11; CA1707 disabled for `tests/**` (underscored test names).

### T-060 — `kb build`: lean runtime bundle for the core
**Status:** done
**Description:** New `kb build` output for the core library (`docs/design/core.md` §6): per edition, rule metadata (id, title, statement, applies_to, context, severity, rendered citations, fix, review), vocabularies, and band names with levels, `book` flag and GRCh37/38 coordinates from `kb/bands/<version>/bands.yaml`; no quotes, no example lists. Carries its own `format_version`. Later also translation templates (T-057).
**Acceptance:** `kb build` writes the bundle (atomically, like the other outputs, byte-identical across runs); tests cover its content; `kb/README.md`, `CLAUDE.md` and `README.md` describe it.
**Notes:** Created 2026-11-18 from T-023 (decided with the user: lean in-memory bundle embedded in Core behind `IKnowledgeBase`; `kb.sqlite` for the rule browser). Proposed (not applied): add T-060 to T-028's Depends. From T-024: switching the core to the bundle = change the `CytoLexKbBundle` default in `src/CytoLex.Core/CytoLex.Core.csproj` (resource name `CytoLex.Kb.bundle.json` stays) and update the smoke test `EmbeddedBundleTests` and the CLAUDE.md/README.md notes on `build/kb.json`.
**Outcome (2026-11-18):** `kb build` also writes `build/kb-runtime.json` (compact JSON, own `format_version` 1, byte-identical across runs; atomic like the other outputs): per edition rule metadata (id, section, title, severity, statement, applies_to, context, related, fix, review {needs_review, note}, cites with `rendered`), vocabularies (entries with tokens/pair/equivalent_forms/meanings, rendered cite; shared_tokens) and bands from `kb/bands/<version>/bands.yaml` (name, chrom, arm, parent, levels, derived, book, grch37/38 + levels_available). No quotes, examples, file paths, vocab source/notes, band stain. `bands.yaml` checks: version = edition, required keys, unique names (errors); missing file → warning and `bands: null`. Code: `tools/src/cytolex_tools/kb/build.py` (`runtime_bundle`, `_load_bands`, `RUNTIME_FORMAT_VERSION`), CLI summary shows the band count; tests in `tools/tests/test_kb_build.py` + fixture `tools/tests/fixtures/kb/bands/2016/bands.yaml`. `CytoLex.Core.csproj` default `CytoLexKbBundle` → `build/kb-runtime.json`; new test `EmbeddedBundle_IsTheLeanRuntimeBundle`; the test project keeps embedding `build/kb.json`. Docs: `kb/README.md`, `CLAUDE.md`, `README.md`, `docs/design/core.md` §6 (plus reminder: `kb build` after `kb bands`). Real KB: 141 rules, 153 vocab entries, 1165 bands, 333 KB (kb.json 509 KB). No deviations from the plan. Still proposed: add T-060 to T-028's Depends.

### T-025 — KB-example test harness
**Status:** done
**Description:** Generate tests from all KB examples (valid → no errors; invalid → expected rule fires; corrected form → fixer output). Produce a coverage report: rules with no implementation or no examples.
**Acceptance:** Harness runs in the normal test command; coverage report generated.
**Notes:** From T-021: KB examples from the other context (e.g. a constitutional-only example run without a hint) must produce the conditional diagnostic, not silence; with a matching context hint, the firm result. From T-022: valid = no errors; expected severities follow the KB after T-056; `needs_review` rules are expected at most as warnings.
**Outcome (2026-11-18):** Decided with the user: build the full harness now and skip per case until the code exists. `tests/CytoLex.Core.Tests/KbExamples/`: `KbCatalog` (reads `build/kb.json`, embedded in the test project as `CytoLex.Tests.kb.json`), `KbExampleTests` (one theory row per valid/invalid/corrected example, 813 for 2016, display name `[2016] RULE.ID kind#n: text`), `ExampleExpectations` (valid = valid without a hint and in the rule's contexts, no errors with a matching hint, own rule never fires; invalid = rule fires without a hint (firm or conditional) and firmly with a matching hint, severity = KB severity capped at warning for `needs_review`/uncertain; corrected = valid + exact `Fix.Preview`; `CORE.UNPARSED` always fails), `ValidatorAdapter` (`null` → skip "no validator yet (T-028)"), `RuleImplementationScan` (`[KbRule]` over Core), `RuleCoverageReport` → `build/review/rule-coverage.md` on every `dotnet test` (140 rules, 0 implemented, 12 without examples), and 47 self-tests with a fake validator. New in Core: `CytoLex.Core.Rules.KbRuleAttribute` (core.md §7.2). CLAUDE.md/README.md updated; notes added to T-028 and T-035. Deviations: book fragments (`inv(2)`, `+21`, `GTG`, `…` strings; `KbCatalog.IsFragment`) are checked only for their own rule (valid) or that it fires (invalid), and the adapter gets an `isFragment` flag; skipped cases print 3 lines each in `dotnet test` (MTP 2.5.2 ignores `--show-test-results failed` for skips), which ends once T-028 plugs in the validator. Follow-up: T-062 (test corpus).

### T-026 — String type detection & segment splitting
**Status:** done
**Description:** Split combined strings on `.` (respecting context) and detect each segment's type (`karyotype`, `ish`, `nuc_ish`, `arr`, …).
**Acceptance:** KB examples of every type are detected correctly.
**Notes:** From T-021: also infer the context from clear markers only (clones/`idem`/`sl`/`sdl`, `c`, ploidy `<Nn>` → acquired; `rec` → constitutional); otherwise unknown. An explicit context hint overrides it. From T-023: design in `docs/design/core.md` §3 (top-level `.` split, keyword type detection, `NotImplemented` segments, contradicting markers → unknown + info).
Outcome (2026-11-18): `Segmenter.Split` (top-level `.` followed by a letter separates segments; leading-keyword type detection with vocab refs) and `ContextInference.Infer` (lexical marker scan, decided with the user: idem/sl/sdl, `c`, `<…n>`, clone structure without mos/chi → acquired; `rec` → constitutional; contradiction → unknown) in `src/CytoLex.Core/{Text,Segmentation,Context}/`; tests in `tests/CytoLex.Core.Tests/{Segmentation,Context}/` (all KB examples split losslessly and match their rule's `applies_to`; other types covered by hand-written strings until their KB chapters exist). Docs: `docs/design/core.md` §3/§14, `docs/decisions.md` T-026 entry, CLAUDE.md/README status. Deviations: segment-level `NotImplemented` left to the edition profile (T-028) and construct-level detection to T-027 (notes added there); the contradiction info diagnostic is emitted in T-028.

### T-027 — Karyotype tokenizer/parser
**Status:** done
**Description:** Parser for karyotype notation producing an AST with character spans. It recovers from errors and reports all problems.
**Acceptance:** Parses all valid karyotype examples from the KB; invalid examples produce errors with correct spans; more than one error is reported per string where present.
**Notes:** From T-023: lossless syntax tree + binder to the semantic model (`docs/design/core.md` §4–§5); every recovery point names the KB rule of the expected form (§8.4). From T-026: the parser works per karyotype segment from `Segmenter.Split` (spans are offsets in the whole input); it also detects the pilot's `NotImplemented` constructs inside a karyotype segment (`idem`/`sl`/`sdl`/`cp`, ch. 10/12 markers, U+0332; core.md §3.2) with their spans — `//` is not one of them (decided in T-027: ch. 4 transplant separator, in scope); it may replace the lexical scan in `ContextInference` with tokens behind the same API.
**Outcome (2026-11-18):** Lexer + recursive-descent parser for karyotype segments in `src/CytoLex.Core/Syntax/` (`Lexer`, `Parser` with `ParseKaryotype`/`ParseFragment`/`ForEdition`, `SyntaxTree`, `SyntaxNode`/`SyntaxToken`, `SyntaxKind`, `TokenKind`, `SyntaxDiagnostic` + `CoreCodes.Unparsed`, `NotImplementedConstruct`) and the vocab loader `src/CytoLex.Core/Kb/Vocabulary.cs` (bundle vocab, `format_version` check). Lossless tree (whitespace as trivia, skipped text as `Skipped` nodes, zero-width missing tokens); letter runs split into listed symbols; typed forms marked; every recovery names a KB rule (`Parser.RuleIds`). Tests in `tests/CytoLex.Core.Tests/Parsing/`: all 454 distinct valid/corrected karyotype/general KB examples parse without diagnostics and losslessly; all 140 invalid ones match `InvalidExampleExpectations.cs` (32 with syntax errors, 108 left to rules); multi-error strings; lexer, constructs, vocab and KB-traceability tests. Harness `KbCatalog.IsFragment` refined (bare bands/variants are fragments). Decided with the user: binder split to T-065 (added to the Depends of T-029–T-031), `//` in scope, expectations kept test-side. Docs: `docs/design/core.md` §3.2/§4/§14, `docs/decisions.md`, CLAUDE.md/README status, T-028 notes. Deviations: permissive grammar — `del(5q)`, `i(17q)`, `fra(Xq27.3)` parse cleanly (rules report them) instead of parser recognisers; skipped text is a node, not trivia; no KB rules added, no `CORE.UNPARSED` on any KB example.

### T-028 — Version-profile rule engine
**Status:** done
**Description:** Loads the KB bundle and applies the rule set for the chosen version (or all versions, returning an array of results).
**Acceptance:** A single-version call and an all-versions call (2016 + `not-implemented` for 2020/2024) both work; diagnostics carry citations.
**Notes:** From T-023: edition profiles, `[KbRule]`-bound checks, severity resolution and the result API in `docs/design/core.md` §7–§9. Proposed: depend on T-060 (runtime bundle). From T-025: implement `ValidatorAdapter.Current` in `tests/CytoLex.Core.Tests/KbExamples/ValidatorAdapter.cs` (map one edition's `ValidationReport` onto `ExampleRun`; the KB-example cases then run instead of skipping); rule checks use `CytoLex.Core.Rules.KbRuleAttribute` (already in Core); optionally replace `RuleImplementationScan` with the engine's coverage list. The adapter gets an `isFragment` flag for book fragments (`inv(2)`, `…` strings). From T-026: segments come from `Segmenter.Split` (`Segment.Type`); the profile's implemented types decide `NotImplemented` per segment. `ContextInference.Infer` gives `ContextInfo` (hint/inferred/effective/markers); when `IsContradictory`, emit the info diagnostic naming both markers — it needs a KB rule ID (or a core code like `CORE.UNPARSED`), decide which. Map the harness's own `SampleContext` enum onto `CytoLex.Core.Context.SampleContext`. From T-027: parse each karyotype segment with `Parser.ForEdition(edition)` — `ParseFragment` when the adapter's `isFragment` is set, else `ParseKaryotype`; map each `SyntaxDiagnostic` (rule ID, span, kind, expected text) to a `Diagnostic` with origin parser and the KB severity/citations (a `Missing` one with `Expected` is level-1 insert material for T-035); `SyntaxTree.Constructs` give the construct-level `NotImplemented` results (`IsComplete = false`). `Vocabulary` (`CytoLex.Core.Kb`) already reads the bundle's vocab and checks `format_version`; `IEditionKb` can wrap it. Many invalid KB examples parse cleanly by design (permissive grammar); their own rules must fire (see `tests/CytoLex.Core.Tests/Parsing/InvalidExampleExpectations.cs`); for unlisted words the parser reports `SYM.LISTED_ONLY` (e.g. `iso`, `abr`), so `ISO.FORMAT`/`HSR.NO_ABR_SYMBOL` checks may need to look at `UnknownWord` tokens. **Outcome (2026-11-18):** `Validator` (`src/CytoLex.Core/Validation/`: `Validator`, `DiagnosticFactory`, `Diagnostic`, `Fix`, `ValidationOptions`, `ValidationReport`), runtime KB loader (`src/CytoLex.Core/Kb/KnowledgeBase.cs`, `KbRule.cs`, `BandSet.cs`), engine (`src/CytoLex.Core/Rules/`: `IRuleCheck`/`RuleContext`, `RuleCatalog` with startup check and most-specific variant, `EditionProfile`, `EditionCoverage`). Single-edition and all-editions calls (2016 + NotImplemented 2020/2024); diagnostics carry KB title, severity (needs_review/uncertain caps, conditional per context) and rendered citations. Harness plugged in (`ValidatorAdapter`): 678 valid/corrected cases pass, 27 parser-covered invalid cases pass, 114 skip; coverage report marks parser-reported rules and core codes. Tests: `tests/CytoLex.Core.Tests/Validation/ValidatorTests.cs`. Decisions with the user: `CORE.CONTEXT_CONFLICT` core code (info, no citation); invalid examples without a check run and skip only if their rule is silent; clone structure dropped as a context marker (revises T-026; §4.1 ¶9 constitutional mosaics without `mos`). Deviations: `IKnowledgeBase.Get` → `ForEdition` (CA1716); parse sharing deferred; `Diagnostic.Fixes` empty until T-035; band `book: false` severity left to T-029.

### T-029 — Rules: general + band validation
**Status:** done
**Description:** Symbols, separators, chromosome identifiers, band existence and resolution (using band data from T-006/T-007).
**Acceptance:** Related KB examples pass.
**Notes:** From T-007: `kb/bands/2016/bands.yaml` entries carry `levels` (300–850, book-corrected) and `book`. `book: false` marks 6 NCBI-only names (6p24.1–3, 9q34.11–13) that are valid for genome-band contexts (arr/seq, e.g. §15.3 `9q34.11q34.3`) but absent from the book's idiograms. Decide with T-022 whether karyotype/ISH strings using them get a warning. 15 book-only 850 sub-bands (e.g. 1q32.11) have `grch37/grch38: null`. From T-022: `book: false` bands give an `info` note (source named) in karyotype/ish strings and nothing in arr/seq; unknown bands stay errors. Rule severities come from the KB after T-056 (do T-056 first).
**Outcome (2026-11-19):** Seven `[KbRule]` checks in `src/CytoLex.Core/Rules/Checks/` (shared queries in `CheckHelpers.cs`): `CHROM.IDENTIFIERS` (numbers outside 1–22; sex-field letters stay with the parser), `BAND.DESIGNATION_FORMAT` (spaces inside a band, `1p 31`/`1 p31`), `BAND.MUST_EXIST` (accepts p10/q10 and region-only `1p1` via new `BandSet.ContainsRegion`; skips uncertain bands, unknown/invalid chromosomes, recovered input), `BAND.GENOME_ASSEMBLY_ONLY`, `SYM.SPACE_BETWEEN_SYMBOLS` (`psudic`), `SYM.SPACE_BEFORE_CHROMOSOME_COUNT` (`mos47`), `SYM.NO_SPACE_AT_PARENTHESIS` (symbol ↔ parenthesis inside abnormalities; `… or i(8)(p10)` stays valid). Decided with the user: `book: false` bands get a new KB info rule `BAND.GENOME_ASSEMBLY_ONLY` (`kb/rules/2016/ch02/02.04-high-resolution-banding.yaml` + quote; `applies_to` karyotype/breakage/meiotic/ish types, so arr/seq stay silent; no special case in `DiagnosticFactory`); its `paraphrase_checked` stays `false` until a person reviews it. Parser-reported rules (`BAND.ARM_SYMBOL`, `BAND.SUBBAND_FORMAT`, `SYM.LISTED_ONLY`, `SYM.TYPED_SYMBOL_FORMS`) get no duplicate check. Tests: `tests/CytoLex.Core.Tests/Rules/GeneralRuleChecksTests.cs` (45 cases); `ValidatorTests.Coverage_ListsParserRules` and `InvalidExampleExpectations` (`9q34.11`) updated. `dotnet test`: 2,314 total, 0 failed, 107 skipped (was 114: every ch. 2–3 example now runs and passes); coverage report: 7 rules implemented (plan said 8, a miscount). Docs: `docs/decisions.md` (log entry + T-022 band bullet), `docs/design/core.md` §7.2/§8.2/§8.5, status lines in `CLAUDE.md` and `README.md`. Follow-up: T-067 (revisit genome-assembly-only bands per edition, at the user's request).

### T-030 — Rules: numerical + sex chromosomes + ordering
**Status:** done
**Description:** Chromosome count consistency, sex chromosome notation, order of abnormalities.
**Acceptance:** Related KB examples pass.
**Notes:** From T-011: the count check is `NUM.COUNT_MATCHES_CHANGES` (count = reference ploidy level from the count range or `<Nn>` + gains − losses; an inference, `needs_review`). It must handle structural changes that alter the count without a sign (rob, dic, fis, …), count ranges (`~`) and `inc`, and the sex-field convention in `SEX.ACQUIRED_GAIN_LOSS` (after a loss the field lists the remaining sex chromosomes, `45,X,–X`; with a gain the constitutional ones, `47,XX,+X`). Ploidy ranges: `NUM.PLOIDY_REFERENCE` (§8.1 ¶2) and the §11.2 Ploidy table.
**Outcome (2026-11-19):** 16 `[KbRule]` checks in `src/CytoLex.Core/Rules/Checks/` (one class per file), built on two shared analyses. `ChromosomeCount.cs` handles the count arithmetic per cell line: level from `<Nn>` or ≤34 → 1, else ⌊(c+11)/23⌋; item deltas for dic/rob/der(A;B)/t/tas/fis/dmin; the `c`-field convention. A mismatch goes to the most precise rule, first match wins: `NUM.PLOIDY_REFERENCE`, `SEX.ABNORMAL_SEX_CHROMOSOME_IN_LIST`, `KARYO.REPLACED_NORMAL_NOT_MISSING`, the sex-field rules, else `NUM.COUNT_MATCHES_CHANGES`. `ItemOrder.cs` gives each item an order key; every out-of-order item gets one finding from `KARYO.ABNORMALITY_ORDER` or one of the `ORDER.*` rules. Other checks: `SEX.CONSTITUTIONAL_COMPLEMENT`, `SEX.ACQUIRED_GAIN_LOSS` (both conditional by KB context), `KARYO.UNCERTAIN_CONSTITUTIONAL`, `MAR.CONSTITUTIONAL_C_SPACE`, `KARYO.MULTIPLY_SIGN_REARRANGED_ONLY`, `ORDER.SHORTEST_STRING_PREFERRED`. Scope agreed with the user: `KARYO.CLONE_ORDER_CONSTITUTIONAL` left for T-032. Deviations from the plan: a lost X/Y still in the sex field (`45,XY,–Y`) is left to the sex-field rules, not the count rule; `SEX.ACQUIRED_GAIN_LOSS` skips lines that use `c` (book quirk §8.2 E20 `48,XXY,+mar c`). Not counted: `[cp]`, `inc`, `…`, `idem`/`sl`/`sdl`, unsigned `mar`/`r`/`fis`; count ranges give warnings only. KB: `NUM.PLOIDY_REFERENCE` cites §11.2 ¶2 (+ quote); the `NUM.COUNT_MATCHES_CHANGES` note lists the counting conventions; no statement changed. Tests: `tests/CytoLex.Core.Tests/Rules/NumericalSexOrderChecksTests.cs` (71 cases); one assertion in `ValidatorTests.Diagnostics_AreSortedBySpanThenSeverity` narrowed. `dotnet test`: 2,385 total, 0 failed, 86 skipped (was 107); coverage report: 23 rules implemented. Docs: `docs/design/core.md` §7.2, `docs/decisions.md`, status lines in `CLAUDE.md`/`README.md`.

### T-031 — Rules: structural rearrangements
**Status:** done
**Description:** All ch. 9 rearrangements, plus the short and detailed systems from ch. 4.
**Acceptance:** Related KB examples pass.
**Notes:** From T-013: `STRUCT.PROXIMAL_BREAKPOINT_FIRST` and `STRUCT.SHORT_ARM_BREAKPOINT_FIRST` do not apply where the band order shows orientation (dup, trp, inserted segments, segments of complex t and rings, band groups in multicentric rings, tas, trc); the order rules for tas/trc (end chromosome first, then the chain) are `TAS.FORMAT`/`TRC.FORMAT`.
**Outcome (2026-11-19):** Split with the user: this task covers 25 rules, and the other ch. 9 symbol rules move to T-068 (added to the Depends of T-032 and T-035). 25 `[KbRule]` checks in `src/CytoLex.Core/Rules/Checks/`. Each is a thin `StructuralCheck` subclass over one of five shared analyses that address each fault to one rule (`StructuralFinding.cs`, which also holds the `Structure` helpers; `BandPosition.cs` orders bands from the designation alone). The analyses: `ChromosomeOrder.cs` (`STRUCT.CHROMOSOME_ORDER`, `STRUCT.MULTI_CHROMOSOME_ORDER`, `STRUCT.INSERTION_RECEPTOR_FIRST`, `TAS.FORMAT`, `TRC.FORMAT`, `RING.DIC_TRC_ORDER`, `RING.MONOCENTRIC_AS_DER`); `BreakpointAnalyses.cs` (`STRUCT.SHORT_ARM_BREAKPOINT_FIRST`, `STRUCT.PROXIMAL_BREAKPOINT_FIRST`, plus `STRUCT.BREAKPOINT_GROUPS` as fallback for `INV`/`RING`/`QDP`/`TRP`/`TRANSLOC.FORMAT`); `Composition.cs` (`STRUCT.DETAILED_SYMBOLS`, `STRUCT.COMPOSITION_ORDER`, `STRUCT.COMPOSITION_CHROMOSOME_NUMBERS`, `DER.CENTROMERE_CHROMOSOME`); `Derivatives.cs` (`DER.ABERRATION_ORDER`, `DER.ABERRATIONS_NO_COMMA` (uncertain unless the der has no aberration of its own), `REC.FORMAT`, `REC.NOT_ACQUIRED`, `DER.MULTIPLE_ABERRATIONS_ONE_CHROMOSOME`, `DER.DICENTRIC_FORMAT`); and `ChromosomeCount` with the new verdict `TasCountedOnce`, reported by `TAS.FORMAT`. `SpaceBetweenSymbolsCheck` leaves `dicr`/`trcr` to `RING.DIC_TRC_ORDER`, and `ItemOrder` uses the shared `Structure.Rank`. KB: `STRUCT.BREAKPOINT_GROUPS` invalid examples changed to `ins(5;2)(p14q22q32)` and `ins(5;2)(p14;q22;q32)` (the group-count error goes to the symbol's rule, decided with the user). Harness, decided with the user: an invalid example whose own markers set a context outside the rule's is checked only with the matching hint (`ExampleRun.InferredContext`; needed by `REC.NOT_ACQUIRED`, since `rec` marks constitutional), with one self-test. Book inconsistencies accepted rather than flagged: a ring inside `der(n)` may start with n or with the lowest number (§4.4 `der(8)r(1;8;17)`); homologue compositions pass with or without numbers; a pericentric inversion's reversed centromere piece is not a reversed composition. Tests: `tests/CytoLex.Core.Tests/Rules/StructuralChecksTests.cs` (75 cases), `InvalidExampleExpectations` and `GeneralRuleChecksTests` updated. `dotnet test`: 2,462 total, 0 failed, 51 skipped (was 86); coverage: 48 rules implemented. Docs: `docs/decisions.md`, `docs/design/core.md` §7.2, `CLAUDE.md`/`README.md` status + harness note. Deviations from the plan: the harness change and the book-inconsistency exceptions (found while running the KB examples).

### T-032 — Rules: neoplastic
**Status:** done
**Description:** Clones, cell counts `[n]`, idem/sl/sdl, composite karyotypes, `/` and `//`.
**Acceptance:** Related KB examples pass.
**Notes:** From T-014: the KB rules are `CLONE.*` (11), `CP.*` (3), `PLOIDY.*` (2, info), `SEX.UNEVEN_PLOIDY_MALE`, `KARYO.CONSTITUTIONAL_ALWAYS_GIVEN` (`kb/rules/2016/ch11/`). Parser work: cell counts after an abnormality (`del(3)(p12)[2]`, §11.1.5 E2) and `idem,×2` (should be reported as `CLONE.POLYPLOID_SUBCLONE` with a delete fix); these examples are in `tests/CytoLex.Core.Tests/KbExamples/PendingExamples.cs` and must be removed from it. One fault, one finding: a missing repeat with idem shows up as a count mismatch (`…/48,idem,+9`) and belongs to `CLONE.SUBCLONE_REFERENCE`, not `NUM.COUNT_MATCHES_CHANGES`; a normal clone listed first belongs to `CLONE.NORMAL_LAST` when acquired and `KARYO.CLONE_ORDER_CONSTITUTIONAL` when constitutional. `CLONE.ORDER_UNRELATED` is a warning (§11.1.6 ¶3). The info rules `CLONE.SINGLE_CELL_REPORTING` (abnormal `[1]` clone), `CP.NOT_FOR_RANDOM_LOSS` (`[cp…]`) and `KARYO.CONSTITUTIONAL_ALWAYS_GIVEN` (`c` present) report cannotVerify only when their notation is present.
**Outcome (2026-11-20):** 14 checks in `src/CytoLex.Core/Rules/Checks/` (`CLONE.SUBCLONE_REFERENCE`, `IDEM_SL_NOT_MIXED`, `SECOND_STEMLINE`, `ORDER_RELATED`, `ORDER_UNRELATED`, `NORMAL_LAST`, `CELL_COUNT_REQUIRED`, `MIN_CELLS`, `SINGLE_CELL_REPORTING`, `CP.FORMAT`, `CP.NOT_FOR_RANDOM_LOSS`, `SEX.UNEVEN_PLOIDY_MALE`, `KARYO.CONSTITUTIONAL_ALWAYS_GIVEN`, `KARYO.CLONE_ORDER_CONSTITUTIONAL`, `TRANSLOC.JUMPING`) over the shared analyses `Clones.cs` and `UnrelatedClones.cs`; `CLONE.POLYPLOID_SUBCLONE` is parser-reported (`idem,×2` → delete the comma). Parser: per-abnormality cell counts (`SyntaxKind.ItemCellCount`), clone notation no longer `NotImplemented` (`ConstructKind.CloneReference`/`Composite` removed). Binder: `CellLine.BasedOn` (`CloneReference`), resolved by `Semantics/CloneReferences.cs`; `SuffixedItem.CellCount`; `CellLineSummary.BasedOn`. `ChromosomeCount.RelativeDelta` for subclone counts. `PendingExamples` emptied; 2 invalid examples classified in `InvalidExampleExpectations.cs`; 31 new unit tests (`Rules/CloneChecksTests.cs`, binder, parser, abnormality list). `dotnet test` 3831 total, 0 failed, 55 skipped (no ch. 11 skips left); rule coverage 87 implemented. Decisions (with the user at plan approval): cell count required only on mixed counts or reference lines; neoplastic clone checks skip `mos`/`chi`, `//` sides ordered separately; jumping-translocation detection; reorder fixes withheld when a reference would change; inherited changes in the abnormality list → T-070. No KB changes; no deviations from the plan. Docs: `docs/design/core.md`, `docs/decisions.md`, `CLAUDE.md`, `README.md`.

### T-033 — Parser + rules: FISH
**Status:** todo
**Description:** `ish` and `nuc ish` grammar and rules.
**Acceptance:** Related KB examples pass.
**Notes:**

### T-034 — Parser + rules: microarray
**Status:** done
**Description:** `arr` grammar and rules, including the genome build and coordinate vs. band consistency.
**Acceptance:** Related KB examples pass.
**Notes:** From T-017:
- Checkable ARR rules (errors): `NUCLEOTIDE_FORMAT`, `NORMAL_RESULT`, `ABERRATIONS_ONLY`, `ABERRATION_ORDER`, `REGION_FORMAT`, `PTER_TO_QTER`, `COPY_NUMBER`, `MOSAIC_PROPORTION`, `INHERITANCE_SPACING`, `COMBINED_WITH_OTHER_METHODS` (general), `ZYGOSITY`, `COMPLEX_CX`, `CHROMOTHRIPSIS`; plus `GEN.GENOME_BUILD_REQUIRED`, `SYM.NO_SPACE_AT_PARENTHESIS` (arr example) and `BAND.MUST_EXIST` (§14.2 E27 invalid example). Info only: `SHORT_AND_DETAILED_SYSTEMS`, `RESULT_TABLE`, `STRUCTURE_NOT_SHOWN`, `GENOME_BROWSER_BANDS`.
- Coordinate vs. band check: §14.2 E23/E24, E34, E40, E44 and E45 print GRCh37 positions under `[GRCh38]` (kept as printed; listed in `ARR.PTER_TO_QTER`'s note). Decide how to treat them before flagging.
- When arr parses, remove the karyotype-segment filter in `ParseHelper.KbExamples()` and check that the 24 harness skips "arr: not implemented in the pilot" become passes.
**Outcome (2026-11-20):**
- `arr` is implemented for ISCN 2016:
  - grammar in `src/CytoLex.Core/Syntax/Parser.Arr.cs` (permissive: faults stay in the tree for the rules);
  - model in `Semantics/ArrNodes.cs` + `Binder.Arr.cs` (`SegmentModel` base of `KaryotypeModel`/`ArrModel`);
  - checks in `Rules/Checks/ArrAnalyses.cs` + `ArrRulesCheck.cs` (`GEN.GENOME_BUILD_REQUIRED` and 12 ARR rules) and `CombinedResultOrderCheck.cs`.
- General checks run on arr: `BAND.MUST_EXIST`, `CHROM.IDENTIFIERS`, `BAND.DESIGNATION_FORMAT`, and `SYM.NO_SPACE_AT_PARENTHESIS` (new arr branch).
- `RuleContext` gained `SegmentModel`, `Arr`, `Karyotype`, `Segments` and `ImplementedTypes`.
- Every arr KB example passes; the 24 "arr: not implemented" skips are gone.
- Results: `dotnet test` 4188 total, 0 failed, 8 skipped; tools tests 180 passed.
- Tests:
  - new `ArrParserTests.cs`, `ArrBinderTests.cs`, `ArrChecksTests.cs`;
  - `ParseHelper`/`BindHelper` cover arr, and `ParseHelper.KbExamples()` no longer needs a karyotype segment;
  - 25 `InvalidExampleExpectations` entries added.
- Deviations from the plan:
  - §14.2.1 E4/E5 print the non-existent band 21q11.21. Treated like E27 (decided with the user): the printed E4 string is a `BAND.MUST_EXIST` invalid example; the `ARR.ZYGOSITY` examples use 21q11.2.
  - A karyotype after an arr result (`…dn.47,XY`, not split by the segmenter) is reported by the arr parser as `ARR.COMBINED_WITH_OTHER_METHODS`, with a move fix.
  - The missing copy number is reported by a check, not the parser.
  - One check class carries all the ARR bindings.
- Deferred:
  - positions vs. bands → T-071 (`discussion`);
  - arr entries in the abnormality list → T-072.
- Docs: `docs/decisions.md`, `docs/design/core.md`, `docs/extraction-review-2016.md`, `CLAUDE.md`/`README.md`, the `ARR.PTER_TO_QTER` note.

### T-035 — Level 1 fixes + level 3 explanations
**Status:** done
**Description:**
- Level 1: deterministic fixes (spacing, letter case, chromosome/breakpoint order, redundant sex chromosome as in `46,XX,t(X;13)` → `46,X,t(X;13)`).
- Level 3: plain-language explanations when no single fix exists.
- Fixes are per version.
**Acceptance:** Corrected forms from the KB are reproduced exactly; explanations include citations.
**Notes:** From T-021: where the correct form depends on context (e.g. `47,XXX` vs `47,XX,+X`) and the context is unknown, offer one fix per context instead of a single Apply. From T-023: fix model (`TextEdit`s on the original input, apply = edit + re-validate, fix all, `ForContext`) in `docs/design/core.md` §10. From T-025: set `SupportsFixes` in the harness adapter so the `corrected` cases also check `Fix.Preview`.
Outcome (2026-11-20):
- Core: `Fix` (+ `Citations`), `FixProposal`, `Fixer` (`Apply`, `ApplyAll` → `FixAllResult`) in `src/CytoLex.Core/Validation/`; `DiagnosticFactory` builds level-1 fixes (KB level/kind/hint, preview, `ForContext` from the rule's single context) and a level-3 explanation (statement + hint + citations) for every other diagnostic; core codes have none. `RuleContext.Report(…, fixes)` / `ReportWithFix`, `StructuralFinding.Fix`, `SyntaxDiagnostic.Edits`/`FixEdits`.
- Edits in the checks (shared analyses: item order as one stable sort per cell line, chromosome order/rotation/chain reversal, breakpoint swap and regrouping, composition reversal and chromosome numbers, derivatives, centromeric breakpoints, symbol forms, markers/dmin/copies, sex field, replaced losses) and in the parser recoveries (inserts/replaces from `Expected`, plus reorders); helper `Rules/Checks/FixHelpers.cs`.
- KB: level-1 fix blocks for 11 ch. 9 rules (DEL/DUP/INV/QDP/TRP/TRANSLOC.FORMAT, FIS, FRA, HSR.REPLACED_SEGMENT_PROXIMAL, DER.ISODERIVATIVE, DER.DICENTRIC_FORMAT); `DER.NO_PH_SYMBOL` hint names the short form (§9.2.3 E35).
- Harness: `SupportsFixes` on; previews checked for KB level-1 rules that are implemented or parser-reported. Tests: `tests/CytoLex.Core.Tests/Validation/FixTests.cs`; 2540 pass, 13 skipped (unimplemented rules' invalid examples).
- Deviations: an "all occurrences" fix when one rule reports several faults; `Fix.Citations` added; the one-fix report method is `ReportWithFix`. Decisions in `docs/decisions.md` (2026-11-20), design in `docs/design/core.md` §10 "As built (T-035)".

### T-062 — Test corpus of ISCN strings per notation type
**Status:** done
**Description:** A corpus of realistic ISCN strings, one file per notation type (e.g. `tests/corpus/karyotype.yaml`, `ish.yaml`, `nuc_ish.yaml`, `arr.yaml`, …), to complement the book-cited KB examples. The KB examples stay the reference suite; the corpus adds what they lack:
- realistic, combined strings (several abnormalities, clones with `[n]`, mosaics/chimeras, karyotype + `ish` + `nuc ish` + `arr` joined by `.`);
- volume for batch validation (T-038), the CytoGPS comparison (T-051) and the translation golden file (T-057);
- robustness inputs: spacing, ASCII `-`/`x`/`->` (T-053), truncated strings, typos.

Each entry records:
- the string, its notation type(s) and the features it exercises;
- the KB rule IDs it relies on;
- the expected result per edition;
- its source: `synthetic`, `book-adapted` or `lab` (anonymised);
- a review flag.

Expected results start as `unreviewed`. Until a person checks an entry, or every rule it relies on is in the KB, it is used only as a parse-only test: it must split, parse and produce no `CORE.UNPARSED`, with no claim about validity. Invalid strings come mainly from a **mutation generator** applied to trusted valid strings (drop a separator, swap the chromosome order, use a non-existent band such as `9q35`, …), so the expected error follows from the mutation, not from memory.
**Acceptance:**
- Karyotype file with ≥ 100 entries covering constitutional, acquired, mosaic/chimera, structural (incl. derivatives and complex rearrangements), uncertainty, and clone/composite strings.
- Files for the other types aim at "every construct at least once" rather than a fixed count. For `ish`, `nuc_ish` and `arr`, expected results are labelled only once their KB tasks (T-015/T-016, T-017) are done; before that they are parse-only.
- A schema for the corpus files, the mutation generator with tests, and the harness running the corpus in `dotnet test` (parse-only entries skip until the parser exists).
**Notes:** Raised 2026-11-18 by the user, after T-025. Labels must trace to KB rules, never to ISCN from memory (CLAUDE.md). Anonymised real strings from the user's lab would be the most valuable source; check the data policy first (no patient identifiers in the repo). Builds on the T-025 harness (`tests/CytoLex.Core.Tests/KbExamples/`).
**Outcome (2026-11-23):** `tests/data/corpus/` (README: format, label policy, operators): 213 synthetic entries — `karyotype.yaml` 141 (constitutional, acquired, mosaic/chimera, structural incl. complex derivatives, uncertainty, clones/composite), `arr.yaml` 23 (labelled), `ish.yaml` 8 and `nuc_ish.yaml` 7 (`unreviewed`, skip until T-033), `combined.yaml` 8, `robustness.yaml` 26 (typed forms, spacing, case, typos, truncation). Schema `kb/schema/corpus.schema.json`; `kb check` covers `tests/data/corpus/` by default (pytest for the schema). Harness `tests/CytoLex.Core.Tests/Corpus/` (`CorpusData`, `CorpusExpectations`, `CorpusTests`, empty `PendingCorpus`): a label is checked when reviewed or when every named rule is in the edition's KB, else parse-only; `invalid` needs every `fires` rule and not-valid unless the severity policy keeps the findings below error; a `fires` rule without a check skips (as in the KB harness). Mutation generator `Corpus/Mutations/`: 14 operators, each grounded in a KB invalid example of its rule (`BookPair`), 916 mutants, each reports its rule. Opt-in export `CYTOLEX_CORPUS_EXPORT=1` → `build/review/corpus/corpus-2016.csv`. YamlDotNet added to Core.Tests. Deviations: no `lab` source (data policy T-048); one mutant per operator and entry (first site); mutants generated at test time, not committed. First run triage: 3 wrong labels fixed, 4 expected behaviours noted in `review.note` (T-032 cell-count decision, `needs_review` cap, `KARYO.CONSTITUTIONAL_C` not implemented, `inc`), no CytoLex bug; truncated input only gives `CORE.UNPARSED` → T-085. `dotnet test`: 8,184 tests, 0 failed.

### T-063 — KB: typed symbol forms in the ch. 3 vocab + `SYM.TYPED_SYMBOL_FORMS`
**Status:** done
**Description:** Apply the T-053 decision (`docs/decisions.md`, Strictness & severity and the T-053 log entry) to the 2016 KB:
- `kb/vocab/2016/ch03/03-symbols.yaml`:
  - minus sign tokens `["–", "-", "−"]`;
  - multiplication sign tokens `["×", "x"]`;
  - replace the "is T-053" notes with the decision.
  The arrow already lists `→`/`->`.
- New rule `SYM.TYPED_SYMBOL_FORMS` (`kb/rules/2016/ch03/`):
  - cites §3 pp.34–35 and §4.3.2.1 ¶1 p.43, with quotes in `kb/source-quotes/`;
  - valid examples use typed forms, e.g. `45,X,-X`, `del(6)(q13q23)x2` and a detailed-system string with `->`;
  - invalid example: uppercase `X2` used as a multiplier, with a level-1 fix to `×2`;
  - `review.note`: the minus/x equivalences are a project decision (T-053), not book text.
- Check whether `kb build`'s lookup of valid examples in their `En` block tolerates the typed forms (`docs/decisions.md`, T-025: a miss is a warning).
**Acceptance:** `kb check` and `kb build` pass; the rule and its examples are in `build/kb.json`; the lexer task (T-028) can read the equivalent tokens from the vocab.
**Notes:** From T-053 (2026-11-18). T-063 was added to T-028's Depends (user, 2026-11-18).
Outcome (2026-11-18):
- Vocab `kb/vocab/2016/ch03/03-symbols.yaml`: minus `["–", "-", "−"]`, multiplication `["×", "x"]`, arrow `["→", "->"]`, each with the new optional flag `equivalent_forms: true` (deviation: needed because brackets and Roman numerals also list several tokens that are *not* interchangeable).
- `kb/schema/vocab.schema.json`: the flag. `kb build`: the flag is an error with `pair` or fewer than 2 tokens; new SQLite column `vocab.equivalent_forms`; the valid-example lookup counts typed forms as printed, so typed examples cite their `En` block without a warning.
- Rule `SYM.TYPED_SYMBOL_FORMS` (`kb/rules/2016/ch03/…`, paraphrase approved) with 4 quotes; valid examples are typed forms of §8.2 E10, §9.3 E1 and §9.2.2 E2; invalid `…X2` with a level-1 fix to `×2`.
- Docs `kb/README.md`; tests `tools/tests/test_kb_build.py` + fixtures.
- Follow-up: T-064 (an ideograms test fails on CRLF checkouts, unrelated; merged into T-055).

### T-064 — Line endings of generated committed files (merged into T-055)
**Status:** done
**Description:** Duplicate of T-055 (same ideograms test failure on CRLF checkouts).
**Acceptance:** –
**Notes:** Found during T-063 (2026-11-18). Merged into T-055 on 2026-11-18 at the user's request: its description, acceptance (`uv run --directory tools pytest` passes on Windows with `core.autocrlf=true`; generators and checkout agree) and fix options now live there. No work done here; the ID is kept so references stay valid.

### T-085 — Parser: truncated input under a KB rule
**Status:** todo
**Description:** Truncated strings from the test corpus (`tests/data/corpus/robustness.yaml`: `46,XY,t(9;22)(q34;q11`, `46,XY,del(5)(q13`, `47,XX,+`) are reported only as `CORE.UNPARSED` ("text not attributed to any KB rule"). Find the KB rule that covers each fault (e.g. a breakpoint group that lacks its closing parenthesis, a sign without a chromosome); if the KB has none, add it with a citation first. Then let the parser report the fault under that rule, with a level-1 fix where one edit is certain (insert `)`).
**Acceptance:** the three corpus entries report a KB rule (their `fires` updated from `CORE.UNPARSED`); KB examples and the corpus stay green.
**Notes:** Created 2026-11-23 from T-062 (corpus robustness entries). Labels must trace to KB rules (CLAUDE.md).

## Desktop app (pilot)

### T-036 — App shell + single-string check
**Status:** done
**Description:** Input box. Shows detected types, highlighting (underline for wrong ranges, insertion marker with grey expected text for missing parts, strikethrough for extra parts) and a diagnostics list with citations.
**Acceptance:** Book examples show correctly; highlighting matches diagnostic ranges.
**Notes:** **Outcome (2026-11-20):** the desktop window validates the input on every change in the default edition (2016) and shows the verdict ("valid as constitutional only", "as far as implemented"), counts, the context with its markers, and the segments with their detected types (not-implemented ones marked). Highlighting in `src/CytoLex.Desktop/Highlighting/`: `HighlightBuilder` maps each diagnostic's own level-1 fix (not the "all occurrences" one) to highlights — replace → underline, insert → marker + grey expected text, delete → strikethrough; no edit → underline of the diagnostic span (marker if empty); severity colours (`SeverityBrushes`); drawn by `DiagnosticColorizer` (text decorations) and `InsertionElementGenerator` (zero-length inline element). Diagnostics list (`DiagnosticItemViewModel`): severity, title, column, rule ID, detail, per-context severities, book-ambiguous note, fixes as text (Apply is T-040), citations; selecting one selects its span. Book-example dropdown (§13 worked examples). App icon (G-banded chromosome + check badge): `src/CytoLex.Desktop/Assets/cytolex.ico`/`.png`, set as exe and window icon. New test project `tests/CytoLex.Desktop.Tests/` (embeds `build/kb.json`): highlight cases plus a check over every KB example string that each diagnostic's highlights lie in the input on its own fix edits (or its span); view-model tests. `dotnet test`: 2,568 total, 0 failed, 13 skipped; `dotnet format` clean. Docs: `CLAUDE.md`/`README.md` (status, layout, commands). Deviation from the plan: no debounce (validation is fast enough per keystroke). Follow-up: T-069 (abnormality list + panel).

### T-037 — Version selector
**Status:** done
**Description:** Switch between a single version and all versions; all-versions shows one panel per version.
**Acceptance:** Both modes work; unimplemented versions show `not-implemented`.
**Notes:** Done 2026-11-20. Edition selector in the desktop top bar (`MainWindowViewModel.EditionChoices`/`SelectedEditionChoice`: known editions, unimplemented ones labelled, plus "All editions"; default = newest implemented). Results are one tab per `EditionResult` (new `ViewModels/EditionPanelViewModel.cs`: header with status, verdict, counts, segment notes, diagnostics, abnormalities, highlights); the editor draws the selected tab's highlights, and the selected tab is kept while re-typing. Not-implemented editions show their reason in grey with no diagnostics/abnormalities. A diagnostic click is routed through `SelectedSpan`. Layout decided with the user: tabs (not side-by-side columns). Tests: `MainWindowViewModelTests` (7 new), `AbnormalityPanelTests` read through the selected panel. Files: `MainWindowViewModel.cs`, `EditionPanelViewModel.cs`, `Views/MainWindow.axaml(.cs)`, CLAUDE.md/README.md status. Core unchanged.

### T-038 — Batch validation
**Status:** done
**Description:** Load a text/CSV file, validate all strings, show a summary table, export to CSV/JSON.
**Acceptance:** A batch of 1,000+ strings completes; exports round-trip.
**Notes:** From T-054: exports may optionally include the literal translation with its KB entry IDs (once T-058 exists; still open).
**Outcome (2026-11-20):**
- **Core (`src/CytoLex.Core/Batch/`):**
  - `BatchReader`: text, or CSV/TSV with a header. String column `iscn`/`karyotype`/`input`/`string`, else the first; optional `id`. Decodes UTF-8 or UTF-16 by BOM, falling back to Latin-1 with a warning. Content problems are warnings that name the line. It also reads its own CSV export back.
  - `BatchRunner`: parallel, input order, progress, cancel.
  - `BatchSummary`: per edition.
  - `BatchExport`: JSON `cytolex-batch` v1 with full reports; CSV summary with one row per string × edition, UTF-8 BOM. Values are written raw (no formula-injection prefix, which would break the round-trip).
  - Internal RFC 4180 `Csv`.
- **Desktop:** main tabs "Single string" / "Batch". `ViewModels/BatchViewModel.cs` and `BatchRowViewModel.cs`: open, edition, run/cancel with progress, summary, filter, export CSV/JSON, open a row in the single check. File pickers are in `Views/MainWindow.axaml.cs`.
- **Sample files (added at the user's request):** `kb samples` (`tools/src/cytolex_tools/kb/samples.py`) writes `tests/data/batch/2016/` from the KB examples:
  - `karyotype.txt`, `arr.txt`, `combined.txt`;
  - `all.csv`/`all.tsv` (940 rows with rule, kind and citation);
  - `large.csv` (1,200 rows);
  - plus `tests/data/batch/README.md`.

  A C# test checks the `type` column against `Segmenter`. No ish-only file until T-015/T-016.
- **Tests:**
  - `tests/CytoLex.Core.Tests/Batch/` (reader, the 1,200-string run against sequential validation, cancellation, summary, sample files, JSON/CSV round-trips, export re-read as input);
  - `tests/CytoLex.Desktop.Tests/BatchViewModelTests.cs`;
  - `tools/tests/test_kb_samples.py`.
- **Docs:** CLAUDE.md, README.md, `docs/design/core.md` §9.
- **Deviations from the plan:**
  - The "Not implemented" filter was dropped as useless with all editions; "partly validated" covers it.
  - JSON items nest `Item {Line, Id, Input}` next to `Report`.

### T-039 — Rule browser
**Status:** done
**Description:** Browse and search rules, paraphrases, quotes and examples (FTS). Clicking a diagnostic opens its rule.
**Acceptance:** Search works; diagnostic → rule navigation works; works without quote files.
**Notes:** Decided with the user at planning: rules only (vocab entries → T-059), a third main tab "Rules".
**Outcome (2026-11-20):**
- **Data:** `src/CytoLex.Desktop/Rules/RuleStore.cs` reads `build/kb.sqlite` read-only via `Microsoft.Data.Sqlite.Core` 10.0.14 + `SQLitePCLRaw.bundle_e_sqlite3` 2.1.13 (FTS5); checks `meta.format_version` = 2. User queries become quoted FTS5 prefix phrases (`t(9;22)` → `"t 9 22"*`), ANDed; `rules_fts` and `quotes_fts` searched together (bm25, exact ID first); section filter; `Rule(edition, id)` with citations, scope, fix, review, related, examples, quotes (optional). The desktop build copies the database as `kb/kb.sqlite` (`CytoLexKbDatabase`; missing → build error).
- **Desktop:** "Rules" tab (`ViewModels/RuleBrowserViewModel.cs`: edition, section, hits, `RuleDetailViewModel` with related-rule links and "Check" on examples → single-string check). A diagnostic's rule ID is a link (`MainWindowViewModel.OpenRule`, `DiagnosticItemViewModel.HasKbRule`/`Edition`); core codes have none. A missing or stale database disables only this tab.
- **Tests:** `tests/CytoLex.Desktop.Tests/RuleStoreTests.cs`, `RuleBrowserTests.cs` (every runtime rule listed and opened with matching metadata, search kinds, odd input, no-quotes copy, missing/foreign database, diagnostic → rule, related links, example check). `dotnet test`: 4,277 total, 0 failed, 8 skipped; `dotnet format` clean.
- **Docs:** `CLAUDE.md`, `README.md`, `docs/design/core.md` §6; note on T-059.
- **Deviations from the plan:** no search debounce (FTS search is fast enough per keystroke); the browser is created at startup rather than lazily (a missing database still doesn't block the app).

### T-040 — Fix UI
**Status:** done
**Description:** Before/after diff of a fix (additions green, removals red) with an "Apply" button that re-validates.
**Acceptance:** Applying the fixes for the KB's invalid examples yields valid strings.
**Notes:** **Outcome (2026-11-20):** core: `FixDiff` (`src/CytoLex.Core/Validation/FixDiff.cs`: same/removed/added runs built from a fix's edits, trimmed per edit by the shared prefix/suffix, adjacent runs merged). Desktop: each level-1 fix is shown as a diff (removed red + struck through, added green, changed spaces as `·`) with Apply (`ViewModels/FixItemViewModel.cs`, `DiffPartViewModel.cs`, colours in `Highlighting/DiffBrushes.cs`); "Fix all (N)" previews one `Fixer.ApplyAll` round for the effective context, offered for ≥2 fixes (`FixAllViewModel.cs`); `MainWindowViewModel.ApplyFix` sets the input (re-validates) and shows "Applied: …"; the editor takes programmatic text through a document replace, so Ctrl+Z undoes an Apply. Acceptance: `tests/CytoLex.Core.Tests/KbExamples/FixToValid.cs` + `FixToValidTests.cs` (repeated fix all, up to 5 rounds, on every invalid KB example: 125 fixed to valid, 33 stop at an explanation, 16 valid as is, 8 skipped, 0 failed; 11 non-book strings end valid), report `build/review/fix-coverage.md`; `FixDiff` unit tests in `Validation/FixTests.cs`; desktop `tests/CytoLex.Desktop.Tests/FixUiTests.cs`. `dotnet test`: 4,484 total, 0 failed, 16 skipped; `dotnet format` clean. Docs: `docs/design/core.md` §10, `CLAUDE.md`/`README.md`. Deviations from the plan: the reorder diff is trimmed by prefix/suffix, not character by character (`t(5;2)` → marks `5;2`); `DiffPart` colours come from style classes, not bound brushes. Follow-up: T-073 (stray spaces inside a karyotype are not reported).

### T-069 — Abnormality list: core API + desktop panel
**Status:** done
**Description:** Return the abnormalities of each karyotype as a flat list from the core and show them in the desktop app.
- **Core:** a public list per edition result (e.g. `EditionResult.Abnormalities`), flattened from the semantic model (T-065, which the validator currently discards), in input order. Per entry: the written text and span, kind (gain/loss/structural symbol), symbol, chromosomes, breakpoints (band + arm), a short form (`t(9;22)`, `+8`, `del(5q)`), the parts of a derivative, the cell line it belongs to, flags (partial, questioned `?`, constitutional `c`, inheritance, copies `×n`) and the diagnostics inside its span. Document the API in `docs/design/core.md` (§5/§9). Reusable by batch export (T-038), the web service (T-044) and the translation (T-058).
- **Desktop:** an "Abnormalities" panel grouped by cell line (header with count, sex, cell count); one card per abnormality with a kind chip, the written form and short form, chromosome/band tags, flag icons and a diagnostics dot; derivatives expand to their parts; hover/click selects the span in the editor; partial items faded.
**Acceptance:** Every abnormality of the KB karyotype examples appears in the list with the right span, kind, chromosomes and breakpoints; derivatives list their parts; partial items are flagged; the panel groups by cell line and selecting a card selects its span.
**Notes:** Created 2026-11-20 from T-036 (decided with the user: one task for core + panel, no clinical catalogue matching for now). Only karyotype segments produce entries (ish/arr: T-033/T-034). `idem`/`sl`/`sdl` clones list only their own items until T-032 resolves the stemline. **Outcome (2026-11-20):** core: `EditionResult.CellLines` (`CellLineSummary`) and `EditionResult.Abnormalities` (`AbnormalityEntry` with kind, sign, symbol, chromosomes, `BreakpointSummary`s, short form, derivative `Parts`, `or` `Readings`, `AbnormalityFlags`, linked diagnostic indices, `KbRef`s), types in `src/CytoLex.Core/Validation/Abnormalities.cs`, built from the semantic model by `AbnormalityList.cs` in `Validator.Run`; plain DTOs, JSON round-trip tested. Desktop: "Abnormalities" panel right of the diagnostics (grid splitter), grouped by cell line (`CellLineGroupViewModel`: count, sex, cells, mos/chi/recipient/donor), cards (`AbnormalityItemViewModel`: kind chip, written + short form, chromosome/band tags, flag badges, diagnostics dot, faded when partial, expander for derivative parts / `or` readings); click on a card or part selects its span (`MainWindowViewModel.SelectSpan`), hover shades it (`DiagnosticColorizer.HoverSpan`). Tests: `tests/CytoLex.Core.Tests/Validation/AbnormalityListTests.cs` (every KB karyotype example compared item by item with the bound model + spot checks), `tests/CytoLex.Desktop.Tests/AbnormalityPanelTests.cs`. `dotnet test`: 3,190 total, 0 failed, 13 skipped; `dotnet format` clean. Docs: `docs/design/core.md` §5/§9/§14, `CLAUDE.md`/`README.md` status. Deviations from the plan: none of substance (hover shades the span instead of selecting it, as planned; short form keeps `×n`/`c`/inheritance in the flags).

### T-070 — Abnormality list: inherited changes of idem/sl/sdl clones (expandable reference, full form)
**Status:** todo
**Description:** A cell line written with `idem`/`sl`/`sdl` lists only its own items in `EditionResult.Abnormalities`, and the reference itself is one opaque `CloneReference` entry. Show what the reference means for the given string, e.g. `46,XX,t(9;22)(q34;q11.2)[7]/43,sl,-7,-9,-15[11]` → line 2 = `43,XX,t(9;22)(q34;q11.2),-7,-9,-15[11]`:
- **Core — inherited entries:** each subclone lists the changes it inherits from the clone it refers to (`CellLineSummary.BasedOn`, T-032) as entries marked inherited, with the source cell line and the source span; reuse the reference expansion of the clone checks (`CloneLine.Changes`, `src/CytoLex.Core/Rules/Checks/Clones.cs`).
- **Core — full form:** each cell line written with a reference gets its written-out form (count, sex chromosomes, inherited + own abnormalities, cell count), e.g. `43,XX,t(9;22)(q34;q11.2),-7,-9,-15[11]`.
- **Desktop:** the `idem`/`sl`/`sdl` row in the Abnormalities panel can be expanded: it names the reference (`sl` = stemline → line 1, with that line's text) and shows the inherited entries under it, greyed and tagged "from line N"; clicking one selects its source span in the editor. The cell-line header shows the full form with a copy button.
**Acceptance:** For every KB example with idem/sl/sdl, each subclone lists the referenced clone's changes (× the `×n` of a polyploid subclone) plus its own; minus-signed stemline abnormalities (`–inv(6)`) remove the inherited one; chained references (`sdl`, `sdlN`, `slN`) and both sides of `//` resolve correctly; the full form of each subclone is reviewed against hand-written expansions for a sample set (incl. the stakeholder example above); the desktop panel expands the reference row, shows inherited entries and the full form, and the click-to-span works (desktop view-model tests).
**Notes:** Created 2026-11-20 from T-032 (out of its scope, decided with the user at plan approval). 2026-11-21: scope extended from stakeholder feedback (user): expandable reference row, inherited entries with source links, full form per clone (options A+B+C). A plain-language explanation of the reference (option D) belongs to the translation (T-057/T-058); a clone tree view (option E) was not chosen. Open for planning: whether the inherited entries also go into the batch export (T-038 JSON/CSV).

### T-071 — Arr: nucleotide positions vs. bands of the stated build
**Status:** discussion
**Description:** T-034 checks the form of arr results but does not compare the nucleotide positions with the bands written before them, or with the genome build in brackets (the band data has GRCh37 and GRCh38 coordinates, `kb/bands/2016/bands.yaml`). No KB rule states that the positions must lie in the named bands; §14.2 ¶3 ("as in the current genome builds") and ¶6 (array bands come from the genome browsers) are the nearest text. Decide:
- whether to add a rule (ID, citation, statement, severity: info, warning or error) and what it compares: start/end in the first/last band, a tolerance at band borders, a position beyond the chromosome end of the build;
- how to treat the book examples that print GRCh37 positions under `[GRCh38]` (§14.2 E23/E24, E34, E40, E44, E45; kept valid as printed in T-017): flagged as warnings, listed as known misprints, or exempt;
- whether a build-mismatch hint (positions fit the other build) is useful.
**Acceptance:** Decision in `docs/decisions.md`; if a rule is added, it goes into the KB (with citation and approved paraphrase) and a follow-up `todo` task implements the check.
**Notes:** Created 2026-11-20 from T-034 (deferred, decided with the user at plan approval). The misprinted bands found so far (§14.2 E27 18q22.33, §14.2.1 E4/E5 21q11.21) are `BAND.MUST_EXIST` invalid examples.

### T-072 — Abnormality list: arr aberrations
**Status:** todo
**Description:** `EditionResult.Abnormalities` and the desktop "Abnormalities" panel list karyotype segments only (T-069). Add the aberrations of arr segments (T-034, `ArrModel`): region or chromosome group, bands, positions and build, copy number (gain/loss/amp/cx/cth), proportion, zygosity and origin, linked diagnostics; one group per arr segment.
**Acceptance:** Every valid arr KB example lists one entry per aberration with its bands, positions and copy number; the desktop panel shows them, and clicking an entry selects its span.
**Notes:** Created 2026-11-20 from T-034 (out of its scope, decided with the user at plan approval). `Validator.Run` adds only `KaryotypeModel`s to `AbnormalityList` today.

### T-073 — Stray spaces inside a karyotype
**Status:** done
**Description:** Found while writing the T-040 non-book fix tests: `47,XY, +21` (space after a comma) and `46,X Y,dup(1)(q21q32)` (space inside the sex chromosome field) validate with no diagnostic. The KB has only `SYM.NO_SPACE_AT_PARENTHESIS`, `SYM.SPACE_BETWEEN_SYMBOLS` and `SYM.SPACE_BEFORE_CHROMOSOME_COUNT` (§3 ¶1). Decide:
- whether the book states (or its examples imply) that a karyotype has no spaces other than those rules; find the citation (§3, §4.1 chunks) or record that there is none;
- if there is a basis: a KB rule (ID, statement, severity, level-1 delete fix) and a follow-up `todo` to implement it in the parser/checks; if not, whether a warning is still wanted and how it's labelled.
Related: lower-case `46,xy` gets only `SYM.LISTED_ONLY` (warning, explanation); an upper-case fix fits T-041 ("did you mean") or a level-1 fix, part of the same decision.
**Acceptance:** Decision in `docs/decisions.md`; if a rule is added, it's in the KB with citation and a `todo` task implements the check.
**Notes:** Created 2026-11-20 from T-040. **Outcome (2026-11-20):** two KB rules in `kb/rules/2016/ch03/03-symbols-and-abbreviated-terms.yaml` (quotes in `kb/source-quotes/2016/ch03/`): `SYM.NO_OTHER_SPACE` (error, level-1 delete; §3 ¶1, §4.1 ¶1–2; the list of space places read as closed from the printed format, all 720 valid KB examples agree) and `SYM.LETTER_CASE` (error, level-1 replace; §3 ¶1, §4.1 ¶1; `i`/`I` and `x`/`X` decided by position). Decision in `docs/decisions.md`. Both rules have `paraphrase_checked: false` (awaiting the statement sign-off). The four new invalid examples are classified in `tests/CytoLex.Core.Tests/Parsing/InvalidExampleExpectations.cs` and skip in the harness until T-074; `kb samples` re-run. `dotnet test`: 4,517 total, 0 failed, 24 skipped. Follow-ups: T-074 (checks), T-075 (severity vs. CytoGPS, after T-047; a warning for the space rule was considered and kept open).

### T-074 — Checks for stray spaces and letter case
**Status:** done
**Description:** Implement the T-073 rules (`docs/decisions.md`, 2026-11-20):
- `SYM.NO_OTHER_SPACE`: scan the token trivia for whitespace in a place no spacing rule prescribes (`SYM.SPACE_BETWEEN_SYMBOLS`, `SYM.SPACE_BEFORE_CHROMOSOME_COUNT`, `KARYO.DE_NOVO_SPACE`, `MAR.CONSTITUTIONAL_C_SPACE`, `GEN.GENOME_BUILD_REQUIRED`, `ARR.INHERITANCE_SPACING`/`ARR.ZYGOSITY`, "or"). One finding per space run, level-1 delete. A space already reported by another rule (`SYM.NO_SPACE_AT_PARENTHESIS`, `BAND.DESIGNATION_FORMAT`, the arr qualifier rules) is not reported twice. The parser must keep reading across the space (`46,X Y` → sex field `XY`).
- `SYM.LETTER_CASE`: case-folded lookup in the `symbols` vocabulary, plus the sex chromosome field. Level-1 replace when the match is unique or decided by position (`i`/`I`, `x`/`X`); this replaces `SYM.LISTED_ONLY` for those tokens. Arm letters stay `BAND.ARM_SYMBOL`. Update `tests/CytoLex.Core.Tests/Parsing/InvalidExampleExpectations.cs`: `46,xy` and `46,XX,Del(5)(q13q33)` are recorded with today's parser finding `SYM.LISTED_ONLY`.
- Remove the matching entries from `FixToValidTests` `Pending` / `PendingExamples.cs` if any; add the non-book strings `47,XY, +21`, `46,X Y,dup(1)(q21q32)`, `46,xy`, `T(9;22)(q34;q11.2)` to the fix-to-valid acceptance test.
**Acceptance:** the new KB examples of both rules pass the harness, with their `corrected` forms as fix previews; no valid KB example gets either diagnostic; `dotnet test` green; `dotnet format` clean.
**Notes:** Created 2026-11-20 from T-073. **Outcome (2026-11-20):** `SYM.NO_OTHER_SPACE`: `src/CytoLex.Core/Rules/Checks/NoOtherSpaceCheck.cs` (every space between two tokens unless prescribed: around `or`, between two listed symbols, symbol + chromosome count, arr after the build or after `arr` when the build is missing, before an arr qualifier; spaces of `SYM.NO_SPACE_AT_PARENTHESIS`/`BAND.DESIGNATION_FORMAT` left to them through their shared `Gaps`/`Designations`; segment-edge whitespace not reported). `SYM.LETTER_CASE`: the lexer reads a run that folds to exactly one listed token (`Del`, `T`, `INV`, `y`) or a sex-letter run with a lower-case letter (`xy`) as the listed form (`SyntaxToken.Text` = listed, new `WrittenText` = input, `IsCaseFolded`; the tree stays lossless); the parser folds `45,x` → `X` and `I(` → `i` by position before parsing; `LetterCaseCheck.cs` reports each folded run with a replace fix (only the wrong letters: `Xy` → `y`). Tests: `tests/CytoLex.Core.Tests/Rules/SpaceAndCaseChecksTests.cs`, `LexerTests.cs`, `InvalidExampleExpectations.cs` (`46,xy`, `Del(5)` → `[]`), 5 non-book strings in `FixToValidTests.cs`. The 4 KB examples are fixed to valid in one round. `dotnet test`: 4,581 total, 0 failed, 16 skipped; `dotnet format` clean. Docs: `docs/design/core.md` §4, `CLAUDE.md`, `README.md`. Deviations: the bare `T(9;22)(…)` non-book string became `46,XY,T(9;22)(q34;q11.2)`; the space after `arr` without a build is left to `GEN.GENOME_BUILD_REQUIRED`. Note: `47,XXY c,…` (invalid example of the not yet checked `KARYO.CONSTITUTIONAL_C`) is reported by `SYM.NO_OTHER_SPACE`; that rule's check must claim this space when it is written.

### T-075 — Stray spaces and letter case: severity vs. CytoGPS
**Status:** discussion
**Description:** T-073 made `SYM.NO_OTHER_SPACE` (spaces outside the prescribed places) and `SYM.LETTER_CASE` (symbols/sex chromosomes not in the printed case) `error` with a level-1 fix. The space rule rests on the printed format, not on an explicit book sentence, so a `warning` + fix was also considered (2026-11-20, kept as error for now). Once CytoGPS is researched (T-047), compare:
- how CytoGPS treats stray spaces (`47,XY, +21`, `46,X Y,dup(1)(q21q32)`, `46 ,XX`, `47,XX,+ 21`) and wrong letter case (`46,xy`, `46,XX,Del(5)(q13q33)`, `46,XY,T(9;22)(q34;q11.2)`): rejected, accepted silently, or normalised;
- what each choice means for the CytoGPS 2×2 comparison and batch results.
Then decide whether each rule stays `error` or becomes `warning` (book-stated spacing rules such as `SYM.SPACE_BETWEEN_SYMBOLS` stay `error` either way). The book, not CytoGPS, remains the basis; CytoGPS informs the choice.
**Acceptance:** Decision in `docs/decisions.md`; if a severity changes, the rule's `severity` and review note are updated in the KB (then `kb build`, `dotnet build`).
**Notes:** Created 2026-11-20 from T-073. T-047 findings (`docs/research/cytogps.md` §4, §5.1, §8): CytoGPS strips all whitespace before parsing and matches keywords case-insensitively, so every listed space/case string is accepted silently (`Success`), with no report or normalisation notice.

## External comparison (CytoGPS)

### T-047 — Research CytoGPS
**Status:** done
**Description:** Find out (web access failed during planning):
- Available interfaces: web form and API (endpoint, fields).
- Input/output format (validity, error messages, LGF vectors, JSON).
- Supported ISCN version and notation coverage.
- Batch limits.
- Source code availability and licence; a public container image.
- Terms of use and privacy.
- Contact the authors if needed. Sites: `https://cytogps-app.mangograss-a1cf8a6c.westus2.azurecontainerapps.io/karyotype_parser`, cytogps.org, the RCytoGPS package.
**Acceptance:** Findings with source URLs in `docs/research/cytogps.md`.
**Notes:** **Outcome (2026-11-20):** `docs/research/cytogps.md` — findings with sources: interfaces (hosted `POST /singleKaryotypeAnalysis` returns HTML in JSON, `POST /batchKaryotypeAnalysis` returns a zip; no documented API; `cytogps.org` no longer resolves), JSON/LGF format (5 statuses, 916 ISCN 2016 bands), coverage (ISCN 2016, short-system karyotypes only, ASCII only, whitespace stripped, case-insensitive; no ish/arr/detailed system), licence (no file in the repo; site User Agreement = non-commercial use), privacy (no policy; batch results kept on the server). Beyond the plan (with the user): the local copy at `C:\_\NMDP\ISCN\CytoGPS` was built and run with a portable Temurin 21 JDK in `C:\_\Tests\PortableJDK` plus `javax.json-1.1.4.jar` and a small wrapper (§7); it matches the hosted app on 28 probe strings and a 5-line batch. Both tools were run on the 951 KB example strings → `docs/research/cytogps-comparison.md` (feature table today/planned, accuracy, caveats; for business presentations) and `docs/research/data/cytogps-vs-cytolex-2016.csv`. Docs: `docs/decisions.md` pointer, `README.md`/`CLAUDE.md` (`docs/research/`), notes in T-048/T-075. Follow-ups: T-076 (licence enquiry), T-077–T-080 (batch statistics, ideogram, CytoGPS JSON export, lenient mode); T-066 and T-044 extended (derivative structure in the abnormality list, browser page).

### T-076 — Ask the CytoGPS authors about the licence and newer code
**Status:** todo
**Description:** The GitHub repo `i2-wustl/CytoGPS` has no licence file. The hosted site's User Agreement grants non-commercial use of "the computer code provided here". Ask the authors (WashU Institute for Informatics / the RCytoGPS maintainer) whether that agreement covers the repo code, and whether a newer parser or the web app's server code is available. Draft text: `docs/research/cytogps.md` §9. The user sends it; Claude doesn't.
**Acceptance:** The answer (or "no reply by <date>") recorded in `docs/research/cytogps.md` §6/§9. If redistribution is allowed, add a note to T-043/T-049.
**Notes:** Created 2026-11-20 from T-047. Needed only before any redistribution or public deployment; private local use is already covered.

### T-077 — Batch statistics per band
**Status:** todo
**Description:** CytoGPS reports for each band the share of clones with a loss, gain or fusion there (its APL/APG/APF), plus an aggregate LGF table with one row per clone (`docs/research/cytogps.md` §3, repo `README.txt` "How to use Summary Statistics"). Add the same to batch results: per edition, over the clones of every valid string, from the T-050 LGF. Show it in the desktop "Batch" summary and export it (CSV: one row per band; one row per clone for the aggregate table).
**Acceptance:** Statistics and both CSVs for the KB sample batch; the numbers for a small hand-checked batch match a hand count; the counting rules (which clones count, which strings are skipped) are documented.
**Notes:** Created 2026-11-20 from the CytoGPS comparison (T-047, `docs/research/cytogps-comparison.md`). 2026-11-21: priority 2 → 3 with T-050 (user).

### T-078 — Desktop: ideogram view
**Status:** todo
**Description:** CytoGPS draws the chromosomes at 850 resolution with each band's loss/gain/fusion, clone by clone. Add a desktop panel that draws the ideogram from the KB band data (`kb/bands/<version>/bands.yaml`, already in the runtime bundle) and colours the T-050 LGF of the selected cell line. Link it to the abnormality list (T-069): selecting an abnormality highlights its bands.
**Acceptance:** The panel shows the LGF of every cell line of a sample of KB examples; band positions are checked against the band data; selection works both ways.
**Notes:** Created 2026-11-20 from the CytoGPS comparison (T-047). Low priority (user).

### T-079 — Export in CytoGPS's JSON result format
**Status:** todo
**Description:** Write batch results in CytoGPS's result format (`producer`, `date`, `iscn2016_bands`, `output[]` with `karyotype`, `status`, `parsing_result[].loss_gain_fusion_computing`; `docs/research/cytogps.md` §3) from the T-050 LGF, so that the RCytoGPS R package (`readLGF`) reads it. Map our verdicts to CytoGPS's five statuses, and mark the producer as CytoLex.
**Acceptance:** RCytoGPS `readLGF` reads an export of the KB sample batch; for strings both tools accept, the LGF arrays equal the local CytoGPS output, or each difference is explained.
**Notes:** Created 2026-11-20 from the CytoGPS comparison (T-047). Low priority (user). Also simplifies T-049/T-051: both sides produce the same format.

### T-080 — Optional lenient mode
**Status:** discussion
**Description:** CytoGPS silently accepts stray spaces and any letter case (`docs/research/cytogps.md` §4). Decide whether CytoLex gets an optional "lenient" mode (e.g. for old records) that reports `SYM.NO_OTHER_SPACE` and `SYM.LETTER_CASE` as warnings instead of errors, and if so: which rules it covers, where it's switched on (validator option, desktop, batch), and how results show that it was on. The book stays the basis; the default stays strict.
**Acceptance:** Decision in `docs/decisions.md`; if adopted, a `todo` task for the implementation.
**Notes:** Created 2026-11-20 from the CytoGPS comparison (T-047). Depends on T-075: if the two rules become warnings anyway, lenient mode may not be needed.

### T-081 — Desktop: CytoGPS panel in the single-string check
**Status:** todo
**Description:** Show CytoGPS's result next to CytoLex's in the single-string check, using the T-049 adapter: a "CytoGPS" tab beside the edition tabs with its verdict, raw status, messages, repaired string, the string as sent (ASCII typed forms) and an agreement badge against the CytoLex 2016 verdict. Notation CytoGPS doesn't cover (ish, arr, detailed system; `docs/research/cytogps.md` §4) is shown as "not supported by CytoGPS", not as a disagreement. CytoGPS runs on demand ("Check with CytoGPS" button, optionally automatic after a typing pause), not on every keystroke (one JVM per run).
**CytoGPS is optional and must never block CytoLex:**
- CytoLex validation, highlights, fixes and every other tab work exactly as without CytoGPS, whether CytoGPS is configured or not.
- Not configured → the tab shows the "CytoGPS not configured: …" reason and a pointer to README → "CytoGPS (optional)"; no process is started.
- CytoGPS runs in the background (async, cancellable); the UI stays responsive, a new input cancels the running check, and a stale result is never shown for a changed input.
- A failure (compile error, crash, timeout, unreadable output) shows the adapter's message in the tab only; nothing throws into the app.
**Acceptance:** View-model tests with a fake checker: not configured, success (valid/invalid), failure, timeout/cancel, input changed while running, unsupported notation; CytoLex results are identical with and without a checker. Checked by hand against the local CytoGPS build on book strings.
**Notes:** Created 2026-11-21 (user request during T-049). Can be built before T-051; the agreement badge here is a plain verdict comparison, and T-051's comparison engine can replace it later. 2026-11-21 (T-051): use `CytoGpsCoverage.Unsupported` for "not supported by CytoGPS" (`src/CytoLex.External/Comparison/`).

### T-082 — Review the translation templates and golden file (ISCN 2016)
**Status:** todo
**Description:** Person review of the T-057 drafts in `kb/translation/2016/`: each template's wording (`templates.en.yaml`, 69 constructs) and each golden entry (`golden.yaml`: 372 book examples of ch. 4, 8, 9, checked against the book's printed explanation of the cited example, plus 26 synthetic strings). Set `review.checked: true` per entry; corrections go into the files (golden sentence and matching template together). A generated review sheet per book section (`build/review/translation/`) may help.
**Acceptance:** Every template and golden entry has `review.checked: true`, or a `review.note` saying why not; `kb build` and `dotnet test` pass; the desktop draft note (T-059) disappears for reviewed templates.
**Notes:** Created 2026-11-21 from T-057 (user: no review for the pilot; drafts stay usable meanwhile). Best done after T-058, so the renderer's output can be reviewed alongside.

### T-083 — Refactor: remove hand-written partial classes (`Binder`, `Parser`)
**Status:** todo
**Description:** Apply the code standard "avoid partial classes" (`CLAUDE.md` → Code standards; decision 2026-11-22, T-058) to the existing hand-written splits:
- `src/CytoLex.Core/Semantics/Binder.cs` + `Binder.Arr.cs` (public `Binder` and its nested `Run` class, both partial): e.g. a karyotype binder and an arr binder sharing a small binding context (KB, tree, issue/span/ref helpers), with `Binder` as the entry point.
- `src/CytoLex.Core/Syntax/Parser.cs` + `Parser.Arr.cs`: e.g. the arr grammar as its own class over the shared token cursor/recovery helpers.
Keep the public API (`Binder.Bind`, `Binder.BindArr`, `Parser.Parse`, `Parser.RuleIds`) unchanged. Out of scope: partials required by generators/frameworks (`[GeneratedRegex]` in `ContextInference`, `FixHelpers`, `KbCatalog`; CommunityToolkit view models; Avalonia `*.axaml.cs`).
**Acceptance:** No hand-written `partial` class left in `src/` or `tests/` (generator/framework ones excepted); behaviour unchanged: `dotnet test` passes with the same counts (KB-example harness, parser/binder tests, golden translation tests), `dotnet format --verify-no-changes` passes.
**Notes:** Created 2026-11-22 from T-058 (user). The T-058 translation renderer was already restructured this way (`RenderContext` + phrase classes in `src/CytoLex.Core/Translation/`), which can serve as the pattern.

### T-048 — Decide CytoGPS integration mode and data policy
**Status:** done
**Description:**
- Integration: local build, container, or online with opt-in.
- What data may be sent online (an organisational policy check is needed if real patient karyotypes may be involved).
**Acceptance:** Decision in `docs/decisions.md`.
**Notes:** Research done in T-047: `docs/research/cytogps.md` §6–§8 (local build works and matches hosted; hosted has no privacy policy and keeps batch results on the server; recommendation: local default, hosted for synthetic strings only).
**Outcome (2026-11-20):** Decided with the user (interview): CytoGPS runs as a local build only (no hosted adapter, no online path in code or CI, CytoGPS code never vendored); test, sample and comparison data are synthetic or ISCN-book strings only, real patient karyotypes are never committed, cached in the repo or sent out, and real-data use needs an organisational policy check; the hosted app may be used by hand with synthetic/book strings only. Recorded in `docs/decisions.md` (External comparison section + log entry) and a data-policy line in `CLAUDE.md`. Follow-ups: T-049 description/acceptance changed to the local adapter (opt-in guard and throttling dropped); T-044 notes carry the in-memory-only rule for uploads. No deviations from the plan.

### T-049 — `ExternalChecker` interface + CytoGPS adapter
**Status:** done
**Description:** A pluggable interface for external checkers, plus a CytoGPS adapter around a **locally compiled** CytoGPS (batch file → JSON, one JVM per batch; config keys in `config/local.yaml`, build in `build/cache/cytogps/`) with a response cache. No online path (T-048).
**Acceptance:** The adapter returns normalized results for a sample batch; it makes no network access; a missing local build gives a clear "CytoGPS not configured" result.
**Notes:** 2026-11-20: description and acceptance follow the T-048 decision (local build only, no hosted adapter; synthetic/book strings only); the earlier hosted opt-in guard and throttling are dropped. Setup and wrapper: `docs/research/cytogps.md` §7–§8.
**Outcome (2026-11-21):** New library `src/CytoLex.External/` (in `CytoLex.slnx`; YamlDotNet 18.1.0 pinned):
- `IExternalChecker` → `ExternalBatchResult`: one normalized item per input (verdict Valid/Invalid/NotConfigured/Failed, CytoGPS status, messages, repaired string, clones with LGF counts aligned with the 916 bands); it never throws for setup or tool problems.
- `TypedSymbolForms`: printed → ASCII typed forms from the KB vocabulary.
- `CytoGps/`: `CytoGpsConfig` (`config/local.yaml` → `cytogps`, env `CYTOLEX_CYTOGPS_<KEY>`); `CytoGpsBuild` (javac on demand into `build/cache/cytogps/`, fingerprint, javax.json SHA-1 pinned); `CytoGpsCli.java` (our wrapper, embedded); `CytoGpsJson`; `CytoGpsChecker` (one JVM per batch, temp input outside the repo, in-memory cache, disk cache only with `disk_cache: true`).

Tests: `tests/CytoLex.External.Tests/` (25 unit tests with a fake runner + an integration test that skips when not configured). The integration test passed against the local build on `tests/data/batch/2016/karyotype.txt` (597 strings + 3 spot checks, ~46 s; the build was reused on the second run).

Docs: `CLAUDE.md`, `README.md` ("CytoGPS (optional)"), `config/local.example.yaml`, `docs/decisions.md` (T-049 entry), `docs/research/cytogps.md` §7, `docs/research/cytogps-comparison.md`.

Deviations from the plan: `ExternalClone` also carries `DetailedSystems`; there is a test seam for the expected jar SHA-1.

Known limitation: CytoGPS writes its result file only at the end of a batch, so a crash on one string fails the whole batch (not seen on the sample).

Follow-ups: T-081 (desktop CytoGPS panel, never blocks CytoLex) added; T-052 notes extended.

### T-050 — LGF computation in the core library
**Status:** todo
**Description:** Compute Loss–Gain–Fusion per band from a parsed karyotype, in a form comparable with CytoGPS output.
**Acceptance:** Matches hand-checked LGF for a sample set of karyotypes.
**Notes:** From T-065: the semantic model gives breakpoints for both systems but band compositions only for the detailed system; T-066 computes them for the short system (proposed as a dependency).
**Deferred (2026-11-21, user):** priority 2 → 3. LGF is research output (CytoGPS's data model, not defined by ISCN) and doesn't improve validation or fixes; T-051 now compares verdicts only and no longer depends on T-050. How CytoGPS computes LGF (read from its source): `docs/research/cytogps.md` §3.1. Open design questions for when it resumes: (1) a CytoGPS-compatible event model (per-event counts, sex field and count ignored, `×n` repeats) or a copy-number model against the baseline; (2) short-system `der`/`rec`/`ider` need band compositions, so T-066 is a likely dependency; (3) the band axis: our 850 level + p10/q10 per arm with a mapping onto CytoGPS's 916 entries (it keeps `6p24` and `9q34.1` whole), or CytoGPS's list itself.

### T-051 — Comparison engine + report (no CLI)
**Status:** done
**Description:** Run a batch through our validator and CytoGPS. Report a 2×2 agreement matrix and the disagreements (verdicts), written by an opt-in test (no CLI). LGF differences where both accept are optional and come only once T-050 is done. Triage labels (our bug / CytoGPS limitation / version difference); confirmed cases can be exported as KB examples. Compare only against CytoGPS's ISCN version.
**Acceptance:** The comparison report (verdicts) is generated for a sample batch; triage export works.
**Notes:** 2026-11-21: T-050 removed from Depends (user; LGF deferred, see T-050). 2026-11-21: CLI dropped (user): the engine goes into `CytoLex.External` for the desktop (T-052, T-081), and an opt-in test writes the report to `build/review/comparison/`; acceptance reworded from "The CLI produces the report".
**Outcome (2026-11-21):** Comparison engine in `src/CytoLex.External/Comparison/`:
- `ComparisonEngine.CompareAsync`: CytoLex in the checker's edition + one checker batch → per-string `Agreement` (both valid/invalid, only CytoLex/checker invalid, not supported, not compared) and the 2×2 `AgreementMatrix`; CytoLex errors and warnings/infos per string; suggested labels.
- `CytoGpsCoverage` (`IExternalCoverage`): strings outside CytoGPS's grammar (non-karyotype segments, detailed system, `fra`/`upd`/`rec`/`fis`/`tas`/`neo`, `//`, per-abnormality cell counts) stay out of the matrix; rule families CytoGPS doesn't check (for the `checker-limitation` suggestion).
- `TriageFile`: committed YAML `tests/data/comparison/cytogps-2016-triage.yaml` (labels `our-bug`/`checker-limitation`/`version-difference`/`severity-policy`, `confirmed`, `note`, optional `expected` + `kb_rule`; line-numbered load errors; stale entries). Format and workflow: `tests/data/comparison/README.md`.
- `ComparisonReport`: Markdown, CSV, triage draft and KB-example draft (`cite: TODO`; nothing edits `kb/`), written to `build/review/comparison/` by the opt-in `CytoGpsComparisonReportTests`.

First run (699 distinct strings of `all.csv`): 240 not supported, 459 compared, 84.3 % agreement (348 both valid, 39 both invalid, 64 only CytoLex invalid, 8 only CytoGPS invalid). All 72 disagreements triaged with the user: 67 `checker-limitation` (64 book-invalid examples CytoGPS accepts, 3 book-valid examples it can't read), 5 `severity-policy` (book-invalid examples CytoLex reports only as warning/info). No CytoLex bug found.

Tests: 31 new unit tests (fake checker + real validator) in `tests/CytoLex.External.Tests/Comparison/` + the report test; `dotnet test` 4,652 total, 0 failed. Docs: `CLAUDE.md`, `README.md`, `docs/decisions.md` (T-051 entry), `docs/research/cytogps-comparison.md`; T-052/T-081 notes. Local `config/local.yaml` got the `cytogps` section (javax.json jar copied to `C:/_/NMDP/ISCN/CytoGPS-lib/`, not in the repo).

Deviations from the plan: no CLI (user); new label `severity-policy` and CytoLex warnings/infos in the report, added after the first run (user); the triage file was filled from the first run's results.

### T-052 — Desktop: comparison view
**Status:** todo
**Description:** Batch table with per-row agreement status, filters (e.g. disagreements only), side-by-side detail and triage; an LGF difference view only once T-050 exists.
**Acceptance:** Works on a sample batch, using the comparison engine.
**Notes:** 2026-11-21: like T-081, CytoGPS must never block CytoLex: when it is not configured, fails or is slow, the Batch tab and its CytoLex results work as before, and the comparison view shows why CytoGPS results are missing. 2026-11-21 (T-051): build on `ComparisonEngine.CompareAsync` (per-string `Agreement`, `AgreementMatrix`, reasons for not supported/not compared), `TriageFile` (labels, stale entries) and `ComparisonReport` (CSV/Markdown/draft exports) in `src/CytoLex.External/Comparison/`.

## Later

### T-041 — Level 2 "did you mean" suggestions
**Status:** todo
**Description:** Suggestions for symbol typos (`dl` → `del`, `trans` → `t`) and nearest existing band (`9q35` → `9q34`?). Shown as 1–3 options, never applied automatically.
**Acceptance:** Suggestions appear for a test set of typos; nothing is auto-applied.
**Notes:**

### T-042 — Underlined homologue notation
**Status:** todo
**Description:** Underlining distinguishes homologous chromosomes, but plain text can't carry it. Decide on an input convention, then parse and validate it.
**Acceptance:** Convention documented; related KB examples pass.
**Notes:** KB rules with underlined examples (U+0332, `needs_review`): `FRA.HOMOLOGUE_UNDERLINING` (§7.2, T-010) and `DER.HOMOLOGUE_UNDERLINING` (§9.2.3 E32–E34, T-012), and `TRANSLOC.HOMOLOGUE_UNDERLINING` (§9.2.17.1 E8, E10, T-013).

### T-043 — Copyright cleanup before public deployment
**Status:** discussion
**Description:** Before any public deployment: remove or exclude verbatim quotes and keep only paraphrased rules plus citations. Review licensing of the ISCN content.
**Acceptance:** Decision and checklist in `docs/decisions.md`.
**Notes:**

### T-044 — Web service
**Status:** todo
**Description:** A web service exposing single/batch validation from the core library, plus a browser page on top of it (no install): single-string check with highlights, diagnostics with citations and fixes, and batch upload with CSV/JSON export, like the desktop app.
**Acceptance:** Same results as the desktop app for the KB example suite; the browser page checks a string and a batch file end to end.
**Notes:** 2026-11-20: browser page added from the CytoGPS comparison (T-047, user). 2026-11-20: priority 3 → 4 (user): the ISCN 2016 core and the desktop app come first. Data policy for uploaded strings (T-048): keep them in memory only — no storage and no logging of strings — unless a later decision changes it (CytoGPS has no policy, `docs/research/cytogps.md` §6).

### T-045 — ISCN 2020 KB + version profile
**Status:** todo
**Description:** Extract the 2020 rules (same pipeline), adding version blocks to existing concept IDs and recording differences from 2016.
**Acceptance:** All-versions mode returns real 2020 results; differences from 2016 are cited.
**Notes:**

### T-046 — ISCN 2024 KB + version profile
**Status:** todo
**Description:** Same as T-045, for ISCN 2024.
**Acceptance:** All-versions mode returns real 2024 results.
**Notes:**

### T-061 — Presentation & documentation material for the pilot
**Status:** discussion
**Description:** Once the pilot works, the user will present CytoLex to colleagues. Decide what material to prepare, for whom and in what form, then create the `todo` tasks to produce it. Questions for the interview:
- Audience(s) and setting: cytogeneticists/lab staff, management, developers; talk length; live demo or not.
- Deliverables, e.g. a slide deck (claude.ai Slides artifact or `.pptx`), architecture and pipeline diagrams (from `docs/design/core.md`: segment → parse → bind → rules → result; KB build pipeline book → chunks → rules → bundle), a one-page overview/handout, a demo script with prepared example strings (valid, invalid with fixes, conditional context, multi-edition), screenshots or a short screen recording of the desktop app, an FAQ (scope, ISCN editions, limits, copyright).
- Format and source of truth: where the sources live (e.g. `docs/presentation/`, diagrams as Mermaid/SVG kept in git so they can be regenerated), branding/design system.
- Copyright: what book content may appear on slides (paraphrases and citations only, no verbatim quotes or idiogram images; see T-043).
- Timing: what can be drafted now (architecture, KB pipeline) and what needs the finished pilot (demo, screenshots).
**Acceptance:** Decision in `docs/decisions.md` (audiences, deliverables, formats, location, copyright limits); follow-up `todo` task(s) for producing the material added with Pri and Depends (the demo-dependent ones depending on the pilot desktop tasks, e.g. T-040).
**Notes:** Raised 2026-11-18 by the user, at the end of T-023.

### T-065 — Binder: syntax tree → semantic model
**Status:** done
**Description:** Walk the T-027 syntax tree and build the meaning nodes of `docs/design/core.md` §5 (`KaryotypeSegment`, `CellLine`, `Abnormality`, `Breakpoint`, `DerivativeStructure`, …) with spans, `KbRef`s (`rule:`, `vocab:`, `band:`) and `IsPartial` for recovered input. Normalise the short and detailed systems into the same breakpoint/derivative shape.
**Acceptance:** Every valid karyotype KB example binds to a complete (not partial) model; spot checks of the model for representative examples (del, t, der with several parts, detailed system, rec, mos/chi, `//`, variants, ×n, uncertainty `?`/`~`/`or`); recovered input gives partial nodes that name the parser diagnostics.
**Notes:** Created 2026-11-18 from T-027 (decided with the user: binder split from the parser). Input: `CytoLex.Core.Syntax.SyntaxTree` (node kinds in `SyntaxKind`; `Parser` is permissive, so the binder sees e.g. arms inside chromosome lists `del(5q)`/`dup(2p)`, symbol sequences `psu dic`/`der dic`, leading counts `3mar`/`12~20dmin`). Bands need the band set from the bundle (`KbRef` `band:`). T-065 added to the Depends of T-029, T-030 and T-031 (user, 2026-11-18), which check the semantic model.
**Outcome (2026-11-18):** Binder in `src/CytoLex.Core/Semantics/` (`Binder.cs`, `MeaningNodes.cs`, `KbRef.cs`); the validator binds every karyotype segment and checks read `RuleContext.Model` (`EditionProfile.Binder`). Decided with the user: breakpoints normalised for both systems (detailed = points at the `::`/`:` joins, minus arm ends and hsr/neo), `DerivativeStructure` only as written (short-system compositions → new task T-066); the binder reports no diagnostics (facts in the model, rules report). KbRefs: symbol vocab items + format rules (`Binder.SymbolRules`, section-checked against the vocab), resolved bands, construct rules. Partial nodes carry the `SyntaxDiagnostic`s themselves. Tests in `tests/CytoLex.Core.Tests/Semantics/` (KB examples valid/corrected complete, invalid → diagnostics named by partial nodes, 32 spot checks incl. 11 short/detailed pairs, KB-trace tests). Docs: `docs/design/core.md` §5 "As built (T-065)" lists the deviations from the sketch (parts, groups, item kinds, `Issues` instead of diagnostic IDs); CLAUDE.md/README status; note on T-050.

### T-066 — Band composition from the short system
**Status:** todo
**Description:** The binder (T-065) keeps `DerivativeStructure` only as written (detailed system); short-form abnormalities have none. Compute the band composition (ordered pieces with orientation, joins) from the short system for every pilot rearrangement type (del, dup, inv, ins, i, idic, r, t, der with several parts, rec, dic, trc, tas, trp, qdp, add, hsr, fis …), so rules can compare both systems and LGF (T-050) and the translation (T-058) can use one shape. Each type's composition must trace to its KB rule (§4.3, §9); missing semantics go into the KB first. Also show the composition in the abnormality list (T-069: `AbnormalityList.cs`, desktop "Abnormalities" panel) as the derivative written out in the detailed system, e.g. `der(9)t(9;22)(q34;q11.2)` → `der(9)(9pter→9q34::22q11.2→22qter)`. CytoGPS shows this (`derivative_chromosome_detailed_systems`, `docs/research/cytogps.md` §3).
**Acceptance:** For every KB example pair printed in both systems, the computed composition of the short form equals the bound detailed form; per-type unit tests; types without a defined composition are listed and left `null`. The abnormality list shows the detailed-system form for every abnormality with a composition (core API + desktop panel; checked on the KB pairs printed in both systems).
**Notes:** Created 2026-11-18 from T-065 (decided with the user: T-065 normalises breakpoints only). Useful for T-031 (short-vs-detailed rules such as `DER.COMPLEX_USE_DETAILED`), T-050 (LGF) and T-058 (translation). 2026-11-20: abnormality-list display added from the CytoGPS comparison (T-047, user).

### T-067 — Revisit genome-assembly-only bands per edition
**Status:** discussion
**Description:** Today (T-022, T-029) the six band names that are in the genome assemblies but not in the ISCN 2016 idiograms (`book: false`: 6p24.1–3, 9q34.11–13) are accepted in ISCN 2016 karyotype/ish strings with an info note (`BAND.GENOME_ASSEMBLY_ONLY`), and any other unknown name is a `BAND.MUST_EXIST` error. Once the ISCN 2020 and 2024 band data exist, compare each edition's idiograms with the assemblies (`kb bands --version 2020/2024`). If a later edition adopts these names (or all assembly bands), decide whether ISCN 2016 should reject them (error in 2016, valid in the edition that defines them), which would follow the per-edition principle (editions are not backward compatible). The comparison should also cover the other direction: the 15 book-only 2016 sub-bands without assembly coordinates, and any names a later edition drops or renames.
**Acceptance:** A per-edition comparison of book idiograms vs. assembly bands (2016/2020/2024); a recorded decision in `docs/decisions.md` on how `book: false` names are reported per edition, also for array/sequence strings, where ISCN 2016 itself uses assembly bands (§15.3 `9q34.11`); follow-up `todo` tasks for any KB/rule changes (e.g. the `BAND.GENOME_ASSEMBLY_ONLY` version blocks, the 2016 severity).
**Notes:** Created 2026-11-19 during T-029 at the user's request. The current behaviour is unchanged until then. The design already supports a per-edition answer: band data and the `book` flag are per edition (`kb/bands/<version>/`), and a rule runs only in editions whose KB has it.

### T-068 — Rules: ch. 9 symbol rules
**Status:** done
**Description:** The ch. 9 symbol rules left over when T-031 was split (decided with the user, 2026-11-19): `ADD.FORMAT`, `DEL.FORMAT`, `DUP.FORMAT`, `FIS.FORMAT`, `FRA.FORMAT`, `ISO.FORMAT`, `DER.NO_PH_SYMBOL`, `DER.ISODERIVATIVE`, `DIC.FORMAT`, `DIC.NO_DER_DIC`, `HSR.REPLACED_SEGMENT_PROXIMAL`, `HSR.INTERFACE_TWO_CHROMOSOMES`, `HSR.NO_ABR_SYMBOL`, `MAR.PLUS_SIGN`, `MAR.NO_SIZE_MORPHOLOGY`, `MAR.NUMBERED`, `DMIN.FORMAT`, `ACE.NOT_IN_KARYOTYPE`, `RING.UNIDENTIFIED`, `TRANSLOC.WHOLE_ARM`, `DER.WHOLE_ARM_TWO_CHROMOSOMES`, `ROB.FORMAT`, `ROB.NOT_ACQUIRED`, `ROB.DICENTRIC_USE_DIC`, `KARYO.MULTIPLY_SIGN_AFTER_ABNORMALITY`.
**Acceptance:** Related KB examples pass.
**Notes:** Created 2026-11-19 from T-031 (split agreed with the user); added to the Depends of T-032 and T-035. Reuse the T-031 analyses in `src/CytoLex.Core/Rules/Checks/` (`BandOrder`, `ChromosomeOrder`, `BreakpointGroups`) and `ChromosomeCount` (the `–13,–15,dic`/`–13,–21,rob`/`–1,–3,der(1;3)` examples are count verdicts, like `KARYO.REPLACED_NORMAL_NOT_MISSING`). A semicolon error in a symbol without a group rule of its own goes to `STRUCT.BREAKPOINT_GROUPS` (T-031); if a FORMAT rule here takes one over, add its kind to `BreakpointGroups`. **Outcome (2026-11-19):** 24 `[KbRule]` checks in `src/CytoLex.Core/Rules/Checks/` (thin `StructuralCheck` subclasses) over four new shared analyses: `SymbolForms.cs` (arm in the chromosome list for del/i/fra, short-system arrows for del/dup, `Ph`, `ace`, `der dic`, size terms before `mar`, `t(n;?)` → `add`), `CentromericBreakpoints.cs` (p10/q10 for i/ider/fis, `TRANSLOC.WHOLE_ARM`, `DER.WHOLE_ARM_TWO_CHROMOSOMES`, `ROB.*`), `Unidentified.cs` (signs of mar/r/dmin, `MAR.NUMBERED`, leading counts and excess `×n`) and `Hsr.cs`. `HSR.NO_ABR_SYMBOL` is parser-only. One fault, one finding (decided with the user): symbol rules take over from the general rules: replaced losses via `ReplacedLoss.Rule` (dic/rob/whole-arm der), ring and dmin order faults via new `OrderRule.Ring`/`DoubleMinute` in `ItemOrder`, and excess copies via the new count verdict `ExcessReplacingCopies`. The parser names `iso` (ISO.FORMAT), `abr` (HSR.NO_ABR_SYMBOL) and an unlisted word before `mar` (MAR.NO_SIZE_MORPHOLOGY) instead of SYM.LISTED_ONLY. KB edits: `45,XX,–1,–3,der(1;3)(p10;q10)` dropped from KARYO.REPLACED_NORMAL_NOT_MISSING; ORDER.UNIDENTIFIED_LAST invalid example → `48,XX,+mar,+8`; the inferred `t(1;3)(p11.1;q11.1)` dropped from TRANSLOC.WHOLE_ARM (the book prints `t(X;18)(p11.1;q11.1)` as valid; decided with the user). Tests: new `tests/CytoLex.Core.Tests/Rules/SymbolChecksTests.cs`; `InvalidExampleExpectations` and `NumericalSexOrderChecksTests` updated. `dotnet test`: 2,533 total, 0 failed, 13 skipped (was 51; none of them T-068 rules); coverage 72 rules implemented (was 48). Docs: `docs/decisions.md`, `docs/design/core.md` §7.2, `CLAUDE.md`/`README.md` status. Deviations from the plan: the p11.1 heuristic was dropped; arms in the list are allowed inside derivatives and recombinants (§4.5); homologues per level = k, not 2k; no yield logic was needed for `del/dup(…→…)`. `del` keeps no group rule of its own (`del(5)(q13;q33)` stays STRUCT.BREAKPOINT_GROUPS).
