# CytoLex — Tasks

Statuses: `todo` · `in_progress` · `blocked` · `done` · `discussion` (a decision to make via interview).
Priority: 1 = foundation/core · 2 = pilot · 3 = later.
Run `/next-task` to pick the next startable task (or `/next-task T-xxx` for a specific one).
A task is startable when its status is `todo` or `discussion` and every task in **Depends** is `done`.
Keep the summary table and the task sections in sync (status changes are made in both places).

## Summary

| ID | Status | Pri | Depends | Title |
|---|---|---|---|---|
| T-001 | in_progress | 1 | – | Choose language/tooling for build & extraction scripts |
| T-002 | todo | 1 | T-001 | Source-text extraction script (PDF → section chunks + manifest) |
| T-003 | todo | 1 | T-002 | Review extraction quality; list sections affected by underlining/tables |
| T-004 | todo | 1 | T-001 | KB JSON Schema (rules, quotes, vocabularies, ID & citation conventions) |
| T-005 | todo | 1 | T-004 | KB build script (validate → SQLite FTS5 + JSON bundle; review reports) |
| T-006 | todo | 1 | T-001 | Band data generator from UCSC cytoBand (all resolutions, versioned) |
| T-007 | todo | 1 | T-006, T-003 | Spot-check bands vs. ISCN 2016 ideograms; exceptions YAML |
| T-008 | todo | 1 | T-003, T-005 | KB: ch. 2–3 (band nomenclature, symbols & abbreviated terms) |
| T-009 | todo | 1 | T-008 | KB: ch. 4 (karyotype designation, short/detailed systems, derivatives) |
| T-010 | todo | 1 | T-008 | KB: ch. 5–7 (uncertainty, order of abnormalities, variable features) |
| T-011 | todo | 1 | T-008 | KB: ch. 8 (numerical abnormalities) |
| T-012 | todo | 1 | T-008 | KB: ch. 9.1–9.2.10 (structural, part 1) |
| T-013 | todo | 1 | T-008 | KB: ch. 9.2.11–9.3 (structural, part 2 incl. translocations) |
| T-014 | todo | 2 | T-013 | KB: ch. 11 (neoplasia) |
| T-015 | todo | 2 | T-013 | KB: ch. 13.1–13.2 (metaphase ish) |
| T-016 | todo | 2 | T-015 | KB: ch. 13.3–13.8 (nuc ish, fib/rev ish, cgh, painting) |
| T-017 | todo | 2 | T-013 | KB: ch. 14 (microarray) |
| T-018 | todo | 3 | T-013 | KB: ch. 10 & 12 (breakage, meiotic) |
| T-019 | todo | 3 | T-017 | KB: ch. 15 & 16 (RSA, sequence-based) |
| T-020 | discussion | 1 | T-013 | Tech stack for core library + desktop app |
| T-021 | discussion | 1 | T-013 | Pilot validator scope (notation types implemented in the pilot) |
| T-022 | discussion | 1 | T-013 | Strictness & severity policy |
| T-023 | todo | 1 | T-020, T-021, T-022 | Core design: data model, version profiles, result/diagnostic API, fix model |
| T-024 | todo | 1 | T-023 | Project scaffolding; update CLAUDE.md with commands |
| T-025 | todo | 1 | T-024 | KB-example test harness + rule coverage report |
| T-026 | todo | 1 | T-024 | String type detection & segment splitting |
| T-027 | todo | 1 | T-026 | Karyotype tokenizer/parser with spans and error recovery |
| T-028 | todo | 1 | T-027 | Version-profile rule engine; multi-version results |
| T-029 | todo | 1 | T-028, T-007 | Rules: general + band validation |
| T-030 | todo | 1 | T-029 | Rules: numerical + sex chromosomes + ordering |
| T-031 | todo | 1 | T-029 | Rules: structural rearrangements |
| T-032 | todo | 2 | T-031, T-014 | Rules: neoplastic (clones, idem/sl/sdl, composite) |
| T-033 | todo | 2 | T-031, T-016 | Parser + rules: FISH (ish, nuc ish) |
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

---

## Foundation

### T-001 — Choose language/tooling for build & extraction scripts
**Status:** in_progress
**Description:** Interview to pick the language and libraries for repo tooling: PDF extraction, KB schema validation and build, band-data generation. It doesn't have to match the app stack (T-020). Consider: PDF text libraries (`pdftotext` is available via mingw64), YAML + JSON Schema support, SQLite FTS5, Windows friendliness.
**Acceptance:**
- Decision recorded in `docs/decisions.md` (language, version, key libraries, how scripts are run).
- `CLAUDE.md` updated with how to run scripts.
**Notes:**

### T-002 — Source-text extraction script
**Status:** todo
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
**Notes:**

### T-003 — Review extraction quality
**Status:** todo
**Description:** Spot-check chunks against the PDF. Produce a list of sections where underlining (homologue distinction), tables, figures or margin markers were lost, so those rules get `needs_review`.
**Acceptance:**
- Review report at `docs/extraction-review-2016.md` listing affected sections and pages.
- Extraction fixes applied to the script if needed.
**Notes:**

