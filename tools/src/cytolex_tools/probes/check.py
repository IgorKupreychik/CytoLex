"""Checks of the probe table that need the band data (T-102); run by `kb build`.

Errors fail the build: band not in an edition's band data, chromosome not the band's, start
after end, duplicate names/aliases, names Excel turned into dates, and any conflict on a
reviewed row. Conflicts (the coordinates don't fall in the band, or the two builds disagree)
are warnings on unreviewed rows and are listed in `build/review/probes-review.md`.
"""

import re
from dataclasses import dataclass, field

from ..kb.schema import Problem
from .table import BUILDS, Row, Table

MONTHS = "jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec"
DATE_LIKE = (
    re.compile(rf"^\d{{1,2}}[-/ ]({MONTHS})([-/ ]\d{{2,4}})?$", re.I),  # 9-Sep, 1-Mar-2024
    re.compile(rf"^({MONTHS})[-/ ]\d{{2,4}}$", re.I),  # Sep-09
    re.compile(r"^\d{4}-\d{2}-\d{2}([ T].*)?$"),  # 2024-09-09
    re.compile(r"^\d{1,2}[/.]\d{1,2}[/.]\d{2,4}$"),  # 09/09/2024
)


@dataclass
class Conflict:
    row: Row
    kind: str  # band-coordinates | builds-disagree
    message: str


@dataclass
class CheckResult:
    errors: list[Problem] = field(default_factory=list)
    conflicts: list[Conflict] = field(default_factory=list)


def _band_cells(cell: str) -> list[str]:
    """`22q11.21-q11.22` -> ['22q11.21', '22q11.22']; `9q34.12` -> ['9q34.12']."""
    head, _, tail = cell.partition("-")
    if not tail:
        return [head]
    chrom = head[: next(i for i, ch in enumerate(head) if ch in "pq")]
    return [head, chrom + tail]


def _band_range(bands: dict[str, dict], names: list[str], build: str) -> tuple[int, int] | None:
    ranges = [bands[n].get(build) for n in names]
    if any(r is None for r in ranges):
        return None  # book-only band without coordinates
    return min(r[0] for r in ranges), max(r[1] for r in ranges)


def check(tab: Table, band_sets: dict[str, dict[str, dict]]) -> CheckResult:
    """`band_sets`: edition -> band name -> band entry of that edition's bands.yaml."""
    res = CheckResult()
    path = tab.path
    seen: dict[str, Row] = {}
    for row in tab.rows:
        for column, name in [("probe", row.name), *(("aliases", a) for a in row.aliases)]:
            if any(p.match(name) for p in DATE_LIKE):
                res.errors.append(
                    Problem(
                        path,
                        row.where(column),
                        (
                            f"{name!r} looks like a date a spreadsheet made of a name "
                            "(SEPT9 → 9-Sep); restore the name and open the file with "
                            "every column as Text"
                        ),
                    )
                )
            key = name.casefold()
            if key in seen and seen[key] is not row:
                res.errors.append(
                    Problem(
                        path, row.where(column), f"{name!r} is already used by {seen[key].where()}"
                    )
                )
            elif key in seen:
                res.errors.append(Problem(path, row.where(column), f"{name!r} listed twice"))
            else:
                seen[key] = row
        for build in BUILDS:
            r = row.range(build)
            if r and r[0] > r[1]:
                res.errors.append(Problem(path, row.where(build), f"start {r[0]} > end {r[1]}"))
        if row["band"]:
            _check_band(row, band_sets, res, path)
    for c in res.conflicts:
        if c.row.reviewed:
            res.errors.append(
                Problem(
                    path,
                    c.row.where(),
                    (
                        f"reviewed row with a conflict: {c.message}; fix the row, or set "
                        "reviewed to no until it is resolved"
                    ),
                )
            )
    return res


def _check_band(row: Row, band_sets: dict[str, dict[str, dict]], res: CheckResult, path) -> None:
    names = _band_cells(row["band"])
    chroms = {n[: next(i for i, ch in enumerate(n) if ch in "pq")] for n in names}
    if chroms != {row["chromosome"]}:
        res.errors.append(
            Problem(
                path,
                row.where("band"),
                f"band {row['band']} is not on chromosome {row['chromosome']}",
            )
        )
        return
    checked = False
    for version, bands in sorted(band_sets.items()):
        missing = [n for n in names if n not in bands]
        if missing:
            res.errors.append(
                Problem(
                    path, row.where("band"), f"{', '.join(missing)}: no such band in ISCN {version}"
                )
            )
        elif not checked:  # coordinates don't differ per edition: one edition suffices
            checked = True
            _check_coordinates(row, names, bands, version, res)


def _check_coordinates(
    row: Row, names: list[str], bands: dict[str, dict], version: str, res: CheckResult
) -> None:
    fits, outside = {}, []
    for build in BUILDS:
        r, br = row.range(build), _band_range(bands, names, build)
        if r and br:
            fits[build] = r[0] <= br[1] and br[0] <= r[1]
            if not fits[build]:
                label = build.replace("grch", "GRCh")
                outside.append(
                    f"{label} {r[0]}-{r[1]} is outside {row['band']} "
                    f"({br[0]}-{br[1]}, ISCN {version} band data)"
                )
    if len(fits) == 2 and fits["grch37"] != fits["grch38"]:
        res.conflicts.append(
            Conflict(row, "builds-disagree", f"GRCh37 and GRCh38 disagree: {outside[0]}")
        )
    else:
        res.conflicts += [Conflict(row, "band-coordinates", m) for m in outside]
