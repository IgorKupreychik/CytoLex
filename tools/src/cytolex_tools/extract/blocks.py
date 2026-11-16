"""Lines -> blocks (headings, prose paragraphs, example strings, small-print notes) -> sections.

All rules here are layout heuristics, deterministic by construction:
- numbered heading: heading font, body size or larger, text starts with a section number;
- prose paragraph: body-size text; a new one starts at a first-line indent, a vertical gap,
  a heading, or at a page break when the previous line ended a sentence;
- example: a body-size block whose lines all look like notation (few spaces, no long words);
- note: small-print block (explanations under examples, table footnotes);
- caption: a figure/table caption (`Fig. 7. ...`, `Table 1. ...`), which always starts a block;
- table: a marker line standing for a configured table region (see tables.py).
"""

import re
from dataclasses import dataclass, field

from .editions import Edition
from .layout import Line

SECTION_NUMBER = re.compile(r"^(\d+(?:\.\d+)*)\s+(.+)$")
INDENT = 6.0  # pt beyond the page's body margin that marks a paragraph's first line
COLUMN_ALIGN = 3.0  # pt; a line starting this close to the 2nd column's x continues that column
GAP_FACTOR = 1.6  # vertical gap > size * factor starts a new block
TITLE_GAP_FACTOR = 1.6  # max distance (x size) between a heading line and its wrapped title
BULLET = "•"
TERMINAL = (".", ":", ";", ")", "]")
FOOTNOTE = re.compile(r"\^\d+")
LONG_WORD = re.compile(r"^[A-Za-z]{4,}[.,;:]?$")
# `Fig. 7. Metaphase ...`, `Fig. 6. (a) G-banded ...`, `Table 1. Examples ...`
CAPTION = re.compile(r"^(Fig\.|Table) (\d+)\.(?:\^\w+)?\s+(?:\(([a-z])\)\s+)?[A-Z]")


def caption_id(text: str) -> str | None:
    """`Fig. 6. (b) R-banded ...` -> `Fig. 6b`; None if the line isn't a caption."""
    m = CAPTION.match(plain(text))
    return f"{m.group(1)} {m.group(2)}{m.group(3) or ''}" if m else None


@dataclass
class Block:
    kind: str  # heading | subheading | prose | example | note
    lines: list[Line] = field(default_factory=list)
    number: str | None = None  # numbered headings only
    title: str | None = None  # headings only
    float_id: str | None = None  # captions only (`Fig. 7`, `Table 1`)
    column_lines: set[int] = field(default_factory=set)  # indexes of wrapped 2nd-column lines

    @property
    def book_page(self) -> int:
        return self.lines[0].book_page


@dataclass
class Section:
    number: str
    title: str
    blocks: list[Block] = field(default_factory=list)

    @property
    def chapter(self) -> int:
        return int(self.number.split(".")[0])

    @property
    def level(self) -> int:
        return self.number.count(".") + 1

    @property
    def parent(self) -> str | None:
        return self.number.rsplit(".", 1)[0] if "." in self.number else None

    @property
    def book_page(self) -> int:
        return self.blocks[0].book_page

    def pages(self) -> tuple[list[int], list[int]]:
        lines = [ln for b in self.blocks for ln in b.lines]
        return (
            [min(ln.book_page for ln in lines), max(ln.book_page for ln in lines)],
            [min(ln.pdf_page for ln in lines), max(ln.pdf_page for ln in lines)],
        )


def plain(text: str) -> str:
    """Line text without table column separators."""
    return text.replace(" | ", " ")


def heading_text(text: str) -> str:
    """Heading line text without column separators or footnote markers (`1956–1984^1`)."""
    return FOOTNOTE.sub("", plain(text)).strip()


def line_class(line: Line, ed: Edition) -> str:
    """heading | table | text | small"""
    if line.table:
        return "table"
    if ed.heading_font in line.font and line.size >= ed.body_size - 0.1:
        return "heading"
    return "text" if line.size > ed.small_size + 0.4 else "small"  # 9pt examples count as text


