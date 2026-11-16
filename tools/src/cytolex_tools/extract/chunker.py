"""Sections -> labelled Markdown chunks with YAML front matter, split to a token cap.

Labels (stable citation anchors, restarting at every numbered heading):
- `¶n`: prose paragraphs, plus small-print blocks that don't follow an example or a float;
- `En`: example blocks; the small-print explanation and signal legends that follow belong to it;
- `Fig. N` / `Table N` (floats): a caption, with its table and footnotes; not counted as ¶.
The page of a label is the book page of the block's first line.
"""

import io
import math
import re
import unicodedata
from dataclasses import dataclass, field

from ruamel.yaml import YAML

from .blocks import Block, Section, join_lines, plain
from .tables import TableRegion, render_table

TOKEN_CAP = 4000
STRAY_SPACE = re.compile(r"[;(:,] | [);:,]|\) \(")
FOOTNOTE_LINE = re.compile(r"^\^\w+ ")
LEGEND = ("●", "○")  # FISH signal legends under nucleus diagrams (`● = probe for ABL1`)


def estimate_tokens(text: str) -> int:
    return math.ceil(len(text) / 4)


@dataclass
class Unit:
    """An indivisible-by-default piece of a chunk (a labelled block, or a sub-heading)."""

    text: str
    paragraphs: dict[str, int] = field(default_factory=dict)
    examples: dict[str, int] = field(default_factory=dict)
    stray: list[str] = field(default_factory=list)
    lines: list[str] | None = None  # set when the unit may be split at line boundaries
    floats: dict[str, int] = field(default_factory=dict)
    changed: list[str] = field(default_factory=list)  # labels next to a margin change bar
    underlined: list[str] = field(default_factory=list)  # labels with underlined characters
    tables: dict[str, dict] = field(default_factory=dict)  # table id -> review status

    def mark(self, label: str, block: Block) -> None:
        if any(ln.changed for ln in block.lines) and label not in self.changed:
            self.changed.append(label)
        if any(ln.underlined for ln in block.lines) and label not in self.underlined:
            self.underlined.append(label)

    def append(self, text: str) -> None:
        self.text += f"\n{text}"
        if self.lines is not None:
            self.lines += text.split("\n")


@dataclass
class Chunk:
    section: Section
    part: int
    parts: int
    path: str
    text: str
    tokens: int
    paragraphs: dict[str, int]
    examples: dict[str, int]
    floats: dict[str, int] = field(default_factory=dict)
    changed: list[str] = field(default_factory=list)
    underlined: list[str] = field(default_factory=list)


def example_lines(block: Block) -> list[str]:
    """Example lines, with ` | ` before the description column on every line that has one.

    The layout pass only sees a column where the gap is wide (`fra(10)(q25.2) | A fragile ...`);
    a longer notation leaves a narrow gap (`fra(10)(q22.1),fra(10)(q25.2) Two fragile ...`).
    Within one example block the description column starts at the same x on every line.
    """
    col = next((ln.col_x for ln in block.lines if ln.col_x), None)
    out = []
    for ln in block.lines:
        text = ln.text
        if col is not None and " | " not in text and ln.words:
            i = next((i for i, w in enumerate(ln.words) if i and abs(w.x0 - col) < 3), None)
            if i is not None:
                text = " ".join(w.text for w in ln.words[:i]) + " | "
                text += " ".join(w.text for w in ln.words[i:])
        out.append(text)
    return out


def render_lines(block: Block, compounds: set[str]) -> list[str]:
    texts = [ln.text for ln in block.lines]
    if block.kind == "example" or any(" | " in t for t in texts):
        return texts  # notation and tables keep their line structure verbatim
    groups: list[list[str]] = [[]]
    for t in texts:
        if FOOTNOTE_LINE.match(t) and groups[-1]:
            groups.append([])  # each table footnote (`^a cen = Centromere, ...`) on its own line
        groups[-1].append(plain(t))
    return [join_lines(g, compounds) for g in groups]


