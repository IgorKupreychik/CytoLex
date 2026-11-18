# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project
CytoLex validates ISCN cytogenomic nomenclature strings (karyotype, FISH, microarray, …). Validation is **version-aware**:
- Results are given per ISCN edition (2016 now; 2020/2024 later — editions are not backward compatible).
- Every diagnostic cites the book (`ISCN 2016 §9.2.17.1, p.75, ¶1`).

Planned architecture: a UI-free core library, a desktop app (pilot UI), and later a web service reusing the library. App stack (T-020): **C# / .NET 10 LTS**. The core library uses a hand-written recursive-descent parser; the desktop app uses **Avalonia + AvaloniaEdit** with CommunityToolkit.Mvvm; tests use **xUnit v3**; the web service will use ASP.NET Core. The projects aren't scaffolded yet (T-024). Core design (data model, version profiles, result/diagnostic API, fix model; KB read at runtime from a lean bundle embedded in the core): `docs/design/core.md` — follow it when writing core code. Full decision record: `docs/decisions.md`.

## Status & workflow
- The project is in its planning/foundation phase. Repo tooling exists in `tools/` (`kb extract`, `kb tables`, `kb check`, `kb build`, `kb bands` and `kb ideograms` are implemented); no application code exists yet.
- **`README.md` is the human-facing overview** (purpose, status, layout, setup, commands). Whenever you change `CLAUDE.md`'s Commands, layout or status, or add a new top-level folder, update `README.md` in the same change.
- All work is tracked in `TASKS.md`: a summary table plus a section per task.
  - Statuses: `todo`, `in_progress`, `blocked`, `done`, `discussion`.
- Use `/next-task` (or `/next-task T-xxx`) to work on tasks. It does any pending table sign-off first, then plans in plan mode, marks `done` only after user confirmation, and never commits (`.claude/skills/next-task/SKILL.md`).
- **Every status change goes in both places** in `TASKS.md`: the summary table and the task's section.
- New open questions become `discussion` tasks; decisions are logged in `docs/decisions.md`.

## Commands
Repo tooling is a **self-contained Python 3.14 project in `tools/`**, managed by **uv** (decision T-001). It's kept apart from the app code; app build/test commands come in T-024, so add them here then.
- Layout: `tools/pyproject.toml`, `tools/uv.lock`, sources in `tools/src/cytolex_tools/`, tests in `tools/tests/`.
- Run every command from the repo root with `--directory tools` (or `cd tools` and drop the flag). If `uv` isn't on PATH, use `py -m uv …`.
  - Setup: `uv sync --directory tools`.
  - KB CLI: `uv run --directory tools kb <extract|tables|build|bands|ideograms> [--version 2016]`, `kb check [PATH …]`, and `… kb <subcommand> --help`.
  - `kb check` validates rule/quote/vocab YAML against `kb/schema/` (default: all of `kb/rules/`, `kb/source-quotes/` and `kb/vocab/`); run it while writing KB files.
  - `kb build [--version 2016]` (default: every edition) runs `kb check` plus the cross-file and chunk checks (see `kb/README.md`), then writes `build/kb.sqlite`, `build/kb.json` and `build/review/kb-review.md` (rules still needing review). Any error → exit 1, nothing written.
  - `kb bands [--version 2016] [--cache-only] [--update-pins]` downloads the pinned NCBI GDP ideograms (400/550/850) + UCSC cytoBand into `build/cache/bands/`, cross-checks them, compares them with the book's idiograms (`kb/bands/<version>/ideograms.yaml`), applies the cited `exceptions.yaml` and writes `kb/bands/<version>/bands.yaml` (committed; levels 300/700 come from the book). A sha256 mismatch (upstream change) → exit 1; re-pin deliberately with `--update-pins` and review the band diff. A book-vs-NCBI difference without an exception, or a stale exception → exit 1 and a draft of all differences in `build/review/bands/<version>/exceptions-draft.yaml`. Sources: `tools/src/cytolex_tools/bands/sources/<version>.yaml`.
  - `kb ideograms [--version 2016]` decodes the band labels of the book's idiograms (ISCN 2016 Fig. 5, pp.16–29) from the PDF text layer → `kb/bands/<version>/ideograms.yaml` (committed) + review crops in `build/review/bands/<version>/`. Config (pages, glyph map, spot-check sign-off): the `ideograms` section of the band sources file.
  - `kb tables` renders the configured table regions to PNG in `build/review/tables/<version>/` for the person sign-off of tables.
  - Tests: `uv run --directory tools pytest`; single test: `uv run --directory tools pytest tests/test_cli.py::test_help_lists_subcommands`.
  - Lint/format: `uv run --directory tools ruff check .` and `uv run --directory tools ruff format .`.
