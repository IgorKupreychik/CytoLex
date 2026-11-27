# Test corpus

Realistic ISCN strings that complement the KB examples (T-062). The KB examples (`curated/rules/`) stay the reference suite: short, book-cited, one rule each. The corpus adds:

- combined strings: several abnormalities, clones with `[n]`, `idem`/`sl`/`sdl`, composite karyotypes, mosaics, chimeras, segments joined by `.`;
- volume for batch runs (T-038) and the CytoGPS comparison (T-051);
- robustness inputs: typed ASCII forms (`-`, `x`, `->`), stray spaces, letter case, typos, truncated strings;
- the trusted valid strings the **mutation generator** turns into invalid ones.

**Data policy (T-048):** synthetic strings only, or strings adapted from the book (`source: book-adapted` with a `cite`). Never real patient strings, so there is no `lab` source.

## Files

One file per notation type. IDs are stable and unique across files; the prefix names the file.

| File | Prefix | Content | Labels |
|---|---|---|---|
| `karyotype.yaml` | `K-` | constitutional, acquired, mosaic/chimera, structural (incl. derivatives, complex), uncertainty, clone/composite | checked |
| `arr.yaml` | `A-` | every ch. 14 construct at least once | checked |
| `ish.yaml` | `I-` | metaphase FISH constructs | `unreviewed` (parse-only) until T-015/T-033 |
| `nuc_ish.yaml` | `N-` | interphase FISH constructs | `unreviewed` (parse-only) until T-016/T-033 |
| `combined.yaml` | `C-` | karyotype + `ish`/`nuc ish`/`arr` | checked when every segment is implemented |
| `robustness.yaml` | `R-` | typed forms, spacing, case, typos, truncation | checked |

Schema: `kb/schema/corpus.schema.json`. `uv run --directory tools kb check` validates these files with the KB.

## Entry format

```yaml
format: cytolex-corpus
version: 1
corpus:
  - id: K-001
    iscn: "46,XY,t(9;22)(q34;q11.2)[20]"
    types: [karyotype]                 # segment types in order, as Segmenter splits them (checked)
    features: [translocation, clone-count]
    rules: [TRANSLOC.FORMAT, CLONE.CELL_COUNT_REQUIRED]   # KB rules it relies on (must exist in the KB)
    source: synthetic                  # synthetic | book-adapted (+ cite)
    expected: {"2016": {verdict: valid, context: acquired}}
    review: {checked: false, note: "…"}
```

- `expected` is per ISCN edition (editions are not backward compatible). `verdict`: `valid`, `invalid` (then `fires` lists the rules that must be reported; others may be reported too) or `unreviewed`. `context` is the hint the verdict is given for (`constitutional`, `acquired`); without it, no hint.
- `fires` may name a core code. `CORE.UNPARSED` is listed only for truncated input that no KB rule covers yet (T-085).
- `review.checked`: a person confirmed the labels. `note` explains a label that isn't obvious (a decision, a severity cap).

## Label policy

Labels trace to KB rules, never to ISCN from memory (CLAUDE.md). A label is **checked** when the verdict isn't `unreviewed` and either a person reviewed the entry or every rule in `rules` and `fires` is a rule of that edition in the KB. Otherwise the entry is **parse-only**: it must split and parse with no `CORE.UNPARSED`, with no claim about validity.

The harness (`tests/CytoLex.Core.Tests/Corpus/`) runs each entry × edition with the entry's context hint:

- `valid`: valid, no errors;
- `invalid`: every `fires` rule is reported, and the string isn't valid unless the KB severity policy keeps those findings below error (`needs_review` rules, uncertain findings, KB severity warning/info). A `fires` rule with no check yet skips the entry (like the KB-example harness);
- entries with a segment the pilot doesn't implement (ish, nuc ish) skip with the not-implemented reason; in combined strings the label is checked only when every segment is implemented;
- entries the core gets wrong go into `PendingCorpus.cs` with their task. They skip, and fail once they pass.

When the validator disagrees with a label, check the label against its KB rules first. Fix a wrong label; record a core bug as pending with a new task.

## Mutation generator

`tests/CytoLex.Core.Tests/Corpus/Mutations/`: each operator makes one small edit at the first applicable site of a trusted valid entry and breaks exactly one KB rule. The site is found through the binder's model. Each operator is **grounded in a book pair**: applied to the corrected form of an invalid KB example of its rule, it reproduces that example (`MutationTests.Grounding_*`), so the expected error follows from the book.

| Operator | Edit (book pair) | Rule |
|---|---|---|
| `swap-chromosomes` | `t(12;16)(q13;p11.1)` → `t(16;12)(p11.1;q13)` | `STRUCT.CHROMOSOME_ORDER` |
| `chromosome-list-comma` | `t(2;5)` → `t(2,5)` | `STRUCT.CHROMOSOME_SEPARATOR` |
| `nonexistent-band` | arm's last band + 1 (`9q34` → `9q35`) | `BAND.MUST_EXIST` |
| `count-plus-one` | `47,XX,+21` → `48,XX,+21` (near-diploid lines, no ploidy, `?`, `×n` or `inc`) | `NUM.COUNT_MATCHES_CHANGES` |
| `swap-items` | `del(5)(q13q33),+21` → `+21,del(5)(q13q33)` | `KARYO.ABNORMALITY_ORDER` |
| `drop-comma-before-sign` | `del(5)(q13q33),+21` → `del(5)(q13q33)+21` | `KARYO.ABNORMALITY_ORDER` |
| `sign-after` | `+21` → `21+` | `KARYO.GAIN_LOSS_SIGN` |
| `drop-count-comma` | `46,XX` → `46XX` | `KARYO.COUNT_SEX_ORDER` |
| `drop-breakpoint-parentheses` | `inv(2)(p21q31)` → `inv(2)p21q31` | `STRUCT.BREAKPOINT_GROUPS` |
| `space-after-comma` | `47,XY,+21` → `47,XY, +21` | `SYM.NO_OTHER_SPACE` |
| `space-before-parenthesis` | `del(5)` → `del (5)` | `SYM.NO_SPACE_AT_PARENTHESIS` |
| `lower-case-sex` | `46,XY` → `46,xy` | `SYM.LETTER_CASE` |
| `capitalized-symbol` | `del` → `Del` | `SYM.LETTER_CASE` |
| `drop-cell-count` | last clone's `[n]` dropped (acquired, ≥ 2 lines) | `CLONE.CELL_COUNT_REQUIRED` |

Each mutant (`<entry id>~<operator>`) must report its rule and must not be valid (`MutationTests.Mutant_FiresItsRule`). Mutants are generated at test time (deterministic), not committed. Only ISCN 2016 has a KB yet.

## Export

`CYTOLEX_CORPUS_EXPORT=1 dotnet test --project tests/CytoLex.Core.Tests --filter-method "*WriteCorpusExport"` writes the corpus and its mutants to `build/review/corpus/corpus-2016.csv` (`id,iscn,type,expected,fires,source`). The desktop **Batch** tab and the CytoGPS comparison read it like any batch file.
