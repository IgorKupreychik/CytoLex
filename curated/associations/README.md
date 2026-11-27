# Association rules: how to read and edit `associations.yaml`

`associations.yaml` lists **conditions associated in the literature** with cytogenomic abnormalities: CML with t(9;22)(q34;q11.2), Down syndrome with +21, and so on. CytoLex will show them next to a result (T-109, T-110), always with their source and under the disclaimer "Not a diagnosis or an interpretation of this result". They never appear in the validation diagnostics or the literal translation (decision T-104).

The file is the same for every ISCN edition. Every rule names its source and whether a person has checked it. Rules nobody has checked are still used, but the app marks them "unreviewed". The easiest way to review is the plain-language list `build/review/associations-review.md`, which `kb build` writes (unreviewed rules first).

## A rule

```yaml
  - id: ASSOC.T9_22_CML                 # stable ID: ASSOC. + UPPER_SNAKE; never renamed
    condition:
      name: Chronic myeloid leukaemia, BCR::ABL1-positive
      ids: [{system: orphanet, code: "ORPHA:521"}]   # optional
    context: neoplastic                 # constitutional | neoplastic | any
    strength: characteristic            # characteristic | recurrent | reported
    defining:                           # optional: classifications where it is a defining criterion
      - source: WHO-HAEM5
        citation: "Myeloproliferative neoplasms: chronic myeloid leukaemia"
    source: WHO-HAEM5                   # WHO-HAEM5 | ICC-2022 | ClinGen | Orphanet | other
    citation: "WHO … 5th ed. (…), myeloproliferative neoplasms: chronic myeloid leukaemia"
    match:
      any:
        - exact: t(9;22)(q34;q11.2)
        - fusion: [9q34, 22q11.2]
    review:
      reviewed: false                   # true once a person checked it against the source
      reviewer: ""                      # required when reviewed is true
      date: ""                          # YYYY-MM-DD, required when reviewed is true
      note: ""
```

| Field | What to write |
|---|---|
| `condition.name` | The condition's name as the source gives it. `ids`: codes in `orphanet` (`ORPHA:870`), `icd10`, `icdo3`, `mondo` or `other`. |
| `context` | Where the association holds: `constitutional` (born with it), `neoplastic` (acquired in a tumour), or `any`. |
| `strength` | `characteristic`: the hallmark abnormality of the condition. `recurrent`: found again and again, but not the hallmark. `reported`: described, less common. |
| `defining` | Only when a classification makes the abnormality a defining criterion of the condition. Kept for a later, separately decided diagnosis mode (T-112); not shown as a diagnosis. |
| `source`, `citation` | Where the fact comes from: the classification and its entry/section, or the dataset and its release. **In your own words; never copy text** from the classification. |

## What a rule matches (`match`)

Exactly one of these per level:

| Key | Matches | Example |
|---|---|---|
| `exact` | One abnormality as written in its short form. Band-level tolerant: a rule band `q11.2` also matches a written `q11.21` (a finer band inside it). | `exact: t(15;17)(q24;q21)`, `exact: "+21"` (quote a leading `+`/`-`) |
| `fusion` | Two bands joined by any rearrangement (translocation, insertion, derivative, ish `con` …), whatever the notation. | `fusion: [8q22, 21q22]` |
| `copy` | A gain or loss that overlaps a region (GRCh38 positions), from a karyotype, microarray or in situ result. `band` is optional and is checked against the positions. | `copy: {effect: loss, chrom: "22", grch38: [18912231, 21465672], band: 22q11.21}` |
| `any` | At least one of the listed parts. | |
| `all` | Every listed part. | |
| `not` | The part must be absent. Use it only inside `all` (a rule of `not` alone is rejected). | `all: [{exact: "+8"}, {not: {exact: "-7"}}]` |

Bands are full names (`9q34`, `16p13.1`) and must exist in the band data of every edition; `pter`, `qter`, `cen`, `p10` and `q10` in an `exact` form are not checked as bands.

## Sources and licences (decision T-104)

- **Neoplastic** rules: typed in from WHO-HAEM5 and ICC 2022 as facts, cited by entry, no copied text.
- **Constitutional** rules: ClinGen dosage-sensitivity regions (CC0) with Orphanet names and IDs (CC BY 4.0; attribution "Orphanet, www.orpha.net"); a pre-fill command is planned (T-111).
- Not used: OMIM (licensed); the Mitelman database until its licence is checked (T-113).
- The seed rules were typed in by hand (T-108) as a starting point; all are `reviewed: false`.

## Reviewing a rule

1. Find the rule in `build/review/associations-review.md` (after `kb build`).
2. Check the condition, context, strength, bands/positions and citation against the source; fix what differs.
3. Set `reviewed: true`, fill in `reviewer` and `date`, and say in `note` what you checked if useful.

A reviewed rule with a conflict (positions outside the band it names) fails the build; an unreviewed one only warns.

## After editing

Run `uv run --directory tools kb check curated/associations` while you edit, then `uv run --directory tools kb build` and `dotnet build` so the app embeds the rules.
