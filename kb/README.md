# Knowledge base (KB) — file format and conventions

The KB is the single source for every validation decision. Rules are YAML, checked against the JSON Schemas (draft 2020-12) in [`schema/`](schema/). Check files with:

```sh
uv run --directory tools kb check                 # all of kb/rules/, kb/source-quotes/, kb/vocab/ and kb/translation/
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
| *With chunks:* a valid example with an `En` cite occurs in that example block (all whitespace ignored; typed forms of `equivalent_forms` vocab entries count as the printed token, so `->`/`-`/`x` match `→`/`–`/`×`) | warning |
| Vocab: names unique per edition; labels unique; a token in one entry only, unless listed in `shared_tokens` (a listed token must be shared); `pair` tokens are in `tokens` | error |
| Vocab `equivalent_forms` only on an entry with 2+ tokens and no `pair` | error |
| *With chunks:* vocab coverage: the entry labels equal the first-column cells of the `source` tables, and each entry cites the block its row is printed in | error |
| No chunks for the edition (`kb extract` not run): the chunk checks above are skipped | warning |
| Bands (`kb/bands/<version>/bands.yaml`, T-060): `version` = edition, every band has `name`, `chrom`, `arm`, `parent`, `levels`, `derived`, `book`, `grch37`, `grch38`, names unique | error |
| No `bands.yaml` for the edition (`kb bands` not run): the runtime bundle has `bands: null` | warning |
| Translation templates (T-057): file named `templates.<language>.yaml` with that `language`; `key` is an existing rule ID or vocab item of the edition, unique; `construct` unique, and every construct of the schema's list has a template; every `{placeholder}` is a declared slot, braces balanced; `when` unique, required when a template has several forms | error |
| A declared template slot that no form uses | warning |
| Golden file (T-057): named `golden.yaml`; inputs unique; each item `span` matches the input at `start` (UTF-16 offsets); every `kb` reference exists (rule, vocab item, band of `bands.yaml`); *with chunks:* `cite` as for rules | error |

Any error → exit 1 and nothing written. Otherwise it writes `build/kb.sqlite` (tables `rules` (with `fix_auto` = the `fix.auto` flag, T-097), `citations`, `examples`, `quotes`, `rule_applies_to`, `rule_context`, `rule_related`, `vocab` (with the vocab file's `section`, T-059), `vocab_tokens`, `bands` (one row per band of `kb/bands/<version>/bands.yaml` in file order: chromosome, arm, parent, levels as JSON, derived, stain, book, GRCh37/38 start/end; T-084), `band_exceptions` (`exceptions.yaml`: level, region, resolution, book/ncbi/keep as JSON lists, note, rendered citation; T-084), `meta` (also `bands.levels_available.<version>`); FTS5 `rules_fts`, `quotes_fts`), `build/kb.json` (`{format_version: 2, editions: {"2016": {chunks_checked, rules, quotes, vocab, translation?: {templates: {<language>: […]}, golden: […]}}}}`, each cite with a `rendered` string; the full KB, also read by the KB-example tests), `build/kb-runtime.json` and `build/review/kb-review.md`.

`build/kb-runtime.json` is the **lean runtime bundle** that `CytoLex.Core` embeds (T-060, `docs/design/core.md` §6). It is compact JSON with its own `format_version` (1, independent of `kb.json`'s) and is byte-identical across runs:
```
{format_version: 1, editions: {"2016": {
  rules: [{id, section, title, severity, statement, applies_to, context?, related?, fix?,
           review: {needs_review, note?}, cite: [{section, page, para?|example?|float?, rendered}]}],
  vocab: {<vocabulary>: {shared_tokens?, entries: [{label, kind, tokens, pair?, equivalent_forms?, meanings, cite: "<rendered>"}]}},
  bands: {levels_available, bands: [{name, chrom, arm, parent, levels, derived, book, grch37, grch38, stain}]} | null,
  translation?: {templates: {<language>: [<template as in the YAML file>]}}}}}
