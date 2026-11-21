# CytoGPS — research findings (T-047)

Researched 2026-11-20. For a feature and accuracy comparison with CytoLex (for presentations), see [`cytogps-comparison.md`](cytogps-comparison.md). Inputs to T-048 (integration mode, data policy), T-049 (adapter), T-050/T-051 (LGF, comparison) and T-075 (severity of stray spaces and letter case).

Every fact below has a source: a URL (accessed 2026-11-20) or an observation in this session, marked **[obs]**. Observations came from two places:
- **local** — the CytoGPS source at `C:\_\NMDP\ISCN\CytoGPS` (an unpacked copy of the GitHub repo, no `.git`), compiled and run with a portable JDK;
- **hosted** — the public web app, probed with **synthetic strings only**: ISCN book examples and made-up variants, never patient data (approved by the user).

## 1. What CytoGPS is

- "CytoGenetic Pattern Sleuth": a rules-based ISCN karyotype parser by Washington University in St. Louis (Institute for Informatics) and Ohio State University (Biomedical Informatics, Pathology). It parses karyotypes and turns them into a binary **Loss–Gain–Fusion (LGF)** model (loss/gain/fusion per cytoband).
  - Abrams ZB, Zhang L, Abruzzo LV, Heerema NA, … Coombes KR, Payne PRO. *CytoGPS: a web-enabled karyotype analysis tool for cytogenetics.* Bioinformatics 2019;35(24):5365–5366. doi:10.1093/bioinformatics/btz520 — https://pmc.ncbi.nlm.nih.gov/articles/PMC6954647
  - Abrams ZB et al. *CytoGPS: A large-scale karyotype analysis of CML data.* Cancer Genet 2020;248–249:34–38. doi:10.1016/j.cancergen.2020.09.005 — https://www.pubmed.gov/33059160/
  - Coombes KR et al. *RCytoGPS: an R package for reading and visualizing cytogenetics data.* Bioinformatics 2021;37(23):4589–4590. doi:10.1093/bioinformatics/btab683 (PMC10262354) — https://academic.oup.com/bioinformatics/article/37/23/4589/6380545
- Implementation: Java with an ANTLR 4.7 grammar (`Karyotype.g4`, 1181 lines) and hand-written validation classes (~22,000 lines of Java). Source: https://www.github.com/i2-wustl/CytoGPS (last push 2021-08-31, per the GitHub API `https://api.github.com/repos/i2-wustl/CytoGPS`).
- The README says that "the current code has not been fully cleaned", that "a section of code has not been completed" and that "several bugs were found but have not yet been fixed" (repo `README.txt`).

## 2. Interfaces

### Hosted web app
- URL: https://cytogps-app.mangograss-a1cf8a6c.westus2.azurecontainerapps.io/ (the parser is at `/karyotype_parser`). The address in the 2019 paper, `http://cytogps.org`, **no longer resolves** (DNS failure, 2026-11-20). The RCytoGPS vignette now links to the Azure address: https://cran.r-project.org/web/packages/RCytoGPS/vignettes/IntroRCytoGPS.html
- There is no documented API. The page's own jQuery calls two endpoints **[obs, page source of `/karyotype_parser`]**:
  - **Single string:** `POST /singleKaryotypeAnalysis`, form-encoded field `karyotype`. It returns `application/json` `{"results": "<HTML fragment>"}` with the clone selector, errors (`Parsing Errors:` with tooltips, `Validation Error: …`, `Suggested Karyotype: …`) and per-chromosome band tables (loss/gain/fusion on G-850 bands). It is meant for the browser, so a client has to scrape the HTML. ~0.5–1.5 s per request **[obs]**.
  - **Batch:** `POST /batchKaryotypeAnalysis`, multipart field `karyotypeFile` (only `.txt`, one karyotype per line). It returns `{"success": true, "fileName": "<upload name>_CytoGPS_analysis_<date>.zip"}`, and the zip is fetched from `GET /download/results/<fileName>`. The zip holds `CytoGPS_Result.json` (the same schema as the local build, §3), `Aggregate_Loss_Gain_Fusion.csv`, `Summary_Statistics.xls`, `README.txt` and `Line_<n>/{KaryotypeNotes.txt, Clone_<k>.xls}` **[obs, 5-line synthetic file]**.