def is_notation_line(text: str) -> bool:
    """Notation (possibly followed by a short description, as in `46,XX Normal female`)."""
    first_column = text.split(" | ", 1)[0]
    tokens = first_column.split(" ")
    if len(tokens) <= 4 and not any(LONG_WORD.match(t) for t in tokens):
        return True
    return bool(NOTATION_START.match(first_column))


# Notation-like line start: a chromosome count followed by `,` (`46,XX`, `43~47,XX,…`), or a
# method prefix (`ish`, `nuc ish`, `arr[GRCh37]`, ...).
NOTATION_START = re.compile(
    r"^(?:\d{2,3}(?:~\d{2,3})?(?:<\d?n>)?,\S"
    r"|(?:nuc |fib |rev )?ish\b|arr\b|arr\[|rsa\b|seq\b|seq\[|cgh\b|mos |chi "
    # a bare abnormality followed by its description: `fra(10)(q25.2) A fragile site ...`
    r"|[a-z]{1,5}\([^\s()]+\)\([^\s()]+\)\S* [A-Z])"
)


def page_margins(lines: list[Line], ed: Edition) -> dict[int, float]:
    """Body-text left margin per PDF page: the most common x0 of body-size lines."""
    by_page: dict[int, dict[int, int]] = {}
    for ln in lines:
        if line_class(ln, ed) == "text":
            counts = by_page.setdefault(ln.pdf_page, {})
            counts[round(ln.x0)] = counts.get(round(ln.x0), 0) + 1
    return {p: float(max(c.items(), key=lambda kv: (kv[1], -kv[0]))[0]) for p, c in by_page.items()}


def build_blocks(lines: list[Line], ed: Edition) -> list[Block]:
    margins = page_margins(lines, ed)
    blocks: list[Block] = []
    cur: Block | None = None
    prev: Line | None = None
    for ln in lines:
        cls = line_class(ln, ed)
        same_page = prev is not None and prev.pdf_page == ln.pdf_page
        gap = ln.top - prev.top if same_page else None

        if cls == "heading":
            text = heading_text(ln.text)
            if (
                cur is not None
                and cur.kind in ("heading", "subheading")
                and same_page
                and gap is not None
                and gap < ln.size * TITLE_GAP_FACTOR
                and not SECTION_NUMBER.match(text)
                and prev is not None
                and prev.size == ln.size
            ):
                cur.lines.append(ln)  # wrapped heading title
                cur.title = f"{cur.title} {text}"
            else:
                m = SECTION_NUMBER.match(text)
                if m:
                    cur = Block("heading", [ln], number=m.group(1), title=m.group(2))
                else:
                    cur = Block("subheading", [ln], title=text)
                blocks.append(cur)
            prev = ln
            continue

        if cls == "table":
            cur = Block("table", [ln])
            blocks.append(cur)
            prev = ln
            continue

        margin = margins.get(ln.pdf_page, ln.x0)
        cur_cls = None if cur is None or cur.kind in ("heading", "subheading") else cur.kind
        bullet = ln.text.startswith(BULLET)
        in_bullet = cur_cls is not None and cur.lines[0].text.startswith(BULLET)
        # a hanging indent inside a bullet item continues it; elsewhere an indent starts a
        # paragraph, unless the previous line stopped mid-sentence and this one goes on in lower
        # case (a wrapped line set with a hanging indent, §16.2 p.126)
        mid_sentence = (
            cur_cls == cls
            and prev is not None
            and not prev.text.endswith(TERMINAL)
            and ln.text[:1].islower()
        )
        indented = cls == "text" and ln.x0 - margin > INDENT and not in_bullet and not mid_sentence
        # wrapped text of a second column (`16qh+ | Increase in length ... chro-` / `mosome 1`)
        col_x = next((x.col_x for x in reversed(cur.lines) if x.col_x), None) if cur else None
        column_cont = (
            cur_cls == cls
            and col_x is not None
            and gap is not None
            and gap <= ln.size * GAP_FACTOR
            and abs(ln.x0 - col_x) < COLUMN_ALIGN
        )
        if column_cont:
            cur.lines.append(ln)
            cur.column_lines.add(len(cur.lines) - 1)
            prev = ln
            continue
        caption = caption_id(ln.text) is not None
        new = (
            cur_cls is None
            or cur_cls != cls
            or caption
            or bullet
            or indented
            or (gap is not None and gap > ln.size * GAP_FACTOR)
            or (gap is None and prev is not None and prev.text.endswith(TERMINAL))
        )
        if new:
            cur = Block(cls, [ln])  # kind refined below: text -> prose/example, small -> note
            blocks.append(cur)
        else:
            cur.lines.append(ln)
        prev = ln

    for b in blocks:
        fid = caption_id(b.lines[0].text) if b.kind in ("text", "small") else None
        if fid:
            b.kind, b.float_id = "caption", fid
        elif b.kind == "text":
            first_column = [ln for i, ln in enumerate(b.lines) if i not in b.column_lines]
            b.kind = "example" if all(is_notation_line(ln.text) for ln in first_column) else "prose"
        elif b.kind == "small":
            b.kind = "note"
    return rejoin_interrupted(blocks)