- When to run each command: see `README.md` → "When to run what".
- **Remind the user to run commands.** Whenever a change makes a command necessary, tell them explicitly which command to run and why, in the step where it becomes necessary and again in the task's final report. Examples:
  - `uv sync --directory tools` after dependency changes;
  - `kb extract` after a fresh clone, a new edition, or an extraction-script or table-layout (`extract/tables/<version>.yaml`) change;
  - `kb ideograms` then `kb bands` after a decoder or `ideograms` config change; `kb bands` after an `exceptions.yaml` or band source change;
  - `kb build` after any KB edit, or before running the app.
- The tools resolve repo paths (`config/`, `kb/`, `build/`) from the repo root, not the working directory.
- ISCN PDF path: `config/local.yaml` (`iscn_pdf.<version>`), or env var `CYTOLEX_ISCN_PDF_2016` to override.

## Knowledge base (the source of truth for validation rules)
| Path | Content | In git |
|---|---|---|
| `kb/source-text/<version>/` | Per-section text chunks extracted from the ISCN PDF by a deterministic script | **No** (gitignored, can be rebuilt) |
| `kb/schema/` | JSON Schemas (draft 2020-12) for rule and quote files; format, ID and citation conventions in `kb/README.md` | Yes |
| `kb/rules/<version>/` | YAML rules: paraphrased statement, citation, valid/invalid examples, `applies_to`, `context`, `fix` hint, review flags | Yes |
| `kb/source-quotes/<version>/` | Verbatim quotes linked by rule ID (for auditing; removable before public release) | Yes |
| `kb/vocab/<version>/` | Printed lists as parser vocabularies: ch. 3 symbols (`ch03/03-symbols.yaml`), Table 2 banding codes; `kb build` checks they cover every book row | Yes |
| `kb/bands/<version>/` | `bands.yaml`: band names (300/400/550/700/850 + nesting ancestors) with GRCh37/38 coordinates, generated by `kb bands`; `ideograms.yaml`: the book's idiogram labels (`kb ideograms`); `exceptions.yaml`: cited book-vs-NCBI decisions | Yes |
| `build/` | Generated SQLite (FTS5) + JSON bundle; download cache | No |

- **Citation anchors come from the chunks:** `[¶n p.N]` (prose paragraph) and `[En p.N]` (example block), numbered per numbered section; figures/tables carry their own label (`[Fig. 7 p.39]`, `[Table 1 p.12]`, `[Ploidy table p.90]`) and are cited by it. Cite as `ISCN 2016 §9.2.17.1, p.75, ¶1` or `… ¶2, ex. E3`; quote chunk text verbatim. See `manifest.yaml` for glyph mappings and conventions.
- **Before writing rules for a section, check `docs/extraction-review-2016.md`**: it lists what the text layer lost (drawn diagrams, shading, bold/italic) and which sections need `needs_review`. Chunk front matter flags `underlined` labels (U+0332 after an underlined character = homologue distinction) and `changed` labels (margin bar = changed since the previous edition).
- **Before writing KB files, read `kb/README.md`** (file layout, fields, vocabularies) and run `kb check` on them.
- **Rule IDs are stable concept IDs** (e.g. `TRANSLOC.CHROMOSOME_ORDER`) with a block per ISCN version. Don't rename IDs; add version blocks instead.
- **KB examples double as the test suite** for the validator and fixer.
- **Validation logic must trace to KB rules.** Don't encode ISCN rules from memory. If a rule is missing from the KB, add it (with a citation) first.
- **When writing rules, read only the relevant chunks** from `kb/source-text/`, not the whole PDF (token economy).

## ISCN source PDF
- The PDF lives **outside the repo**. Its path comes from `config/local.yaml` (gitignored; template `config/local.example.yaml`). Never commit the PDF or `kb/source-text/`.
- ISCN 2016: **book page = PDF page − 6**. Citations always use book pages.
- Extraction quirks:
  - `→` extracts as `]`.
  - Example strings may contain stray spaces (`t(2; 5)`).
  - Underlining (used to tell homologues apart) is recovered as U+0332 after the underlined character (T-003).
  - Ideograms (book pp. 16–29, Fig. 5) are drawings whose band labels use a figure-only font with private-use glyphs; `kb ideograms` decodes them (T-007). Band data = NCBI GDP + UCSC cytoBand (`kb bands`), checked against the book with a cited exceptions file.
