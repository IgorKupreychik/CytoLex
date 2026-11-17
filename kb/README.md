# Knowledge base (KB) — file format and conventions

The KB is the single source for every validation decision. Rules are YAML, checked against the JSON Schemas (draft 2020-12) in [`schema/`](schema/). Check files with:

```sh
uv run --directory tools kb check                 # all of kb/rules/, kb/source-quotes/ and kb/vocab/
uv run --directory tools kb check kb/rules/2016/ch09/09.02.17.01-reciprocal-translocations.yaml
```

`kb check` checks each file against the schema only. `uv run --directory tools kb build` runs the schema check on every file and then, per edition:

| Check | Result |
|---|---|
| File `version` = its edition directory; file name starts with its `section` (`09.02.17.01-…` ↔ `"9.2.17.1"`); a rules file under `rules/`, a quotes file under `source-quotes/` | error |
| Rule IDs unique within the edition; `related` IDs exist in the edition | error |
| Every quote file has a rule file at the same relative path with the same `section`; every quote's `rule` is in that rule file | error |
| *With chunks:* every `cite` (rules, examples, quotes): the section exists, its `para`/`example`/`float` label is in the chunk front matter, and `page` is that label's page | error |
| *With chunks:* quote `text` occurs verbatim in a cited label's block (whitespace, line breaks, `>` and code fences ignored) | error |
| *With chunks:* a valid example with an `En` cite occurs in that example block (all whitespace ignored) | warning |
| Vocab: names unique per edition; labels unique; a token in one entry only, unless listed in `shared_tokens` (a listed token must be shared); `pair` tokens are in `tokens` | error |
| *With chunks:* vocab coverage: the entry labels equal the first-column cells of the `source` tables, and each entry cites the block its row is printed in | error |
| No chunks for the edition (`kb extract` not run): the chunk checks above are skipped | warning |

Any error → exit 1 and nothing written. Otherwise it writes `build/kb.sqlite` (tables `rules`, `citations`, `examples`, `quotes`, `rule_applies_to`, `rule_context`, `rule_related`, `vocab`, `vocab_tokens`, `meta`; FTS5 `rules_fts`, `quotes_fts`), `build/kb.json` (`{format_version: 2, editions: {"2016": {chunks_checked, rules, quotes, vocab}}}`, each cite with a `rendered` string) and `build/review/kb-review.md`.

## Layout
| Path | Content |
|---|---|
| `schema/common.schema.json` | Shared definitions: rule ID, citation, vocabularies |
| `schema/rules.schema.json` | Rule files |
| `schema/quotes.schema.json` | Quote files |
| `rules/<version>/chNN/<section-file>.yaml` | Rules for one ISCN section |
| `source-quotes/<version>/chNN/<section-file>.yaml` | Verbatim quotes for the same section (same relative path) |
| `schema/vocab.schema.json` | Vocabulary files |
| `vocab/<version>/chNN/<section>-<name>.yaml` | A printed list as a parser vocabulary (ch. 3 symbols, Table 2 banding codes) |
| `source-text/<version>/` | Extracted chunks (gitignored; `kb extract`) |
| `schema/band-exceptions.schema.json` | `bands/<version>/exceptions.yaml` |
| `bands/<version>/bands.yaml` | Generated band data (`kb bands`; committed, don't edit by hand) |
| `bands/<version>/ideograms.yaml` | Band labels of the book's idiograms (`kb ideograms`; committed, don't edit by hand) |
| `bands/<version>/exceptions.yaml` | Cited decisions on every book-vs-NCBI band difference (hand-written; `kb check`) |

- **One file per numbered section**, named like its chunk with `.yaml` instead of `.md`: chunk `source-text/2016/ch09/09.02.17.01-reciprocal-translocations.md` → `rules/2016/ch09/09.02.17.01-reciprocal-translocations.yaml`. A rule that draws on several sections lives in the file of its main section and cites the others.
- **One directory per edition.** "A block per version" means the same rule ID appears in `rules/2016/…` and later in `rules/2020/…`, each with that edition's statement, citations and examples. The file's `version` must match its directory.

## Rule file
```yaml
version: "2016"                  # quoted
section: "9.2.17.1"              # the section this file covers, quoted
rules:
  - id: TRANSLOC.CHROMOSOME_ORDER
    title: Sex chromosome or lowest-numbered autosome first
    applies_to: [karyotype]
    context: [constitutional, neoplastic]   # optional; omitted = every context
    severity: error
    statement: >-
      In a two-chromosome translocation, a sex chromosome is written first; otherwise
      the autosome with the lowest number is written first.
    cite:
      - {section: "9.2.17.1", page: 75, para: "¶1"}
    examples:
      valid:
        - text: "46,XY,t(2;5)(q21;q31)"
          cite: {section: "9.2.17.1", page: 75, example: E1}
      invalid:
        - text: "46,XY,t(5;2)(q31;q21)"
          why: Chromosome 5 is listed before chromosome 2.
          corrected: "46,XY,t(2;5)(q21;q31)"
    fix: {level: 1, kind: reorder, hint: Swap the chromosomes and their breakpoints.}
    review: {paraphrase_checked: false, needs_review: false}
    related: [TRANSLOC.THREE_WAY_ORDER]     # optional
```

