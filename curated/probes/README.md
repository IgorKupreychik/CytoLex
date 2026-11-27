# Probe table: how to read and edit `probes.csv`

`probes.csv` tells CytoLex where a FISH probe sits on the genome: its chromosome, band and positions. ISCN strings name probes (`nuc ish(ABL1,BCR)×2`) but don't say where they are, and the book doesn't either, so this table supplies it. CytoLex uses it to place probes on the Map and to check that loci are written in the right order (T-103).

**Every row says where its data comes from (`source`) and whether a person has checked it (`reviewed`).** Rows that nobody has checked are still used, but the app marks them "unreviewed", and an order finding that depends on one is only a warning.

## The columns

| Column | What to write | Example |
|---|---|---|
| `probe` | The name exactly as it is written in ISCN strings. No spaces. | `ABL1`, `RP11-265P11`, `D21S65` |
| `aliases` | Other names for the same probe, separated by `;` (no spaces). Older gene symbols go here. | `MLL` (for `KMT2A`) |
| `kind` | One of: `gene`, `centromere`, `clone`, `paint`, `commercial`, `other` | `gene` |
| `chromosome` | 1–22, X or Y | `9` |
| `band` | The band, or a band range written like `22q11.21-q11.22`. It must be a band CytoLex knows (any level: 9q34, 9q34.1, 9q34.12). | `9q34.12` |
| `grch37`, `grch38` | Position in each genome build: `start-end` in base pairs, no commas or spaces. Leave empty if unknown. | `130713043-130887675` |
| `source` | Where you took the data from: a datasheet (vendor, product, version or date), a database entry, a paper. | `<vendor> <product> datasheet, rev. 2024-05` |
| `reviewed` | `yes` once a person has checked the row against its source; otherwise `no`. | `yes` |
| `reviewer` | Who checked it (needed when `reviewed` is `yes`). | `A. Person` |
| `date` | When it was checked, as `YYYY-MM-DD` (needed when `reviewed` is `yes`). | `2026-11-27` |
| `note` | Anything worth knowing, e.g. why a band differs from the source. | |

Rows whose `source` ends with `(auto)` were filled in by `kb probes` from HGNC (gene names) and NCBI (positions). They are a starting point and are not checked. To review one, compare it with the source, fix it if needed, then set `reviewed` to `yes` and fill in `reviewer` and `date`. From then on no tool will change the row.

## Opening the file without breaking it

Spreadsheets "helpfully" turn some gene names into dates (`SEPT9` → `9-Sep`, `MARCH1` → `1-Mar`) and strip leading zeros. CytoLex rejects a file with date-like names, so open it this way:

- **Excel:** don't double-click the file. Use *Data → From Text/CSV*, choose `probes.csv`, *File origin: 65001 Unicode (UTF-8)*, *Delimiter: Comma*, then *Transform Data* (or *Edit*), select all columns, set *Data Type → Text*, and *Close & Load*. To save, use *File → Save As → CSV UTF-8 (Comma delimited)*.
- **LibreOffice Calc:** *File → Open*, choose `probes.csv`. In the import dialog set *Character set: Unicode (UTF-8)*, *Separated by: Comma*, click the top-left corner of the column preview to select every column, and set *Column type: Text*. Save with *Keep Current Format* (CSV).
- **A text editor** (VS Code, Notepad++) is always safe: one row per line, cells separated by commas. A cell that contains a comma must be put in double quotes.

Keep the header row exactly as it is and keep the columns in this order.

## Adding a probe

- **A gene:** a maintainer can run `uv run --directory tools kb probes --add SYMBOL` to fill in the row from HGNC and NCBI. You then review it as described above.
- **A BAC clone, a vendor probe or a region:** type the row in yourself from its source, and name that source in `source`. Copy only the facts you need (name, location); never paste a vendor's catalogue.

## Checking your change

A maintainer (or you, if you have the tools set up) runs `uv run --directory tools kb build` from the repo root. It stops with a message naming the row and column if something is wrong: an unknown band, a name used twice, a date-like name, a missing reviewer. It also writes `build/review/probes-review.md`, which lists rows whose position doesn't match their band, rows not yet reviewed, and probe names used in the book's examples that have no row yet.

## Sources and licences

- HGNC gene symbols, previous symbols and locations: HUGO Gene Nomenclature Committee, licence CC0 (free to reuse).
- Gene positions: NCBI RefSeq annotation releases; NCBI data are public domain.
- Vendor datasheets: cite them per row; the names and locations are facts typed in by a person.

The exact releases are pinned in `sources.yaml` next to this file.
