# CytoLex vs. CytoGPS — comparison

Status as of 2026-11-20. "CytoLex today" means the `feature/task-47` branch (after T-074). "CytoLex planned" lists the open tasks in [`TASKS.md`](../../TASKS.md). CytoGPS facts and sources: [`cytogps.md`](cytogps.md) (T-047).

## Summary

- **Accuracy on the ISCN 2016 book examples.** Both tools ran on the same 951 strings (§3).
  - Valid karyotypes: CytoLex accepts all 549. CytoGPS accepts 417 (76 %).
  - Faulty karyotypes: CytoLex catches 138 of 143 (97 %). CytoGPS catches 58 (41 %).
- **Coverage.**
  - CytoLex also validates microarray results (`arr`) and combined karyotype/array strings. CytoGPS rejects all of them.
  - CytoLex validates the detailed system (`->`, `::`) and many more abnormality symbols.
  - CytoGPS doesn't handle FISH (`ish`); CytoLex checks it since T-033.
- **Feedback quality.** Every CytoLex finding:
  - names a rule,
  - cites the book (`ISCN 2016 §…, p.…, ¶…`),
  - marks the exact characters in the input,
  - and usually offers a one-click fix with a before/after preview.

  CytoGPS gives a short English message and, for bracket/slant typos, a suggested revised string.
- **Speed and privacy.** CytoLex checks 951 strings in **0.4 s**. CytoGPS (local build) takes **66 s**, and the hosted site takes about 1 s per string. CytoLex runs entirely on the user's machine. The hosted CytoGPS has no privacy policy and keeps uploaded batch results on its server.
- **Where CytoGPS is ahead today:**
  - Loss–Gain–Fusion (LGF) band data for research analysis, with batch statistics per band (CytoLex: T-050, T-077; deferred to priority 3, 2026-11-21);
  - an ideogram view (CytoLex: a "Map" view, T-101; LGF colouring, T-078);
  - no installation (web site; CytoLex: web service + browser page, T-044);
  - an R package ecosystem (CytoLex: T-079, an export that RCytoGPS can read);
  - peer-reviewed publications.

  Every item except the publications has a planned CytoLex task.

## 1. Feature comparison

✅ = yes · ◐ = partly · ❌ = no · 🗓 = planned (task)

