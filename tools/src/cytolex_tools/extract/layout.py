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
UNDERLINE = "\u0332"  # COMBINING LOW LINE, appended to each underlined character
RULE_HEIGHT = 1.5  # pt; a drawn line/rect thinner than this is a rule
UNDERLINE_GAP = (-3.0, -1.0)  # rule top - glyph bottom (pt): an underline cuts the descender zone
UNDERLINE_COVER = 0.9  # the glyphs above an underline cover at least this share of its width
MARGIN_BAR_MIN_HEIGHT = 4.0  # pt; change bars in the outer margin (ISCN 2016 §1.6)


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


def _rules(page: Page) -> list[dict]:
    return page.objects.get("line", []) + page.objects.get("rect", [])


def underlined_chars(page: Page) -> set[tuple[float, float, str]]:
    """Characters printed with an underline (ISCN uses it to tell homologues apart).

    Underlines are thin drawn rules that cut through the glyphs' descender zone and span exactly
    the glyphs above them; table rules sit lower and run past the text, so they don't qualify.
    """
    found: set[tuple[float, float, str]] = set()
    chars = [c for c in page.chars if c["text"].strip()]
    for r in _rules(page):
        width = r["x1"] - r["x0"]
        if abs(r["bottom"] - r["top"]) >= RULE_HEIGHT or width <= 2:
            continue
        above = [
            c
            for c in chars
            if r["x0"] - 1 <= (c["x0"] + c["x1"]) / 2 <= r["x1"] + 1
            and UNDERLINE_GAP[0] <= r["top"] - c["bottom"] <= UNDERLINE_GAP[1]
        ]
        if above and sum(c["x1"] - c["x0"] for c in above) / width >= UNDERLINE_COVER:
            found.update(_char_key(c) for c in above)
    return found


def margin_bars(page: Page, text_right: float) -> list[tuple[float, float]]:
    """Vertical spans (top, bottom) of change bars drawn right of the text block."""
    return sorted(
        (r["top"], r["bottom"])
        for r in _rules(page)
        if r["x1"] - r["x0"] < RULE_HEIGHT
        and r["x0"] > text_right
        and r["bottom"] - r["top"] >= MARGIN_BAR_MIN_HEIGHT
    )


@dataclass(frozen=True)
class Word:
    x0: float
    text: str  # after glyph mapping, superscript and underline marking


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
    words: tuple[Word, ...] = ()
    changed: bool = False  # next to a margin change bar (changed since the previous edition)
    underlined: int = 0  # number of underlined characters
    table: str | None = None  # set on a synthetic line that carries a rendered table


def page_lines(
    page: Page, pdf_page: int, ed: Edition, mapper: GlyphMapper
) -> tuple[list[Line], bool]:
    """Return the page's lines in reading order, and whether it carried figure-label glyphs."""
    book_page = pdf_page - ed.page_offset
    has_figure = any(font_base(c["fontname"]) in ed.figure_fonts for c in page.chars)

    mapper.reanchored += reanchor_misplaced(page.chars)
    phantoms = phantom_spaces(page.chars)
    mapper.phantom_spaces += len(phantoms)
    underlined = underlined_chars(page)
    bars = margin_bars(page, ed.text_right)

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
        line_words: list[Word] = []
        n_under = 0
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
                if _char_key(c) in underlined:
                    ch = "".join(x + UNDERLINE for x in ch)
                    n_under += 1
                text += ch
            line_words.append(Word(round(w["x0"], 2), text))
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
        top = min(w["top"] for w in cluster)
        low = max(w["bottom"] for w in cluster)
        lines.append(
            Line(
                pdf_page=pdf_page,
                book_page=book_page,
                top=round(top, 2),
                x0=round(cluster[0]["x0"], 2),
                text=text,
                font=font,
                size=size,
                col_x=col_x,
                words=tuple(line_words),
                changed=any(b_top < low and b_bot > top for b_top, b_bot in bars),
                underlined=n_under,
            )
        )
    lines.sort(key=lambda ln: (ln.top, ln.x0))
    return lines, has_figure
