"""PDF page -> text lines with font, size and position (pdfplumber)."""

from collections import Counter
from dataclasses import dataclass

from pdfplumber.page import Page
from pdfplumber.utils import cluster_objects

from .editions import Edition
from .glyphs import GlyphMapper, font_base

COLUMN_GAP = 15.0  # horizontal gap (pt) inside a line that separates table columns
PHANTOM_GAP = 0.8  # pt; a real word space leaves >= ~1.5 pt between the glyphs around it
SUPERSCRIPT_RATIO = 0.8  # chars smaller than this x the line's size, and raised, are superscripts
LIGATURES = {"ﬁ": "fi", "ﬂ": "fl", "ﬀ": "ff", "ﬃ": "ffi", "ﬄ": "ffl"}


def _char_key(c: dict) -> tuple[float, float, str]:
    return (round(c["x0"], 3), round(c["top"], 3), c["text"])


def phantom_spaces(chars: list[dict]) -> set[tuple[float, float, str]]:
    """Space chars with no visible width in print.

    The PDF's text layer contains spaces that take up no room on the page (the next glyph
    starts where the previous one ends, or overlaps it), e.g. `t(8; 14)` or `in volving`.
    They are invisible in the book, so dropping them reproduces the printed text.
    """
    found = set()
    i = 1
    while i < len(chars) - 1:
        if chars[i]["text"] != " ":
            i += 1
            continue
        j = i
        while j < len(chars) and chars[j]["text"] == " ":
            j += 1  # chars[i:j] is a run of spaces
        if j < len(chars):
            prev, nxt = chars[i - 1], chars[j]
            same_line = abs(prev["top"] - nxt["top"]) < 2
            if same_line and nxt["x0"] - prev["x1"] < PHANTOM_GAP:
                found.update(_char_key(c) for c in chars[i:j])
        i = j
    return found


def reanchor_misplaced(chars: list[dict]) -> int:
    """Move glyphs with broken vertical metrics back onto their line; return how many.

    Some fonts (Cambria `′` in ch. 16) report a position a line or two below where the glyph is
    printed. When both content-stream neighbours sit on one line and the glyph is more than its
    own height away from it, it takes the neighbours' vertical position.
    """
    moved = 0
    for prev, c, nxt in zip(chars, chars[1:], chars[2:], strict=False):
        if c["text"].isspace():
            continue  # spaces don't form words; phantom-space detection handles them
        if abs(prev["top"] - nxt["top"]) < 2 and abs(c["top"] - prev["top"]) > c["size"]:
            for key in ("top", "bottom", "doctop", "y0", "y1"):
                c[key] = prev[key]
            moved += 1
    return moved


@dataclass(frozen=True)
class Line:
    pdf_page: int
    book_page: int
    top: float
    x0: float
    text: str
    font: str  # dominant font (base name)
    size: float  # dominant size, rounded to 0.1 pt
    col_x: float | None = None  # x0 of the second column, when the line has a column gap


def page_lines(
    page: Page, pdf_page: int, ed: Edition, mapper: GlyphMapper
) -> tuple[list[Line], bool]:
    """Return the page's lines in reading order, and whether it carried figure-label glyphs."""
    book_page = pdf_page - ed.page_offset
    has_figure = any(font_base(c["fontname"]) in ed.figure_fonts for c in page.chars)

    mapper.reanchored += reanchor_misplaced(page.chars)
    phantoms = phantom_spaces(page.chars)
    mapper.phantom_spaces += len(phantoms)

    def keep(obj: dict) -> bool:
        if obj.get("object_type") != "char":
            return True
        if _char_key(obj) in phantoms:
            return False
        return font_base(obj["fontname"]) not in ed.figure_fonts and obj["top"] < ed.footer_top

    words = page.filter(keep).extract_words(return_chars=True)
    lines: list[Line] = []
    for cluster in cluster_objects(words, "top", 3):
        cluster = sorted(cluster, key=lambda w: (w["x0"], w["top"]))
        styles: Counter[tuple[str, float]] = Counter(
            (font_base(c["fontname"]), round(c["size"], 1)) for w in cluster for c in w["chars"]
        )
        if not styles:
            continue
        (font, size), _ = styles.most_common(1)[0]
        bottom = Counter(
            round(c["bottom"]) for w in cluster for c in w["chars"] if round(c["size"], 1) == size
        ).most_common(1)[0][0]
        parts: list[str] = []
        prev_x1 = None
        col_x = None
        for w in cluster:
            text = ""
            in_sup = False
            for c in w["chars"]:
                ch = LIGATURES.get(c["text"], c["text"])
                ch = mapper.map(ch, c["fontname"], book_page)
                # superscript (e.g. footnote marker): smaller and raised above the baseline
                sup = c["size"] < size * SUPERSCRIPT_RATIO and c["bottom"] < bottom - 1
                if sup and not in_sup:
                    text += "^"
                in_sup = sup
                text += ch
            if prev_x1 is not None:
                is_col = w["x0"] - prev_x1 > COLUMN_GAP
                parts.append(" | " if is_col else " ")
                if is_col and col_x is None:
                    col_x = round(w["x0"], 2)
            parts.append(text)
            prev_x1 = w["x1"]
        text = "".join(parts).strip()
        if not text:
            continue
        lines.append(
            Line(
                pdf_page=pdf_page,
                book_page=book_page,
                top=round(min(w["top"] for w in cluster), 2),
                x0=round(cluster[0]["x0"], 2),
                text=text,
                font=font,
                size=size,
                col_x=col_x,
            )
        )
    lines.sort(key=lambda ln: (ln.top, ln.x0))
    return lines, has_figure
