"""The probe table `curated/probes/probes.csv` (T-102): read, write, schema-check.

One row per probe name, edited by people (also in Excel/LibreOffice). Every cell is text;
`aliases` is `;`-separated; `grch37`/`grch38` are `start-end` (1-based, closed). The reader
accepts a UTF-8 BOM and CRLF line ends (what Excel writes); the writer writes UTF-8 with LF.
"""

import csv
import io
import re
from dataclasses import dataclass, field
from pathlib import Path

from ..config import CURATED_DIR
from ..kb.schema import Problem, schema_validator

PROBES_FILE = CURATED_DIR / "probes" / "probes.csv"
COLUMNS = (
    "probe",
    "aliases",
    "kind",
    "chromosome",
    "band",
    "grch37",
    "grch38",
    "source",
    "reviewed",
    "reviewer",
    "date",
    "note",
)
KINDS = ("gene", "centromere", "clone", "paint", "commercial", "other")
BUILDS = ("grch37", "grch38")
AUTO_MARK = "(auto)"  # end of `source` on rows written by `kb probes`
RANGE_RE = re.compile(r"^([0-9]+)-([0-9]+)$")


@dataclass
class Row:
    line: int  # 1-based line in the file (header = 1); 0 for rows not read from a file
    cells: dict[str, str] = field(default_factory=dict)

    def __getitem__(self, column: str) -> str:
        return self.cells.get(column, "")

    @property
    def name(self) -> str:
        return self["probe"]

    @property
    def aliases(self) -> list[str]:
        return [a.strip() for a in self["aliases"].split(";") if a.strip()]

    @property
    def reviewed(self) -> bool:
        return self["reviewed"] == "yes"

    @property
    def is_auto(self) -> bool:
        """Written by `kb probes` and not reviewed yet: the only rows `kb probes` may change."""
        return not self.reviewed and self["source"].endswith(AUTO_MARK)

    def range(self, build: str) -> tuple[int, int] | None:
        m = RANGE_RE.match(self[build])
        return (int(m[1]), int(m[2])) if m else None

    def where(self, column: str | None = None) -> str:
        label = f"row {self.line}" + (f" ({self.name})" if self.name else "")
        return f"{label}.{column}" if column else label


@dataclass
class Table:
    path: Path
    rows: list[Row]
    problems: list[Problem]  # header and schema problems


def parse(text: str, path: Path) -> Table:
    """Rows of a probe table; header/schema problems are collected, never raised."""
    text = text.removeprefix("﻿")
    reader = csv.reader(io.StringIO(text, newline=""))
    records = list(reader)
    problems: list[Problem] = []
    if not records:
        return Table(path, [], [Problem(path, "", "empty file; expected the header row")])
    header = [h.strip() for h in records[0]]
    if tuple(header) != COLUMNS:
        problems.append(
            Problem(path, "row 1", f"header must be exactly: {','.join(COLUMNS)} (got {header})")
        )
        return Table(path, [], problems)
    rows = []
    line = 1
    for rec in records[1:]:
        line += 1
        if not any(c.strip() for c in rec):
            continue  # blank line
        if len(rec) != len(COLUMNS):
            problems.append(
                Problem(path, f"row {line}", f"{len(rec)} cells, expected {len(COLUMNS)}")
            )
            continue
        rows.append(Row(line, {c: v.strip() for c, v in zip(COLUMNS, rec, strict=True)}))
    validator = schema_validator("probes.schema.json")
    for row in rows:
        for err in sorted(validator.iter_errors(row.cells), key=lambda e: e.message):
            column = err.path[0] if err.path else None
            problems.append(Problem(path, row.where(column), _message(err)))
    return Table(path, rows, problems)


def _message(err) -> str:
    if err.validator == "required":
        return f"missing {err.message.split(' ')[0]}"
    if err.validator == "minLength":
        return "required, must not be empty"
    if err.validator == "pattern" and isinstance(err.schema, dict) and "x-expected" in err.schema:
        return f"{err.instance!r} is not {err.schema['x-expected']}"
    return err.message


def read(path: Path = PROBES_FILE) -> Table:
    return parse(path.read_text(encoding="utf-8"), path)


def render(rows: list[Row]) -> str:
    out = io.StringIO(newline="")
    writer = csv.writer(out, lineterminator="\n")
    writer.writerow(COLUMNS)
    for row in rows:
        writer.writerow([row[c] for c in COLUMNS])
    return out.getvalue()


def write(path: Path, rows: list[Row]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(render(rows).encode("utf-8"))


def validate_csv(path: Path) -> list[Problem]:
    """`kb check` for a probe table: header and per-row schema."""
    return read(path).problems
