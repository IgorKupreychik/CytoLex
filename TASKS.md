# CytoLex — Tasks

Statuses: `todo` · `in_progress` · `blocked` · `done` · `discussion` (a decision to make via interview).
Priority: 1 = foundation/core · 2 = pilot · 3 = later.
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
| T-014 | todo | 2 | T-013 | KB: ch. 11 (neoplasia) |
| T-015 | todo | 3 | T-013 | KB: ch. 13.1–13.2 (metaphase ish) |
| T-016 | todo | 3 | T-015 | KB: ch. 13.3–13.8 (nuc ish, fib/rev ish, cgh, painting) |
| T-017 | todo | 2 | T-013 | KB: ch. 14 (microarray) |
| T-018 | todo | 3 | T-013 | KB: ch. 10 & 12 (breakage, meiotic) |
| T-019 | todo | 3 | T-017 | KB: ch. 15 & 16 (RSA, sequence-based) |
| T-020 | done | 1 | T-013 | Tech stack for core library + desktop app |
| T-021 | done | 1 | T-013 | Pilot validator scope (notation types implemented in the pilot) |
| T-022 | done | 1 | T-013 | Strictness & severity policy |
| T-023 | done | 1 | T-020, T-021, T-022 | Core design: data model, version profiles, result/diagnostic API, fix model |
| T-024 | done | 1 | T-023 | Project scaffolding; update CLAUDE.md with commands |
| T-025 | done | 1 | T-024 | KB-example test harness + rule coverage report |
| T-026 | in_progress | 1 | T-024 | String type detection & segment splitting |
| T-027 | todo | 1 | T-026 | Karyotype tokenizer/parser with spans and error recovery |
| T-028 | todo | 1 | T-027, T-063 | Version-profile rule engine; multi-version results |
| T-029 | todo | 1 | T-028, T-007 | Rules: general + band validation |
| T-030 | todo | 1 | T-029 | Rules: numerical + sex chromosomes + ordering |
| T-031 | todo | 1 | T-029 | Rules: structural rearrangements |
| T-032 | todo | 2 | T-031, T-014 | Rules: neoplastic (clones, idem/sl/sdl, composite) |
| T-033 | todo | 3 | T-031, T-016 | Parser + rules: FISH (ish, nuc ish) |
| T-034 | todo | 2 | T-031, T-017 | Parser + rules: microarray (arr) |
| T-035 | todo | 1 | T-030, T-031 | Level 1 deterministic fixes + level 3 explanations |
| T-036 | todo | 2 | T-028 | Desktop: app shell + single-string check with highlighting |
| T-037 | todo | 2 | T-036 | Desktop: version selector |
| T-038 | todo | 2 | T-036 | Desktop: batch validation + export |
| T-039 | todo | 2 | T-036, T-005 | Desktop: rule browser |
| T-040 | todo | 2 | T-036, T-035 | Desktop: fix UI (diff view, Apply → re-validate) |
| T-041 | todo | 3 | T-035 | Level 2 "did you mean" suggestions |
| T-042 | todo | 3 | T-027 | Handling of underlined homologue notation |
| T-043 | discussion | 3 | – | Copyright cleanup before any public deployment |
| T-044 | todo | 3 | T-028 | Web service wrapping the core library |
| T-045 | todo | 3 | T-013 | ISCN 2020 KB + version profile |
| T-046 | todo | 3 | T-045 | ISCN 2024 KB + version profile |
| T-047 | todo | 2 | – | Research CytoGPS (API, LGF output, ISCN version, source/licence, local options, terms) |
| T-048 | discussion | 2 | T-047 | Decide CytoGPS integration mode and data policy |
| T-049 | todo | 2 | T-028, T-048 | `ExternalChecker` interface + CytoGPS adapter |
| T-050 | todo | 2 | T-031 | LGF computation in the core library |
| T-051 | todo | 2 | T-049, T-050 | Comparison engine + CLI report |
| T-052 | todo | 3 | T-051, T-038 | Desktop: comparison view |
| T-053 | done | 1 | T-003 | Accept ASCII `-`/`x`/`->` for printed `–`/`×`/`→`? |
| T-054 | done | 2 | T-021 | Literal translation of ISCN strings into plain language: scope, style, architecture |
| T-055 | todo | 2 | T-007 | Tooling: LF line endings for committed generated files (CRLF checkout breaks the ideograms test) |
| T-056 | todo | 1 | T-022 | KB: apply the strictness & severity policy to the 2016 rules |
| T-057 | todo | 2 | T-023 | Translation templates + schema; reviewed golden file (ISCN 2016) |
| T-058 | todo | 2 | T-030, T-031, T-057 | Literal translation renderer in the core library |
| T-059 | todo | 2 | T-058, T-036, T-039 | Desktop: translation view |
| T-060 | done | 1 | T-023 | `kb build`: lean runtime bundle (rules metadata, vocab, bands) for the core |
| T-061 | discussion | 3 | – | Presentation & documentation material for the pilot: define what to prepare |
| T-062 | todo | 2 | T-026 | Test corpus of ISCN strings per notation type (review-flagged) + mutation generator |
| T-063 | done | 1 | T-053 | KB: typed symbol forms (`-`/`x`/`->`) in the ch. 3 vocab + rule `SYM.TYPED_SYMBOL_FORMS` |
| T-064 | todo | 3 | – | Line endings: generated committed files vs `core.autocrlf` (ideograms test fails on Windows) |

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
**Status:** todo
**Description:** Clone definition and size, mainline, stemline/sideline, idem vs. sl/sdl, composite karyotype, unrelated clones, modal number, constitutional karyotype in neoplasia.
**Acceptance:** Shared KB criteria.
**Notes:** Before starting, person sign-off of the **Ploidy table** (p.90; its ranges become numeric limits — check every number): run `kb tables`, compare the images with the chunks, set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml`, then `kb extract`. Indentation/bold of the printed table are lost (`docs/extraction-review-2016.md`). From T-013: `TRANSLOC.JUMPING` (§9.2.17.4) points to §11.1.6 for the order of unrelated clones (decreasing frequency); link it to the clone-order rule written here.

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
**Status:** todo
**Description:** `arr` nomenclature, genome build requirement, SNP-array specifics, complex results.
**Acceptance:** Shared KB criteria.
**Notes:**

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
**Status:** todo
**Description:** Apply the T-022 policy (`docs/decisions.md` → "Strictness & severity") to the ISCN 2016 rules:
- Re-check every rule's `severity` against the wording map (mandatory → error, preference → warning, explanation → info).
- Apply the dispositions listed in the T-022 decision log entry (info when triggered, conditional, warning, capped while `needs_review`, `NUM.COUNT_MATCHES_CHANGES` error-when-certain).
- Document in `kb/README.md`: the `needs_review` cap at warning, the "info only when the notation is present" convention for rules a string can't verify, and how a rule states "error when certain, else warning" (add a schema field only if needed).
- Run `kb build`.
**Acceptance:** Every severity matches the wording map or has a recorded reason; `kb build` passes; the changed severities are listed in the task's Notes.
**Notes:** Should be done before T-029 (rule implementation); proposed to add it to T-029's Depends.

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

### T-057 — Translation templates + schema; reviewed golden file
**Status:** todo
**Description:** Data side of the literal translation (T-054, `docs/decisions.md` → "Literal translation"):
- Phrase templates per ISCN edition, keyed by KB entry (rule ID or vocab item), English for the pilot; proposed location `kb/translation/<version>/`.
- A JSON Schema for them in `kb/schema/`; `kb check` validates them, `kb build` checks that every key is an existing KB entry and puts them into the bundle.
- A golden file for ISCN 2016: string → expected summary, items (with spans) and KB IDs. Seeded from book examples by comparing with the printed explanation (cited, not quoted) plus non-book strings; each entry carries a person-review flag and counts only once reviewed.
**Acceptance:** Templates cover every karyotype ch. 2–9 construct of the T-023 model; `kb build` passes with them; golden file has reviewed entries for the book examples of ch. 4, 8 and 9 plus at least 20 non-book strings; `CLAUDE.md`/`README.md` updated for the new folder.
**Notes:** Created 2026-11-18 from T-054.

### T-058 — Literal translation renderer
**Status:** todo
**Description:** Renderer in the core library working on the T-023 model: summary line + one sentence per item, each with its span and KB entry links (title shown, ID on hover; no book citation); partial translation of strings with errors (bad parts marked "not translated, see diagnostic"); neutral wording when the context is unknown, both readings where context changes the meaning, a single reading with a hint; optional approximate GRCh37/38 coordinates and sizes from `kb/bands` ("no coordinates" for book-only bands). Per edition.
**Acceptance:** All reviewed golden-file entries (T-057) pass in the normal test command; strings with errors give a partial translation.
**Notes:** Created 2026-11-18 from T-054. LGF link (T-050) is a later addition.

### T-059 — Desktop: translation view
**Status:** todo
**Description:** Show the translation next to the diagnostics: hovering an item highlights its span (and vice versa), KB links open the version-specific entry in the rule browser, a coordinates toggle (off by default) with a GRCh37/38 choice, and the partial-translation banner.
**Acceptance:** Book examples show their translation; hover ↔ span highlighting and KB links work.
**Notes:** Created 2026-11-18 from T-054.

## Core library

### T-055 — Tooling: LF line endings for committed generated files
**Status:** todo
**Description:** With `core.autocrlf=true` (Windows), git checks out `kb/bands/2016/ideograms.yaml` with CRLF, while `kb ideograms` writes LF, so `tests/test_ideograms.py::test_pdf_decodes_every_chromosome_and_matches_committed` (byte comparison) fails. The same can happen to `bands.yaml` and any other generated, committed file.
**Acceptance:** A `.gitattributes` that keeps `kb/**/*.yaml` (and other generated files) LF on checkout, or tests that compare text with normalised line endings (decide which); the full test suite passes on a Windows checkout with `core.autocrlf=true`.
**Notes:** Found during T-008 (2026-11-17); unrelated to T-008's changes. After adding `.gitattributes`, re-normalise the working tree (`git add --renormalize .`).

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
**Status:** in_progress
**Description:** Split combined strings on `.` (respecting context) and detect each segment's type (`karyotype`, `ish`, `nuc_ish`, `arr`, …).
**Acceptance:** KB examples of every type are detected correctly.
**Notes:** From T-021: also infer the context from clear markers only (clones/`idem`/`sl`/`sdl`, `c`, ploidy `<Nn>` → acquired; `rec` → constitutional); otherwise unknown. An explicit context hint overrides it. From T-023: design in `docs/design/core.md` §3 (top-level `.` split, keyword type detection, `NotImplemented` segments, contradicting markers → unknown + info).

### T-027 — Karyotype tokenizer/parser
**Status:** todo
**Description:** Parser for karyotype notation producing an AST with character spans. It recovers from errors and reports all problems.
**Acceptance:** Parses all valid karyotype examples from the KB; invalid examples produce errors with correct spans; more than one error is reported per string where present.
**Notes:** From T-023: lossless syntax tree + binder to the semantic model (`docs/design/core.md` §4–§5); every recovery point names the KB rule of the expected form (§8.4). From T-026: the parser works per karyotype segment from `Segmenter.Split` (spans are offsets in the whole input); it also detects the pilot's `NotImplemented` constructs inside a karyotype segment (`idem`/`sl`/`sdl`/`cp`/`//`, ch. 10/12 markers, U+0332; core.md §3.2) with their spans; it may replace the lexical scan in `ContextInference` with tokens behind the same API.

