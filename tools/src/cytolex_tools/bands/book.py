"""Book idiograms vs. NCBI band sets: compare, match the cited exceptions, apply them (T-007).

Inputs (committed, per edition): `ideograms.yaml` (decoded by `kb ideograms`) and
`exceptions.yaml` (every difference, decided). Differences are compared per level
(400/550/850, the levels NCBI has) and grouped by region: the shortest differing name
plus its differing sub-bands, e.g. level 850, 9q34.1: book [9q34.1] vs NCBI
[9q34.11, 9q34.12, 9q34.13].
"""

from dataclasses import dataclass, field
from pathlib import Path

from ruamel.yaml import YAML

from ..kb.schema import validate_data
from .common import flow, lineage, split_name
from .ideograms import LEVELS as BOOK_LEVELS
from .ideograms import expand_range, sort_key
from .parse import CHROM_INDEX

NCBI_LEVELS = (400, 550, 850)


@dataclass
class Book:
    levels: dict[str, set[int]]  # band name -> book levels (ranges expanded)
    pages: dict[str, int]  # chromosome -> book page
    exceptions: list[dict]
    section: str
    figure: str


@dataclass
class Applied:
    entries: list[dict]
    problems: list[str] = field(default_factory=list)
    draft: str | None = None  # exceptions.yaml text covering every difference (for authoring)


def name_key(name: str) -> tuple:
    """pter → qter order of a name: p arm distal first, q arm proximal first."""
    chrom, arm, band = split_name(name)
    k = sort_key(band)
    return (CHROM_INDEX[chrom], arm, tuple(-d for d in k) if arm == "p" else k)


def book_levels(labels: list[dict]) -> dict[str, set[int]]:
    """Band name -> levels; a range label ('22–24') counts for every band it spans."""
    by: dict[tuple[str, str, int], list[str]] = {}
    for e in labels:
        by.setdefault((e["chrom"], e["arm"], e["level"]), []).append(e["label"])
    out: dict[str, set[int]] = {}
    for (chrom, arm, lvl), labs in by.items():
        finer = [x for f in BOOK_LEVELS if f > lvl for x in by.get((chrom, arm, f), [])]
        for lab in labs:
            for b in expand_range(lab, finer):
                out.setdefault(f"{chrom}{arm}{b}", set()).add(lvl)
    return out


def load_book(book_dir: Path, version: str, exc_path: Path | None = None) -> Book:
    """`book_dir` holds the generated ideograms.yaml; the person-written exceptions.yaml is
    `exc_path` (default: next to it)."""
    yaml = YAML(typ="safe")
    ide_path, exc_path = book_dir / "ideograms.yaml", exc_path or book_dir / "exceptions.yaml"
    if not ide_path.exists():
        raise FileNotFoundError(f"{ide_path} missing; run `kb ideograms --version {version}`")
    ide = yaml.load(ide_path.read_text(encoding="utf-8"))
    exc = yaml.load(exc_path.read_text(encoding="utf-8")) if exc_path.exists() else None
    exc = exc or {"version": version, "exceptions": []}
    problems = validate_data(exc, exc_path)
    if problems:
        raise ValueError("; ".join(str(p) for p in problems))
    pages = {e["chrom"]: e["page"] for e in ide["labels"]}
    return Book(
        book_levels(ide["labels"]),
        pages,
        exc["exceptions"],
        str(ide["source"]["section"]),
        ide["source"]["figure"],
    )


def differences(book: dict[str, set[int]], ncbi: dict[int, set[str]]) -> list[dict]:
    """Grouped per-level differences, in level then chromosome order."""
    groups = []
    for lvl in NCBI_LEVELS:
        b_only = {n for n, ls in book.items() if lvl in ls} - ncbi[lvl]
        n_only = ncbi[lvl] - {n for n, ls in book.items() if lvl in ls}
        diff = b_only | n_only
        regions: dict[str, dict] = {}
        for n in sorted(diff, key=lambda x: (len(lineage(split_name(x)[2])), name_key(x))):
            c, a, b = split_name(n)
            region = next(
                (f"{c}{a}{x}" for x in reversed(lineage(b)) if f"{c}{a}{x}" in regions), n
            )
            g = regions.setdefault(region, {"level": lvl, "region": region, "book": [], "ncbi": []})
            g["book" if n in b_only else "ncbi"].append(n)
        for g in sorted(regions.values(), key=lambda g: name_key(g["region"])):
            g["book"].sort(key=name_key)
            g["ncbi"].sort(key=name_key)
            groups.append(g)
    return groups


def draft_exceptions(version: str, groups: list[dict], book: Book) -> str:
    lines = [
        f"# Draft: every idiogram (ISCN {version} {book.figure}) vs NCBI difference, as found by",
        "# `kb bands`. Copy entries into curated/bands/<version>/exceptions.yaml and decide them.",
        f"version: '{version}'",
        "exceptions:",
    ]
    for g in groups:
        chrom = split_name(g["region"])[0]
        cite = {"section": book.section, "page": book.pages.get(chrom, 0), "float": book.figure}
        entry = g | {"resolution": "book", "cite": cite, "note": "TODO"}
        lines.append(f"  - {flow(entry)}")
    return "\n".join(lines) + "\n"


