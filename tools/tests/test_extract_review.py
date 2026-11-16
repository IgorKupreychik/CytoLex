"""Unit tests for the T-003 extraction fixes: underlines, change bars, floats, tables, legends."""

from types import SimpleNamespace

from cytolex_tools.extract.blocks import build_blocks, build_sections, caption_id
from cytolex_tools.extract.chunker import chunk_section
from cytolex_tools.extract.editions import get_edition
from cytolex_tools.extract.glyphs import GlyphMapper
from cytolex_tools.extract.layout import Line, Word, margin_bars, underlined_chars
from cytolex_tools.extract.tables import (
    Region,
    TableRegion,
    TableSpec,
    divert_tables,
    load_tables,
    render_table,
)

ED = get_edition("2016")
BODY, HEAD = "TimesSqu-Roman", "MyriadPro-Bold"
ED9 = ED.__class__(**{**ED.__dict__, "first_chapter": 9})


def line(text, top, x0=112.0, font=BODY, size=10.0, page=81, **kw):
    return Line(page, page - 6, top, x0, text, font, size, **kw)


def char(text, x0, x1, top=100.0, size=10.0):
    return {"text": text, "x0": x0, "x1": x1, "top": top, "bottom": top + size, "size": size}


def rule(x0, x1, top, bottom):
    return {"x0": x0, "x1": x1, "top": top, "bottom": bottom}


def page(chars, rules):
    return SimpleNamespace(chars=chars, objects={"line": rules, "rect": []})


def heading_lines():
    return [
        line("9 Structural Chromosome Rearrangements", 60, x0=74, font=HEAD, size=17.0),
        line("9.1 General Principles", 80, x0=74, font=HEAD, size=11.5),
    ]


def chunk(lines, tables=None):
    section = build_sections(build_blocks(heading_lines() + lines, ED9), ED9)[1]
    [c] = chunk_section("2016", section, set(), tables=tables)
    return c


# --- glyphs ---------------------------------------------------------------------------------


def test_signal_and_arrow_glyphs():
    m = GlyphMapper()
    assert m.map("P", "X+GeneralSymbolsPE", 109) == "●"
    assert m.map("S", "X+GeneralSymbolsPE", 109) == "○"
    assert m.map("s", "X+GeneralSymbolsPB", 106) == "→"
    assert all("uncertain" not in g for g in m.report()["mapped"])


# --- layout ---------------------------------------------------------------------------------


def test_underline_under_a_digit():
    # `der(1)`: the rule cuts the descender zone of `1` (top = glyph bottom - 2) and spans it
    chars = [char("(", 100, 103.3), char("1", 103.3, 108.9), char(")", 108.9, 112.2)]
    found = underlined_chars(page(chars, [rule(103.3, 108.9, 108.0, 108.5)]))
    assert found == {(103.3, 100.0, "1")}


def test_table_rule_is_not_an_underline():
    # a table rule sits below the text (gap 5.5 pt) and runs past it
    chars = [char("9", 235, 240), char("3", 240, 245)]
    assert underlined_chars(page(chars, [rule(225, 364, 115.5, 116.0)])) == set()


def test_margin_change_bar():
    bars = margin_bars(page([], [rule(533.0, 533.5, 375, 381), rule(112, 300, 400, 400.4)]), 525)
    assert bars == [(375, 381)]


# --- blocks ---------------------------------------------------------------------------------


def test_caption_ids():
    assert caption_id("Fig. 7. Metaphase chromosomes in a cell") == "Fig. 7"
    assert caption_id("Fig. 6. (b) R-banded chromosomes arranged") == "Fig. 6b"
    assert caption_id("Table 1. Examples of heteromorphisms with various stains^a.") == "Table 1"
    assert caption_id("Table 4 gives the possible unbalanced gametes") is None
    assert caption_id("Fig. 5.") is None


def test_caption_starts_a_new_block_after_an_example_note():
    c = chunk(
        [
            line("46,XY[3]//46,XX[17]", 100),
            line("Three cells from the male recipient were identified.", 112, x0=125, size=8.2),
            line("Fig. 7. Metaphase chromosomes in a cell which has undergone", 124, size=8.2),
            line("endoreduplication.", 134, size=8.2),
            line("Endoreduplication is the replication of the chromosomes.", 160),
        ]
    )
    assert c.examples == {"E1": 75}
    assert c.floats == {"Fig. 7": 75}
    assert c.paragraphs == {"¶1": 75}  # the caption isn't counted as a paragraph
    assert "\n[Fig. 7 p.75] Fig. 7. Metaphase chromosomes in a cell which has undergone " in c.text
    assert "[¶1 p.75] Endoreduplication is" in c.text


def test_hanging_indent_mid_sentence_continues_the_paragraph():
    c = chunk(
        [
            line("To cover the joint standards for the description, existing HGVS", 100),
            line("recommendations have been extended:", 113, x0=125),
            line("However, a new paragraph starts here and the text runs on to the", 126, x0=125),
            line("margin of the page like the rest of the body text.", 139),
        ]
    )
    assert c.paragraphs == {"¶1": 75, "¶2": 75}
    assert "existing HGVS recommendations have been extended:" in c.text


def test_signal_legend_belongs_to_the_example():
    c = chunk(
        [
            line("nuc ish(ABL1,BCR)×2[400]", 100),
            line("● = probe for ABL1", 140, x0=250),
            line("○ = probe for BCR", 152, x0=250),
            line("However, if they have become juxtaposed on one chromosome.", 190),
        ]
    )
    assert c.paragraphs == {"¶1": 75}
    assert "```\n> ● = probe for ABL1\n> ○ = probe for BCR" in c.text


