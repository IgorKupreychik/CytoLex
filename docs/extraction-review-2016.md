# ISCN 2016 — extraction review (T-003)

Review of the source-text chunks written by `kb extract` (`kb/source-text/2016/`, extractor
version 2) against the printed pages of the ISCN 2016 PDF. Date: 2026-11-16.

**Use this file when writing KB rules (T-008+):** before writing a rule from a section listed
under [Sections that need `needs_review`](#sections-that-need-needs_review), check the printed
page — something in it is not in the text layer.

## Method

Two independent channels, plus a person:

1. **Text layer (source of characters).** pdfplumber gives every printed character exactly
   (after the font-aware glyph map). Layout problems — lost columns, floats in mid-sentence,
   underlines, change bars — were found by probing the PDF's drawn objects (rules, rects,
   curves, images) and the word positions on each page.
2. **Page images (verification only).** Pages were rendered to PNG (pdfplumber, deterministic)
   and compared by eye with the extracted chunks. Images never supply characters.
3. **Person.** Tables are signed off by a person against the printed book
   (`review.human` in `tools/src/cytolex_tools/extract/tables/2016.yaml`). Run
   `uv run --directory tools kb tables` to get one PNG per table region plus an index that
   names the chunk to compare it with (`build/review/tables/2016/`).

A table counts as verified only when the text layer, the image and the person agree.

## What changed in the extractor (version 1 → 2)

| Problem found | Fix | Where in the code |
|---|---|---|
| Underlining (tells homologues apart) was lost | Drawn underlines are detected (a thin rule 1–3 pt above the glyph bottom whose width the glyphs cover ≥ 90 %); each underlined character is followed by U+0332 COMBINING LOW LINE, e.g. `der(1̲)` | `layout.underlined_chars` |
| Margin change bars were lost | Vertical bars right of the text block are detected; front matter `changed` lists the labels beside a bar. ISCN 2016 §1.6 (p.6): "Changes in the main text compared to the previous edition would be marked in the margin" | `layout.margin_bars` |
| Figure/table captions were numbered as `¶n` (shifting every later ¶) or glued to an example note (Fig. 7) | A caption line always starts a block; captions are labelled `[Fig. N p.X]` / `[Table N p.X]` and not counted as ¶. Footnotes and table bodies after a caption belong to it | `blocks.caption_id`, `chunker.section_units` |
| Tables had scrambled columns | Per-table column layout in `tables/2016.yaml`; words go to columns by x position; output is a Markdown table. Tables without a caption get a name label (`[Ploidy table p.90]`) | `tables.py` |
| Paragraphs split by a float or a page footnote in mid-sentence (7 cases, e.g. `ad-` / Table 1 / `ditional digits`) | Rejoined when prose resumes in lower case after the float and the paragraph before it stopped mid-sentence; the float follows the paragraph | `blocks.rejoin_interrupted` |
| §16.2 p.126: a wrapped line with a hanging indent started a new ¶ | An indented line that continues a sentence in lower case continues the paragraph | `blocks.build_blocks` |
| FISH signal legends (`P = probe for ABL1`) were ¶ with wrong glyphs | `GeneralSymbolsPE` `P`/`S` → `●`/`○` (filled/open signal); legend lines belong to the example above (`> ● = probe for ABL1`) | `glyphs.py`, `chunker.LEGEND` |
| Example lists with a description column were prose when the notation was long (§7.1.2, §7.2, §10.1.2) | Notation start also accepts `symbol(…)(…) Description`; ` \| ` is placed at the description column on every line of an example | `blocks.NOTATION_START`, `chunker.example_lines` |
| Table footnotes merged into one line | Each `^a …` footnote keeps its own line | `chunker.render_lines` |
| Uncertain / unmapped glyphs | `MathTechnicalPA ^` → `≤` verified (p.90 `≤34`, p.100 `≤1 kb`); `GeneralSymbolsPB s` → `→` (p.106 `pter→qter`) | `glyphs.py` |

