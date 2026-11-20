# CytoLex

CytoLex validates **ISCN cytogenomic nomenclature** strings: karyotypes, FISH (`ish`, `nuc ish`), microarray (`arr`) and more.

- **Version-aware.** Results are given per ISCN edition. ISCN 2016 comes first; 2020 and 2024 follow later, because editions are not backward compatible.
- **Cited.** Every diagnostic points to the book, e.g. `ISCN 2016 §9.2.17.1, p.75, ¶1`.
- **Helpful.** It suggests deterministic fixes where one exists, or explains the problem otherwise.

> **Status:** planning/foundation phase. The repo tooling (`tools/`) is set up, `kb extract` works and its output has been reviewed against the book ([`docs/extraction-review-2016.md`](docs/extraction-review-2016.md)), the KB file format is fixed by JSON Schemas ([`kb/README.md`](kb/README.md)), `kb build` checks the KB and compiles it to SQLite + JSON, and `kb bands` generates the band data, checked against the book's idiograms (`kb ideograms`). The .NET solution (core library, desktop app, tests) is scaffolded and builds; the core splits strings into technique segments, detects their type and infers the sample context (T-026), parses karyotype segments into a lossless syntax tree with error recovery (T-027), and validates per ISCN edition through the rule engine (`Validator`, T-028: KB-cited diagnostics, all-editions results with 2020/2024 not implemented) and binds each tree to a semantic model of meaning nodes with KB refs (T-065); rule checks exist for the ch. 2–3 general and band rules (chromosome identifiers, band format and existence, symbol spacing; T-029) and for chromosome counts, ploidy, the sex chromosome field and the order of abnormalities (T-030), and for structural rearrangements: chromosome and breakpoint order, breakpoint groups, the detailed system and derivatives (T-031), and the other ch. 9 symbols: del/dup/i/fis/fra forms, p10/q10 and whole-arm rearrangements, rob/dic, hsr, markers, unidentified rings, double minutes and copy numbers (T-068), and neoplastic clones: idem/sl/sdl references (resolved to the clone they refer to and checked against its count), composite karyotypes (`[cpN]`, per-abnormality cell counts), clone size, related and unrelated clone order, normal and constitutional-only lines last, constitutional clone order and jumping translocations (T-032). Microarray results (`arr`, ch. 14) are parsed and checked too (T-034): genome build, nucleotide format, normal results, order of aberrations, regions and pter→qter, copy numbers, proportions, inheritance and zygosity, `cx`/`cth`, and the order of combined karyotype/FISH/array results. Diagnostics carry fixes (T-035): deterministic level-1 edits with a before/after preview (every KB `corrected` form is reproduced exactly), or a level-3 explanation with book citations when no single fix exists; `Fixer` applies one fix or all non-overlapping ones. The desktop app does the single-string check (T-036): it shows the detected segment types, the context and the verdict, highlights the input (wrong ranges underlined, missing text as an insertion marker with grey expected text, extra text struck through) and lists the diagnostics with their citations and fixes. An edition selector (T-037) validates in one ISCN edition or in all of them, one tab per edition (2020/2024 show "not implemented"); the editor highlights the selected tab's edition. Results also list each karyotype's abnormalities by cell line (T-069: kind, chromosomes, breakpoints, short form such as `t(9;22)`, derivative parts, flags, diagnostics), which the desktop app shows in an "Abnormalities" panel linked to the input. Batch validation (T-038): the desktop "Batch" tab loads a text, CSV or TSV file, validates every string in parallel (1,000+ strings in about a second), shows a summary per edition and a filterable table, opens a row in the single-string check, and exports a CSV summary (one row per string and edition) or the full reports as JSON. Both exports read back. Reusable sample files generated from the KB examples are in `tests/data/batch/` (`kb samples`). A rule browser (T-039): the desktop "Rules" tab searches the KB rules of an edition (full-text over IDs, titles, statements, example strings such as `t(9;22)` and source quotes, read from `build/kb.sqlite`), filters by book section and shows a rule's statement, citations, scope, fix hint, review state, related rules, valid/invalid examples (each can be opened in the single-string check) and quotes; quotes are optional. Clicking a diagnostic's rule ID opens its rule. Fix UI (T-040): every level-1 fix shows a before/after diff (removed text red and struck through, added text green) with an **Apply** button that changes the input and re-validates (Ctrl+Z undoes it); "Fix all (N)" previews one round of every non-overlapping fix and comes back while fixable findings remain. Fixes are computed from the string, so they work on any string, not only book examples; a test checks that every whole-string invalid KB example whose findings have certain fixes ends valid (`build/review/fix-coverage.md`). Stray spaces (`47,XY, +21`, `46,X Y`) and wrong letter case (`46,xy`, `Del(5)`, `T(9;22)`) are reported with a one-click fix (T-074); a wrong-case symbol is still read as the listed one, so it gets no follow-on errors. Progress is tracked in [`TASKS.md`](TASKS.md).

