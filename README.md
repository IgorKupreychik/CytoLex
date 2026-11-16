# CytoLex

CytoLex validates **ISCN cytogenomic nomenclature** strings: karyotypes, FISH (`ish`, `nuc ish`), microarray (`arr`) and more.

- **Version-aware.** Results are given per ISCN edition. ISCN 2016 comes first; 2020 and 2024 follow later, because editions are not backward compatible.
- **Cited.** Every diagnostic points to the book, e.g. `ISCN 2016 §9.2.17.1, p.75, ¶1`.
- **Helpful.** It suggests deterministic fixes where one exists, or explains the problem otherwise.

> **Status:** planning/foundation phase. The repo tooling (`tools/`) is set up, `kb extract` works and its output has been reviewed against the book ([`docs/extraction-review-2016.md`](docs/extraction-review-2016.md)), the KB file format is fixed by JSON Schemas ([`kb/README.md`](kb/README.md)), and `kb build` checks the KB and compiles it to SQLite + JSON; the validator itself does not exist yet. Progress is tracked in [`TASKS.md`](TASKS.md).

## Architecture (planned)
- **Core library:** UI-free ISCN parsing and validation, driven by a knowledge base (KB) of cited rules.
- **Desktop app:** the pilot UI. Single-string check, batch validation, rule browser, fix view.
- **Web service:** later, reusing the core library.

The app tech stack is not chosen yet (T-020). All decisions and their rationale are in [`docs/decisions.md`](docs/decisions.md).

## Repository layout
| Path | Content |
|---|---|
| `tools/` | Self-contained Python project for repo tooling: PDF extraction, KB build, band data |
| `kb/schema/` | JSON Schemas for rule and quote files; format and conventions in [`kb/README.md`](kb/README.md) |
| `kb/rules/<version>/` | KB rules (YAML): paraphrase, citation, valid/invalid examples |
| `kb/source-quotes/<version>/` | Verbatim quotes linked to rules (for auditing; private) |
| `kb/source-text/<version>/` | Text extracted from the ISCN PDF (**gitignored**, rebuildable) |
| `build/` | Generated SQLite (FTS5) + JSON bundle, review images (**gitignored**) |
| `config/` | `local.example.yaml` template; your `local.yaml` is gitignored |
| `docs/` | Decision record, design docs, extraction review |
| `TASKS.md` | Task list and status |