Chunk front matter now also has `floats`, `changed`, `underlined` and `tables` (review status);
the manifest lists the tables and documents the conventions.

## Findings by category

### Underlining (homologue distinction) — recovered

Every drawn underline in ch. 1–16 was found (no others exist in the PDF: all short thin rules
were checked). The ch. 3 symbol list confirms the scope: "underlining (single) — Used to
distinguish homologous chromosomes (4.1, 9.2.3, 9.2.17.1)".

| § | Label | Page | What is underlined |
|---|---|---|---|
| 7.2 | E1 | 53 | `fra(1̲0̲)(q25.2)` — fragile sites on different homologues |
| 9.2.3 | ¶13 | 64 | the words "single underlining" (demonstration) |
| 9.2.3 | E32, E33, E34 | 64–65 | `der(1̲)t(1̲;3)`, `der(1̲)t(1;1̲)`, `der(1)t(1;1̲)` |
| 9.2.17.1 | ¶1 | 75 | the words "single underlining" (demonstration) |
| 9.2.17.1 | E8, E10 | 76 | `t(2;7;7̲)…`, `t(3;9;9̲;22)…` (short and detailed forms) |
| 16.2 | ¶3 | 125 | the word "must" (emphasis) |
| 16.3.7 | E2 | 129 | `t(9;9̲)(9̲qter→…)` |

§4.1 (named in the symbol list) states the rule but has no underlined example.
How the validator should accept underlined input is T-042.

### Margin change bars (changed since ISCN 2013) — recovered

Labels beside a change bar (front matter `changed`):

| § | Labels (page) |
|---|---|
| 2.4 | Fig. 4 (13) |
| 3 | ¶2 (34), ¶3 (35), ¶4 (36) — rows: add, cen, colon double (::), curly braces, del, der, dip, dis, dup, GRCh, h, hmz, inh, ins, inv, period, r, rec, sce, sdl, seq, t, underscore |
| 4.1 | Fig. 7, ¶12, ¶13 (39) |
| 4.2 | ¶2, ¶3 (40) |
| 4.3 | ¶2 (41) |
| 5.4 | ¶1 (48) |
| 9.2.2 | ¶1 (60) |
| 9.2.3 | ¶3 (61) |
| 9.2.5 | ¶1 (67) |
| 9.2.9 | ¶1 (68) |
| 9.2.13 | ¶2 (72) — `NEO.FORMAT` (T-013): the text is complete in the chunk; no rule depends on what changed |
| 11.1.1 | ¶1 (84) |
| 13.2 | ¶1, ¶2 (101) |
| 13.3.1 | ¶1 (106) |
| 13.3.2 | ¶2 (109) |
| 13.8 | ¶1 (114) |
| 14.1 | ¶2, ¶3 (115) |
| 14.2 | ¶1, E1, ¶3 (116), ¶4 (117), ¶5 (118) |
| 15.1 | ¶1 (123) |

In ch. 3 a bar marks single rows, but the label is per page; the row list above is read from the
bar positions. These are the places where ISCN 2016 differs from 2013 — useful for T-045/T-046
(version profiles).

### Tables

All configured tables: text layer and image agree cell for cell (`review.image: ok`).
Person check (`review.human`): `ok` for Table 1, Table 2, Table 3, Table 4 and Symbols (Table 3 on 2026-11-16; Table 2 and Symbols on 2026-11-17, T-008; Table 4 on 2026-11-17, T-009; Table 1 on 2026-11-17, T-010; Ploidy table on 2026-11-20, T-014); pending for the rest.