def rejoin_interrupted(blocks: list[Block]) -> list[Block]:
    """Rejoin a paragraph that a float or footnote interrupts mid-sentence.

    A figure, table (with its caption and footnotes) or page footnote is often printed in the
    middle of a sentence (`...with ad-` / Table 1 / `ditional digits, ...`). When prose resumes in
    lower case after such a run and the paragraph before it stopped mid-sentence, the two halves
    are one paragraph; the float keeps its place after it.
    """
    out: list[Block] = []
    before: Block | None = None  # the prose block right before the current run of floats
    in_run = False
    for b in blocks:
        if b.kind in ("caption", "table") or (b.kind == "note" and (in_run or before)):
            in_run = in_run or b.kind != "note" or FOOTNOTE.match(b.lines[0].text) is not None
            if in_run:
                out.append(b)
                continue
        if (
            in_run
            and before is not None
            and b.kind == "prose"
            and b.lines[0].text[:1].islower()
            and not before.lines[-1].text.endswith(TERMINAL)
        ):
            before.lines.extend(b.lines)
            in_run = False
            continue
        in_run = False
        out.append(b)
        before = b if b.kind == "prose" else None
    return out


def build_sections(blocks: list[Block], ed: Edition) -> list[Section]:
    """Group blocks under numbered headings, within the edition's chapter range."""
    sections: list[Section] = []
    chapter = 0
    for b in blocks:
        if b.kind == "heading":
            ch = int(b.number.split(".")[0])
            plausible = (
                ch == chapter or (ch == chapter + 1) or (chapter == 0 and ch == ed.first_chapter)
            )
            if plausible:
                chapter = ch
                if ch > ed.last_chapter:
                    break
                sections.append(Section(b.number, b.title, [b]))
                continue
            b.kind = "subheading"  # a numbered-looking line out of sequence: keep as text
            b.title = f"{b.number} {b.title}"
        if sections:
            sections[-1].blocks.append(b)
    return sections


HYPHEN_COMPOUND = re.compile(r"\b([A-Za-z]+-[A-Za-z]+)\b")


def hyphen_compounds(blocks: list[Block]) -> set[str]:
    """Hyphenated words that occur *inside* lines anywhere in the text (lower-cased)."""
    found: set[str] = set()
    for b in blocks:
        if b.kind in ("prose", "note", "caption"):
            for ln in b.lines:
                body = ln.text[:-1] if ln.text.endswith("-") else ln.text
                found.update(m.lower() for m in HYPHEN_COMPOUND.findall(body))
    return found


def join_lines(texts: list[str], compounds: set[str]) -> str:
    """Join wrapped lines; drop a line-end hyphen unless the hyphenated word occurs mid-line."""
    out = texts[0]
    for nxt in texts[1:]:
        m = re.search(r"([A-Za-z]+)-$", out)
        n = re.match(r"[a-z]+", nxt)
        if m and n:
            compound = f"{m.group(1)}-{n.group(0)}".lower()
            out = out + nxt if compound in compounds else out[:-1] + nxt
        else:
            out = f"{out} {nxt}"
    return out
