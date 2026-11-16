# CytoLex — Decisions

Decisions made during planning (2026-11-16) and later `discussion` tasks. Each new decision gets a dated entry under **Decision log**.

## Product & architecture
- **Core library + desktop app.** All ISCN parsing/validation lives in a UI-free core library. A desktop app is the pilot UI. A web service is planned later (T-044) and reuses the same library.
- **Tech stack:** not decided yet (T-020), deliberately postponed until scope and performance needs are clear. Script tooling language: T-001.

## Version-aware validation
- ISCN 2016 in the pilot; 2020/2024 later (T-045/T-046). Editions are not backward compatible.
- The user selects a single version (one result) or all versions (an array with one result per version; unimplemented versions return `not-implemented`).
- Each diagnostic has: severity (error/warning/info), character span, rule ID, citation — format `ISCN 2016 §9.2.17.1, p.75, ¶1` (book page numbers).
- Input type detection (`ish`, `nuc ish`, `arr[...]`, …). Combined strings are split on `.` and each segment is typed.
- The parser must recover from errors and report all problems, not stop at the first.

## Fix suggestions
- **Pilot:** level 1 (deterministic fix, shown as a before/after diff with an "Apply" button) and level 3 (plain-language explanation with citation when no single fix exists).
- **Later:** level 2, "did you mean" (typos, nearest band) — T-041.
- **Highlighting:** wrong range underlined; missing text shown as an insertion marker with grey expected text; extra text struck through.
- Fixes are per version.

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
  - A script generates bands at 300/400/550/700/850 resolution from UCSC cytoBand (GRCh37/38), deriving coarser levels by nesting names.
  - Spot-check against the book's ideograms; differences go in a cited YAML exceptions file.
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
