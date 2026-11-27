"""Build kb/bands/<version>/bands.yaml from the pinned NCBI GDP + UCSC sources
(curated/bands/<version>/sources.yaml).

Every band name of the 400/550/850 sets is kept, plus every nesting ancestor
(9q34.11 -> 9q34.1 -> 9q34). Names not in any source set are marked `derived`.
Coordinates: UCSC for 850-level bands; unions of their sub-bands for coarser names.
When the edition's sources configure the book idiograms (T-007), the band sets are
compared with them and the cited exceptions are applied (`book.py`); levels 300/700 then
come from the book. Any consistency problem fails the run and nothing is written.
"""

from dataclasses import dataclass, field
from pathlib import Path

from .common import flow, lineage, parent_band, scalar
from .fetch import Source, fetch, load_sources, sha256, sources_file, update_pins
from .parse import CHROM_INDEX, SourceBand, parse_ncbi_gdp, parse_ucsc_cytoband

GENERATOR_VERSION = 2  # 2: book idiograms + exceptions (T-007)
ASSEMBLIES = ("GRCh37", "GRCh38")
LEVELS = (400, 550, 850)
TOLERANCE = 1  # bp; the NCBI 850 files mix 0- and 1-based starts
MAX_LISTED = 10


@dataclass
class Report:
    out: Path | None = None
    problems: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    counts: dict[str, int] = field(default_factory=dict)
    pinned: list[str] = field(default_factory=list)  # ids whose pin was (re)written
    draft: Path | None = None  # exceptions draft written when differences are unexplained


def _key(b: SourceBand) -> tuple[str, str, str]:
    return (b.chrom, b.arm, b.band)


def _name(k: tuple[str, str, str]) -> str:
    return "".join(k)


def _listed(items, limit: int = MAX_LISTED) -> str:
    items = sorted(items)
    more = f", … (+{len(items) - limit})" if len(items) > limit else ""
    return ", ".join(items[:limit]) + more


def build(
    ncbi: dict[tuple[str, int], list[SourceBand]], ucsc: dict[str, list[SourceBand]]
) -> tuple[list[dict], list[str]]:
    """Return (band entries, problems)."""
    problems: list[str] = []
    sets = {k: {_key(b) for b in v} for k, v in ncbi.items()}

    # Same band names in both assemblies, per level.
    for lvl in LEVELS:
        a, b = sets[("GRCh37", lvl)], sets[("GRCh38", lvl)]
        if a != b:
            problems.append(
                f"NCBI {lvl}: band names differ between GRCh37 and GRCh38: "
                f"{_listed(_name(k) for k in a ^ b)}"
            )

    # NCBI 850 == UCSC, names and coordinates.
    canon: dict[str, dict[tuple[str, str, str], tuple[int, int]]] = {}
    for asm in ASSEMBLIES:
        u = {_key(b): b for b in ucsc[asm]}
        n = {_key(b): b for b in ncbi[(asm, 850)]}
        if set(u) != set(n):
            diff = _listed(_name(k) for k in set(u) ^ set(n))
            problems.append(f"{asm}: NCBI 850 vs UCSC band names differ: {diff}")
        off = [
            _name(k)
            for k in set(u) & set(n)
            if abs(u[k].start - n[k].start) > TOLERANCE or abs(u[k].end - n[k].end) > TOLERANCE
        ]
        if off:
            problems.append(f"{asm}: NCBI 850 vs UCSC coordinates differ: {_listed(off)}")
        canon[asm] = {k: (b.start, b.end) for k, b in u.items()}

    # Every coarser name nests over 850 names.
    fine = sets[("GRCh38", 850)]
    covered = {(c, a, x) for (c, a, b) in fine for x in lineage(b)}
    for lvl in (400, 550):
        orphan = sets[("GRCh38", lvl)] - covered
        if orphan:
            problems.append(
                f"NCBI {lvl}: names with no 850 sub-band: {_listed(map(_name, orphan))}"
            )

    # Coordinates of every name = union of its 850 sub-bands.
    coords: dict[str, dict[tuple[str, str, str], list[int]]] = {a: {} for a in ASSEMBLIES}
    for asm in ASSEMBLIES:
        for (c, a, b), (s, e) in canon[asm].items():
            for x in lineage(b):
                r = coords[asm].setdefault((c, a, x), [s, e])
                r[0], r[1] = min(r[0], s), max(r[1], e)
        # Coarser source bands must tile exactly onto their 850 sub-bands.
        for lvl in (400, 550):
            off = [
                _name(_key(sb))
                for sb in ncbi[(asm, lvl)]
                if (r := coords[asm].get(_key(sb)))
                and (abs(r[0] - sb.start) > TOLERANCE or abs(r[1] - sb.end) > TOLERANCE)
            ]
            if off:
                problems.append(
                    f"{asm} NCBI {lvl}: coordinates don't match sub-bands: {_listed(off)}"
                )

    # Stain from the finest source level that has the name (GRCh38 files).
    stain: dict[tuple[str, str, str], str] = {}
    for lvl in LEVELS:
        for sb in ncbi[("GRCh38", lvl)]:
            stain[_key(sb)] = sb.stain

    entries = []
    for k in coords["GRCh38"]:
        c, a, b = k
        levels = [lvl for lvl in LEVELS if k in sets[("GRCh38", lvl)]]
        p = parent_band(b)
        entries.append(
            {
                "name": _name(k),
                "chrom": c,
                "arm": a,
                "parent": f"{c}{a}{p}" if p else None,
                "levels": levels,
                "derived": not levels,
                "stain": stain.get(k),
                "grch37": coords["GRCh37"].get(k),
                "grch38": coords["GRCh38"][k],
                "_sort": (CHROM_INDEX[c], coords["GRCh38"][k][0], len(lineage(b))),
            }
        )
    entries.sort(key=lambda e: e["_sort"])
    for e in entries:
        del e["_sort"]
    return entries, problems