def section_units(
    section: Section, compounds: set[str], tables: dict[str, TableRegion] | None = None
) -> list[Unit]:
    tables = tables or {}
    units: list[Unit] = []
    n_par = n_ex = 0
    attach: Unit | None = None  # the example or float that notes/legends/tables attach to
    attach_label = ""

    def join_text(pieces: list[str]) -> str:
        return join_lines(pieces, compounds)

    for b in section.blocks[1:]:  # blocks[0] is the heading itself
        if b.kind == "subheading":
            units.append(Unit(f"#### {b.title}"))
            attach = None
        elif b.kind == "example":
            n_ex += 1
            label = f"E{n_ex}"
            lines = example_lines(b)
            stray = [label] if any(STRAY_SPACE.search(t) for t in lines) else []
            body = "\n".join(lines)
            attach = Unit(
                f"[{label} p.{b.book_page}]\n```\n{body}\n```",
                examples={label: b.book_page},
                stray=stray,
            )
            attach.mark(label, b)
            attach_label = label
            units.append(attach)
        elif b.kind == "caption":
            label = b.float_id
            text = join_lines([plain(ln.text) for ln in b.lines], compounds)
            head = f"[{label} p.{b.book_page}] {text}"
            attach = Unit(head, floats={label: b.book_page}, lines=[head])
            attach.mark(label, b)
            attach_label = label
            units.append(attach)
        elif b.kind == "table":
            tr = tables[b.lines[0].table]
            rendered = render_table(tr, join_text)
            spec = tr.spec
            if spec.kind == "paragraph":
                n_par += 1
                label = f"¶{n_par}"
                head = f"[{label} p.{b.book_page}]"
                u = Unit(
                    "\n".join([head, *rendered]),
                    paragraphs={label: b.book_page},
                    lines=[head, *rendered],
                )
                u.tables[spec.id] = spec.review
                u.mark(label, b)
                units.append(u)
                attach = None
                continue
            if attach is None or attach_label != spec.id:
                head = f"[{spec.id} p.{b.book_page}]"  # a table printed without a caption
                attach = Unit(head, floats={spec.id: b.book_page}, lines=[head])
                attach_label = spec.id
                units.append(attach)
            attach.append("\n".join(["", *rendered, ""]))  # blank lines delimit the table
            attach.tables[spec.id] = spec.review
            attach.mark(attach_label, b)
        elif b.kind == "note" and attach is not None:
            attach.append("\n".join(f"> {t}" for t in render_lines(b, compounds)))
            attach.mark(attach_label, b)
        elif b.kind == "prose" and attach is not None and b.lines[0].text.startswith(LEGEND):
            attach.append("\n".join(f"> {ln.text}" for ln in b.lines))
            attach.mark(attach_label, b)
        else:  # prose, or small print not attached to an example or float
            n_par += 1
            label = f"¶{n_par}"
            rendered = render_lines(b, compounds)
            head = f"[{label} p.{b.book_page}]"
            if len(rendered) == 1:
                u = Unit(f"{head} {rendered[0]}", paragraphs={label: b.book_page})
            else:
                u = Unit(
                    "\n".join([head, *rendered]),
                    paragraphs={label: b.book_page},
                    lines=[head, *rendered],
                )
            u.mark(label, b)
            units.append(u)
            attach = None
    return units


def slug(text: str, limit: int = 60) -> str:
    ascii_text = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode()
    s = re.sub(r"[^a-z0-9]+", "-", ascii_text.lower()).strip("-")
    return s[:limit].rstrip("-") or "section"


def chunk_path(section: Section, part: int, parts: int) -> str:
    number = ".".join(f"{int(p):02d}" for p in section.number.split("."))
    suffix = f"-p{part}" if parts > 1 else ""
    return f"ch{section.chapter:02d}/{number}-{slug(section.title)}{suffix}.md"


def _yaml() -> YAML:
    y = YAML(typ="safe", pure=True)
    y.default_flow_style = None
    y.allow_unicode = True
    y.width = 4096
    y.sort_base_mapping_type_on_output = False
    return y


def dump_yaml(data: object) -> str:
    buf = io.StringIO()
    _yaml().dump(data, buf)
    return buf.getvalue()