def test_change_bar_and_underline_marks_in_front_matter():
    c = chunk(
        [
            line("A paragraph that was changed in this edition.", 100, changed=True),
            line("46,XX,der(1̲)t(1̲;3)(p34.3;q21)", 130, underlined=2),
        ]
    )
    assert c.changed == ["¶1"]
    assert c.underlined == ["E1"]
    assert "changed: [¶1]" in c.text and "underlined: [E1]" in c.text


# --- tables ---------------------------------------------------------------------------------


def words_line(top, words, page=18):
    text = " ".join(t for _, t in words)
    return line(text, top, x0=words[0][0], page=page, words=tuple(Word(x, t) for x, t in words))


TABLE1 = Region(
    book_page=12,
    top=80,
    bottom=215,
    body_top=110,
    columns=(112, 155, 201),
    key=(0,),
    join="br",
    header="auto",
)


def table1_lines():
    return [
        words_line(83, [(112, "Technique"), (155, "Chromosome")]),
        words_line(98, [(155, "1"), (201, "2")]),
        words_line(113, [(112, "G^b"), (155, "q12"), (201, "inv(p11.2q13)")]),
        words_line(123, [(155, "inv(p13q21)")]),
        words_line(132, [(112, "C^c"), (155, "qh")]),
    ]


def test_render_table_with_wrapped_cells():
    spec = TableSpec("Table 1", "float", {"image": "pending"}, (TABLE1,))
    tr = TableRegion(spec, 0, table1_lines())
    assert render_table(tr, " ".join) == [
        "| Technique | Chromosome 1 | 2 |",
        "|---|---|---|",
        "| G^b | q12<br>inv(p13q21) | inv(p11.2q13) |",
        "| C^c | qh |  |",
    ]


def test_divert_tables_and_render_under_caption():
    spec = TableSpec("Table 1", "float", {"image": "ok", "human": "pending"}, (TABLE1,))
    caption = line("Table 1. Examples of heteromorphisms with various stains^a.", 63, page=18)
    after = line("^a cen = Centromere, h = heterochromatin.", 230, x0=112, page=18, size=8.2)
    found: dict[str, TableRegion] = {}
    lines = divert_tables([caption, *table1_lines(), after], [spec], found)
    assert [ln.table for ln in lines] == [None, "Table 1#1", None]
    c = chunk(lines, found)
    assert c.floats == {"Table 1": 12}
    assert c.paragraphs == {}
    assert "| C^c | qh |  |\n\n> ^a cen = Centromere" in c.text
    assert "tables:\n  Table 1: {image: ok, human: pending}" in c.text


def test_paragraph_table_without_header():
    region = Region(34, 340, 700, 340, (110, 199), (0,), "text", "none")
    spec = TableSpec("Symbols", "paragraph", {}, (region,))
    rows = [
        words_line(345, [(112, "approximate"), (161, "sign"), (178, "(~)"), (202, "Denotes")], 40),
        words_line(356, [(202, "fragments,"), (243, "or"), (253, "re-")], 40),
        words_line(366, [(202, "gion")], 40),
    ]
    found: dict[str, TableRegion] = {}
    intro = line("All symbols are listed below.", 300, page=40)
    lines = divert_tables([intro, *rows], [spec], found)
    assert render_table(found["Symbols#1"], " ".join)[:3] == [
        "|  |  |",
        "|---|---|",
        "| approximate sign (~) | Denotes fragments, or re- gion |",  # chunker joins hyphens
    ]
    c = chunk(lines, found)
    assert c.paragraphs == {"¶1": 34, "¶2": 34}
    assert "| approximate sign (~) | Denotes fragments, or region |" in c.text


def test_table_config_loads():
    specs = {s.id: s for s in load_tables("2016")}
    assert {"Table 1", "Table 2", "Table 3", "Table 4", "Ploidy table", "Symbols"} <= set(specs)
    assert len(specs["Table 1"].regions) == 2
    assert specs["Symbols"].kind == "paragraph"


def test_paragraph_interrupted_by_a_table_is_rejoined():
    region = Region(75, 120, 140, 120, (112, 200), (0,), "text", "none")
    spec = TableSpec("Table 9", "float", {}, (region,))
    lines = [
        line("If a sub-band is subdivided, ad-", 100),
        line("Table 9. Codes used in this example.", 110, size=8.2),
        line("x", 125, words=(Word(112, "Q"), Word(200, "Q-bands"))),
        line("ditional digits are used.", 160),
    ]
    found: dict[str, TableRegion] = {}
    c = chunk(divert_tables(lines, [spec], found), found)
    assert c.paragraphs == {"¶1": 75}
    assert c.floats == {"Table 9": 75}
    body = c.text.split("---\n", 2)[2]
    assert body.index("[¶1 p.75] If a sub-band is subdivided, additional digits") < body.index(
        "[Table 9"
    )


def test_example_with_description_column():
    w = lambda *ws: tuple(Word(x, t) for x, t in ws)  # noqa: E731
    c = chunk(
        [
            line(
                "fra(10)(q25.2) | A fragile site.",
                100,
                col_x=262.0,
                words=w((112, "fra(10)(q25.2)"), (262, "A"), (270, "fragile"), (300, "site.")),
            ),
            line(
                "fra(10)(q22.1),fra(10)(q25.2) Two fragile sites.",
                112,
                words=w((112, "fra(10)(q22.1),fra(10)(q25.2)"), (262, "Two"), (280, "fragile")),
            ),
        ]
    )
    assert c.examples == {"E1": 75}
    assert "\nfra(10)(q22.1),fra(10)(q25.2) | Two fragile\n" in c.text