### T-004 — KB JSON Schema
**Status:** todo
**Description:** Schemas for rule YAML files (`kb/rules/<version>/…`) and quote files (`kb/source-quotes/<version>/…`).
- Vocabularies: `applies_to`, `context`, severity, `fix` hints.
- Conventions: rule ID format (stable concept IDs) and citation format `ISCN 2016 §x.y.z, p.N, ¶M`.
**Acceptance:**
- Schemas committed.
- Sample rule + quote files validate; a deliberately broken sample fails with a clear message.
- Conventions documented in `docs/decisions.md` or `kb/README.md`.
**Notes:**

### T-005 — KB build script
**Status:** todo
**Description:** Validates all YAML against the schemas, checks quote ↔ rule links and unique IDs, and compiles a SQLite DB (with FTS5 over rules and quotes) and a JSON bundle into `build/` (gitignored). Works with or without the quote files present.
**Acceptance:**
- One command builds everything.
- Reports list rules with `paraphrase_checked: false` and `needs_review: true`.
- Fails on schema errors, duplicate IDs or orphan quotes.
**Notes:**

### T-006 — Band data generator
**Status:** todo
**Description:** A script that downloads or reads UCSC cytoBand (GRCh37 + GRCh38) and generates versioned band data (`kb/bands/2016/…`).
- Records band names at 300/400/550/700/850 resolution (coarser levels derived by nesting) plus coordinates.
- Records the data source and version.
**Acceptance:**
- Repeatable output.
- Known bands resolve (e.g. 9q34, 9q34.1, 22q11.2, 1p36.3).
- Invented bands don't (e.g. 9q35).
**Notes:**

### T-007 — Spot-check bands vs. ideograms
**Status:** todo
**Description:** Compare generated band data against ISCN 2016 ideograms (book pp. 14–33) for a sample of chromosomes (e.g. 1, 9, 22, X). Record differences in a cited exceptions YAML.
**Acceptance:**
- Sample checked.
- Exceptions file committed, with citations.
- The generator applies the exceptions.
**Notes:**

## Knowledge base (ISCN 2016)

All KB tasks share these steps and criteria:
- Read the relevant chunks from `kb/source-text/2016/`, never the whole book.
- Write rules (paraphrase + cite + examples + `applies_to`/`context` + optional `fix` hint) and the matching quote files.
- Mark ambiguous items `needs_review`.
- Review paraphrases with the user, side by side with the quotes, and set `paraphrase_checked` for approved ones.
- Acceptance: KB build passes; every book example in the section range is captured as a valid example; invalid examples include `why` and the corrected form where one exists.

### T-008 — KB: ch. 2–3 (band nomenclature, symbols & abbreviated terms)
**Status:** todo
**Description:** Band and region designation rules; full symbol/abbreviation table (ch. 3) as a vocabulary the parser can use.
**Acceptance:** Shared KB criteria; symbol vocabulary file covers every ch. 3 entry.
**Notes:**

### T-009 — KB: ch. 4 (karyotype designation)
**Status:** todo
**Description:** General principles, breakpoint specification, short and detailed systems, derivative and recombinant chromosomes (§4.1–4.5).
**Acceptance:** Shared KB criteria.
**Notes:**

### T-010 — KB: ch. 5–7
**Status:** todo
**Description:** Uncertainty (`?`, `~`, `or`, `inc`), order of abnormalities, normal variable features (heteromorphisms, fragile sites).
**Acceptance:** Shared KB criteria.
**Notes:**

### T-011 — KB: ch. 8 (numerical abnormalities)
**Status:** todo
**Description:** General principles, sex chromosome and autosomal abnormalities, UPD.
**Acceptance:** Shared KB criteria.
**Notes:**

### T-012 — KB: ch. 9.1–9.2.10 (structural, part 1)
**Status:** todo
**Description:** General principles; add, del, der, dic, dup, fission, fra, hsr, ins, inv.
**Acceptance:** Shared KB criteria.
**Notes:**

### T-013 — KB: ch. 9.2.11–9.3 (structural, part 2)
**Status:** todo
**Description:** i, mar, neo, qdp, r, tas, translocations (reciprocal, whole-arm, Robertsonian, jumping), trc, trp, multiple copies of rearranged chromosomes.
**Acceptance:** Shared KB criteria; rules affected by lost underlining are flagged (see T-003).
**Notes:**

### T-014 — KB: ch. 11 (neoplasia)
**Status:** todo
**Description:** Clone definition and size, mainline, stemline/sideline, idem vs. sl/sdl, composite karyotype, unrelated clones, modal number, constitutional karyotype in neoplasia.
**Acceptance:** Shared KB criteria.
**Notes:**

### T-015 — KB: ch. 13.1–13.2 (metaphase ish)
**Status:** todo
**Description:** In situ hybridization introduction, `ish` notation, `dim`/`enh`, subtelomeric ish.
**Acceptance:** Shared KB criteria.
**Notes:**