def render_chunk(version: str, section: Section, part: int, parts: int, units: list[Unit]) -> str:
    book_pages, pdf_pages = section.pages()
    paragraphs: dict[str, int] = {}
    examples: dict[str, int] = {}
    floats: dict[str, int] = {}
    stray: list[str] = []
    changed: list[str] = []
    underlined: list[str] = []
    tables: dict[str, dict] = {}
    for u in units:
        paragraphs.update(u.paragraphs)
        examples.update(u.examples)
        floats.update(u.floats)
        stray.extend(u.stray)
        changed.extend(x for x in u.changed if x not in changed)
        underlined.extend(x for x in u.underlined if x not in underlined)
        tables.update(u.tables)
    front = {
        "version": version,
        "section": section.number,
        "title": section.title,
        "chapter": section.chapter,
        "level": section.level,
        "parent": section.parent,
        "book_pages": book_pages,
        "pdf_pages": pdf_pages,
        "part": part,
        "parts": parts,
        "paragraphs": paragraphs,
        "examples": examples,
        "floats": floats,
        "stray_spaces": stray,
        "changed": changed,
        "underlined": underlined,
        "tables": tables,
    }
    title = f"# {section.number} {section.title}"
    if parts > 1:
        title += f" (part {part}/{parts})"
    body = "\n\n".join([title, *(u.text.strip("\n") for u in units)])
    return f"---\n{dump_yaml(front)}---\n{body}\n"


def split_oversized(units: list[Unit], budget: int) -> list[Unit]:
    """Split units larger than `budget` tokens at line boundaries."""
    out: list[Unit] = []
    for u in units:
        if estimate_tokens(u.text) <= budget:
            out.append(u)
            continue
        if u.lines:  # tables, examples: split at line boundaries
            head, rest, sep = u.lines[0], u.lines[1:], "\n"
        else:  # one long prose paragraph: split at sentence boundaries
            m = re.match(r"(\[[^\]]+\]) (.*)$", u.text, re.S)
            head, body = (m.group(1), m.group(2)) if m else ("", u.text)
            rest, sep = re.split(r"(?<=[.;:])\s+", body), " "
        piece: list[str] = [head]
        first = True
        for line in rest:
            if estimate_tokens(sep.join([*piece, line])) > budget and len(piece) > 1:
                out.append(_piece(u, piece, first, sep))
                first = False
                piece = [f"{head[:-1]} cont.]" if head.endswith("]") else head]
            piece.append(line)
        out.append(_piece(u, piece, first, sep))
    return out


def _piece(u: Unit, lines: list[str], first: bool, sep: str) -> Unit:
    return Unit(
        sep.join(lines),
        paragraphs=dict(u.paragraphs) if first else {},
        examples=dict(u.examples) if first else {},
        stray=list(u.stray) if first else [],
        floats=dict(u.floats) if first else {},
        changed=list(u.changed) if first else [],
        underlined=list(u.underlined) if first else [],
        tables=dict(u.tables) if first else {},
    )


def pack(units: list[Unit], budget: int) -> list[list[Unit]]:
    groups: list[list[Unit]] = [[]]
    size = 0
    for u in units:
        t = estimate_tokens(u.text) + 1
        if groups[-1] and size + t > budget:
            groups.append([])
            size = 0
        groups[-1].append(u)
        size += t
    return groups


def chunk_section(
    version: str,
    section: Section,
    compounds: set[str],
    cap: int = TOKEN_CAP,
    tables: dict[str, TableRegion] | None = None,
) -> list[Chunk]:
    units = section_units(section, compounds, tables)
    budget = cap
    while budget > 200:
        groups = pack(split_oversized(units, budget), budget)
        parts = len(groups)
        texts = [render_chunk(version, section, i + 1, parts, g) for i, g in enumerate(groups)]
        if all(estimate_tokens(t) <= cap for t in texts):
            return [
                Chunk(
                    section=section,
                    part=i + 1,
                    parts=parts,
                    path=chunk_path(section, i + 1, parts),
                    text=t,
                    tokens=estimate_tokens(t),
                    paragraphs={k: v for u in g for k, v in u.paragraphs.items()},
                    examples={k: v for u in g for k, v in u.examples.items()},
                    floats={k: v for u in g for k, v in u.floats.items()},
                    changed=[x for u in g for x in u.changed],
                    underlined=[x for u in g for x in u.underlined],
                )
                for i, (g, t) in enumerate(zip(groups, texts, strict=True))
            ]
        budget -= 200
    raise ValueError(f"section {section.number}: cannot fit under {cap} tokens")