def render(
    version: str,
    sources: list[Source],
    data: dict[str, bytes],
    entries,
    unavailable,
    levels=LEVELS,
    extra_sources: list[dict] | None = None,
):
    lines = [
        f"# Band data for ISCN {version}. Generated by `kb bands` (T-006); do not edit by hand.",
        "# Sources: NCBI Genome Decoration Page ideograms (400/550/850 band sets) and UCSC",
        "# cytoBand (850 level; cross-check + canonical coordinates). Public data; see",
        "# docs/decisions.md (T-006). Per band: levels = source band sets containing the name;",
        "# derived = nesting ancestor only; stain = finest source level with the name;",
        "# grch37/grch38 = [start, end] in bp, 1-based closed.",
    ]
    if extra_sources:
        lines += [
            "# Book (T-007): the idiogram labels (kb/bands/<version>/ideograms.yaml) give levels",
            "# 300/700, and every book vs NCBI difference is decided in",
            "# curated/bands/<version>/exceptions.yaml (the book wins unless an exception says",
            "# otherwise). book = the name is shown in the book's idiograms or is an ancestor of",
            "# one; book-only names have no coordinates or stain.",
        ]
    lines += [
        f"version: '{version}'",
        f"generator_version: {GENERATOR_VERSION}",
        f"levels_available: {scalar(list(levels))}",
        f"levels_unavailable: {scalar(list(unavailable))}",
        "sources:",
    ]
    for s in sources:
        meta = {
            "id": s.id,
            "kind": s.kind,
            "assembly": s.assembly,
            "level": s.level,
            "url": s.url,
            "sha256": sha256(data[s.id]),
            "bytes": len(data[s.id]),
        }
        lines.append(f"  - {flow(meta)}")
    lines += [f"  - {flow(x)}" for x in extra_sources or []]
    derived = sum(e["derived"] for e in entries)
    counts = {"bands": len(entries), "derived": derived}
    counts |= {f"level_{lvl}": sum(lvl in e["levels"] for e in entries) for lvl in levels}
    if extra_sources:
        counts["not_in_book"] = sum(not e["book"] for e in entries)
        counts["no_coordinates"] = sum(e["grch38"] is None for e in entries)
    lines.append(f"counts: {flow(counts)}")
    lines.append("bands:")
    lines += [f"  - {flow(e)}" for e in entries]
    return "\n".join(lines) + "\n", counts