```
`translation` (T-057) is present only when the edition has `kb/translation/<version>/`; it was added without a format bump because readers ignore unknown keys. Left out: the golden file, quotes, examples, `paraphrase_checked`, file paths, vocab `source`/`review`/`note`, band sources/counts. Band `stain` (gneg, gpos25…gpos100, acen, gvar, stalk; `null` for derived and book-only names) was added in T-101 without a format bump, for the desktop ideograms; a bundle without it reads as no stain. Bump `RUNTIME_FORMAT_VERSION` in `tools/src/cytolex_tools/kb/build.py` on any incompatible change; the core checks it at load (T-028).

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
| `schema/translation-templates.schema.json`, `schema/translation-golden.schema.json` | Translation templates and golden file (T-057) |
| `translation/<version>/templates.<language>.yaml` | Literal translation phrase templates, keyed by KB entry (English: `templates.en.yaml`) |
| `translation/<version>/golden.yaml` | Expected translations (string → summary, items with spans and KB refs): the renderer's test set |

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
| `severity` | ✔ | `error` · `warning` · `info`: severity of a violation, from the book's wording (see [Severity](#severity)) |
| `statement` | ✔ | **Paraphrase** of the book's rule, never a verbatim copy |
| `cite` | ✔ | ≥1 citation (below) |
| `examples.valid` | ✔ | `{text, cite?, note?}`. **Every book example** in the section goes here, with its `En` cite. May be `[]` |
| `examples.invalid` | | `{text, why, corrected?, cite?}`. `why` names the faulty part; `corrected` when there is exactly one valid form |
| `fix` | | `level: 1` = deterministic edit (`kind`: `replace` · `insert` · `delete` · `reorder`, required); `level: 3` = explanation only (no `kind`). `hint` describes the edit or the explanation. `auto: true` (level 1 only, T-096) = the validator may apply the rule's fixes automatically when autocorrection is on (`ValidationOptions.AutoCorrect`); set it only for fixes that keep the meaning, have a single alternative and don't depend on the sample context, and only after review (2016: `SYM.NO_OTHER_SPACE`, `SYM.LETTER_CASE`, `SYM.NO_SPACE_AT_PARENTHESIS`, `BAND.DESIGNATION_FORMAT`) |
| `review.paraphrase_checked` | ✔ | `true` once a person has approved `statement` against the quote |
| `review.needs_review` | ✔ | `true` when the book is ambiguous or the section is listed in `docs/extraction-review-<version>.md`; then `review.note` (why) is required. Caps findings at `warning` (see [Severity](#severity)) |
| `related` | | IDs of related rules |

Unknown fields are errors (catches typos). Example strings are written the way the book prints them (`→`, `–`, `×`, U+0332 for underlining); see T-053 for ASCII input forms. Underlining has no ASCII form: U+0332 after each underlined character is also the input convention (T-042).

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

### Severity
Policy: `docs/decisions.md` → "Strictness & severity" (T-022); applied to the 2016 rules in T-056. The engine resolves the effective severity at runtime (`docs/design/core.md` §8.2–8.3); the KB states only the rule's own severity.

| Book wording (for the violation a check flags) | `severity` |
|---|---|
| Mandatory: "is", "must", "always", a forbidding "should not", the printed format | `error` |
| Preference: "recommended", "preferred", "discouraged", "may be simplified" | `warning` |
| Explanation or convention | `info` |

- **Judge the violation, not the whole rule.** "mos or chi may be used … a space should follow" allows an optional symbol but fixes how it is written, so the rule is `error`. "may" only grants the option.
- **A different severity needs a reason** in `review.note` ("Severity info (T-056): …"). Typical reason: a string can't violate the rule (it defines how to read the notation), or a warning would fire on the book's own examples.
- **`needs_review` caps findings at `warning`**, marked "book ambiguous" with the note, until a person resolves the reading. Set it only for real book ambiguity or extraction loss, not for open policy questions. Keep the rule's own severity in `severity`: it applies once the flag is cleared.
- **Rules a single string can't verify** (they need the report or the family, e.g. `KARYO.ABBREVIATED_FORM`, `REC.KNOWN_PARENTAL_REARRANGEMENT`) are `info`. Their check reports a finding with `cannotVerify` **only when the string uses the notation concerned** ("can't be verified from the string: <what to check>"); otherwise it stays silent. A valid example may therefore show this note, and the KB-example harness accepts it.
- **"Error when certain, else warning"** (e.g. `NUM.COUNT_MATCHES_CHANGES`) is `severity: error`. The check reports `Uncertain` where it can't decide (dic/rob/fis counts, `~`, `inc`, uncertain markers), and the engine caps those findings at warning. No schema field is needed.
- **Context-limited rules** have a `context`. With the context unknown, the engine turns their findings into conditional diagnostics with a severity per context ("error if acquired"). The string is valid if one context has no errors.
- **Valid examples never fire their own rule.** A book form the book prints as correct but that triggers a `warning` (e.g. the longer E6 form of `ORDER.SHORTEST_STRING_PREFERRED`) goes under `invalid` with its `cite`, a `why` that says it is a warning, and `corrected`.

### Vocabularies
| Field | Values |
|---|---|
| `applies_to` | `general` · `karyotype` · `breakage` · `meiotic` · `ish` · `nuc_ish` · `fib_ish` · `rev_ish` · `cgh` · `arr` · `rsa` · `seq` |
| `context` | `constitutional` · `neoplastic` · `meiotic` |
| `severity` | `error` · `warning` · `info` |
| `fix.level` / `fix.kind` / `fix.auto` | `1` with `replace` · `insert` · `delete` · `reorder` (optional `auto: true`); `3` (no kind, no auto) |

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
- `tokens`: as printed (`→`, `–`, `×`) first, then the typed forms of the same symbol: those the book gives itself (`->`) and those decided in T-053 (`-`/`−` for the minus sign, lowercase `x` for the multiplication sign; rule `SYM.TYPED_SYMBOL_FORMS`). Several tokens for one row are fine (`I`, `II`, `III`, `IV`). Empty only for `kind: typographic` (underlining, T-042).
- `equivalent_forms: true`: every token is a form of the same symbol (the lexer treats them as one; arrow, minus, multiplication sign). Not for bracket pairs or lists of different symbols (`I`…`IV`). Where a form is only valid in some positions (lowercase `x`), the entry's `note` says so.
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

## Translation (`translation/<version>/`)
Data of the literal translation (decision T-054 in `docs/decisions.md`; renderer T-058, desktop view T-059). **All entries are drafts** (`review.checked: false`) until a person reviews them (T-082). Drafts are used as they are: the renderer is tested against every golden entry, and the desktop app marks text from unreviewed templates as draft wording.

### Templates (`templates.<language>.yaml`, schema `schema/translation-templates.schema.json`)
```yaml
version: "2016"
language: en
templates:
  - construct: deletion                 # one of the constructs below; each needs exactly one template
    key: "vocab:symbols/del"            # the KB entry the phrase renders (KbRef text form): rule:ID or vocab:<vocabulary>/<label>
    slots: [chromosome, arm, from, to]  # placeholders the renderer fills
    forms:                              # one phrase, or one per variant (when), picked by the renderer
      - when: terminal
        text: "Terminal deletion of chromosome {chromosome}: break at {from}; …"
      - when: interstitial
        text: "Interstitial deletion of chromosome {chromosome}: the segment between {from} and {to} is lost."
        # readings: {constitutional: …, neoplastic: …}   only where the context changes the meaning
    note: …                             # optional, for renderer authors
    review: {checked: false}            # note optional