## Architecture (planned)
- **Core library:** UI-free ISCN parsing and validation, driven by a knowledge base (KB) of cited rules.
- **Desktop app:** the pilot UI. Single-string check, batch validation, rule browser, fix view.
- **Web service:** later, reusing the core library.

The app stack is C# on .NET 10 LTS: a core library with a hand-written parser, an Avalonia + AvaloniaEdit desktop app, xUnit v3 tests, and ASP.NET Core for the web service later (decision T-020). The projects are scaffolded (T-024); see [Getting started (app)](#getting-started-app). The pilot validator covers karyotypes from ISCN ch. 2–9 (constitutional and acquired, numerical and structural, mos/chi), using only the string as input. Neoplastic clones, microarray and FISH come later, in that order (decision T-021). A string is valid when it has no errors; warnings and info notes are listed but don't affect validity (decision T-022). The core library's design (data model, version profiles, result/diagnostic API, fix model) is in [`docs/design/core.md`](docs/design/core.md) (T-023). All decisions and their rationale are in [`docs/decisions.md`](docs/decisions.md).

## Repository layout
| Path | Content |
|---|---|
| `CytoLex.slnx` | .NET solution: the projects below |
| `src/CytoLex.Core/` | UI-free core library (parser, validator, fixes, batch reading/running/export in `Batch/`); embeds the KB bundle from `build/` |
| `src/CytoLex.Desktop/` | Desktop app (Avalonia + AvaloniaEdit, CommunityToolkit.Mvvm): single-string check, Batch tab and Rules tab (rule browser over `build/kb.sqlite`, copied next to the app as `kb/kb.sqlite`) |
| `tests/CytoLex.Core.Tests/` | xUnit v3 tests for the core library, incl. the KB-example harness (`KbExamples/`, T-025) |
| `tests/CytoLex.Desktop.Tests/` | xUnit v3 tests for the desktop app's UI-free parts (highlight mapping, view models incl. the abnormalities panel, the edition selector, the Batch tab, the rule browser and the fix UI; T-036, T-069, T-037, T-038, T-039, T-040) |
| `tests/data/batch/<version>/` | Batch sample files generated from the KB examples by `kb samples` (text per notation type, `all.csv`/`.tsv`, 1,200-row `large.csv`); see [`tests/data/batch/README.md`](tests/data/batch/README.md) |
| `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` | .NET SDK pin (+ test runner), shared build settings, central NuGet versions, code style |
| `tools/` | Self-contained Python project for repo tooling: PDF extraction, KB build, band data |
| `kb/schema/` | JSON Schemas for rule and quote files; format and conventions in [`kb/README.md`](kb/README.md) |
| `kb/rules/<version>/` | KB rules (YAML): paraphrase, citation, valid/invalid examples |
| `kb/source-quotes/<version>/` | Verbatim quotes linked to rules (for auditing; private) |
| `kb/vocab/<version>/` | Printed lists as parser vocabularies: ch. 3 symbols and abbreviations, Table 2 banding codes |
| `kb/bands/<version>/` | Band names (300/400/550/700/850 + ancestors) with GRCh37/38 coordinates (`bands.yaml`, generated by `kb bands`), the book's idiogram labels (`ideograms.yaml`, `kb ideograms`) and the cited book-vs-NCBI decisions (`exceptions.yaml`) |
| `kb/source-text/<version>/` | Text extracted from the ISCN PDF (**gitignored**, rebuildable) |
| `build/` | Generated SQLite (FTS5), full JSON KB (`kb.json`), lean runtime bundle for the core (`kb-runtime.json`), review images, download cache (**gitignored**) |
| `config/` | `local.example.yaml` template; your `local.yaml` is gitignored |
| `docs/` | Decision record, design docs (core library: [`docs/design/core.md`](docs/design/core.md)), extraction review |
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
3. Run the KB CLI from the repo root. All subcommands work.
   ```sh
   uv run --directory tools kb --help
   uv run --directory tools kb extract --version 2016   # PDF → kb/source-text/2016/ (--pdf, --out to override)
   uv run --directory tools kb tables --version 2016    # table images for review → build/review/tables/2016/
   uv run --directory tools kb check                    # rule/quote/vocab YAML vs kb/schema/ (or: kb check <files/dirs>)
   uv run --directory tools kb build                    # check KB → build/kb.sqlite, kb.json, kb-runtime.json, review/kb-review.md
   uv run --directory tools kb ideograms --version 2016 # book idiogram labels (PDF) → kb/bands/2016/ideograms.yaml
   uv run --directory tools kb bands                    # NCBI GDP + UCSC cytoBand + book → kb/bands/2016/bands.yaml
   uv run --directory tools kb samples                  # KB examples (build/kb.json) → tests/data/batch/2016/ sample files
   ```