| Capability | CytoGPS | CytoLex today | CytoLex planned |
|---|---|---|---|
| **Notation types** | | | |
| Conventional karyotype, short system | ✅ | ✅ | |
| Detailed system (`pter->q13::q33->qter`) | ❌ | ✅ | |
| Neoplastic clones, `idem`/`sl`/`sdl`, composite `[cp]` | ✅ | ✅ | |
| Constitutional mosaics/chimeras (`mos`, `chi`) | ◐ (read, but the order of cell lines isn't checked; transplant chimera `//` is rejected) | ✅ | |
| `fra`, `upd`, `rec`, `fis`, `tas`, `neo` | ❌ | ✅ | |
| Microarray (`arr`) | ❌ ("not yet", per its web site) | ✅ | ✅ arr results in the abnormality list (T-072) |
| Combined karyotype + arr strings | ❌ | ✅ | |
| FISH (`ish`, `nuc ish`) | ❌ | ✅ (ish, nuc ish, fib ish, rev ish, cgh; T-033; locus order not checked: probe table planned, T-102/T-103) | ✅ in situ results in the abnormality list (T-092) |
| Breakage, meiotic, RSA, sequence-based (ch. 10, 12, 15, 16) | ❌ | ❌ (breakage and meiotic strings are detected by their leading symbol, rsa and seq by their keyword, and all are reported as not implemented; their KB rules exist, T-018, T-019) | 🗓 T-090, T-091 |
| Printed symbols `–` `×` `→` (as in the book) | ❌ (ASCII only) | ✅ (printed and typed forms) | |
| **ISCN editions** | | | |
| ISCN 2016 | ✅ | ✅ | |
| ISCN 2020 / 2024 | ❌ | ◐ (selectable, reported as not implemented) | 🗓 T-045, T-046 |
| Results per edition, side by side | ❌ | ✅ | |
| **Checks** (§3.2 has the numbers) | | | |
| Band exists, breakpoint order | ✅ | ✅ | |
| Derivative chromosome consistency | ✅ | ✅ | |
| Order of abnormalities and of chromosomes in `t(…)` | ❌ | ✅ | |
| Chromosome count vs. the changes listed | ❌ | ✅ | |
| Sex-chromosome conventions | ❌ | ✅ | |
| Clone order, cell counts, `idem`/`sl` references | ◐ | ✅ | ✅ inherited changes and the written-out full form of each referenced clone in the abnormality list (T-070) |
| Stray spaces, letter case | ❌ (silently ignored) | ✅ | rules stay `error` (T-075 decided); autocorrection with the original kept, "valid after autocorrection": core and batch exports done (T-096; replaces the lenient mode of T-080); desktop single-string check and Batch tab done (T-097) |
| Underlined homologues | ❌ | ✅ (U+0332 on chromosome identifiers; Ctrl+U in the desktop editor) | done (T-042) |
| Rules catalogue | none published | 179 rules for ISCN 2016; 122 checked (103 by a dedicated check, the rest by the parser); the others are mostly informational | 🗓 more rules with each chapter |
| **Feedback** | | | |
| Book citation per finding | ❌ | ✅ | |
| Exact error position highlighted | ◐ (token only) | ✅ (character range in the editor) | |
| Automatic corrections | ◐ (brackets/slants: revised string) | ✅ one-click fixes with preview, "Fix all", explanation when no single fix exists; "did you mean" options for unknown symbols and bands (T-041) | |
| Severity levels (error / warning / info) | ❌ | ✅ | |
| Constitutional vs. acquired context | ❌ | ✅ | |
| Plain-language translation of a string | ❌ | ◐ core library: summary + one sentence per part, with KB links, both readings when the context is unknown, partial translation of strings with errors, optional GRCh37/38 coordinates (T-058; in situ results since T-093; tested against all 521 expected translations; draft wording until T-082); desktop translation view with hover ↔ span, KB links and coordinates (T-059) | 🗓 wording review (T-082) |
| Rule browser (search the rules, examples) | ❌ | ✅ rules and symbols/banding codes (T-039, T-059) | |
| Abnormality list per cell line | ◐ (clone list with cell counts) | ✅ | |
| Derivative written out in the detailed system | ✅ | ✅ computed from the short form for every rearrangement type, shown in the abnormality list (T-066; equal to the book on 87 of its 89 short/detailed pairs, the 2 others `der(?)` with an insertion at an unknown site); a detailed composition is checked against its symbol (`STRUCT.DETAILED_MATCHES_SYMBOL`, T-086) | |
| **Analysis output** | | | |
| Loss–Gain–Fusion per band | ✅ (916 bands, JSON/CSV) | ❌ | 🗓 T-050 (deferred, priority 3) |
| Aggregate statistics over a batch | ✅ (per-band loss/gain/fusion share, aggregate LGF table) | ◐ (summary per edition and rule) | 🗓 per-band statistics and aggregate LGF table (T-077) |
| Ideogram view | ✅ | ✅ | desktop "Map" view (T-101): 850-band ideograms with the loci of the string (breaks, del/dup, ±chromosomes, arr copy numbers as written, in situ regions and paint/D-number probes), linked both ways with the input and the abnormality list; LGF colouring per band and clone still to do (T-078, low priority) |
| R package | ✅ (RCytoGPS) | ❌ | 🗓 export in CytoGPS's JSON format, readable by RCytoGPS (T-079, low priority) |
| Comparison with CytoGPS built in | – | ✅ (adapter: runs the local CytoGPS build on a batch, T-049; comparison engine: 2×2 verdict matrix, not-supported notation kept out, triage labels, KB-example drafts, report in `build/review/comparison/`, T-051; desktop CytoGPS tab in the single-string check with an agreement badge, T-081; desktop Batch tab comparison: matrix, agreement column and filters, side-by-side detail, triage editing saved to the YAML, report export, T-052) | 🗓 Loss–Gain–Fusion differences (T-050) |
| **Batch** | | | |
| Batch input | ✅ `.txt` only | ✅ text, CSV, TSV (with IDs) | |
| Batch export | zip (JSON, CSV, XLS) | CSV summary + full JSON report (both read back) | |
| Speed, 951 strings | 66 s locally; ~1 s per string online | **0.4 s** | |
| **Deployment and terms** | | | |
| Runs offline on the user's PC | ◐ (only after building it from source with a JDK) | ✅ desktop app | |
| Web access, no install | ✅ | ❌ | 🗓 web service + browser page (T-044) |
| Data privacy | ❌ no policy; batch results kept on the server | ✅ nothing leaves the machine | |
| Licence for use | non-commercial only (WU User Agreement); repo has no licence file | own code | 🗓 copyright clean-up before public release (T-043) |
| Maintenance | last code change Aug 2021; the README says "not been fully cleaned", "several bugs … not yet been fixed" | active | |

## 2. Test set

- The test set is every example string in the CytoLex knowledge base for ISCN 2016: 951 strings from the book, each labelled `valid`, `invalid` (a broken form, with the rule it breaks) or `corrected` (the fixed form of an invalid one). Source: `tests/data/batch/2016/all.csv`.
- Breakdown: 822 karyotypes, 112 microarray strings, 17 combined strings (karyotype/arr/FISH).
- CytoGPS was given the strings with the printed symbols changed to ASCII (`–`→`-`, `×`→`x`, `→`→`->`). Without that change it rejects 75 more valid karyotypes, so this setup is in CytoGPS's favour.
- "Accepted by CytoGPS" means the status `Success` or `Fixable grammar error and success`.
- "Caught by CytoLex" means CytoLex reports an error on the string. In 136 of the 138 cases it is exactly the rule the book example is about.

## 3. Results

### 3.1 Overall

| | Strings | CytoLex | CytoGPS |
|---|---|---|---|
| Valid karyotypes accepted | 549 | **549 (100 %)** | 417 (76 %) |
| Corrected karyotypes accepted | 130 | **130 (100 %)** | 119 (92 %) |
| Invalid karyotypes caught | 143 | **138 (97 %)** | 58 (41 %)¹ |
| Valid / corrected arr and combined strings accepted | 103 | **103 (100 %)**² | 0 (not supported) |
| Invalid arr and combined strings caught | 26 | **26 (100 %)** | – (not supported) |

¹ 42 of the 58 are rejected because CytoGPS can't read the notation at all, not because it found the specific fault.

² 7 of the 17 combined strings contain FISH parts (`ish`, `nuc ish`). CytoLex checks their karyotype and arr parts and marks the FISH part as not implemented. It doesn't validate it.

Why CytoGPS rejects 132 valid karyotypes: mostly notation it doesn't support.

- The detailed system: 100.
- `fra` 8, `rec` 7, `upd` 4, `tas` 3, `fis` 2, `neo` 2.
- Per-abnormality cell counts inside `[cp]`: 1.
- The transplant chimera separator `//`: 2.
- Three others: `46,XX,der(5)mat` (CytoGPS requires a rearrangement in `der`), a constitutional derivative list with `c`, and the book fragment `end 46,XX`.

The 5 invalid karyotypes CytoLex doesn't flag are context- or form-dependent rules: `KARYO.DE_NOVO_SPACE`, `UNCERTAIN.OR_ALTERNATIVE`, `UNCERTAIN.INC_AT_END`, and `REC.NOT_ACQUIRED` (it applies only to acquired karyotypes). CytoGPS accepts all 5 too.

### 3.2 Invalid karyotypes caught, by kind of fault

| Kind of fault | Examples | CytoLex | CytoGPS |
|---|---|---|---|
| Order of abnormalities and chromosomes | 14 | **14** | 0 |
| Chromosome count and ploidy | 7 | **7** | 1 |
| Sex chromosomes | 6 | **6** | 0 |
| Clones, cell counts, `idem`/`sl`/`sdl` | 21 | **20** | 4 |
| Spacing, letter case, symbol forms | 15 | **14** | 4 |
| Bands, breakpoints, `del`/`dup`/`inv`/`add` forms | 5 | **5** | 3 |
| Other structural (`der`, ring, `rob`, `dic`, `i`, `hsr`, `mar`, …) | 75 | **72** | 46 |
| **Total** | **143** | **138** | **58** |

### 3.3 The same faults side by side

| Input | CytoGPS | CytoLex |
|---|---|---|
| `47,XY,+21,del(5)(q13q33)` (wrong order) | accepted | error `KARYO.ABNORMALITY_ORDER` + fix, ISCN 2016 citation |
| `48,XX,+21` (count doesn't match) | accepted | error `NUM.COUNT_MATCHES_CHANGES` |
| `46,XY,t(16;12)(p11.1;q13)` (chromosome order) | accepted | error `STRUCT.CHROMOSOME_ORDER` + fix |
| `47,XY, +21` / `46,xy` | accepted | error `SYM.NO_OTHER_SPACE` / `SYM.LETTER_CASE` + fix |
| `46,XY,del(5)(q33q13)` | "the breakpoint more proximal to the centromere should be specified first, maybe you want to try del(5)(q13q33)" | error `STRUCT.PROXIMAL_BREAKPOINT_FIRST` (ISCN 2016 §4.3.1.1, p.41, ¶2) + one-click fix to `del(5)(q13q33)` |
| `46,XX,del(6)(q13q23)×2` (book form) | "Unrecognized token" | valid |
| `arr[GRCh38] 9p24.3(1310386_1709409)×1 mat, …` | "incorrect input for karyotype parsing" | validated (genome build, positions, copy number, order …) |

## 4. Caveats (read before presenting)

- **Home advantage.** The test set is CytoLex's own regression suite. CytoLex is built to pass it, so its 100 % on valid examples is expected and doesn't measure performance on unseen strings. The CytoGPS numbers on the same set are a fair measure of CytoGPS. For a neutral figure, compare both tools on an independent corpus: the planned review-flagged test corpus (T-062) or anonymised real-world strings run through the local CytoGPS only.
- **Reference edition.** Both tools target ISCN 2016. Some CytoGPS acceptances (spaces, case) are deliberate leniency, not bugs. CytoLex keeps them as errors (T-075, decided 2026-11-24) and offers autocorrection, which fixes them and reports the correction (core T-096; desktop T-097).
- **Different goals.** CytoGPS was built to extract LGF data for research, with validation as a filter. CytoLex is built to validate and correct notation against the book. On LGF output CytoGPS is ahead until T-050, and on research tooling until T-077–T-079 (all deferred to priority 3 on 2026-11-21: CytoLex focuses on validation).
- **CytoGPS version.** The local build is the 2021 GitHub snapshot. The hosted site gave identical results on all 28 strings probed and on a 5-line batch ([`cytogps.md`](cytogps.md) §5.1).

## 5. Reproducing

- Per-string results: [`data/cytogps-vs-cytolex-2016.csv`](data/cytogps-vs-cytolex-2016.csv). Columns:
  - `id`, `iscn` (as printed), `type`, `kb_rule`, `kb_kind`;
  - `cytolex_valid`, `cytolex_flags_kb_rule`, `cytolex_diagnostics` (`Severity:RULE@start-end`);
  - `cytogps_status`, `cytogps_valid` (CytoGPS was run on the ASCII form).
- **CytoLex:** run `BatchRunner.Run(Validator.Default, …)` on `tests/data/batch/2016/all.csv` and write it with `BatchExport.WriteCsv` (the same as the desktop "Batch" tab followed by the CSV export). Use the ISCN 2016 rows.
- **CytoGPS:** run the local build ([`cytogps.md`](cytogps.md) §7) on the `iscn` column after the ASCII conversion, then join the results by line.
- The automated version of this comparison is T-051: run `dotnet test --project tests/CytoLex.External.Tests --filter-class "CytoLex.External.Tests.CytoGpsComparisonReportTests"` with CytoGPS set up (README → "CytoGPS (optional)"). It writes `build/review/comparison/cytogps-2016.md` and `.csv` for every distinct string of `all.csv`, keeping notation CytoGPS can't read out of the 2×2 matrix, with the triage labels of `tests/data/comparison/cytogps-2016-triage.yaml`.