```
- **Keys:** a symbol's construct is keyed by its vocab item (`vocab:symbols/inv`); a construct without a symbol by its rule (`rule:KARYO.TRANSPLANT_CHIMERA`, `rule:UNCERTAIN.APPROX_INTERVAL`). The renderer links each phrase to that entry; the rule browser shows its citation (no citations in the translation itself).
- **Readings:** the neutral `text` is used when the context is unknown and the meaning does not depend on it. `readings` give one text per context where it does (whole-chromosome gain/loss, an abnormal sex chromosome complement); with the context unknown the renderer shows both, with a known context only that one.
- **Constructs** (the schema's `construct` list: karyotype ch. 2–9 of the core model, `docs/design/core.md` §5):
  - header: `summary`, `chromosome_count`, `ploidy`, `sex_chromosomes`, `cell_count`, `composite` (`cp`), `cell_line_separator`, `clone_reference` (`idem`/`sl`/`sdl`, T-058);
  - relations: `mosaic`, `chimera`, `transplant_chimera`, `endoreduplication`;
  - numerical changes and suffixes: `gain_loss`, `copies`, `constitutional_c`, `uncertain_constitutional`, `inherited`, `maternal`, `paternal`, `de_novo`, `uniparental_disomy`;
  - ch. 9 symbols: `additional_material`, `deletion`, `derivative`, `isoderivative`, `dicentric`, `isodicentric`, `pseudodicentric`, `double_minutes`, `duplication`, `fission`, `fragile_site`, `hsr`, `insertion`, `inversion`, `isochromosome`, `marker`, `neocentromere`, `quadruplication`, `ring`, `recombinant`, `robertsonian`, `telomeric_association`, `translocation`, `tricentric`, `triplication`, `incomplete`;
  - uncertainty (ch. 5): `questionable_item`, `unknown_designation`, `approximate_interval`, `alternative`;
  - breakpoints and bands: `band`, `region_only`, `centromere_band`, `short_arm`, `long_arm`, `short_arm_end`, `long_arm_end`, `arm_end`, `centromere`;
  - detailed system: `band_composition`, `break_reunion`, `break`, `segment`;
  - variants (ch. 7): `heterochromatin`, `satellite`, `stalk`, `short_arm_satellite`, `long_arm_satellite`, `variant_region`, `length_change`;
  - in situ hybridization (ch. 13, T-093): `in_situ` (technique names and the segment summary), `in_situ_cell_line`, `cells_scored`, `probe_results`, `locus_status`, `signal_count`, `normal_chromosome`, `connected` (`con`), `separated` (`sep`), `amplified`, `diminished`, `enhanced` (`amp`/`dim`/`enh`, also the cgh gain/loss forms), `subtelomeric`, `whole_chromosome_paint`, `partial_chromosome_paint`, `reverse_ish`, `fiber_ish`, `cgh_region`.
  - Omitted items `…` (book fragments; no KB entry) are forms of `summary`. Not covered: other notation types (arr: T-105, rsa, seq; their parts are "not translated"). In situ: the technique is named in the summary only (one sentence per segment after the karyotype's); an ish abnormality is the karyotype sentence of its symbols + "; probe(s) …" (lists per chromosome after `;`, per breakpoint after separate breakpoint groups, per part of a derivative). Adding a construct = a schema enum value + a template in every language file.
- **How the renderer uses the forms (T-058):** a structural sentence is a `*_head` form ("Terminal deletion of chromosome {chromosome}") + a `*_rest` form (": break {break}; …") + suffixes (`copies`, `constitutional_c`, inheritance), joined with "; ". A gained item is `gain_loss.gain_rearranged` with the head in lower case, or the construct's `*_gain` form where that would read wrongly ("chromosome 7 with a terminal deletion"). `*_part` forms are the same rearrangement inside a derivative. Form texts carry no final full stop (the renderer adds it), except whole-sentence forms of `summary`, `cell_line_separator.header` and `transplant_chimera.one_side`. Lists ("A, B and C") and the punctuation between phrases come from the renderer. Every form the renderer asks for must exist; the golden test fails otherwise.

### Golden file (`golden.yaml`, schema `schema/translation-golden.schema.json`)
```yaml
version: "2016"
golden:
  - input: "46,XX,del(5)(q13q33)"
    cite: {section: "9.2.2", page: 60, example: "E2"}   # book example; or origin: synthetic (+ note)
    fragment: true                                      # optional: parsed with the fragment entry
    context: unknown                                    # unknown · constitutional · neoplastic (the hint)
    summary: "46 chromosomes; sex chromosomes XX; 1 abnormality."
    items:
      - span: {start: 6, text: "del(5)(q13q33)"}         # UTF-16 offset in input + the text there
        sentence: "Interstitial deletion of chromosome 5: the segment between 5q13 and 5q33 is lost."
        readings: {constitutional: …, neoplastic: …}     # optional, as in the templates
        kb: ["vocab:symbols/del", "rule:DEL.FORMAT", …]
    review: {checked: false}