## Getting started (app)
Requirements: the **.NET 10 SDK** (any 10.0.x; `global.json` pins the major version and opts `dotnet test` into Microsoft.Testing.Platform). Run everything from the repo root.

1. Build the KB bundle first. `CytoLex.Core` embeds the lean runtime bundle `build/kb-runtime.json` (the test project also embeds the full `build/kb.json`), and the desktop app copies `build/kb.sqlite` for its rule browser; `dotnet build` stops with "KB bundle not found …" or "KB database not found … Run 'uv run --directory tools kb build'" if one is missing:
   ```sh
   uv run --directory tools kb build
   ```
2. Build, test and run:
   ```sh
   dotnet build                                   # whole solution (CytoLex.slnx); warnings are errors
   dotnet test                                    # all tests, incl. one case per KB example (skipped until the validator exists);
                                                  # also writes build/review/rule-coverage.md and build/review/fix-coverage.md
   dotnet test --filter-method "CytoLex.Core.Tests.EmbeddedBundleTests.EmbeddedBundle_IsPresentAndParses"   # single test
   dotnet test --project tests/CytoLex.Desktop.Tests   # one test project (desktop highlight mapping + view models)
   dotnet run --project src/CytoLex.Desktop       # desktop app
   dotnet format --verify-no-changes              # lint (code style); `dotnet format` fixes
   ```
   `--filter-method` accepts `*` at either end (`"*IsPresentAndParses"`); `--filter-class` selects a whole test class.
3. NuGet versions live in `Directory.Packages.props` (Central Package Management); projects reference packages without a version.

