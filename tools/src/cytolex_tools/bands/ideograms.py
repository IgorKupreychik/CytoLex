"""Decode the band labels of the book's idiograms (ISCN 2016 Fig. 5) from the PDF text layer.

The labels are drawn in a figure-only font whose glyphs map to private-use code points
(U+F001 = '3', U+F003 = '.', …); the map is configured per edition in
`bands/sources/<version>.yaml` → `ideograms.glyphs`. Layout per page: one chromosome per
row, its number in bold below the row; five idiograms per row, left to right
300/400/550/700/850 bands; each label sits left of its idiogram (or further left with a
leader line). Output: `kb/bands/<version>/ideograms.yaml` (committed) + review crops.
"""

import re
from dataclasses import dataclass, field
from pathlib import Path

from ..extract.editions import get_edition
from .common import flow, lineage
from .fetch import load_sources, sources_file
from .parse import CHROM_INDEX

DECODER_VERSION = 1
LEVELS = (300, 400, 550, 700, 850)
SAME_LINE = 0.5  # pt; glyphs of one label share a baseline
ADJACENT = 0.3  # pt; next glyph starts where the previous one ends
COLUMN_GAP = 2.0  # pt; curves closer than this belong to one idiogram
LEFT_SLACK = 1.0  # pt; a label may touch its idiogram
MIN_ARM = 20.0  # pt; outline curves at least this tall are arms (smaller: centromere, stalks)
RANGE_DASH = "–"
LABEL_RE = re.compile(r"^(\d+(?:\.\d+)?)(?:–(\d+(?:\.\d+)?))?$")


@dataclass
class Label:
    text: str
    x0: float
    x1: float
    top: float
    size: float


@dataclass
class Report:
    out: Path | None = None
    problems: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    counts: dict[str, int] = field(default_factory=dict)
    crops: list[Path] = field(default_factory=list)


@dataclass(frozen=True)
class IdeogramConfig:
    figure: str
    section: str
    font: str
    chromosome_font: str
    chromosome_min_size: float
    first_page: int
    last_page: int
    glyphs: dict[str, str]
    spot_check: tuple[dict, ...]


def load_config(version: str, path: Path | None = None) -> IdeogramConfig:
    _, cfg = load_sources(version, path or sources_file(version))
    ide = cfg.get("ideograms")
    if not ide:
        raise KeyError(f"no `ideograms` section in the band sources for ISCN {version}")
    glyphs = {chr(int(k.removeprefix("U+"), 16)): str(v) for k, v in ide["glyphs"].items()}
    first, last = ide["pages"]
    return IdeogramConfig(
        figure=ide["figure"],
        section=str(ide["section"]),
        font=ide["font"],
        chromosome_font=ide["chromosome_font"],
        chromosome_min_size=float(ide["chromosome_min_size"]),
        first_page=int(first),
        last_page=int(last),
        glyphs=glyphs,
        spot_check=tuple(ide.get("spot_check") or ()),
    )


# --- pure helpers (unit-tested) ----------------------------------------------------------------


def group_labels(chars: list[dict], glyphs: dict[str, str]) -> tuple[list[Label], list[str]]:
    """Join figure-font glyphs (in content-stream order) into labels and decode them.

    A label continues while the next glyph is on the same baseline and starts where the
    previous one ends; anything else starts a new label. Word grouping by y-overlap is not
    enough: stacked labels with leader lines overlap vertically.
    """
    labels: list[Label] = []
    unknown: set[str] = set()
    cur: list[dict] = []

    def flush():
        if cur:
            text = "".join(glyphs.get(c["text"], "\0") for c in cur)
            unknown.update(c["text"] for c in cur if c["text"] not in glyphs)
            labels.append(
                Label(text, cur[0]["x0"], cur[-1]["x1"], cur[0]["top"], round(cur[0]["size"], 2))
            )
            cur.clear()

    for c in chars:
        if cur and (
            abs(c["top"] - cur[-1]["top"]) > SAME_LINE or abs(c["x0"] - cur[-1]["x1"]) > ADJACENT
        ):
            flush()
        cur.append(c)
    flush()
    problems = [f"unmapped glyph U+{ord(u):04X}" for u in sorted(unknown)]
    return labels, problems


