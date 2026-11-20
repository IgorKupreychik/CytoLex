# Batch sample files

Reusable inputs for batch validation (T-038), the CytoGPS comparison (T-051) and manual testing of the desktop app's **Batch** tab. One folder per ISCN edition (`2016/`).

The files are **generated** from the KB examples by `uv run --directory tools kb samples [--version 2016]` (it reads `build/kb.json`, so run `kb build` first). Don't edit them by hand. Regenerate them after KB edits that change examples. Every string traces to a KB rule and its citation. Book fragments (`inv(2)`, strings with `…`) are left out.

| File | Content |
|---|---|
| `karyotype.txt`, `arr.txt`, `combined.txt` | One unique string per line, by notation type (`combined` = several segments joined with `.`, e.g. karyotype + `ish`/`nuc ish`/`arr`; it exercises segments not implemented in the pilot). A group with no examples gets no file (no ish-only examples before T-015/T-016). |
| `all.csv`, `all.tsv` | One row per KB example and `corrected` form. Columns: `id,iscn,type,kb_rule,kb_kind,cite`. |
| `large.csv` | The rows of `all.csv` cycled to 1,200 rows (IDs `E0001`…), for the 1,000+ batch run. |

Columns:
- `type`: the segment types joined with `+` (`karyotype`, `arr`, `karyotype+nuc_ish`, …). A test checks it against `Segmenter`.
- `kb_rule`, `kb_kind`: the string is a `valid` or `invalid` example, or the `corrected` form, **of that rule**. It's a label for one rule, not an overall verdict: a valid example of one rule can break another, and the same string can appear under several rules.
- `cite`: the example's citation (or its rule's).

The batch reader takes the string from the `iscn` column and the ID from `id`; it ignores the other columns.

**These files are not the reviewed corpus.** T-062 adds realistic, hand-labelled strings with expected results per edition and a mutation generator, in its own files.
