# CytoLex — Decisions

Decisions made during planning (2026-11-16) and later `discussion` tasks. Each new decision gets a dated entry under **Decision log**.

## Product & architecture
- **Core library + desktop app.** All ISCN parsing/validation lives in a UI-free core library. A desktop app is the pilot UI. A web service is planned later (T-044) and reuses the same library.
- **Tech stack:** not decided yet (T-020), deliberately postponed until scope and performance needs are clear. Script tooling (extraction, KB build, bands): **Python 3.14 + uv** (T-001), independent of the app stack.

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
