# CytoLex

CytoLex validates **ISCN cytogenomic nomenclature** strings: karyotypes, FISH (`ish`, `nuc ish`), microarray (`arr`) and more.

- **Version-aware.** Results are given per ISCN edition. ISCN 2016 comes first; 2020 and 2024 follow later, because editions are not backward compatible.
- **Cited.** Every diagnostic points to the book, e.g. `ISCN 2016 §9.2.17.1, p.75, ¶1`.
- **Helpful.** It suggests deterministic fixes where one exists, or explains the problem otherwise.

> **Status:** planning/foundation phase. The repo tooling (`tools/`) is set up and `kb extract` works; the validator itself does not exist yet. Progress is tracked in [`TASKS.md`](TASKS.md).

## Architecture (planned)
- **Core library:** UI-free ISCN parsing and validation, driven by a knowledge base (KB) of cited rules.
- **Desktop app:** the pilot UI. Single-string check, batch validation, rule browser, fix view.
- **Web service:** later, reusing the core library.

The app tech stack is not chosen yet (T-020). All decisions and their rationale are in [`docs/decisions.md`](docs/decisions.md).

## Repository layout
| Path | Content |
|---|---|
| `tools/` | Self-contained Python project for repo tooling: PDF extraction, KB build, band data |
| `kb/rules/<version>/` | KB rules (YAML): paraphrase, citation, valid/invalid examples |
| `kb/source-quotes/<version>/` | Verbatim quotes linked to rules (for auditing; private) |
| `kb/source-text/<version>/` | Text extracted from the ISCN PDF (**gitignored**, rebuildable) |
| `build/` | Generated SQLite (FTS5) + JSON bundle (**gitignored**) |
| `config/` | `local.example.yaml` template; your `local.yaml` is gitignored |
| `docs/` | Decision record and design docs |
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
3. Run the KB CLI from the repo root. `extract` works; `build` and `bands` come in T-005 and T-006.
   ```sh
   uv run --directory tools kb --help
   uv run --directory tools kb extract --version 2016   # PDF → kb/source-text/2016/ (--pdf, --out to override)
   uv run --directory tools kb build                    # KB → build/ (SQLite + JSON)
   uv run --directory tools kb bands                    # UCSC cytoBand → band data
   ```

## When to run what
> `kb build` and `kb bands` are **placeholders** until their tasks are done: they print "not implemented yet" and exit with code 2. `kb extract` is implemented.

| Command | Built in | What it does | When to run it |
|---|---|---|---|
| `kb extract --version 2016` | T-002 | ISCN PDF → per-section text + manifest in `kb/source-text/2016/` (gitignored) | **Rarely:** once per machine/edition or after a fresh clone, and again whenever the extraction script changes. Re-running is always safe; the output is identical. |
| `kb bands` | T-006 / T-007 | UCSC cytoBand → versioned band data, with cited exceptions applied | **Rarely:** once at the start, and again when the exceptions file or source data changes. |
| `kb build` | T-005 | Checks `kb/` (schemas, unique IDs, rule ↔ quote links) → SQLite (FTS5) + JSON bundle in `build/`; lists rules still needing review | **Often:** after every KB edit, and before running or testing the app (it loads the bundle). |
| `pytest`, `ruff check`, `ruff format` | — | Test, lint and format the tooling code in `tools/` (not the validator) | After any change in `tools/`. |

Typical flow:
1. **One-time setup:** `uv sync --directory tools`, fill in `config/local.yaml`, run `kb extract` and `kb bands`.
2. **KB work (T-008 onwards):** write or edit rules, then run `kb build` until it passes.
3. **App work (T-024 onwards):** run `kb build` first, so the app and its tests use the current KB.

## Source-text chunks (`kb extract`)
`kb extract` turns the ISCN PDF into one Markdown file per numbered section, plus `manifest.yaml`. It is deterministic and uses no AI: re-running gives byte-identical output.
- **Files:** `kb/source-text/2016/ch09/09.02.17.01-reciprocal-translocations.md`. Numbers are zero-padded so files sort in book order. A section over the 4,000-token cap is split into `-p1`, `-p2`…
- **Front matter:** section, title, book and PDF page ranges, and the citation labels in the file, with their pages.
- **Citation labels:** `[¶n p.75]` marks a prose paragraph and `[En p.75]` an example block (with its explanation quoted below it). Both restart at every numbered heading, so a citation reads `ISCN 2016 §9.2.17.1, p.75, ¶1` or `… ¶2, ex. E3`.
- **Faithful text:**
  - Symbols that the PDF stores under other characters are restored by font (`]` → `→`, `~` → `×`, `!4n1` → `<4n>`, …).
  - Invisible zero-width spaces are dropped.
  - Example strings are otherwise verbatim. The manifest lists every glyph mapping and anything it couldn't map.
- **Exit code 1** if a TOC section isn't found on its TOC page.

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
