# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project
CytoLex validates ISCN cytogenomic nomenclature strings (karyotype, FISH, microarray, …). Validation is **version-aware**:
- Results are given per ISCN edition (2016 now; 2020/2024 later — editions are not backward compatible).
- Every diagnostic cites the book (`ISCN 2016 §9.2.17.1, p.75, ¶1`).

Planned architecture: a UI-free core library, a desktop app (pilot UI), and later a web service reusing the library. The tech stack is **not chosen yet** (task T-020). Full decision record: `docs/decisions.md`.

## Status & workflow
- The project is in its planning/foundation phase; no application code exists yet.
- All work is tracked in `TASKS.md`: a summary table plus a section per task.
  - Statuses: `todo`, `in_progress`, `blocked`, `done`, `discussion`.
- Use `/next-task` (or `/next-task T-xxx`) to work on tasks. It plans in plan mode, marks `done` only after user confirmation, and never commits (`.claude/skills/next-task/SKILL.md`).
- **Every status change goes in both places** in `TASKS.md`: the summary table and the task's section.
- New open questions become `discussion` tasks; decisions are logged in `docs/decisions.md`.

## Commands
Not defined yet. Script tooling is decided in T-001; app build/test commands come in T-024. Update this section when they exist, including how to run a single test.

## Knowledge base (the source of truth for validation rules)
| Path | Content | In git |
|---|---|---|
| `kb/source-text/<version>/` | Per-section text chunks extracted from the ISCN PDF by a deterministic script | **No** (gitignored, can be rebuilt) |
| `kb/rules/<version>/` | YAML rules: paraphrased statement, citation, valid/invalid examples, `applies_to`, `context`, `fix` hint, review flags | Yes |
| `kb/source-quotes/<version>/` | Verbatim quotes linked by rule ID (for auditing; removable before public release) | Yes |
| `build/` | Generated SQLite (FTS5) + JSON bundle | No |

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
  - Underlining (used to tell homologues apart) is lost.
  - Ideograms (book pp. 14–33) are images; band data comes from UCSC cytoBand plus a cited exceptions file.