def merge_intervals(spans: list[tuple[float, float]], gap: float = COLUMN_GAP):
    """Merge x-intervals that overlap or lie within `gap` of each other."""
    out: list[list[float]] = []
    for a, b in sorted(spans):
        if out and a <= out[-1][1] + gap:
            out[-1][1] = max(out[-1][1], b)
        else:
            out.append([a, b])
    return [(a, b) for a, b in out]


def column_for(label: Label, columns: list[tuple[float, float]]) -> int | None:
    """Index of the nearest idiogram whose left edge is right of the label."""
    best = None
    for i, (x0, _x1) in enumerate(columns):
        if x0 >= label.x1 - LEFT_SLACK and (best is None or x0 < columns[best][0]):
            best = i
    return best


def sort_key(label: str) -> tuple[int, ...]:
    """'36.33' -> (3, 6, 3, 3); a range sorts by its first band."""
    m = LABEL_RE.match(label)
    if not m:
        raise ValueError(f"not a band label: {label!r}")
    return tuple(int(d) for d in m.group(1) if d.isdigit())


def split_candidates(labels: list[str]) -> list[int]:
    """Split points k: labels[:k] (p arm, top→down) strictly decrease, labels[k:] increase."""
    keys = [sort_key(x) for x in labels]
    n = len(keys)
    d = 1 if n else 0  # end of the strictly decreasing prefix
    while d < n and keys[d] < keys[d - 1]:
        d += 1
    s = n - 1 if n else 0  # start of the strictly increasing suffix
    while s > 0 and keys[s - 1] < keys[s]:
        s -= 1
    return list(range(s, d + 1)) if s <= d else []


def expand_range(label: str, finer: list[str]) -> list[str]:
    """'22–24' -> the bands of `finer` (same depth) from 22 to 24; a plain label -> [label]."""
    m = LABEL_RE.match(label)
    if not m or not m.group(2):
        return [label]
    lo, hi = sort_key(m.group(1)), sort_key(m.group(2))
    depth = m.group(1).count(".")
    picked = [
        b
        for b in finer
        if RANGE_DASH not in b and b.count(".") == depth and lo <= sort_key(b) <= hi
    ]
    return sorted(set(picked) | {m.group(1), m.group(2)}, key=sort_key)


# --- PDF ---------------------------------------------------------------------------------------


@dataclass
class Row:
    chrom: str
    page: int
    top: float
    bottom: float
    columns: list[tuple[float, float]]
    labels: dict[int, list[Label]]  # column index -> labels top→down
    centromeres: dict[int, float] = field(default_factory=dict)  # column -> y (top of q arm)


def _rows(page, book_page: int, cfg: IdeogramConfig, report: Report) -> list[Row]:
    numbers = sorted(
        (
            w
            for w in page.extract_words(extra_attrs=["fontname", "size"])
            if cfg.chromosome_font in w["fontname"]
            and w["size"] >= cfg.chromosome_min_size
            and w["text"] in CHROM_INDEX
        ),
        key=lambda w: w["top"],
    )
    if not numbers:
        report.problems.append(f"p.{book_page}: no chromosome numbers found")
        return []
    rows, prev = [], 0.0
    for w in numbers:
        curves = [c for c in page.curves if prev <= (c["top"] + c["bottom"]) / 2 <= w["top"]]
        cols = merge_intervals([(c["x0"], c["x1"]) for c in curves])
        if len(cols) != len(LEVELS):
            report.problems.append(
                f"p.{book_page} chr{w['text']}: {len(cols)} idiogram columns, "
                f"expected {len(LEVELS)}"
            )
        row = Row(w["text"], book_page, prev, w["top"], cols, {})
        for i, (x0, x1) in enumerate(cols):
            mine = [c for c in curves if x0 <= c["x0"] and c["x1"] <= x1]
            arms = [c for c in mine if c["bottom"] - c["top"] >= MIN_ARM]
            if not arms:
                continue
            # The q arm is the lowest outline and starts at the centromere; the small
            # centromere piece drawn across that point gives its middle.
            q_top = max(arms, key=lambda c: c["bottom"])["top"]
            pieces = [
                c
                for c in mine
                if c["bottom"] - c["top"] < MIN_ARM and c["top"] <= q_top <= c["bottom"]
            ]
            row.centromeres[i] = (
                (pieces[0]["top"] + pieces[0]["bottom"]) / 2 if len(pieces) == 1 else q_top
            )
        rows.append(row)
        prev = w["top"]
    return rows