- Sample batch file: `GET /download/sampleFile`.
- On failure the batch UI shows "Either your file is too large or there are problems with the karyotypes contained in it. Please contact us." **No size limit is stated** anywhere (the page, the terms or the zip README) **[obs]**.

### Local (source)
- There is no command line. `main/Run.java` contains four placeholder paths that you replace and then run from an IDE (repo `README.txt`). It calls:
  - `AggregateJsonForBatchFile.generateJson(in, out.json)`
  - `AggregateCsvForBatchFile.generateAggregateLGF(in, lgf.csv)`
  - `SummaryStatisticsForBatchFile.writeSummaryStatistics(in, stats.csv)`
- Per-string entry point: `main.KaryotypeRunner.getFinalResult(String)` → `business.FinalResult` (flags for lexer/parser and validation errors, token errors, revised karyotype, LGF per clone).
- Dependencies: the ANTLR 4.7 runtime (shipped as `resource/antlr-4.7-complete.jar`) and **`javax.json`**. The second one is *not* shipped and not in `.classpath`. `org.glassfish:javax.json:1.1.4` from Maven Central works (SHA-1 `943f240a…7c88`, checked against Central) **[obs]**.
- The committed generated parser (`target/generated-sources/antlr4`) matches `Karyotype.g4`: I regenerated it with ANTLR 4.7 and the only difference was the package line **[obs]**.

## 3. Output format

The JSON has the same schema locally and hosted (repo `README.txt`; **[obs]** both):

```json
{ "producer": "CytoGPS", "date": "YYYY-MM-dd",
  "iscn2016_bands": [ "1p36.33", …, "Yq12" ],          // 916 bands
  "output": [ { "karyotype": "<input line>", "status": "<one of 5>", … } ] }
```

| `status` | Extra fields | Meaning |
|---|---|---|
| `Success` | `parsing_result[]` | parsed and validated |
| `Fixable grammar error and success` | `grammar_error[]`, `revised_karyotype`, `parsing_result[]` | auto-corrected, then valid |
| `Fixable grammar error but containing validation error` | `grammar_error[]`, `revised_karyotype`, `validation_error[]` | auto-corrected, still invalid |
| `Nonfixable grammar error` | `grammar_error[]` | not parsable |
| `Validation error` | `validation_error[]` | parsed, fails a semantic check |

- `parsing_result[]` has one object per clone: optional `cell_number`, `relationship`, `uncertain_events[]` and `derivative_chromosome_detailed_systems[]`, plus `loss_gain_fusion_computing: {loss[916], gain[916], fusion[916]}` (counts aligned with `iscn2016_bands`) **[obs]**.
- Granularity **[obs]**:
  - `del(5)(q13q33)` → loss on the 25 bands `5q13.1`…`5q33.3`.
  - `t(9;22)(q34;q11.2)` → fusion on `9q34.1–q34.3` and `22q11.21–q11.23`. A breakpoint band is expanded to all its sub-bands.
- Messages:
  - grammar errors: `"<token>: <message>"`, e.g. `"–: Unrecognized token"`, `"…: This is an incorrect input for karyotype parsing."`, `"Missing a slant line before '…'"`;
  - validation errors: free English text with a suggestion, e.g. `Event "del(5)(q33q13)": the breakpoint more proximal to the centromere should be specified first, maybe you want to try "del(5)(q13q33)"`.
  - **Messages carry no rule IDs, citations or character positions** **[obs]**.
- RCytoGPS (CRAN, v1.2.14, 2026-09-25, Apache-2.0) only *reads* these JSON files (`readLGF` → `source`, `raw`, `frequency`, `size`, `CL`). It doesn't call the service: https://cran.r-project.org/web/packages/RCytoGPS/index.html

## 4. ISCN edition and notation coverage

