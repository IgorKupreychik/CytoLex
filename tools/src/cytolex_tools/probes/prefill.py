"""`kb probes`: pre-fill gene rows of the probe table from HGNC + NCBI (T-102).

Sources (pinned in `curated/probes/sources.yaml`, cached in `build/cache/probes/`):
- HGNC complete set (CC0): approved symbol, previous symbols, location (band), NCBI Gene ID;
- NCBI RefSeq annotation GFF per build (public domain): the `gene` feature of each Gene ID
  on a primary chromosome (`NC_0000NN`) gives the GRCh37/GRCh38 positions.

Rows written here end their `source` with `(auto)` and have `reviewed: no`. Only such rows are
ever changed; a reviewed row or a person's row with the same name is left alone.
"""

import csv
import gzip
import io
import re
from dataclasses import dataclass, field
from pathlib import Path

from ruamel.yaml import YAML

from .. import downloads
from ..config import CURATED_DIR, REPO_ROOT
from . import table
from .names import example_probe_names
from .table import AUTO_MARK, BUILDS, Row

SOURCES_FILE = CURATED_DIR / "probes" / "sources.yaml"
CACHE_DIR = REPO_ROOT / "build" / "cache" / "probes"
NC_RE = re.compile(r"^NC_0000(\d\d)\.\d+$")
LOCATION_RE = re.compile(
    r"^([1-9]|1[0-9]|2[0-2]|X|Y)([pq][0-9]+(?:\.[0-9]+)?)(?:-([pq][0-9]+(?:\.[0-9]+)?))?$"
)
GENE_ID_RE = re.compile(r"GeneID:(\d+)")


@dataclass(frozen=True)
class Source:
    id: str
    kind: str  # hgnc | ncbi_gff
    url: str
    sha256: str
    label: str  # how the source is named in the `source` column
    build: str | None = None  # GRCh37 | GRCh38 for ncbi_gff

    @property
    def cache_name(self) -> str:
        return downloads.cache_name(self.id, self.url)


@dataclass
class Gene:
    symbol: str
    gene_id: str
    location: str
    previous: list[str]


@dataclass
class Report:
    added: list[str] = field(default_factory=list)
    refreshed: list[str] = field(default_factory=list)
    kept: list[str] = field(default_factory=list)  # names a person's row already has
    not_found: list[str] = field(default_factory=list)  # requested, not an HGNC symbol
    notes: list[str] = field(default_factory=list)
    problems: list[str] = field(default_factory=list)
    pinned: list[str] = field(default_factory=list)
    out: Path | None = None


def load_sources(path: Path = SOURCES_FILE) -> list[Source]:
    data = YAML(typ="safe").load(path.read_text(encoding="utf-8"))
    return [
        Source(f["id"], f["kind"], f["url"], f.get("sha256") or "", f["label"], f.get("build"))
        for f in data["files"]
    ]


# --- parsing -----------------------------------------------------------------------------


def parse_hgnc(data: bytes) -> dict[str, Gene]:
    """Approved genes by symbol."""
    reader = csv.DictReader(io.StringIO(data.decode("utf-8")), delimiter="\t")
    out = {}
    for rec in reader:
        if rec.get("status") != "Approved":
            continue
        prev = [p for p in (rec.get("prev_symbol") or "").strip('"').split("|") if p]
        out[rec["symbol"]] = Gene(
            rec["symbol"], rec.get("entrez_id") or "", rec.get("location") or "", prev
        )
    return out


def parse_gff_genes(data: bytes, wanted: set[str]) -> dict[str, list[tuple[str, int, int]]]:
    """Gene ID -> [(chromosome, start, end)] of its `gene` features on primary chromosomes."""
    out: dict[str, list[tuple[str, int, int]]] = {}
    stream = (
        gzip.open(io.BytesIO(data), "rt", encoding="utf-8")
        if data[:2] == b"\x1f\x8b"
        else io.StringIO(data.decode("utf-8"))
    )
    with stream:
        for line in stream:
            if line.startswith("#"):
                continue
            cols = line.split("\t", 8)
            if len(cols) < 9 or cols[2] not in ("gene", "pseudogene"):
                continue
            m = NC_RE.match(cols[0])
            if not m:
                continue
            g = GENE_ID_RE.search(cols[8])
            if not g or g[1] not in wanted:
                continue
            n = int(m[1])
            chrom = {23: "X", 24: "Y"}.get(n, str(n))
            if n > 24:
                continue
            out.setdefault(g[1], []).append((chrom, int(cols[3]), int(cols[4])))
    return out