### T-028 — Version-profile rule engine
**Status:** todo
**Description:** Loads the KB bundle and applies the rule set for the chosen version (or all versions, returning an array of results).
**Acceptance:** A single-version call and an all-versions call (2016 + `not-implemented` for 2020/2024) both work; diagnostics carry citations.
**Notes:** From T-023: edition profiles, `[KbRule]`-bound checks, severity resolution and the result API in `docs/design/core.md` §7–§9. Proposed: depend on T-060 (runtime bundle). From T-025: implement `ValidatorAdapter.Current` in `tests/CytoLex.Core.Tests/KbExamples/ValidatorAdapter.cs` (map one edition's `ValidationReport` onto `ExampleRun`; the KB-example cases then run instead of skipping); rule checks use `CytoLex.Core.Rules.KbRuleAttribute` (already in Core); optionally replace `RuleImplementationScan` with the engine's coverage list. The adapter gets an `isFragment` flag for book fragments (`inv(2)`, `…` strings). From T-026: segments come from `Segmenter.Split` (`Segment.Type`); the profile's implemented types decide `NotImplemented` per segment. `ContextInference.Infer` gives `ContextInfo` (hint/inferred/effective/markers); when `IsContradictory`, emit the info diagnostic naming both markers — it needs a KB rule ID (or a core code like `CORE.UNPARSED`), decide which. Map the harness's own `SampleContext` enum onto `CytoLex.Core.Context.SampleContext`.

### T-029 — Rules: general + band validation
**Status:** todo
**Description:** Symbols, separators, chromosome identifiers, band existence and resolution (using band data from T-006/T-007).
**Acceptance:** Related KB examples pass.
**Notes:** From T-007: `kb/bands/2016/bands.yaml` entries carry `levels` (300–850, book-corrected) and `book`. `book: false` marks 6 NCBI-only names (6p24.1–3, 9q34.11–13) that are valid for genome-band contexts (arr/seq, e.g. §15.3 `9q34.11q34.3`) but absent from the book's idiograms. Decide with T-022 whether karyotype/ISH strings using them get a warning. 15 book-only 850 sub-bands (e.g. 1q32.11) have `grch37/grch38: null`. From T-022: `book: false` bands give an `info` note (source named) in karyotype/ish strings and nothing in arr/seq; unknown bands stay errors. Rule severities come from the KB after T-056 (do T-056 first).

### T-030 — Rules: numerical + sex chromosomes + ordering
**Status:** todo
**Description:** Chromosome count consistency, sex chromosome notation, order of abnormalities.
**Acceptance:** Related KB examples pass.
**Notes:** From T-011: the count check is `NUM.COUNT_MATCHES_CHANGES` (count = reference ploidy level from the count range or `<Nn>` + gains − losses; an inference, `needs_review`). It must handle structural changes that alter the count without a sign (rob, dic, fis, …), count ranges (`~`) and `inc`, and the sex-field convention in `SEX.ACQUIRED_GAIN_LOSS` (after a loss the field lists the remaining sex chromosomes, `45,X,–X`; with a gain the constitutional ones, `47,XX,+X`). Ploidy ranges: `NUM.PLOIDY_REFERENCE` (§8.1 ¶2) and the §11.2 Ploidy table.

### T-031 — Rules: structural rearrangements
**Status:** todo
**Description:** All ch. 9 rearrangements, plus the short and detailed systems from ch. 4.
**Acceptance:** Related KB examples pass.
**Notes:** From T-013: `STRUCT.PROXIMAL_BREAKPOINT_FIRST` and `STRUCT.SHORT_ARM_BREAKPOINT_FIRST` do not apply where the band order shows orientation (dup, trp, inserted segments, segments of complex t and rings, band groups in multicentric rings, tas, trc); the order rules for tas/trc (end chromosome first, then the chain) are `TAS.FORMAT`/`TRC.FORMAT`.

### T-032 — Rules: neoplastic
**Status:** todo
**Description:** Clones, cell counts `[n]`, idem/sl/sdl, composite karyotypes, `/` and `//`.
**Acceptance:** Related KB examples pass.
**Notes:**

### T-033 — Parser + rules: FISH
**Status:** todo
**Description:** `ish` and `nuc ish` grammar and rules.
**Acceptance:** Related KB examples pass.
**Notes:**

### T-034 — Parser + rules: microarray
**Status:** todo
**Description:** `arr` grammar and rules, including the genome build and coordinate vs. band consistency.
**Acceptance:** Related KB examples pass.
**Notes:**

### T-035 — Level 1 fixes + level 3 explanations
**Status:** todo
**Description:**
- Level 1: deterministic fixes (spacing, letter case, chromosome/breakpoint order, redundant sex chromosome as in `46,XX,t(X;13)` → `46,X,t(X;13)`).
- Level 3: plain-language explanations when no single fix exists.
- Fixes are per version.
**Acceptance:** Corrected forms from the KB are reproduced exactly; explanations include citations.
**Notes:** From T-021: where the correct form depends on context (e.g. `47,XXX` vs `47,XX,+X`) and the context is unknown, offer one fix per context instead of a single Apply. From T-023: fix model (`TextEdit`s on the original input, apply = edit + re-validate, fix all, `ForContext`) in `docs/design/core.md` §10. From T-025: set `SupportsFixes` in the harness adapter so the `corrected` cases also check `Fix.Preview`.

### T-062 — Test corpus of ISCN strings per notation type
**Status:** todo
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
- Follow-up: T-064 (an ideograms test fails on CRLF checkouts, unrelated).

### T-064 — Line endings of generated committed files
**Status:** todo
**Description:** With `core.autocrlf=true` (Windows), git checks out `kb/bands/2016/ideograms.yaml` with CRLF, but `kb ideograms` writes LF bytes. So `tools/tests/test_ideograms.py::test_pdf_decodes_every_chromosome_and_matches_committed` fails on a byte compare, even on a clean tree. The same risk applies to `bands.yaml` and any other generated file that is committed. Fix with a `.gitattributes` (`eol=lf` for `kb/**` generated YAML, or `* text=auto eol=lf`) plus a renormalise, or compare normalised text in the test.
**Acceptance:** `uv run --directory tools pytest` passes on Windows with `core.autocrlf=true`; the generators and the checkout agree on line endings.
**Notes:** Found during T-063 (2026-11-18); the failure is unrelated to T-063.

## Desktop app (pilot)

### T-036 — App shell + single-string check
**Status:** todo
**Description:** Input box. Shows detected types, highlighting (underline for wrong ranges, insertion marker with grey expected text for missing parts, strikethrough for extra parts) and a diagnostics list with citations.
**Acceptance:** Book examples show correctly; highlighting matches diagnostic ranges.
**Notes:**

### T-037 — Version selector
**Status:** todo
**Description:** Switch between a single version and all versions; all-versions shows one panel per version.
**Acceptance:** Both modes work; unimplemented versions show `not-implemented`.
**Notes:**

### T-038 — Batch validation
**Status:** todo
**Description:** Load a text/CSV file, validate all strings, show a summary table, export to CSV/JSON.
**Acceptance:** A batch of 1,000+ strings completes; exports round-trip.
**Notes:** From T-054: exports may optionally include the literal translation with its KB entry IDs (once T-058 exists).

### T-039 — Rule browser
**Status:** todo
**Description:** Browse and search rules, paraphrases, quotes and examples (FTS). Clicking a diagnostic opens its rule.
**Acceptance:** Search works; diagnostic → rule navigation works; works without quote files.
**Notes:**

### T-040 — Fix UI
**Status:** todo
**Description:** Before/after diff of a fix (additions green, removals red) with an "Apply" button that re-validates.
**Acceptance:** Applying the fixes for the KB's invalid examples yields valid strings.
**Notes:**

## External comparison (CytoGPS)

### T-047 — Research CytoGPS
**Status:** todo
**Description:** Find out (web access failed during planning):
- Available interfaces: web form and API (endpoint, fields).
- Input/output format (validity, error messages, LGF vectors, JSON).
- Supported ISCN version and notation coverage.
- Batch limits.
- Source code availability and licence; a public container image.
- Terms of use and privacy.
- Contact the authors if needed. Sites: `https://cytogps-app.mangograss-a1cf8a6c.westus2.azurecontainerapps.io/karyotype_parser`, cytogps.org, the RCytoGPS package.
**Acceptance:** Findings with source URLs in `docs/research/cytogps.md`.
**Notes:**

### T-048 — Decide CytoGPS integration mode and data policy
**Status:** discussion
**Description:**
- Integration: local build, container, or online with opt-in.
- What data may be sent online (an organisational policy check is needed if real patient karyotypes may be involved).
**Acceptance:** Decision in `docs/decisions.md`.
**Notes:**

### T-049 — `ExternalChecker` interface + CytoGPS adapter
**Status:** todo
**Description:** A pluggable interface for external checkers, plus a CytoGPS adapter with a response cache, throttling, and an opt-in guard for online use (off by default, with a third-party warning).
**Acceptance:** The adapter returns normalized results for a sample batch; online use can't happen without the opt-in.
**Notes:**

### T-050 — LGF computation in the core library
**Status:** todo
**Description:** Compute Loss–Gain–Fusion per band from a parsed karyotype, in a form comparable with CytoGPS output.
**Acceptance:** Matches hand-checked LGF for a sample set of karyotypes.
**Notes:**

### T-051 — Comparison engine + CLI report
**Status:** todo
**Description:** Run a batch through our validator and CytoGPS. Report a 2×2 agreement matrix, disagreements, and LGF differences where both accept. Triage labels (our bug / CytoGPS limitation / version difference); confirmed cases can be exported as KB examples. Compare only against CytoGPS's ISCN version.
**Acceptance:** The CLI produces the report for a sample batch; triage export works.
**Notes:**

### T-052 — Desktop: comparison view
**Status:** todo
**Description:** Batch table with per-row agreement status, filters (e.g. disagreements only), side-by-side detail, LGF difference view and triage.
**Acceptance:** Works on a sample batch, using the comparison engine.
**Notes:**

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
**Description:** A web service exposing single/batch validation from the core library.
**Acceptance:** Same results as the desktop app for the KB example suite.
**Notes:**

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