def _mid(lb: Label) -> float:
    return lb.top + lb.size / 2


def split_at(labels: list[Label], cen: float | None) -> tuple[int | None, list[int]]:
    """Arm split: the label order decides; the drawn centromere breaks ties."""
    cands = split_candidates([lb.text for lb in labels])
    if len(cands) == 1:
        return cands[0], cands
    if cen is None:
        return None, cands
    fit = [
        k
        for k in cands
        if (k == 0 or _mid(labels[k - 1]) < cen) and (k == len(labels) or _mid(labels[k]) > cen)
    ]
    return (fit[0] if len(fit) == 1 else None), cands


def decode(pdf_path: Path, version: str, cfg: IdeogramConfig, report: Report):
    """Return (entries, rows, pdf) with entries = {chrom, level, arm, label, page} in order."""
    import pdfplumber

    offset = get_edition(version).page_offset
    pdf = pdfplumber.open(pdf_path)
    rows: list[Row] = []
    for book_page in range(cfg.first_page, cfg.last_page + 1):
        page = pdf.pages[book_page + offset - 1]
        chars = [c for c in page.chars if cfg.font in c["fontname"]]
        labels, problems = group_labels(chars, cfg.glyphs)
        report.problems += [f"p.{book_page}: {p}" for p in problems]
        page_rows = _rows(page, book_page, cfg, report)
        for lb in labels:
            if lb.text in ("p", "q"):  # arm letters beside the first idiogram
                continue
            row = next((r for r in page_rows if r.top <= lb.top < r.bottom), None)
            col = column_for(lb, row.columns) if row else None
            if row is None or col is None:
                report.problems.append(
                    f"p.{book_page}: label {lb.text!r} at ({lb.x0:.0f}, {lb.top:.0f}) "
                    "belongs to no idiogram"
                )
                continue
            if not LABEL_RE.match(lb.text):
                report.problems.append(f"p.{book_page} chr{row.chrom}: bad label {lb.text!r}")
                continue
            row.labels.setdefault(col, []).append(lb)
        rows += page_rows

    entries = []
    for row in rows:
        cols = {c: sorted(v, key=lambda lb: lb.top) for c, v in row.labels.items()}
        splits = {}
        for c, labels in cols.items():
            k, cands = split_at(labels, row.centromeres.get(c))
            if k is None:
                report.problems.append(
                    f"p.{row.page} chr{row.chrom} {LEVELS[c]}: cannot split arms "
                    f"(candidates {cands}): {[lb.text for lb in labels]}"
                )
            elif len(cands) > 1:
                report.counts["split_by_centromere"] = (
                    report.counts.get("split_by_centromere", 0) + 1
                )
            splits[c] = k
        for c in sorted(cols):
            if splits[c] is None:
                continue
            for i, lb in enumerate(cols[c]):
                arm = "p" if i < splits[c] else "q"
                entries.append(
                    {
                        "chrom": row.chrom,
                        "level": LEVELS[c],
                        "arm": arm,
                        "label": lb.text,
                        "page": row.page,
                    }
                )
    seen = {r.chrom for r in rows}
    missing = [c for c in CHROM_INDEX if c not in seen]
    if missing:
        report.problems.append(f"chromosomes not found: {', '.join(missing)}")
    return entries, rows, pdf


