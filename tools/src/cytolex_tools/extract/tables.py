"""Tables: per-edition column layouts (tables/<version>.yaml) -> Markdown tables.

The PDF text layer has every character of a table, but `page_lines` reads it as plain lines, so
the cell structure is lost. For each configured table region, the region's lines are taken out
of the normal text flow and replaced by one marker line (`Line.table`); the chunker renders the
region as a Markdown table, assigning every word to a column by its x position.
"""

from dataclasses import dataclass, field
from pathlib import Path

from ruamel.yaml import YAML

from .layout import Line

TABLES_DIR = Path(__file__).parent / "tables"
X_TOLERANCE = 2.0  # pt; a word may start this far left of its column's x


@dataclass(frozen=True)
class Region:
    book_page: int
    top: float
    bottom: float
    body_top: float
    columns: tuple[float, ...]
    key: tuple[int, ...]
    join: str  # text | br
    header: str  # auto | none

    def contains(self, ln: Line) -> bool:
        return ln.book_page == self.book_page and self.top <= ln.top <= self.bottom


@dataclass(frozen=True)
class TableSpec:
    id: str
    kind: str  # float | paragraph | figure (a diagram: its text lines are kept verbatim)
    review: dict
    regions: tuple[Region, ...]


@dataclass
class TableRegion:
    """One region's raw lines, collected during extraction."""

    spec: TableSpec
    index: int  # position of the region in spec.regions
    lines: list[Line] = field(default_factory=list)

    @property
    def region(self) -> Region:
        return self.spec.regions[self.index]

    @property
    def key(self) -> str:
        return f"{self.spec.id}#{self.index + 1}"


def load_tables(version: str, path: Path | None = None) -> list[TableSpec]:
    path = path or TABLES_DIR / f"{version}.yaml"
    if not path.exists():
        return []
    data = YAML(typ="safe", pure=True).load(path.read_text(encoding="utf-8")) or {}
    specs = []
    for t in data.get("tables", []):
        regions = tuple(
            Region(
                book_page=int(r["book_page"]),
                top=float(r["top"]),
                bottom=float(r["bottom"]),
                body_top=float(r.get("body_top", r["top"])),
                columns=tuple(float(x) for x in r["columns"]),
                key=tuple(int(k) for k in r.get("key", [0])),
                join=r.get("join", "text"),
                header=r.get("header", "auto"),
            )
            for r in t["regions"]
        )
        if t["kind"] not in ("float", "paragraph", "figure"):
            raise ValueError(f"{path.name}: {t['id']}: unknown kind {t['kind']!r}")
        specs.append(TableSpec(t["id"], t["kind"], dict(t.get("review", {})), regions))
    return specs


def divert_tables(
    lines: list[Line], specs: list[TableSpec], found: dict[str, TableRegion]
) -> list[Line]:
    """Replace the lines of each table region by one marker line; collect them in `found`."""
    out: list[Line] = []
    for ln in lines:
        hit = next(((s, i) for s in specs for i, r in enumerate(s.regions) if r.contains(ln)), None)
        if hit is None:
            out.append(ln)
            continue
        spec, i = hit
        tr = found.get(f"{spec.id}#{i + 1}")
        if tr is None:
            tr = TableRegion(spec, i)
            found[tr.key] = tr
            r = spec.regions[i]
            out.append(
                Line(
                    pdf_page=ln.pdf_page,
                    book_page=ln.book_page,
                    top=ln.top,
                    x0=r.columns[0],
                    text=f"[table {tr.key}]",
                    font="TABLE",
                    size=ln.size,
                    table=tr.key,
                )
            )
        tr.lines.append(ln)
    # a marker line inherits the change/underline marks of the lines it stands for
    return [_with_marks(ln, found) if ln.table else ln for ln in out]


def _with_marks(marker: Line, found: dict[str, TableRegion]) -> Line:
    lines = found[marker.table].lines
    return Line(
        **{
            **marker.__dict__,
            "changed": any(x.changed for x in lines),
            "underlined": sum(x.underlined for x in lines),
        }
    )


def column_of(x0: float, columns: tuple[float, ...]) -> int:
    idx = 0
    for i, c in enumerate(columns):
        if x0 + X_TOLERANCE >= c:
            idx = i
    return idx


def cells(ln: Line, columns: tuple[float, ...]) -> list[str]:
    out = [""] * len(columns)
    for w in ln.words:
        i = column_of(w.x0, columns)
        out[i] = f"{out[i]} {w.text}".strip()
    return out


def _escape(cell: str) -> str:
    return cell.replace("|", "\\|")


def render_table(tr: TableRegion, join_text) -> list[str]:
    """Markdown table lines. `join_text(list[str]) -> str` joins wrapped prose lines."""
    r = tr.region
    if tr.spec.kind == "figure":
        return [f"> {ln.text}" for ln in tr.lines]
    ncol = len(r.columns)
    header_lines = [ln for ln in tr.lines if ln.top < r.body_top]
    body_lines = [ln for ln in tr.lines if ln.top >= r.body_top]

    if r.header == "none":
        header = [""] * ncol
    else:
        parts: list[list[str]] = [[] for _ in range(ncol)]
        for ln in header_lines:
            for i, c in enumerate(cells(ln, r.columns)):
                if c:
                    parts[i].append(c)
        header = [" ".join(p) for p in parts]

    rows: list[list[list[str]]] = []  # row -> column -> wrapped pieces
    for ln in body_lines:
        cs = cells(ln, r.columns)
        if not rows or any(cs[k] for k in r.key if k < ncol):
            rows.append([[c] if c else [] for c in cs])
        else:
            for i, c in enumerate(cs):
                if c:
                    rows[-1][i].append(c)

    def joined(pieces: list[str]) -> str:
        if not pieces:
            return ""
        return "<br>".join(pieces) if r.join == "br" else join_text(pieces)

    out = ["| " + " | ".join(_escape(h) for h in header) + " |", "|" + "---|" * ncol]
    out += ["| " + " | ".join(_escape(joined(c)) for c in row) + " |" for row in rows]
    return out