## When to run what
| Command | Built in | What it does | When to run it |
|---|---|---|---|
| `kb extract --version 2016` | T-002 | ISCN PDF → per-section text + manifest in `kb/source-text/2016/` (gitignored) | **Rarely:** once per machine/edition or after a fresh clone, and again whenever the extraction script changes. Re-running is always safe; the output is identical. |
| `kb tables --version 2016` | T-003 | Renders every configured table region of the PDF to PNG, with an index naming the chunk that holds its extracted Markdown, in `build/review/tables/2016/` | **Rarely:** when signing off tables (`review.human` in `tools/src/cytolex_tools/extract/tables/<version>.yaml`) or after changing a table layout. |
| `kb check [PATH …]` | T-004 | Checks rule, quote and vocab YAML (default: all of `kb/rules/`, `kb/source-quotes/` and `kb/vocab/`; also accepts `kb/bands/<version>/exceptions.yaml`) against `kb/schema/`; prints each problem as `file:rules[0].cite[0].para: …`; exit 1 on any problem. Schema only: no cross-file checks | **Often:** while writing KB files, before `kb build`. |
| `kb ideograms [--version 2016]` | T-007 | Decodes the band labels of the book's idiograms (Fig. 5, pp.16–29; 24 chromosomes × 300/400/550/700/850) from the PDF text layer into `kb/bands/2016/ideograms.yaml` (committed; byte-identical re-runs) and writes a crop + decoded-label list per chromosome to `build/review/bands/2016/` for visual checks. Glyph map, pages and spot-check sign-off: `tools/src/cytolex_tools/bands/sources/<version>.yaml` → `ideograms` | **Rarely:** only after a decoder or `ideograms` config change. Needs the PDF; not needed after a clone. |
| `kb bands [--version 2016]` | T-006 / T-007 | Downloads the sha256-pinned NCBI GDP ideograms (400/550/850, GRCh37/38) and UCSC cytoBand into `build/cache/bands/`, cross-checks names and coordinates, compares the result with the book's idiograms and applies `kb/bands/2016/exceptions.yaml`, then writes `kb/bands/2016/bands.yaml` (committed; byte-identical re-runs). Any book-vs-NCBI difference without an exception (or a stale exception) → exit 1, plus a draft of all differences in `build/review/bands/2016/exceptions-draft.yaml`. `--cache-only` = offline; `--update-pins` accepts changed upstream files | **Rarely:** after a change to the sources, the generator, `ideograms.yaml` or `exceptions.yaml`. The committed file is already current after a clone. Run `kb build` afterwards: the bands go into the runtime bundle. |
| `kb build [--version 2016]` | T-005 | Checks `kb/` (schemas, `version`/section vs path, unique IDs, `related` IDs, rule ↔ quote links, vocab tokens; with chunks present also cite labels/pages, verbatim quote text and vocab coverage of the printed lists) → `build/kb.sqlite` (FTS5) + `build/kb.json` (full KB) + `build/kb-runtime.json` (lean runtime bundle for the core: rule metadata, vocab, bands; T-060); lists rules with `needs_review: true` / `paraphrase_checked: false` (also in `build/review/kb-review.md`). Default: every edition; exit 1 and nothing written on any error | **Often:** after every KB edit, and before running or testing the app (it loads the bundle). |
| `kb samples [--version 2016]` | T-038 | Writes batch sample files from the KB examples in `build/kb.json` to `tests/data/batch/2016/` (committed; byte-identical re-runs): one unique string per line per notation type, `all.csv`/`all.tsv` (each example and `corrected` form with its rule, kind and citation) and `large.csv` (1,200 rows). Book fragments are left out | **Sometimes:** after `kb build`, when KB examples were added or changed. The test projects copy these files; a C# test checks their `type` column against the segmenter. |
| `dotnet build`, `dotnet test`, `dotnet format` | T-024 / T-025 | Build the solution (embedding `build/kb-runtime.json` into `CytoLex.Core` and `build/kb.json` into the test projects, copying `build/kb.sqlite` next to the desktop app for the rule browser), run the xUnit tests, including the KB-example harness: one case per valid/invalid/corrected KB example, invalid cases skipped with a reason while their rule is not implemented or the string has no segment the pilot implements (e.g. ish-only strings; T-017); corrected forms must be reproduced by a fix preview (T-035); examples the core can't handle yet skip with the task named in `KbExamples/PendingExamples.cs` until they pass (T-014). `dotnet test` also writes `build/review/rule-coverage.md` (rules without implementation or examples) and `build/review/fix-coverage.md` (invalid KB examples fixed to valid by repeated "fix all", or stopped at an explanation; T-040). Check/fix code style | After any change in `src/` or `tests/`, and after every `kb build` (rebuild so the app and tests embed the new KB). |
| `pytest`, `ruff check`, `ruff format` | — | Test, lint and format the tooling code in `tools/` (not the validator) | After any change in `tools/`. |

Typical flow:
1. **One-time setup:** `uv sync --directory tools`, fill in `config/local.yaml`, run `kb extract`. (`kb/bands/` is committed, so `kb bands` isn't needed after a clone.)
2. **KB work (T-008 onwards):** if the task's tables still need a person sign-off, do that first and run `kb extract`; then write or edit rules and vocab files (format: [`kb/README.md`](kb/README.md)), run `kb check` while editing, then `kb build` until it passes, then `kb samples` if examples changed.
3. **App work (T-024 onwards):** run `kb build` first, then `dotnet build`/`dotnet test`, so the app and its tests use the current KB.

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
App (.NET), from the repo root: `dotnet build`, `dotnet test` (single test: `dotnet test --filter-method "<Namespace.Class.Method>"`), `dotnet format --verify-no-changes`; see [Getting started (app)](#getting-started-app).

Tooling (Python):
```sh
uv run --directory tools pytest                                                   # all tests
uv run --directory tools pytest tests/test_cli.py::test_help_lists_subcommands   # single test
uv run --directory tools ruff check .                                             # lint
uv run --directory tools ruff format .                                            # format
```

You can also `cd tools` and drop `--directory tools`.

**Line endings:** every file is LF on every OS (`.gitattributes`: `* text=auto eol=lf`, T-055), so generated committed files (`kb/bands/…`) match their generators byte for byte. If a working copy from before T-055 still has CRLF files (`git ls-files --eol | grep w/crlf`), convert it once on a clean tree: `git rm -r --cached -q . && git reset --hard`.

## Copyright
CytoLex is a private project for now. The ISCN book and its extracted text never enter the repo. Verbatim quotes are kept in separate, removable files, and will be removed before any public release (T-043).
