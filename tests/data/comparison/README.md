# Comparison triage files

Triage labels for the comparison of CytoLex with an external checker (T-051, `src/CytoLex.External/Comparison/`). One file per checker and ISCN edition: `cytogps-2016-triage.yaml`.

**Data policy (T-048):** book or synthetic strings only. Never add real patient karyotypes.

## Format

```yaml
format: cytolex-triage
version: 1
checker: "CytoGPS"
edition: "2016"
entries:
  - iscn: "47,XY,+21,del(5)(q13q33)"    # the string exactly as in the batch (the key; no duplicates)
    label: checker-limitation           # our-bug | checker-limitation | version-difference | severity-policy
    confirmed: true                     # a person checked the label
    note: "CytoGPS doesn't check the order of abnormalities"
    expected: invalid                   # optional, with kb_rule: exported as a KB example draft
    kb_rule: KARYO.ABNORMALITY_ORDER
```

Labels:
- `our-bug`: CytoLex is wrong. Add a `todo` task in `TASKS.md` for the fix; give `expected` + `kb_rule` so the string becomes a KB example once the fix lands.
- `checker-limitation`: the checker doesn't check, or can't read, what the book prescribes (e.g. CytoGPS ignores the order of abnormalities, spaces and letter case; `docs/research/cytogps.md` §5.2).
- `version-difference`: the two follow different editions or readings of the edition.
- `severity-policy`: CytoLex reports the fault, but as a warning or info under its severity policy (decisions T-022/T-056), so its verdict is valid while the checker rejects the string. The report lists CytoLex's warnings/infos next to each checker-only rejection and suggests this label when there are any. Related: T-075, T-080.

The loader rejects unknown keys or labels, a duplicate `iscn`, and `expected` without `kb_rule` (or the reverse), and the error names the line.

## Updating

1. Run the comparison report (needs the local CytoGPS build; README → "CytoGPS (optional)"):
   `dotnet test --project tests/CytoLex.External.Tests --filter-class "CytoLex.External.Tests.CytoGpsComparisonReportTests"`.
2. Read `build/review/comparison/cytogps-2016.md`. The untriaged disagreements are in `build/review/comparison/cytogps-2016-triage-draft.yaml`, with a suggested label (`checker-limitation` when only CytoLex rejects and every error belongs to a rule family CytoGPS doesn't check; `severity-policy` when only CytoGPS rejects and CytoLex has warnings/infos), else `TODO`.
3. Review each draft entry, set its label and `confirmed: true`, and copy it into the triage file here.
4. Entries listed as **stale** in the report no longer disagree (e.g. after a fix); remove them.
5. Confirmed entries with `expected` + `kb_rule` are written to `build/review/comparison/cytogps-2016-kb-examples-draft.yaml`. Paste them into `kb/rules/` by hand with a citation, then run `kb check` and `kb build`. Nothing edits the KB automatically.