## Getting started (tooling)
Requirements: Windows, macOS or Linux with **Python 3.14** and **[uv](https://docs.astral.sh/uv/)**. Install uv with `py -m pip install --user uv` or `winget install astral-sh.uv`. If `uv` isn't on PATH, use `py -m uv …` instead.

1. Point the tools at your copy of the ISCN book. The PDF is copyrighted and is never committed.
   ```sh
   cp config/local.example.yaml config/local.yaml   # then edit iscn_pdf paths
   ```
   Or set an environment variable instead, e.g. `CYTOLEX_ISCN_PDF_2016=C:/books/ISCN_2016.pdf`.
2. Install the environment:
   ```sh
   uv sync --directory tools
   ```
3. Run the KB CLI from the repo root. `extract`, `tables`, `check` and `build` work; `bands` comes in T-006.
   ```sh
   uv run --directory tools kb --help
   uv run --directory tools kb extract --version 2016   # PDF → kb/source-text/2016/ (--pdf, --out to override)
   uv run --directory tools kb tables --version 2016    # table images for review → build/review/tables/2016/
   uv run --directory tools kb check                    # rule/quote YAML vs kb/schema/ (or: kb check <files/dirs>)
   uv run --directory tools kb build                    # check KB → build/kb.sqlite, kb.json, review/kb-review.md
   uv run --directory tools kb bands                    # UCSC cytoBand → band data
   ```

## When to run what
> `kb bands` is a **placeholder** until T-006 is done: it prints "not implemented yet" and exits with code 2. `kb extract`, `kb tables`, `kb check` and `kb build` are implemented.

| Command | Built in | What it does | When to run it |
|---|---|---|---|
| `kb extract --version 2016` | T-002 | ISCN PDF → per-section text + manifest in `kb/source-text/2016/` (gitignored) | **Rarely:** once per machine/edition or after a fresh clone, and again whenever the extraction script changes. Re-running is always safe; the output is identical. |
| `kb tables --version 2016` | T-003 | Renders every configured table region of the PDF to PNG, with an index naming the chunk that holds its extracted Markdown, in `build/review/tables/2016/` | **Rarely:** when signing off tables (`review.human` in `tools/src/cytolex_tools/extract/tables/<version>.yaml`) or after changing a table layout. |
| `kb check [PATH …]` | T-004 | Checks rule and quote YAML (default: all of `kb/rules/` and `kb/source-quotes/`) against `kb/schema/`; prints each problem as `file:rules[0].cite[0].para: …`; exit 1 on any problem. Schema only: no cross-file checks | **Often:** while writing KB files, before `kb build`. |
| `kb bands` | T-006 / T-007 | UCSC cytoBand → versioned band data, with cited exceptions applied | **Rarely:** once at the start, and again when the exceptions file or source data changes. |
| `kb build [--version 2016]` | T-005 | Checks `kb/` (schemas, `version`/section vs path, unique IDs, `related` IDs, rule ↔ quote links; with chunks present also cite labels/pages and verbatim quote text) → `build/kb.sqlite` (FTS5) + `build/kb.json`; lists rules with `needs_review: true` / `paraphrase_checked: false` (also in `build/review/kb-review.md`). Default: every edition; exit 1 and nothing written on any error | **Often:** after every KB edit, and before running or testing the app (it loads the bundle). |
| `pytest`, `ruff check`, `ruff format` | — | Test, lint and format the tooling code in `tools/` (not the validator) | After any change in `tools/`. |

Typical flow:
1. **One-time setup:** `uv sync --directory tools`, fill in `config/local.yaml`, run `kb extract` and `kb bands`.
2. **KB work (T-008 onwards):** write or edit rules (format: [`kb/README.md`](kb/README.md)), run `kb check` while editing, then `kb build` until it passes.
3. **App work (T-024 onwards):** run `kb build` first, so the app and its tests use the current KB.

## Source-text chunks (`kb extract`)
`kb extract` turns the ISCN PDF into one Markdown file per numbered section, plus `manifest.yaml`. It is deterministic and uses no AI: re-running gives byte-identical output.
- **Files:** `kb/source-text/2016/ch09/09.02.17.01-reciprocal-translocations.md`. Numbers are zero-padded so files sort in book order. A section over the 4,000-token cap is split into `-p1`, `-p2`…
- **Front matter:** section, title, book and PDF page ranges, and the citation labels in the file, with their pages.
- **Citation labels:** `[¶n p.75]` marks a prose paragraph and `[En p.75]` an example block (with its explanation quoted below it). Both restart at every numbered heading, so a citation reads `ISCN 2016 §9.2.17.1, p.75, ¶1` or `… ¶2, ex. E3`. Figures and tables are labelled by their own number (`[Fig. 7 p.39]`, `[Table 1 p.12]`) and don't count as paragraphs.
- **Tables:** tables listed in `tools/src/cytolex_tools/extract/tables/<version>.yaml` (page area, column positions, review status) become Markdown tables.
- **Layout marks:** an underlined character (used to tell homologues apart) is followed by U+0332, e.g. `der(1̲)`; front matter `changed` lists the labels beside a margin change bar (changes since the previous edition) and `underlined` the labels with underlining.
- **Faithful text:**
  - Symbols that the PDF stores under other characters are restored by font (`]` → `→`, `~` → `×`, `!4n1` → `<4n>`, …).
  - Invisible zero-width spaces are dropped.
  - Example strings are otherwise verbatim. The manifest lists every glyph mapping and anything it couldn't map.
- **Exit code 1** if a TOC section isn't found on its TOC page, or a configured table region is empty.
- **Known limits:** what the text layer can't carry (drawn diagrams, shading, bold/italic, the ploidy table's indentation) is listed per section in [`docs/extraction-review-2016.md`](docs/extraction-review-2016.md); rules citing those sections get `needs_review`.

## Development
```sh
uv run --directory tools pytest                                                   # all tests
uv run --directory tools pytest tests/test_cli.py::test_help_lists_subcommands   # single test
uv run --directory tools ruff check .                                             # lint
uv run --directory tools ruff format .                                            # format
```

You can also `cd tools` and drop `--directory tools`.

## Copyright
CytoLex is a private project for now. The ISCN book and its extracted text never enter the repo. Verbatim quotes are kept in separate, removable files, and will be removed before any public release (T-043).