- **ISCN 2016**: the band list is named `iscn2016_bands` (916 bands at the 850 level), and the class `toolkit/ISCN2016.java` holds it (repo `README.txt`, source). The edition matches our pilot profile. Later editions (2020, 2024) are not supported.
- **Conventional karyotypes, short system only.** The grammar has no rules for `ish`, `nuc ish`, `arr`, the detailed system (`->`, `::`), `fra`, `upd`, `rec`, `fis`, `tas`, `neo` or the transplant chimera separator `//`. `idic`, `ider`, `psu`, `trc`, `dic`, `rob`, `hsr`, `mar`, `dmin`, `mos`/`chi` and `idem`/`sl`/`sdl` are supported (grammar rules `id`, `derId`, `idUncertainChr`, `undecoded`, `mosChi`). The hosted About page says FISH and microarray are "not yet" supported.
- **ASCII only.** The printed symbols `–` (en dash minus), `×` and `→` are "Unrecognized token". Only `-`, `x` and `->` are accepted **[obs]**. A comparison must first convert printed symbols to their typed forms (the same mapping as our `SYM.TYPED_SYMBOL_FORMS`, T-063).
- **Whitespace is removed before parsing**: `KaryotypeRunner.getFinalResult` runs `input.replaceAll("\\s","")` (source), and the lexer also skips `WS`.
- **Case-insensitive**: every keyword is written as `('d'|'D')('e'|'E')('l'|'L')`, and `sex: 'x'|'X'|'y'|'Y'` (grammar).

## 5. Observed behaviour

### 5.1 Targeted strings (hosted = local, all 28 agree) [obs]

| String | CytoGPS (hosted and local) | CytoLex 2016 |
|---|---|---|
| `47,XY, +21` / `46,X Y,dup(1)(q21q32)` / `46 ,XX` / `47,XX,+ 21` | Success (spaces dropped) | error `SYM.NO_OTHER_SPACE` |
| `46,xy` / `46,XX,Del(5)(q13q33)` / `46,XY,T(9;22)(q34;q11.2)` | Success (case ignored) | error `SYM.LETTER_CASE` |
| `46,XY,+21` / `47,XY` | Success | error (count vs. changes) |
| `46,XY,inv(16)(p13.1q22),t(9;22)(q34;q11.2)` | Success | error (order of abnormalities) |
| `46,XY,t(22;9)(q11.2;q34)` | Success | error `STRUCT.CHROMOSOME_ORDER` |
| `46,XY,del(5)(q33q13)` | Validation error (proximal breakpoint first) | error |
| `46,XY,del(5)(q13q99)` | Validation error ("not a valid chromosome breakpoint") | error |
| `46,XY,t(9;22)(q34;q11.2)×2` | Nonfixable grammar error (`×`) | valid |
| `46,XY,t(9;22)(q34–q11.2)` | Fixable → `t(9;22)(q34q11.2)`, then validation error | error |
| `46,XY.ish del(22)(q11.2q11.2)(TUPLE1-)` / `arr(1-22)x2,(XY)x1` | Nonfixable grammar error (not supported) | ish: not implemented / arr: checked |
| `46,XY,t(9;22)(q34;q11.2)[20]/46,XY[5]`, `46,XY,del(5)(q13q33)[2]/47,idem,+8[3]` | Success, 2 clones with `cell_number` | valid |

### 5.2 All KB examples, run locally [obs]

Input: the 951 strings of `tests/data/batch/2016/all.csv`. Rows of type `karyotype`: 822, with KB labels valid 549, invalid 143, corrected 130.

**After converting printed symbols to ASCII** (`–`/`−`→`-`, `×`→`x`, `→`→`->`). "Accepted" = `Success` or `Fixable grammar error and success`:

| KB label | accepted by CytoGPS | rejected by CytoGPS |
|---|---|---|
| valid | 417 | 132 |
| invalid | 85 | 58 |
| corrected | 119 | 11 |

