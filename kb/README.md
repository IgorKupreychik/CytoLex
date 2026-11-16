# Knowledge base (KB) — file format and conventions

The KB is the single source for every validation decision. Rules are YAML, checked against the JSON Schemas (draft 2020-12) in [`schema/`](schema/). Check files with:

```sh
uv run --directory tools kb check                 # all of kb/rules/ and kb/source-quotes/
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
| No chunks for the edition (`kb extract` not run): the chunk checks above are skipped | warning |

Any error → exit 1 and nothing written. Otherwise it writes `build/kb.sqlite` (tables `rules`, `citations`, `examples`, `quotes`, `rule_applies_to`, `rule_context`, `rule_related`, `meta`; FTS5 `rules_fts`, `quotes_fts`), `build/kb.json` (`{format_version, editions: {"2016": {chunks_checked, rules, quotes}}}`, each cite with a `rendered` string) and `build/review/kb-review.md`.

## Layout
| Path | Content |
|---|---|
| `schema/common.schema.json` | Shared definitions: rule ID, citation, vocabularies |
| `schema/rules.schema.json` | Rule files |
| `schema/quotes.schema.json` | Quote files |
| `rules/<version>/chNN/<section-file>.yaml` | Rules for one ISCN section |
| `source-quotes/<version>/chNN/<section-file>.yaml` | Verbatim quotes for the same section (same relative path) |
| `source-text/<version>/` | Extracted chunks (gitignored; `kb extract`) |

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
- The first segment is a topic prefix, e.g. `GEN` (general), `BAND`, `SYM`, `KARYO`, `SEX`, `NUM`, `STRUCT`, `DEL`, `DER`, `DUP`, `INS`, `INV`, `ISO`, `MAR`, `RING`, `TRANSLOC`, `CLONE`, `ISH`, `ARR`, … Use readable words rather than one-letter ISCN symbols (`ISO`, not `I`). New prefixes are fine.
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