def band_of(location: str) -> tuple[str, str] | None:
    """HGNC location `9q34.12` / `22q11.21-q11.22` -> (chromosome, band cell)."""
    m = LOCATION_RE.match(location.strip())
    return (m[1], location.strip()) if m else None


# --- rows --------------------------------------------------------------------------------


def gene_row(
    gene: Gene, positions: dict[str, list[tuple[str, int, int]]], source: str, aliases: list[str]
) -> Row:
    band = band_of(gene.location)
    chrom = band[0] if band else ""
    cells = {c: "" for c in table.COLUMNS}
    cells |= {
        "probe": gene.symbol,
        "aliases": ";".join(aliases),
        "kind": "gene",
        "band": band[1] if band else "",
        "source": source,
        "reviewed": "no",
    }
    notes = []
    for build in BUILDS:
        places = positions.get(build, [])
        if chrom:
            places = [p for p in places if p[0] == chrom] or places
        if places:
            c, start, end = places[0]
            chrom = chrom or c
            cells[build] = f"{start}-{end}"
        else:
            notes.append(f"no {build.replace('grch', 'GRCh')} position in NCBI")
    if not band and gene.location:
        notes.append(f"HGNC location {gene.location!r} is not a band")
    cells["chromosome"] = chrom
    cells["note"] = "; ".join(notes)
    return Row(0, cells)


def _sort_key(row: Row) -> tuple:
    chrom = row["chromosome"]
    order = {"X": 23, "Y": 24}.get(chrom, int(chrom) if chrom.isdigit() else 99)
    pos = row.range("grch38") or row.range("grch37") or (0, 0)
    return order, pos[0], row.name


def merge(rows: list[Row], genes: list[Row], report: Report) -> list[Row]:
    """Refresh auto rows, append new gene rows (chromosome/position order), keep the rest."""
    by_name = {g.name: g for g in genes}
    out = []
    taken = set()
    for row in rows:
        new = by_name.get(row.name)
        if new is not None and row.is_auto:
            if new.cells != row.cells:
                report.refreshed.append(row.name)
            out.append(Row(row.line, new.cells))
            taken.add(row.name)
        else:
            if new is not None:
                report.kept.append(row.name)
                taken.add(row.name)
            out.append(row)
    names = {n.casefold() for r in rows for n in (r.name, *r.aliases)}
    added = []
    for g in genes:
        if g.name in taken:
            continue
        if g.name.casefold() in names:
            report.kept.append(g.name)  # a person's row has it as an alias
            continue
        added.append(g)
    report.added = sorted(g.name for g in added)
    return out + sorted(added, key=_sort_key)


def resolve(names: set[str], hgnc: dict[str, Gene], report: Report) -> dict[str, Gene]:
    """Requested names -> genes (approved symbol, else a previous symbol of exactly one gene)."""
    previous: dict[str, list[str]] = {}
    for g in hgnc.values():
        for p in g.previous:
            previous.setdefault(p, []).append(g.symbol)
    out = {}
    for name in sorted(names):
        if name in hgnc:
            out[name] = hgnc[name]
        elif len(previous.get(name, [])) == 1:
            out[name] = hgnc[previous[name][0]]
            report.notes.append(f"{name}: previous symbol of {out[name].symbol}")
        elif name in previous:
            report.notes.append(
                f"{name}: previous symbol of several genes ({', '.join(previous[name])}); not added"
            )
        else:
            report.not_found.append(name)
    return out