- **Valid examples that CytoGPS rejects** are mostly notation it doesn't cover: the detailed system (100), `fra` (8), `rec` (7), `upd` (4), `tas` (3), `fis` (2), `neo` (2), `//` (2), and per-abnormality counts in `[cp]` (1). The rest: `46,XX,der(5)mat` (CytoGPS requires a rearrangement in `der`), a constitutional derivative list with `c`, and the fragment `end 46,XX`.
- **Invalid examples that CytoGPS accepts** (85) show which kinds of rule it doesn't check:
  - order of abnormalities and clones (`KARYO.ABNORMALITY_ORDER`, `ORDER.*`, `CLONE.ORDER_*`, `CLONE.NORMAL_LAST`);
  - chromosome count vs. changes (`NUM.COUNT_MATCHES_CHANGES`, `NUM.PLOIDY_REFERENCE`);
  - chromosome order inside `t`/`dic`/`rob`/`tas`/`trc` (`STRUCT.CHROMOSOME_ORDER`, `*.FORMAT`);
  - sex-chromosome conventions (`SEX.*`);
  - required forms (`DEL.FORMAT` `del(5q)`, `ISO.FORMAT` `i(17q)`);
  - spaces and case (`SYM.NO_OTHER_SPACE`, `SYM.NO_SPACE_AT_PARENTHESIS`, `SYM.SPACE_BEFORE_CHROMOSOME_COUNT`, `SYM.LETTER_CASE`, `SYM.TYPED_SYMBOL_FORMS` `X2`);
  - cell-count and clone rules (`CLONE.CELL_COUNT_REQUIRED`, `CLONE.MIN_CELLS`, `CLONE.SUBCLONE_REFERENCE`);
  - `MAR.*`, `RING.*`, `UNCERTAIN.*`, `TRANSLOC.JUMPING`, `KARYO.CONSTITUTIONAL_C`, `KARYO.DE_NOVO_SPACE`.
- What CytoGPS does check: band existence, breakpoint order, bracket and slant repair, and derivative consistency (most of its code is `validation/Dic*`, `DerivativeValidation`).
- Without the ASCII conversion, 180 of the valid examples fail first on `–`/`×`/`→`, and the number of valid examples rejected rises from 132 to 207. Many of the 180 still fail after conversion because of `->`.
- The raw outputs aren't committed (they came from the scratchpad). Re-run with §7.

### 5.3 Speed [obs]
- Local, JSON only: 951 strings in 66 s (~70 ms per string; a fixable string is parsed twice).
- With all three outputs (JSON + LGF CSV + statistics): 184 s, because each writer re-parses the whole file.
- JVM start-up: under 1 s.

## 6. Licence, terms, privacy

