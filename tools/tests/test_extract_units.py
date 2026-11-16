"""Unit tests for the extractor (no PDF needed)."""

from cytolex_tools.extract.blocks import (
    Section,
    build_blocks,
    build_sections,
    is_notation_line,
    join_lines,
)
from cytolex_tools.extract.chunker import chunk_path, chunk_section, estimate_tokens, slug
from cytolex_tools.extract.editions import get_edition
from cytolex_tools.extract.glyphs import GlyphMapper
from cytolex_tools.extract.layout import Line, phantom_spaces, reanchor_misplaced
from cytolex_tools.extract.toc import parse_toc

ED = get_edition("2016")
BODY, SMALL, HEAD = "TimesSqu-Roman", "TimesSqu-Roman", "MyriadPro-Bold"


def line(text, top, x0=112.0, font=BODY, size=10.0, page=81, col_x=None):
    return Line(page, page - 6, top, x0, text, font, size, col_x)


# --- glyphs ---------------------------------------------------------------------------------


def test_arrow_depends_on_font():
    m = GlyphMapper()
    assert m.map("]", "ABCDEF+GeneralSymbolsPB", 75) == "→"
    assert m.map("]", "ABCDEF+TimesSqu-Roman", 75) == "]"  # a real bracket, as in [25]


def test_symbol_font_glyphs():
    m = GlyphMapper()
    assert m.map("~", "X+MathTechnicalPD", 1) == "×"
    assert m.map("~", "X+TimesSqu-Roman", 1) == "~"
    assert m.map(",", "X+MathematicalPi-Three", 1) == "~"
    assert m.map("!", "X+MathTechnicalPA", 1) + "4n" + m.map("1", "X+MathTechnicalPA", 1) == "<4n>"


def test_unmapped_symbol_glyph_is_reported():
    m = GlyphMapper()
    assert m.map("Q", "X+MathTechnicalPA", 54) == "Q"
    report = m.report()
    assert report["unmapped"] == [
        {"font": "MathTechnicalPA", "char": "U+0051 'Q'", "book_pages": [54]}
    ]


# --- layout ---------------------------------------------------------------------------------


def ch(text, x0, x1, top=100.0, size=10.0):
    return {
        "text": text,
        "x0": x0,
        "x1": x1,
        "top": top,
        "bottom": top + size,
        "size": size,
        "doctop": top,
        "y0": 700 - top - size,
        "y1": 700 - top,
    }


def test_zero_width_space_is_phantom():
    # `)` ends at 174.1, `(` starts at 174.5: the space between them is invisible in print
    chars = [ch(")", 170.7, 174.1), ch(" ", 174.1, 176.9), ch("(", 174.5, 177.9)]
    assert len(phantom_spaces(chars)) == 1


def test_real_space_is_kept():
    chars = [ch("a", 100.0, 105.0), ch(" ", 105.0, 107.8), ch("b", 107.8, 112.8)]
    assert phantom_spaces(chars) == set()


def test_run_of_phantom_spaces():
    chars = [ch(")", 0, 4.0), ch(" ", 4.0, 6.8), ch(" ", 4.1, 6.9), ch("×", 2.0, 7.0)]
    assert len(phantom_spaces(chars)) == 2


def test_misplaced_glyph_is_reanchored():
    chars = [ch("3", 10, 15, top=103.4), ch("′", 15, 17, top=125.0), ch("x", 18, 23, top=103.4)]
    assert reanchor_misplaced(chars) == 1
    assert chars[1]["top"] == 103.4


# --- TOC ------------------------------------------------------------------------------------


def test_toc_wrapped_title_and_spaced_leaders():
    toc = parse_toc(
        [
            "9.2.17 Translocations ............................ 75",
            "2.3.1 Identification and Definition of Chromosome Landmarks, Regions, and Bands. . 9",
            "4.3 Designating Structural Chromosome Aberrations by Breakpoints and",
            "Band Composition ................................ 40",
            "Contents V",
        ]
    )
    assert [(e.number, e.title, e.book_page) for e in toc] == [
        ("9.2.17", "Translocations", 75),
        ("2.3.1", "Identification and Definition of Chromosome Landmarks, Regions, and Bands", 9),
        (
            "4.3",
            "Designating Structural Chromosome Aberrations by Breakpoints and Band Composition",
            40,
        ),
    ]


# --- blocks ---------------------------------------------------------------------------------


def test_hyphen_joining():
    compounds = {"three-break"}
    assert join_lines(["a re-", "arrangement"], compounds) == "a rearrangement"
    assert join_lines(["in three-", "break rearrangements"], compounds) == (
        "in three-break rearrangements"
    )
    assert join_lines(["see Section", "9.2.1."], compounds) == "see Section 9.2.1."


def test_notation_lines():
    assert is_notation_line("46,XY,t(2;5)(q21;q31)")
    assert is_notation_line("46,XX | Normal female")
    assert is_notation_line("46,XX Normal female")
    assert is_notation_line("nuc ish(ABL1,BCR)×3(ABL1 con BCR×1)(ABL1 con BCR con ABL1×1)[400]")
    assert not is_notation_line("In translocations (t) affecting two chromosomes, the sex")
    assert not is_notation_line("Section 9.2.1.")