def check(entries: list[dict], report: Report) -> None:
    """Duplicates per idiogram; each coarser level nests in the next finer one (warnings)."""
    by: dict[tuple[str, int], list[str]] = {}
    for e in entries:
        by.setdefault((e["chrom"], e["level"]), []).append(e["arm"] + e["label"])
    for (chrom, lvl), names in by.items():
        dup = sorted({n for n in names if names.count(n) > 1})
        if dup:
            report.problems.append(f"chr{chrom} {lvl}: duplicate labels {dup}")
    for chrom in CHROM_INDEX:
        for coarse, fine in zip(LEVELS, LEVELS[1:], strict=False):
            fine_names = by.get((chrom, fine), [])
            covered = set(fine_names)  # a range printed at both levels matches itself
            covered |= {n[0] + x for n in fine_names if RANGE_DASH not in n for x in lineage(n[1:])}
            for n in by.get((chrom, coarse), []):
                m = LABEL_RE.match(n[1:])
                parts = [m.group(1), m.group(2)] if m and m.group(2) else [n[1:]]
                miss = [p for p in parts if n not in covered and n[0] + p not in covered]
                if miss:
                    report.warnings.append(
                        f"chr{chrom}: {coarse}-level {n} has no {fine}-level equivalent"
                    )


# --- output ------------------------------------------------------------------------------------


def render(version: str, cfg: IdeogramConfig, entries: list[dict]) -> tuple[str, dict]:
    counts = {f"level_{lvl}": sum(e["level"] == lvl for e in entries) for lvl in LEVELS}
    counts["labels"] = len(entries)
    counts["ranges"] = sum(RANGE_DASH in e["label"] for e in entries)
    lines = [
        f"# Band labels of the ISCN {version} idiograms ({cfg.figure}, §{cfg.section}, book "
        f"pp.{cfg.first_page}–{cfg.last_page}),",
        "# decoded from the PDF text layer by `kb ideograms` (T-007); do not edit by hand.",
        "# One entry per label: idiogram level (300/400/550/700/850), arm, label as printed",
        "# (a 300-level range such as '22–24' is one merged band), book page. Order: chromosome,",
        "# level, then top to bottom (p arm pter → cen, q arm cen → qter).",
        f"version: '{version}'",
        f"decoder_version: {DECODER_VERSION}",
        f"source: {flow({'figure': cfg.figure, 'section': cfg.section})}",
        f"counts: {flow(counts)}",
        "labels:",
    ]
    lines += [f"  - {flow(e)}" for e in entries]
    return "\n".join(lines) + "\n", counts


def _crops(rows: list[Row], pdf, version: str, entries, out_dir: Path, report: Report):
    offset = get_edition(version).page_offset
    out_dir.mkdir(parents=True, exist_ok=True)
    for row in rows:
        if not row.columns:
            continue
        xs = [lb.x0 for v in row.labels.values() for lb in v] + [row.columns[0][0]]
        page = pdf.pages[row.page + offset - 1]
        tops = [lb.top for v in row.labels.values() for lb in v]
        top = max(row.top, min(tops) - 12) if tops else row.top
        box = (max(0, min(xs) - 15), top, min(page.width, row.columns[-1][1] + 15), row.bottom)
        png = out_dir / f"chr{row.chrom}.png"
        page.crop(box).to_image(resolution=300).save(png)
        text = [f"chr{row.chrom}, book p.{row.page}"]
        for lvl in LEVELS:
            mine = [e for e in entries if e["chrom"] == row.chrom and e["level"] == lvl]
            for arm in "pq":
                text.append(
                    f"{lvl} {arm}: " + " ".join(e["label"] for e in mine if e["arm"] == arm)
                )
        (out_dir / f"chr{row.chrom}.txt").write_text("\n".join(text) + "\n", encoding="utf-8")
        report.crops.append(png)


def run(
    version: str,
    pdf_path: Path,
    out_dir: Path,
    review_dir: Path | None = None,
    sources_path: Path | None = None,
) -> Report:
    report = Report()
    cfg = load_config(version, sources_path)
    entries, rows, pdf = decode(pdf_path, version, cfg, report)
    try:
        if not report.problems:
            check(entries, report)
        if report.problems:
            return report
        entries.sort(key=lambda e: (CHROM_INDEX[e["chrom"]], e["level"]))  # stable: keeps order
        text, counts = render(version, cfg, entries)
        report.counts |= counts
        out_dir.mkdir(parents=True, exist_ok=True)
        report.out = out_dir / "ideograms.yaml"
        report.out.write_bytes(text.encode("utf-8"))
        if review_dir is not None:
            _crops(rows, pdf, version, entries, review_dir, report)
    finally:
        pdf.close()
    return report