### T-016 — KB: ch. 13.3–13.8
**Status:** todo
**Description:** `nuc ish` (signal numbers, relative positions, fusion and break-apart probes), `fib ish`, `rev ish`, `cgh`, multi-color and partial painting.
**Acceptance:** Shared KB criteria.
**Notes:**

### T-017 — KB: ch. 14 (microarray)
**Status:** todo
**Description:** `arr` nomenclature, genome build requirement, SNP-array specifics, complex results.
**Acceptance:** Shared KB criteria.
**Notes:**

### T-018 — KB: ch. 10 & 12 (breakage, meiotic)
**Status:** todo
**Description:** Chromatid/chromosome aberrations and scoring; meiotic terminology.
**Acceptance:** Shared KB criteria.
**Notes:**

### T-019 — KB: ch. 15 & 16 (RSA, sequence-based)
**Status:** todo
**Description:** Region-specific assays (`rsa`, MLPA) and sequence-based nomenclature (`seq`).
**Acceptance:** Shared KB criteria.
**Notes:**

## Decisions before code

### T-020 — Tech stack for core library + desktop app
**Status:** discussion
**Description:** Interview to choose the language/framework for the core library and desktop app.
- Inputs: an error-recovering parser with spans; desktop now and web service later; loading the KB bundle at runtime; batch performance; Windows environment.
- Postponed during initial planning until scope was clear.
**Acceptance:** Decision and rationale in `docs/decisions.md`.
**Notes:**

### T-021 — Pilot validator scope
**Status:** discussion
**Description:** Which notation types and contexts the pilot validator implements (e.g. constitutional karyotype only, or also neoplastic, ish, nuc ish, arr). Informed by the KB built so far.
**Acceptance:** Decision in `docs/decisions.md`; T-032..T-034 priorities adjusted.
**Notes:**

### T-022 — Strictness & severity policy
**Status:** discussion
**Description:**
- When input that is non-canonical but unambiguous (stray spaces, letter case) is an error vs. a warning.
- How book-ambiguous rules are reported.
- What "valid" means when there are only warnings.
**Acceptance:** Policy in `docs/decisions.md`.
**Notes:**

## Core library

### T-023 — Core design
**Status:** todo
**Description:** Design doc covering:
- Data model (AST with spans).
- Version profiles (shared parser, per-version rules from the KB).
- Result API (single version vs. array for all versions; `not-implemented`).
- Diagnostics (severity, span, rule ID, citation).
- Fix model (replace/insert/delete/reorder; per version).
- Type detection and segment splitting.
**Acceptance:** `docs/design/core.md` approved by the user.
**Notes:**

### T-024 — Project scaffolding
**Status:** todo
**Description:** Create the solution/packages for the core library, desktop app and tests, using the T-020 stack. Build/test/lint commands work from the command line.
**Acceptance:** Build and an empty test run succeed; `CLAUDE.md` lists the exact commands, including how to run a single test.
**Notes:**

### T-025 — KB-example test harness
**Status:** todo
**Description:** Generate tests from all KB examples (valid → no errors; invalid → expected rule fires; corrected form → fixer output). Produce a coverage report: rules with no implementation or no examples.
**Acceptance:** Harness runs in the normal test command; coverage report generated.
**Notes:**

### T-026 — String type detection & segment splitting
**Status:** todo
**Description:** Split combined strings on `.` (respecting context) and detect each segment's type (`karyotype`, `ish`, `nuc_ish`, `arr`, …).
**Acceptance:** KB examples of every type are detected correctly.
**Notes:**

### T-027 — Karyotype tokenizer/parser
**Status:** todo
**Description:** Parser for karyotype notation producing an AST with character spans. It recovers from errors and reports all problems.
**Acceptance:** Parses all valid karyotype examples from the KB; invalid examples produce errors with correct spans; more than one error is reported per string where present.
**Notes:**

### T-028 — Version-profile rule engine
**Status:** todo
**Description:** Loads the KB bundle and applies the rule set for the chosen version (or all versions, returning an array of results).
**Acceptance:** A single-version call and an all-versions call (2016 + `not-implemented` for 2020/2024) both work; diagnostics carry citations.
**Notes:**

### T-029 — Rules: general + band validation
**Status:** todo
**Description:** Symbols, separators, chromosome identifiers, band existence and resolution (using band data from T-006/T-007).
**Acceptance:** Related KB examples pass.
**Notes:**

### T-030 — Rules: numerical + sex chromosomes + ordering
**Status:** todo
**Description:** Chromosome count consistency, sex chromosome notation, order of abnormalities.
**Acceptance:** Related KB examples pass.
**Notes:**

### T-031 — Rules: structural rearrangements
**Status:** todo
**Description:** All ch. 9 rearrangements, plus the short and detailed systems from ch. 4.
**Acceptance:** Related KB examples pass.
**Notes:**

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
**Notes:**

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
**Notes:**

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
**Notes:**

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
