# Curated files

Everything in this folder is **written, filled in, reviewed or signed off by a person**. If a file is here, a person owns it: tools read these files but never rewrite them on their own. The one exception is `kb probes`, which adds and refreshes only its own `(auto)` rows of the probe table, plus the sha256 pins when you ask for `--update-pins`.

Generated files stay outside this folder: band data (`kb/bands/<version>/bands.yaml`, `ideograms.yaml`), the text extracted from the book (`kb/source-text/`, not in git), the batch sample files (`tests/data/batch/`) and everything in `build/`. The JSON Schemas that the files here are checked against are in `kb/schema/`, and the file formats are described in [`kb/README.md`](../kb/README.md).

**Data policy (T-048):** only ISCN-book or synthetic strings, never real patient karyotypes.

## What is where

| Folder / file | What it is | Who edits it | Check with |
|---|---|---|---|
| `rules/<version>/chNN/*.yaml` | Validation rules per ISCN section: paraphrased statement, citation, valid/invalid examples, fix hint, review flags. The KB examples double as the validator's test suite | KB authors; cytogeneticists review (`review.paraphrase_checked`, `needs_review`) | `kb check`, then `kb build` |
| `source-quotes/<version>/chNN/*.yaml` | Verbatim book quotes, one per rule, for auditing (removable before a public release, T-043) | KB authors | `kb build` (quotes must match the book text) |
| `vocab/<version>/chNN/*.yaml` | Printed lists used as parser vocabularies: ch. 3 symbols and abbreviations, Table 2 banding codes | KB authors; review flags in the file | `kb check`, `kb build` (covers every printed row) |
| `translation/<version>/` | Plain-language translation: phrase templates (`templates.en.yaml`) and expected translations (`golden.yaml`); all drafts until reviewed (T-082) | KB authors; medical reviewers (`review.checked`) | `kb check`, `kb build` |
| `bands/<version>/exceptions.yaml` | Cited decisions where the book's band names differ from NCBI's | KB authors, from the book | `kb check`, `kb bands` |
| `bands/<version>/sources.yaml` | Band download URLs and their sha256 pins, the idiogram glyph map and the spot-check sign-off | Maintainers (re-pin only on purpose, after reviewing the band diff) | `kb bands`, `kb ideograms` |
| `extract/tables/<version>.yaml` | Where each printed table sits in the PDF (page area, columns) and the person sign-off per table (`review.human`) | Maintainers; sign-off with a person (`/next-task` does it with you) | `kb extract`, `kb tables` |
| `extract/glyphs.yaml` | Repair map for symbols the PDF text layer gets wrong (`→` extracted as `]`), each checked against the page image | Maintainers | `kb extract` |
| `probes/probes.csv` | **Probe table** (T-102): probe names with chromosome, band, GRCh37/GRCh38 positions, source and review columns | Medical reviewers and KB authors: see [`probes/README.md`](probes/README.md) | `kb build` (review report `build/review/probes-review.md`) |
| `probes/sources.yaml` | HGNC + NCBI download pins for `kb probes` | Maintainers | `kb probes` |
| `corpus/*.yaml` | Test corpus (T-062): realistic synthetic strings with the expected result per edition; labels are reviewed. See [`corpus/README.md`](corpus/README.md) | Testers, KB authors | `kb check`, `dotnet test` |
| `comparison/cytogps-2016-triage.yaml` | Triage labels for the CytoLex vs. CytoGPS comparison (T-051); also edited in the desktop app's Batch tab. See [`comparison/README.md`](comparison/README.md) | Testers, KB authors | `dotnet test` |

## After you change something

| You changed | Run | Why |
|---|---|---|
| rules, quotes, vocab, translation, the probe table | `uv run --directory tools kb build`, then `dotnet build` | checks the files and rebuilds the bundle that the app embeds |
| KB examples | the above, then `kb samples` | regenerates the batch sample files |
| `bands/<version>/exceptions.yaml` or `sources.yaml` | `kb bands`, then `kb build` | regenerates `kb/bands/<version>/bands.yaml` |
| `extract/tables/<version>.yaml`, `extract/glyphs.yaml` | `kb extract`, then `kb build` | re-extracts the book text (needs the PDF) |
| `probes/sources.yaml`, or to add genes | `kb probes …`, then `kb build` | refreshes the gene rows |

Every command runs from the repo root; see the main [README](../README.md#when-to-run-what) for details.