def gene_aliases(
    gene: Gene, requested: list[str], previous_owners: dict[str, int], approved: dict[str, Gene]
) -> list[str]:
    """HGNC previous symbols that belong to this gene only and aren't another gene's approved
    symbol (so aliases stay unique); a requested old name always."""
    out = [p for p in gene.previous if previous_owners.get(p, 0) == 1 and p not in approved]
    out += [r for r in requested if r != gene.symbol and r not in out]
    return out


# --- entry point -------------------------------------------------------------------------


def run(
    add: list[str],
    from_kb_examples: bool,
    table_path: Path = table.PROBES_FILE,
    cache_dir: Path = CACHE_DIR,
    cache_only: bool = False,
    repin: bool = False,
    sources_path: Path = SOURCES_FILE,
    rules_dir: Path = CURATED_DIR / "rules",
) -> Report:
    report = Report()
    existing = table.read(table_path) if table_path.exists() else table.Table(table_path, [], [])
    if existing.problems:
        report.problems += [str(p) for p in existing.problems]
        return report
    sources = load_sources(sources_path)
    data: dict[str, bytes] = {}
    for s in sources:
        try:
            data[s.id] = downloads.fetch(s.id, s.url, cache_dir, s.cache_name, cache_only)
        except OSError as exc:
            report.problems.append(f"{s.id}: {exc}")
    if report.problems:
        return report
    shas = {s.id: downloads.sha256(data[s.id]) for s in sources}
    if repin:
        changed = {s.id: shas[s.id] for s in sources if s.sha256 != shas[s.id]}
        if changed:
            downloads.update_pins(sources_path, changed)
            report.pinned = sorted(changed)
    else:
        report.problems += downloads.pin_problems(
            {s.id: s.sha256 for s in sources},
            shas,
            cache_dir,
            "kb probes",
            {s.id: s.cache_name for s in sources},
        )
        if report.problems:
            return report

    hgnc_src = [s for s in sources if s.kind == "hgnc"]
    gff_src = {s.build: s for s in sources if s.kind == "ncbi_gff"}
    if len(hgnc_src) != 1 or set(gff_src) != {"GRCh37", "GRCh38"}:
        report.problems.append("sources: need one `hgnc` and `ncbi_gff` for GRCh37 and GRCh38")
        return report
    hgnc = parse_hgnc(data[hgnc_src[0].id])

    requested = set(add)
    if from_kb_examples:
        requested |= example_probe_names(_rules(rules_dir))
    requested |= {r.name for r in existing.rows if r.is_auto}
    found = resolve(requested, hgnc, report)
    # names from the examples that aren't genes are listed by the review report instead
    report.not_found = [n for n in report.not_found if n in add]
    by_gene: dict[str, list[str]] = {}
    for name, gene in found.items():
        by_gene.setdefault(gene.symbol, []).append(name)
    owners: dict[str, int] = {}
    for g in hgnc.values():
        for p in g.previous:
            owners[p] = owners.get(p, 0) + 1
    wanted = {hgnc[s].gene_id for s in by_gene if hgnc[s].gene_id}
    positions = {
        f"grch{b[-2:]}": parse_gff_genes(data[gff_src[b].id], wanted) for b in ("GRCh37", "GRCh38")
    }
    label = (
        f"{hgnc_src[0].label} + {gff_src['GRCh38'].label} + {gff_src['GRCh37'].label} {AUTO_MARK}"
    )
    rows = []
    for symbol, names in sorted(by_gene.items()):
        gene = hgnc[symbol]
        pos = {b: p.get(gene.gene_id, []) for b, p in positions.items()}
        rows.append(gene_row(gene, pos, label, gene_aliases(gene, names, owners, hgnc)))
    merged = merge(existing.rows, rows, report)
    table.write(table_path, merged)
    report.out = table_path
    return report


def _rules(rules_dir: Path):
    yaml = YAML(typ="safe")
    for f in sorted(rules_dir.rglob("*.yaml")):
        yield from (yaml.load(f.read_text(encoding="utf-8")) or {}).get("rules") or []