def sample_lines():
    return [
        line("9.2.17.1 Reciprocal Translocations", 144, x0=74, font=HEAD),
        line("In translocations (t) affecting two chromosomes, the sex chromosome is", 168),
        line("always specified first. Whenever applicable, the same rules apply.", 181),
        line("When two homologues are involved, one of the numerals may be under-", 194, x0=125),
        line("lined.", 206),
        line("Two-break rearrangements", 230, font=HEAD),
        line("46,XY,t(2;5)(q21;q31)", 254),
        line("46,XY,t(2;5)(2pter→2q21::5q31→5qter;5pter→5q31::2q21→2qter)", 266),
        line("Breakage and reunion have occurred at bands 2q21 and 5q31.", 280, x0=125, size=8.2),
        line(
            "16qh+ | Increase in length of the heterochromatin on the long arm of chro-",
            302,
            col_x=180.0,
        ),
        line("mosome 16.", 314, x0=180.0),
    ]


def test_blocks_and_sections():
    blocks = build_blocks(sample_lines(), ED)
    assert [b.kind for b in blocks] == [
        "heading",
        "prose",
        "prose",
        "subheading",
        "example",
        "note",
        "example",
    ]
    assert blocks[0].number == "9.2.17.1"
    assert len(blocks[-1].lines) == 2  # wrapped second-column text stays with its example
    sections = build_sections(blocks, ED)
    assert [s.number for s in sections] == []  # chapter 9 can't start the book


def test_chunk_labels_and_rendering():
    lines = [line("9 Structural Chromosome Rearrangements", 100, x0=74, font=HEAD, size=17.0)]
    blocks = build_blocks(lines + sample_lines(), ED)
    ed = ED.__class__(**{**ED.__dict__, "first_chapter": 9})
    sections = build_sections(blocks, ed)
    assert [s.number for s in sections] == ["9", "9.2.17.1"]
    [chunk] = chunk_section("2016", sections[1], {"under-lined"})
    assert chunk.path == "ch09/09.02.17.01-reciprocal-translocations.md"
    assert chunk.paragraphs == {"¶1": 75, "¶2": 75}
    assert chunk.examples == {"E1": 75, "E2": 75}
    text = chunk.text
    assert text.startswith("---\nversion: '2016'\nsection: 9.2.17.1\n")
    assert (
        "[¶2 p.75] When two homologues are involved, one of the numerals may be under-lined."
        in text
    )
    assert "[E1 p.75]\n```\n46,XY,t(2;5)(q21;q31)\n46,XY,t(2;5)(2pter→2q21" in text
    assert "```\n> Breakage and reunion have occurred at bands 2q21 and 5q31." in text
    assert chunk_section("2016", sections[1], {"under-lined"})[0].text == text  # deterministic


def test_chunk_split_respects_cap():
    lines = [line("9 Structural Chromosome Rearrangements", 100, x0=74, font=HEAD, size=17.0)]
    lines.append(line("9.1 General Principles", 120, x0=74, font=HEAD, size=11.5))
    top = 140.0
    for i in range(40):  # 40 paragraphs: indented first line + one margin line each
        lines.append(line(f"Paragraph {i} " + "word " * 30, top, x0=125))
        lines.append(line("word " * 30, top + 13))
        top += 26
    ed = ED.__class__(**{**ED.__dict__, "first_chapter": 9})
    section = build_sections(build_blocks(lines, ed), ed)[1]
    chunks = chunk_section("2016", section, set(), cap=1000)
    assert len(chunks) > 1
    assert all(estimate_tokens(c.text) <= 1000 for c in chunks)
    assert [c.path for c in chunks][:2] == [
        "ch09/09.01-general-principles-p1.md",
        "ch09/09.01-general-principles-p2.md",
    ]
    labels = [k for c in chunks for k in c.paragraphs]
    assert labels == [f"¶{i}" for i in range(1, 41)]  # numbering continues across parts


def test_oversized_paragraph_splits_at_sentences():
    lines = [line("9 Structural Chromosome Rearrangements", 100, x0=74, font=HEAD, size=17.0)]
    lines.append(line("9.1 General Principles", 120, x0=74, font=HEAD, size=11.5))
    lines += [
        line(f"Sentence number {i} has several words in it.", 140 + 13 * i) for i in range(400)
    ]
    ed = ED.__class__(**{**ED.__dict__, "first_chapter": 9})
    section = build_sections(build_blocks(lines, ed), ed)[1]
    chunks = chunk_section("2016", section, set(), cap=1000)
    assert len(chunks) > 1
    assert all(estimate_tokens(c.text) <= 1000 for c in chunks)
    assert chunks[0].paragraphs == {"¶1": 75} and chunks[1].paragraphs == {}
    assert "[¶1 p.75 cont.] Sentence number" in chunks[1].text


def test_slug_and_path_padding():
    assert slug("1956–1984") == "19561984"
    assert slug("Prophase/Metaphase in situ Hybridization (ish)") == (
        "prophase-metaphase-in-situ-hybridization-ish"
    )
    section = Section("13.3.2.1", "Single Fusion Probes")
    assert chunk_path(section, 1, 1) == "ch13/13.03.02.01-single-fusion-probes.md"
    assert chunk_path(section, 2, 3) == "ch13/13.03.02.01-single-fusion-probes-p2.md"
