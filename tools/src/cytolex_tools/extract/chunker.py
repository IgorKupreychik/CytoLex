"""Sections -> labelled Markdown chunks with YAML front matter, split to a token cap.

Labels (stable citation anchors, restarting at every numbered heading):
- `¶n`: prose paragraphs, plus small-print blocks that don't follow an example (e.g. tables);
- `En`: example blocks; the small-print explanation that follows belongs to the example.
The page of a label is the book page of the block's first line.
"""

import io
import math
import re
import unicodedata
from dataclasses import dataclass, field

from ruamel.yaml import YAML

from .blocks import Block, Section, join_lines, plain

TOKEN_CAP = 4000
STRAY_SPACE = re.compile(r"[;(:,] | [);:,]|\) \(")


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


def render_lines(block: Block, compounds: set[str]) -> list[str]:
    texts = [ln.text for ln in block.lines]
    if block.kind == "example" or any(" | " in t for t in texts):
        return texts  # notation and tables keep their line structure verbatim
    return [join_lines([plain(t) for t in texts], compounds)]


def section_units(section: Section, compounds: set[str]) -> list[Unit]:
    units: list[Unit] = []
    n_par = n_ex = 0
    current_example: Unit | None = None
    for b in section.blocks[1:]:  # blocks[0] is the heading itself
        if b.kind == "subheading":
            units.append(Unit(f"#### {b.title}"))
            current_example = None
        elif b.kind == "example":
            n_ex += 1
            label = f"E{n_ex}"
            lines = [ln.text for ln in b.lines]
            stray = [label] if any(STRAY_SPACE.search(t) for t in lines) else []
            body = "\n".join(lines)
            current_example = Unit(
                f"[{label} p.{b.book_page}]\n```\n{body}\n```",
                examples={label: b.book_page},
                stray=stray,
            )
            units.append(current_example)
        elif b.kind == "note" and current_example is not None:
            note = "\n".join(f"> {t}" for t in render_lines(b, compounds))
            current_example.text += f"\n{note}"
        else:  # prose, or small print not attached to an example
            n_par += 1
            label = f"¶{n_par}"
            rendered = render_lines(b, compounds)
            head = f"[{label} p.{b.book_page}]"
            if len(rendered) == 1:
                units.append(Unit(f"{head} {rendered[0]}", paragraphs={label: b.book_page}))
            else:
                units.append(
                    Unit(
                        "\n".join([head, *rendered]),
                        paragraphs={label: b.book_page},
                        lines=[head, *rendered],
                    )
                )
            current_example = None
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
    stray: list[str] = []
    for u in units:
        paragraphs.update(u.paragraphs)
        examples.update(u.examples)
        stray.extend(u.stray)
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
        "stray_spaces": stray,
    }
    title = f"# {section.number} {section.title}"
    if parts > 1:
        title += f" (part {part}/{parts})"
    body = "\n\n".join([title, *(u.text for u in units)])
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
    version: str, section: Section, compounds: set[str], cap: int = TOKEN_CAP
) -> list[Chunk]:
    units = section_units(section, compounds)
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
                )
                for i, (g, t) in enumerate(zip(groups, texts, strict=True))
            ]
        budget -= 200
    raise ValueError(f"section {section.number}: cannot fit under {cap} tokens")