- **GitHub repo: no licence file** (`"license": null` from the GitHub API). On its own that means all rights are reserved.
- **The hosted "User Agreement"** (`GET /terms`, linked in the site's navigation bar) is a WU licence for "the computer code provided here (the 'Software')". It grants a "non-exclusive, royalty-free license to use … specifically excluding commercial use or sales". Copies must keep the agreement and the notice "Copyright © 2016 - 2021 Washington University in St. Louis". The software is provided "AS IS", there is an indemnification clause, and the WU name may not be used for promotion: https://cytogps-app.mangograss-a1cf8a6c.westus2.azurecontainerapps.io/terms
  - Reading: private, non-commercial research use (this project as it is now) is covered. **Don't vendor or commit CytoGPS code into CytoLex.** Bundling or redistributing it, or any commercial use, would need permission from WU.
- **Privacy:** the site has no privacy policy and states nothing about retention **[obs]**.
  - Batch results are stored on the server and downloaded from `/download/results/<upload name>_CytoGPS_analysis_<date>.zip`. The name has no random part, so others could probably retrieve results whose file name and date are known (not tested).
  - It's hosted on Azure Container Apps (westus2, USA).
  - **Conclusion for T-048:** don't send real patient karyotypes to the hosted app.
- No public container image was found. The hosted app's own server code (it serves HTML fragments, zips and an ideogram UI) is **not** in the GitHub repo; only the core parser is.

## 7. Running locally (what worked) [obs]

- JDK: Eclipse Temurin 21.0.13.1+2, zip from https://api.adoptium.net/v3/assets/latest/21/hotspot (SHA-256 checked), unpacked to `C:\_\Tests\PortableJDK\jdk-21.0.13.1+2`. Nothing is installed system-wide.
- Libraries: `resource/antlr-4.7-complete.jar` (shipped) and `javax.json-1.1.4.jar` (Maven Central).
- Wrapper (our own code, kept outside both repos). It makes the same calls as `Run.java`, with paths taken from the command line:

```java
import java.nio.file.*;
import toolkit.*;
public class CytoGpsCli {
    public static void main(String[] a) throws Exception {
        Path in = Paths.get(a[0]);
        AggregateJsonForBatchFile.generateJson(in, Paths.get(a[1]));
        if (a.length > 2) AggregateCsvForBatchFile.generateAggregateLGF(in, Paths.get(a[2]));
        if (a.length > 3) SummaryStatisticsForBatchFile.writeSummaryStatistics(in, Paths.get(a[3]));
    }
}
```

- Build and run (Windows paths, `;` classpath separator):

```
javac -nowarn -encoding UTF-8 -d classes -cp antlr-4.7-complete.jar;javax.json-1.1.4.jar  <all .java under src/, target/generated-sources/antlr4/, CytoGpsCli.java>
java  -cp classes;antlr-4.7-complete.jar;javax.json-1.1.4.jar  CytoGpsCli input.txt out.json [lgf.csv stats.csv]
```

- Input: UTF-8 text, one karyotype per line. The output matches the hosted app (§5.1).
- **Since T-049 the adapter does this itself:** `src/CytoLex.External/CytoGps/` compiles the CytoGPS sources plus this wrapper (JSON output only) into `build/cache/cytogps/` on demand and runs one JVM per batch; setup in `config/local.yaml` → `cytogps` (see README → "CytoGPS (optional)"). The javax.json jar is checked against Maven Central's SHA-1 `943f240a509d3c70b448a55c6735591ecbd37c88`. A scan of the CytoGPS sources (2026-11-21) found no `java.net`/socket use.

## 8. Implications

- **T-048 (integration mode, data policy):** recommend the **local build** as the default.
  - Same results as hosted, no network, no data leaves the machine, and no rate limit.
  - The hosted app is fine for synthetic strings only. Its single-string endpoint returns HTML, and its batch endpoint keeps result files on the server.
- **T-049 (adapter):** use a batch file → JSON adapter around the local jar (one JVM per batch, not per string). Config keys in `config/local.yaml`, e.g. `cytogps.dir` (source/classes) and `cytogps.java` (path to the JDK `java`). Compile once into `build/cache/cytogps/` (gitignored). Before sending, convert printed symbols to ASCII. Map the five statuses to valid/invalid: `Success` and `Fixable … and success` count as valid. Record `revised_karyotype` when present.
- **T-051 (comparison):** compare `karyotype` segments only, and mark ish/arr/detailed-system strings as "not supported by CytoGPS" so they stay out of the 2×2 matrix.
  - Expect many "CytoLex error / CytoGPS valid" cells. Their cause is CytoGPS not checking order, count, chromosome order, sex conventions, spaces or case (§5.2), not our bugs.
  - Triage by rule family will be quick.
- **T-050/T-051 (LGF):** 916 bands at 850 resolution, a breakpoint band expanded to its sub-bands, and counts per clone. Our LGF must use the same band set and expansion to be comparable.
- **T-075 (severity of spaces and case):** CytoGPS silently accepts stray spaces and any letter case. It doesn't normalise or report them, and has no warning level. Keeping them as `error` adds 2×2 disagreements of a known, harmless kind. That's a point for `warning` or for a separate "format only" category in the comparison, but the book stays the basis.

## 9. Open questions

- The licence of the GitHub source (no file) vs. the site's User Agreement: does the agreement cover the repo code? It only matters before any redistribution or public deployment (see T-043). A draft enquiry to the authors (not sent):
  > CytoLex is a private, non-commercial ISCN validator. We'd like to run CytoGPS locally to compare validation results on synthetic and book examples. Does the WU User Agreement on the CytoGPS site apply to the code in github.com/i2-wustl/CytoGPS? Is a newer version of the parser, or of the web app's server code, available?
- Whether the hosted app runs newer parser code than the 2021 GitHub snapshot: no difference was seen on the 28 probe strings and the 5-line batch.