def run(
    version: str,
    out_dir: Path,
    cache_dir: Path,
    cache_only: bool = False,
    repin: bool = False,
    sources_path: Path | None = None,
    book_dir: Path | None = None,
    review_dir: Path | None = None,
) -> Report:
    """`book_dir` holds ideograms.yaml (default kb/bands/<version>) and, when given, also
    exceptions.yaml (default curated/bands/<version>/exceptions.yaml);
    `review_dir` receives exceptions-draft.yaml on unexplained differences
    (default build/review/bands/<version>)."""
    report = Report()
    sources_path = sources_path or sources_file(version)
    sources, cfg = load_sources(version, sources_path)
    data: dict[str, bytes] = {}
    for s in sources:
        try:
            data[s.id] = fetch(s, cache_dir, cache_only)
        except OSError as exc:
            report.problems.append(f"{s.id}: {exc}")
    if report.problems:
        return report

    shas = {s.id: sha256(data[s.id]) for s in sources}
    if repin:
        changed = {s.id: shas[s.id] for s in sources if s.sha256 != shas[s.id]}
        if changed:
            update_pins(sources_path, changed)
            report.pinned = sorted(changed)
    else:
        for s in sources:
            if not s.sha256:
                report.problems.append(f"{s.id}: no sha256 pin; run `kb bands --update-pins`")
            elif s.sha256 != shas[s.id]:
                report.problems.append(
                    f"{s.id}: sha256 mismatch (pinned {s.sha256[:12]}…, got {shas[s.id][:12]}…); "
                    f"upstream changed or the cache is stale. Delete {cache_dir / s.cache_name} "
                    "to re-download, or re-pin with --update-pins and review the diff"
                )
        if report.problems:
            return report

    ncbi: dict[tuple[str, int], list[SourceBand]] = {}
    ucsc: dict[str, list[SourceBand]] = {}
    for s in sources:
        if s.kind == "ncbi_gdp":
            ncbi[(s.assembly, s.level)] = parse_ncbi_gdp(data[s.id])
        elif s.kind == "ucsc_cytoband":
            ucsc[s.assembly] = parse_ucsc_cytoband(data[s.id])
        else:
            report.problems.append(f"{s.id}: unknown kind {s.kind!r}")
    missing = [f"ncbi {a} {lvl}" for a in ASSEMBLIES for lvl in LEVELS if (a, lvl) not in ncbi]
    missing += [f"ucsc {a}" for a in ASSEMBLIES if a not in ucsc]
    if missing:
        report.problems.append(f"sources missing: {', '.join(missing)}")
    if report.problems:
        return report

    entries, problems = build(ncbi, ucsc)
    report.problems += problems
    if report.problems:
        return report

    levels, extra = LEVELS, None
    if cfg.get("ideograms"):
        from ..config import CURATED_DIR, KB_DIR, REPO_ROOT
        from .book import apply, load_book
        from .ideograms import LEVELS as BOOK_LEVELS

        exc_path = (book_dir or CURATED_DIR / "bands" / version) / "exceptions.yaml"
        book_dir = book_dir or KB_DIR / "bands" / version
        try:
            book = load_book(book_dir, version, exc_path)
        except (FileNotFoundError, ValueError) as exc:
            report.problems.append(str(exc))
            return report
        applied = apply(version, entries, book)
        if applied.problems:
            report.problems += applied.problems
            if applied.draft:
                review_dir = review_dir or REPO_ROOT / "build" / "review" / "bands" / version
                review_dir.mkdir(parents=True, exist_ok=True)
                report.draft = review_dir / "exceptions-draft.yaml"
                report.draft.write_bytes(applied.draft.encode("utf-8"))
            return report
        entries, levels = applied.entries, BOOK_LEVELS
        extra = []
        book_files = (
            ("ideograms.yaml", "book_ideograms", book_dir / "ideograms.yaml", "kb"),
            ("exceptions.yaml", "exceptions", exc_path, "curated"),
        )
        for name, kind, f, root in book_files:
            if f.exists():
                raw = f.read_bytes().replace(b"\r\n", b"\n")  # checkout-independent
                extra.append(
                    {
                        "id": f"book-{name.removesuffix('.yaml')}",
                        "kind": kind,
                        "file": f"{root}/bands/{version}/{name}",
                        "sha256": sha256(raw),
                        "bytes": len(raw),
                    }
                )

    text, report.counts = render(
        version,
        sources,
        data,
        entries,
        cfg.get("levels_unavailable") or [],
        levels,
        extra,
    )
    out_dir.mkdir(parents=True, exist_ok=True)
    report.out = out_dir / "bands.yaml"
    report.out.write_bytes(text.encode("utf-8"))
    return report