| Field | Req. | Meaning |
|---|---|---|
| `id` | ✔ | Stable concept ID (see below) |
| `title` | ✔ | Short name for lists and the rule browser |
| `applies_to` | ✔ | Notation types, ≥1 (vocabulary below) |
| `context` | | Clinical contexts; omit when the rule holds everywhere |
| `severity` | ✔ | `error` · `warning` · `info`: default severity of a violation (policy: T-022) |
| `statement` | ✔ | **Paraphrase** of the book's rule, never a verbatim copy |
| `cite` | ✔ | ≥1 citation (below) |
| `examples.valid` | ✔ | `{text, cite?, note?}`. **Every book example** in the section goes here, with its `En` cite. May be `[]` |
| `examples.invalid` | | `{text, why, corrected?, cite?}`. `why` names the faulty part; `corrected` when there is exactly one valid form |
| `fix` | | `level: 1` = deterministic edit (`kind`: `replace` · `insert` · `delete` · `reorder`, required); `level: 3` = explanation only (no `kind`). `hint` describes the edit or the explanation |
| `review.paraphrase_checked` | ✔ | `true` once a person has approved `statement` against the quote |
| `review.needs_review` | ✔ | `true` when the book is ambiguous or the section is listed in `docs/extraction-review-<version>.md`; then `review.note` (why) is required |
| `related` | | IDs of related rules |

Unknown fields are errors (catches typos). Example strings are written the way the book prints them (`→`, `–`, `×`, U+0332 for underlining); see T-053 for ASCII input forms.

## Quote file
```yaml
version: "2016"
section: "9.2.17.1"
quotes:
  - rule: TRANSLOC.CHROMOSOME_ORDER
    cite: {section: "9.2.17.1", page: 75, para: "¶1"}
    text: >-
      In translocations (t) affecting two chromosomes, the sex chromosome or the
      autosome with the lowest number is always specified first.
```
- `text` is copied **verbatim** from the chunk (without the `[¶1 p.75]` label). It may be a sentence-level excerpt of the cited paragraph.
- A rule may have several quotes. Quote files can be deleted before any public release (T-043); rules must stand without them.

## Conventions
### Rule IDs
- Pattern: dot-separated `UPPER_SNAKE` segments, at least two: `TOPIC.CONCEPT` or `TOPIC.SUBTOPIC.CONCEPT` (`TRANSLOC.CHROMOSOME_ORDER`, `BAND.SUBBAND_FORMAT`).
- The first segment is a topic prefix, e.g. `GEN` (general), `CHROM` (chromosome identifiers), `BAND`, `SYM`, `KARYO`, `SEX`, `NUM`, `STRUCT`, `DEL`, `DER`, `DUP`, `INS`, `INV`, `ISO`, `MAR`, `RING`, `TRANSLOC`, `CLONE`, `ISH`, `ARR`, … Use readable words rather than one-letter ISCN symbols (`ISO`, not `I`). New prefixes are fine.
- IDs name a **concept**, not a section or edition, so they stay valid across editions. **Never rename** an ID once used; retire it via the edition directories instead.

### Citations
Structured in YAML, rendered as text in diagnostics:

| YAML | Rendered |
|---|---|
| `{section: "9.2.17.1", page: 75, para: "¶1"}` | `ISCN 2016 §9.2.17.1, p.75, ¶1` |
| `{section: "9.2.17.1", page: 75, para: "¶2", example: E3}` | `ISCN 2016 §9.2.17.1, p.75, ¶2, ex. E3` |
| `{section: "9.2.17.1", page: 75, example: E3}` | `ISCN 2016 §9.2.17.1, p.75, ex. E3` |
| `{section: "2.2", page: 12, float: "Table 1"}` | `ISCN 2016 §2.2, p.12, Table 1` |