def apply(version: str, entries: list[dict], book: Book) -> Applied:
    """Check the exceptions against the differences and return the final band entries."""
    by_name = {e["name"]: e for e in entries}
    ncbi = {lvl: {e["name"] for e in entries if lvl in e["levels"]} for lvl in NCBI_LEVELS}
    groups = differences(book.levels, ncbi)
    out = Applied(entries)

    declared = {(x["level"], x["region"]): x for x in book.exceptions}
    found = {(g["level"], g["region"]): g for g in groups}
    for key, g in found.items():
        x = declared.get(key)
        if x is None:
            out.problems.append(
                f"unexplained difference at {g['level']}, {g['region']}: book {g['book']} vs "
                f"NCBI {g['ncbi']}; add it to exceptions.yaml"
            )
        elif (
            sorted(x["book"], key=name_key) != g["book"]
            or sorted(x["ncbi"], key=name_key) != g["ncbi"]
        ):
            out.problems.append(
                f"exception {g['level']}, {g['region']} doesn't match the data: now book "
                f"{g['book']} vs NCBI {g['ncbi']}"
            )
    for key in declared.keys() - found.keys():
        out.problems.append(f"stale exception {key[0]}, {key[1]}: no such difference any more")
    for x in book.exceptions:
        bad = [n for n in x.get("keep") or [] if n not in x["ncbi"]]
        if bad or (x.get("keep") and x["resolution"] != "book"):
            out.problems.append(
                f"exception {x['level']}, {x['region']}: `keep` needs resolution book and "
                f"names from `ncbi` (got {bad or x['keep']})"
            )
    if out.problems:
        out.draft = draft_exceptions(version, groups, book)
        return out

    # Book names, minus those an `ncbi` resolution rejects at their only level(s).
    rejected: dict[str, set[int]] = {}
    for x in book.exceptions:
        if x["resolution"] == "ncbi":
            for n in x["book"]:
                rejected.setdefault(n, set()).add(x["level"])
    accepted = [n for n, ls in book.levels.items() if ls - rejected.get(n, set())]
    covered = _closure(accepted)  # every accepted book name and its ancestors
    missing = sorted((n for n in covered if n not in by_name), key=name_key)
    justified = _closure(n for x in book.exceptions if x["resolution"] == "book" for n in x["book"])
    unexplained = [n for n in missing if n not in justified]
    if unexplained:
        out.problems.append(
            f"book names unknown to NCBI without an exception: {', '.join(unexplained)}"
        )
        return out

    levels = {e["name"]: set(e["levels"]) for e in entries}
    keep = {n for x in book.exceptions for n in x.get("keep") or []}
    for x in book.exceptions:
        if x["resolution"] != "book":
            continue
        for n in x["ncbi"]:
            if n not in keep:
                levels[n].discard(x["level"])
        for n in x["book"]:
            levels.setdefault(n, set()).add(x["level"])
    for n in accepted:  # 300/700 come from the book only
        levels.setdefault(n, set()).update(lv for lv in book.levels[n] if lv not in NCBI_LEVELS)

    # New names (book only): no coordinates, no stain; ancestors are added as needed.
    for n in missing:
        c, a, b = split_name(n)
        p = lineage(b)[1] if len(lineage(b)) > 1 else None
        by_name[n] = {
            "name": n,
            "chrom": c,
            "arm": a,
            "parent": f"{c}{a}{p}" if p else None,
            "levels": [],
            "derived": True,
            "stain": None,
            "grch37": None,
            "grch38": None,
        }

    # Drop names left with no level that the book doesn't cover and nobody keeps; keep the
    # ancestors of every surviving name.
    alive = _closure(n for n in by_name if n in covered or n in keep or levels.get(n))
    final = []
    for n, e in by_name.items():
        if n not in alive:
            continue
        ls = sorted(levels.get(n, set()))
        final.append(
            e
            | {
                "levels": ls,
                "derived": not ls,
                "book": n in covered,
            }
        )
    final.sort(key=_position(by_name))
    out.entries = final
    return out


def _closure(names) -> set[str]:
    """The names plus all their ancestors."""
    out = set()
    for n in names:
        c, a, b = split_name(n)
        out |= {f"{c}{a}{x}" for x in lineage(b)}
    return out


def _position(by_name: dict[str, dict]):
    """Sort key: chromosome, GRCh38 start (a new name takes its nearest placed ancestor's),
    then depth, then pter → qter order among siblings."""

    def start(n: str) -> int:
        c, a, b = split_name(n)
        for x in lineage(b):
            e = by_name.get(f"{c}{a}{x}")
            if e and e["grch38"]:
                return e["grch38"][0]
        return 0

    def key(e: dict):
        c, a, b = split_name(e["name"])
        return (CHROM_INDEX[c], start(e["name"]), len(lineage(b)), name_key(e["name"]))

    return key