```
- **Items** are exactly the translated parts of the model, in input order: with two or more cell lines, each line's header (count, ploidy, sex chromosomes; its cell count goes into the sentence) followed by its items; with one cell line, only its items (the header is in the summary). Prefixes (`mos`, `chi`, `end`) go into the summary. In situ segments (T-093) follow in segment order, one item per result (its span includes the cells; with two or more cell lines the sentence starts with the line or donor/recipient). `GoldenFileTests` (C#) checks the spans and KB refs against the binder, and that synthetic entries are valid ISCN.
- **Book entries** cover every cited valid example of ch. 4, 8 and 9 that is a karyotype (372; the 4 `arr` strings of ch. 4 are left out) and, since T-093, of ch. 13 made of karyotype and in situ segments (123; strings with an `arr` segment wait for T-105); the sentence paraphrases the book's printed explanation (cite, never quote). **Synthetic entries** (26) cover what the book lacks: both readings with the context unknown, `mos`/`chi`/`//`, `~`, `?`.
- **Exact match (T-058):** `RendererGoldenTests` requires the renderer's summary, items, sentences, readings and KB links to equal each entry. Where the first drafts contradicted each other or added book interpretation, the sentence was changed to the renderer's wording and the old one kept in `review.note` ("T-058: wording normalized for the renderer; was: …") for T-082. To list mismatches after a change: `CYTOLEX_GOLDEN_DIFF=<file>` with `dotnet test --project tests/CytoLex.Core.Tests --filter-method "*WriteDifferenceReport"` writes a report (and `<file>.json`).
- **Skeleton for new entries:** `CYTOLEX_GOLDEN_DRAFT=1` (and optionally `CYTOLEX_GOLDEN_DRAFT_INPUTS=<file>`, one non-book string per line) with `dotnet test --project tests/CytoLex.Core.Tests --filter-class "CytoLex.Core.Tests.Translation.GoldenDraftTests"` writes `build/review/translation/golden-draft-2016.yaml`: the inputs not yet in the golden file (book examples of ch. 4, 8, 9 and 13), with their spans and KB refs from the binder and empty sentences to fill in. With `CYTOLEX_GOLDEN_DRAFT_RENDER=1` too (T-093), the summary, sentences, readings and KB links are the renderer's output, to be checked by hand against the book before they go into the golden file.