- The edition comes from the file's `version`.
- `page` is the **book** page of the cited label, as in the chunk (`[¶1 p.75]`, front matter `paragraphs`/`examples`/`floats`).
- At least one anchor: `para` (`¶n`), `example` (`En`), or `float` (the chunk's float label: `Fig. 7`, `Fig. 6a`, `Table 1`, `Ploidy table`, `Alignment figure`, `Fibre diagram`).
- Labels restart at every numbered section, so `section` + label is unique.

### Vocabularies
| Field | Values |
|---|---|
| `applies_to` | `general` · `karyotype` · `breakage` · `meiotic` · `ish` · `nuc_ish` · `fib_ish` · `rev_ish` · `cgh` · `arr` · `rsa` · `seq` |
| `context` | `constitutional` · `neoplastic` · `meiotic` |
| `severity` | `error` · `warning` · `info` |
| `fix.level` / `fix.kind` | `1` with `replace` · `insert` · `delete` · `reorder`; `3` (no kind) |

Type assignment: the chapter sets the default `applies_to`/`context`; explicit book text overrides it; ambiguous cases get `needs_review` (see `docs/decisions.md`).

Changing a vocabulary means editing `schema/common.schema.json` (and this table) and logging the decision in `docs/decisions.md`.

## Vocabulary file (`vocab/<version>/`)
A list the book prints (ch. 3 symbols, Table 2 banding codes) as data the parser can use. The file name starts with its section (`ch03/03-symbols.yaml`, `ch02/02.04-banding-codes.yaml`).
```yaml
version: "2016"
section: "3"
vocabulary: symbols              # unique per edition
source:                          # every chunk block holding the printed table
  - {section: "3", page: 34, para: "¶2"}
shared_tokens: ["."]             # tokens allowed in more than one entry
review: {paraphrase_checked: false, needs_review: false}   # note required when needs_review
entries:
  - label: "plus sign, single (+)"   # first-column text exactly as in the chunk
    tokens: ["+"]                    # what appears in a notation string
    kind: punctuation                # abbreviation · punctuation · typographic · code
    meanings:                        # one per ';'-part of the book's description
      - {text: Additional normal or abnormal chromosome., sections: ["4.1", "8.1"]}
    cite: {section: "3", page: 36, para: "¶4"}
  - label: "parentheses ( )"
    tokens: ["(", ")"]
    pair: {open: "(", close: ")"}    # bracket pairs
    ...
```
- `label` is only for the coverage check: `kb build` fails if a book row has no entry or an entry has no book row. It never appears in a string.
- `tokens`: as printed (`→`, `–`, `×`), plus alternatives only when the book itself gives them (`->`). Typed ASCII forms are T-053. Several tokens for one row are fine (`I`, `II`, `III`, `IV`). Empty only for `kind: typographic` (underlining, T-042).
- `meanings[].text` is a paraphrase; single-term names (`Deletion`) may equal the book. `sections` are the book's cross-references.
- Which meaning applies at a position (`.` = sub-band or technique separator; `p` = arm or nothing else) is the parser's job; the vocab lists all meanings.
- `note`: hints for parser authors (e.g. the en dash vs hyphen question).

## Band data (`bands/<version>/`)
`bands.yaml` is generated by `kb bands` (T-006, T-007) from the NCBI Genome Decoration Page ideograms (400/550/850 band sets), UCSC cytoBand and the book's idiograms; sources and sha256 pins are in `tools/src/cytolex_tools/bands/sources/<version>.yaml`. Don't edit it by hand; book-based decisions go in `exceptions.yaml`.
- Header: `version`, `generator_version`, `levels_available` (300, 400, 550, 700, 850), `levels_unavailable` (none for 2016), `sources` (URL or file, sha256, bytes; the last two are `ideograms.yaml` and `exceptions.yaml`), `counts`.
- `bands`: one entry per valid band name, in chromosome order (pter → qter), parents before children:
  - `name` (`9q34.1`), `chrom`, `arm`, `parent` (`9q34`; `null` at band level);
  - `levels`: the band levels showing the name: 300/700 from the book, 400/550/850 from NCBI as corrected by the exceptions; `derived: true` when no level shows it and it is only a nesting ancestor (e.g. `1p36`);
  - `stain` (finest NCBI level with the name; `null` if derived or book-only);
  - `grch37`, `grch38`: `[start, end]` in bp, 1-based closed; for coarser names the union of their 850-level sub-bands; `null` for book-only sub-bands no public source has (e.g. `1q32.11`);
  - `book`: the name appears in the book's idiograms (or is an ancestor of one). `false` only for NCBI names an exception keeps (`9q34.11`–`13`, `6p24.1`–`3`: valid for array/sequence notation, e.g. §15.3).
- A name absent from the file (`9q35`, `9q34.4`) is not a valid band. Arm-level terms (`pter`, `cen`, `p10`, …) are notation, not band data.

`ideograms.yaml` (`kb ideograms`): one entry per idiogram label, `{chrom, level, arm, label, page}`, in chromosome, level and pter → qter order. A 300/400-level label like `22–24` is a merged band (printed as a range); it counts for every band it spans (`22`, `23`, `24`).

`exceptions.yaml` (schema `schema/band-exceptions.schema.json`): every difference between the book's band sets and NCBI's at 400/550/850, grouped by region. `kb bands` fails on a difference without an entry or an entry that no longer matches, and writes a draft of all differences to `build/review/bands/<version>/exceptions-draft.yaml`. Entry fields:
- `level`, `region` (shortest name of the group), `book` / `ncbi` (names only that source has at this level);
- `resolution`: `book` (book names gain the level, NCBI-only names lose it; a name then shown at no level and not kept is dropped) or `ncbi` (NCBI level kept, book-only names ignored);
- `keep` (optional, with `book`): NCBI-only names that stay valid with their NCBI levels and `book: false`;
- `cite` (`{section: '2.5', page: 23, float: 'Fig. 5'}`) and `note`.
