"""Read extracted chunks (`kb/source-text/<version>/`) for the KB build's citation checks.

Each chunk has YAML front matter (`section`, `paragraphs`, `examples`, `floats`: label → book
page) and a body where every labelled block starts with `[<label> p.<page>]` at line start.
Sections split into parts (`…-p1.md`, `…-p2.md`) are merged.
"""

import re
from dataclasses import dataclass, field
from pathlib import Path

from ruamel.yaml import YAML

LABEL_RE = re.compile(r"^\[(?P<label>[^\]]+?) p\.(?P<page>\d+)\]", re.MULTILINE)
HEADING_RE = re.compile(r"^#+ ", re.MULTILINE)


@dataclass
class Section:
    section: str
    files: list[Path] = field(default_factory=list)
    labels: dict[str, int] = field(default_factory=dict)  # ¶n / En / float label -> book page
    blocks: dict[str, str] = field(default_factory=dict)  # label -> block text (raw)


def normalise(text: str) -> str:
    """Collapse whitespace; drop Markdown quote markers and code fences of a block."""
    lines = []
    for line in text.splitlines():
        s = line.strip()
        if s.startswith("```"):
            continue
        if s.startswith(">"):
            s = s[1:].strip()
        lines.append(s)
    return " ".join(" ".join(lines).split())


def squash(text: str) -> str:
    """Remove all whitespace (for example strings, which may wrap across lines)."""
    return "".join(normalise(text).split())


def _split(path: Path) -> tuple[dict, str]:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---"):
        return {}, text
    _, fm, body = text.split("---", 2)
    return YAML(typ="safe").load(fm) or {}, body


def _blocks(body: str) -> dict[str, str]:
    starts = list(LABEL_RE.finditer(body))
    out = {}
    for i, m in enumerate(starts):
        end = starts[i + 1].start() if i + 1 < len(starts) else len(body)
        chunk = body[m.end() : end]
        h = HEADING_RE.search(chunk)
        out[m["label"]] = chunk[: h.start()] if h else chunk
    return out


def load_sections(source_dir: Path) -> dict[str, Section]:
    """All sections of one edition's chunk directory, keyed by section number."""
    sections: dict[str, Section] = {}
    for path in sorted(source_dir.rglob("*.md")):
        fm, body = _split(path)
        if "section" not in fm:
            continue
        key = str(fm["section"])
        sec = sections.setdefault(key, Section(key))
        sec.files.append(path)
        for group in ("paragraphs", "examples", "floats"):
            for label, page in (fm.get(group) or {}).items():
                sec.labels[str(label)] = int(page)
        sec.blocks.update(_blocks(body))
    return sections