| Table | § (chunk) | Page | Shape | Notes |
|---|---|---|---|---|
| Table 1 heteromorphisms | 2.3.2 | 12 | 2 parts, 11 + 15 columns | stacked cell entries joined with `<br>`; "Chromosome" spanning header merged into the first number column (`Chromosome 1`, `Chromosome 11`) |
| Table 2 banding codes | 2.4 | 14 | 2 columns, 22 rows | no header row in the book |
| Table 3 landmarks | 2.5 | 15 | 4 columns, 28 rows | two-level header joined (`Chromosome Number`, `Arm`) |
| Table 4 unbalanced gametes | 4.3.2.2 | 44 | 4 columns, 16 rows | segregation pattern given on its first row only, as printed |
| Ploidy table | 11.1.6 | 90 | 4 columns, 12 rows | no caption; §11.2 refers to it ("given above"). **Indentation (sub-levels) and bold (near-x levels) are lost** — the names carry the hierarchy (Near-/Hypo-/Hyper-) |
| Symbols | 3 | 34–36 | 2 columns, 131 rows, one ¶ per page | was missing ` \| ` on wide rows; wrapped descriptions joined |
| Aberration groups | 10.3 | 83 | 2 columns, 3 rows | was a ¶ of run-together words |
| Alignment figure | 16.2 | 126 | 4 columns, 6 rows | **shading (chr2 light, chrX dark), bold (reference) and italic (alternative alignment) carry meaning and are lost** |

### Figures and diagrams

| Item | § | Page | Status |
|---|---|---|---|
| Fig. 1–4 karyograms, Fig. 6 G/R-banded chromosomes | 2.2–2.5 | 9–13, 30–33 | images; captions kept |
| Fig. 5 idiograms | 2.5 | 16–29 | drawings; their band labels (figure font, private-use glyphs) are dropped from the chunks and decoded separately by `kb ideograms` into `kb/bands/2016/ideograms.yaml` (T-007; spot-check of chr1, 9, 22, X signed off 2026-11-17) |
| Fig. 7 endoreduplication | 4.1 | 39 | image; caption kept |
| Fig. 8 pachytene diagram, Fig. 9 inversion loop | 4.3.2, 4.4 | 43, 45 | images; the text refers to their labels (A–D telomeres) |
| Fig. 10 chromomere idiogram | 12.1.2 | 96–98 | image; "continued" note attached |
| Nucleus diagrams (signal positions) | 13.3.2, 13.3.2.4 | 109–112 | drawn; only the legends (`● = probe for …`) are text |
| Fibre-FISH legend and diagram | 13.4 | 112 | drawn symbols (bar, dots, `xxxx`) lost; labels kept as `[Fibre diagram p.112]` (`> = D15S11`, …, `> pter \| qter`) |

### Glyphs and characters

- `≤` (p.90, 100) and `→` from `GeneralSymbolsPB s` (p.106) verified against the page images.
  No unmapped symbol glyphs remain.
- The en dash `–` (minus, e.g. `–5`, `arr(1–22)`) and `×` (copy number) are the printed
  characters. Whether the validator also accepts `-` and `x` is an open decision → **T-053**.
  Note that the symbol list itself writes the arrow as "→ or ->".
- Bold and italic are not kept anywhere. Except for the §16.2 alignment figure this only loses
  emphasis (gene symbols in italic, key terms in bold).

### Paragraphs and examples

- 7 paragraphs interrupted by a float or footnote were rejoined (pp. 2, 10, 12, 13, 14, 15, 44);
  §16.2 ¶14/¶15 (p.126) rejoined.
- No example is split across a page break: every example ending a page has its own
  explanation, and the next page starts a new example.
- **Long karyotypes wrap onto a second line** inside one example block, e.g. §11.1.5 E1 p.88
  (`…der(18)t(18;20)` / `(q23;q11.1),+21,+21,+22[cp24]`). One block may also hold several
  separate strings, one per line. KB authors must join wrapped lines: a line that ends inside
  parentheses, or after `,` or `/`, continues on the next.
- §11.1.5 ¶4 p.88 ("then the composite karyotype will be") correctly starts in lower case: the
  sentence continues after an example list.
- §12.1 p.93: the terminology list (`spm | Spermatogonial metaphase`, …) is labelled as an
  example (E1). Harmless; re-check when writing ch. 12 rules (T-018).
- §1.5 p.5: the date line "May 2012" is labelled as an example (ch. 1 is history, not used for
  rules).

## Sections that need `needs_review`

Rules citing these places must set `needs_review` (or say in the rule how the lost layout was
checked):

| § | Page | Reason |
|---|---|---|
| 4.3.2, 4.3.2.2, 4.4 | 43–45 | the Fig. 8/9 diagrams the text depends on (Table 4, p.44: signed off 2026-11-17). **Checked in T-009 (2026-11-17):** the printed figures add no rule content beyond the text, so the ch. 4 rules say so in `review.note` and keep `needs_review: false` |
| 7.2, 9.2.3, 9.2.17.1, 16.3.7 | 53, 61–65, 75–76, 129 | underlined homologues (U+0332 in the source text; validator behaviour is T-042). §7.2: `FRA.HOMOLOGUE_UNDERLINING` (T-010) and §9.2.3: `DER.HOMOLOGUE_UNDERLINING` (T-012) carry `needs_review`; the other §9.2.3 rules cite no underlined block. §9.2.17.1: `TRANSLOC.HOMOLOGUE_UNDERLINING` (T-013) holds E8/E10 and carries `needs_review`; `TRANSLOC.FORMAT`, `STRUCT.CHROMOSOME_ORDER` and `STRUCT.MULTI_CHROMOSOME_ORDER` cite ¶1, whose underlining only demonstrates the words "single underlining", and use no underlined example |
| 10.3 | 83 | small table — until `review.human: ok` |
| 11.1.6 / 11.2 (Ploidy table) | 90 | table; indentation and bold lost. **Signed off 2026-11-20 (T-014):** all 48 cells match; the lost hierarchy is carried by the names (Near-/Hypo-/Hyper-), so rules that encode it from the names need no `needs_review` for this. Encoded in `PLOIDY.LEVELS` (T-014, `needs_review: false`) |
| 13.3.2, 13.3.2.4 | 109–112 | signal positions shown only in drawings |
| 13.4 | 112 | fibre diagram symbols lost |
| 16.2 | 125–126 | alignment figure: shading, bold and italic carry meaning |

## Backup plan for tables

Used when a table's channels disagree on more than ~5 % of cells, or when its column layout
can't separate the cells cleanly. Recorded per table as `review.fallback` in
`tables/2016.yaml`. **Not needed so far: every table matched on the first pass.**

- **B1 — third channel.** Extract the page with `pdftotext -layout` (Poppler, mingw64). If it
  agrees with one of the two channels, take that reading and note it.
- **B2 — manual transcription, machine-checked.** Transcribe the table (person, or Claude from
  the rendered image) into a committed file `kb/source-tables/2016/<table>.yaml`, marked
  `source: manual` with `verified_by`. Check it automatically: every token of every cell must
  occur among the words of that page's text layer, which catches misreadings.
- **B3 — no source table.** Keep only the caption with `needs_review`; the KB task that needs
  the table (T-007, T-008, …) cites the page and transcribes only the rows it uses.

Underlining needs no fallback (detected directly). If a later edition draws underlines
differently, list the affected sections by searching for "homolog"/"underlin" and check them on
the page images.

## Person sign-off checklist

1. `uv run --directory tools kb extract --version 2016` (if not run since the last change).
2. `uv run --directory tools kb tables` → open `build/review/tables/2016/index.md`.
3. For each table, compare the image with the Markdown table in the named chunk.
4. Set `review.human: ok` in `tools/src/cytolex_tools/extract/tables/2016.yaml` (or `mismatch`
   plus a note, and apply the backup plan), then rerun `kb extract`.
