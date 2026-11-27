"""`kb extract`: ISCN PDF -> per-section Markdown chunks + manifest (deterministic, no AI)."""

import hashlib
import re
from dataclasses import dataclass, field
from pathlib import Path

import pdfplumber

from .blocks import build_blocks, build_sections, hyphen_compounds
from .chunker import TOKEN_CAP, Chunk, chunk_section, dump_yaml
from .editions import get_edition
from .glyphs import GlyphMapper
from .layout import page_lines
from .tables import TableRegion, divert_tables, load_tables
from .toc import parse_toc

EXTRACTOR_VERSION = 2


@dataclass
class Report:
    out_dir: Path
    sections: int = 0
    chunks: int = 0
    max_tokens: int = 0
    problems: list[str] = field(default_factory=list)  # TOC mismatches: fail the run
    warnings: list[str] = field(default_factory=list)


def _norm_title(t: str) -> str:
    return re.sub(r"[^a-z0-9]", "", t.lower())


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def clear_output(out_dir: Path) -> None:
    """Remove previous output only (chunk files and manifest), nothing else."""
    for p in sorted(out_dir.glob("ch*/*.md")):
        p.unlink()
    for d in sorted(out_dir.glob("ch*")):
        if d.is_dir() and not any(d.iterdir()):
            d.rmdir()
    manifest = out_dir / "manifest.yaml"
    if manifest.exists():
        manifest.unlink()


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode("utf-8"))  # UTF-8, LF line endings on every OS


def run(version: str, pdf_path: Path, out_dir: Path, cap: int = TOKEN_CAP) -> Report:
    ed = get_edition(version)
    mapper = GlyphMapper()
    report = Report(out_dir=out_dir)
    specs = load_tables(version)
    tables: dict[str, TableRegion] = {}

    with pdfplumber.open(pdf_path) as pdf:
        toc_lines: list[str] = []
        first, last = ed.toc_pdf_pages
        for n in range(first, last + 1):
            toc_lines += [ln.text for ln in page_lines(pdf.pages[n - 1], n, ed, GlyphMapper())[0]]
        toc = [e for e in parse_toc(toc_lines) if ed.first_chapter <= e.chapter <= ed.last_chapter]

        lines = []
        figure_pages: list[int] = []
        for n in range(last + 1, len(pdf.pages) + 1):
            page, has_figure = page_lines(pdf.pages[n - 1], n, ed, mapper)
            lines += divert_tables(page, specs, tables)
            if has_figure:
                figure_pages.append(n - ed.page_offset)

    for spec in specs:
        for i, r in enumerate(spec.regions):
            if f"{spec.id}#{i + 1}" not in tables:
                report.problems.append(f"{spec.id}: no text in region {i + 1} (p.{r.book_page})")

    blocks = build_blocks(lines, ed)
    sections = build_sections(blocks, ed)
    compounds = hyphen_compounds(blocks)

    # TOC cross-check: every TOC section must be detected on its TOC page.
    found = {s.number: s for s in sections}
    for e in toc:
        s = found.get(e.number)
        if s is None:
            report.problems.append(f"§{e.number} {e.title!r} (p.{e.book_page}): heading not found")
            continue
        if s.book_page != e.book_page:
            report.problems.append(
                f"§{e.number}: TOC says p.{e.book_page}, heading found on p.{s.book_page}"
            )
        if _norm_title(s.title) != _norm_title(e.title):
            report.warnings.append(f"§{e.number}: title {s.title!r} differs from TOC {e.title!r}")
    toc_numbers = {e.number for e in toc}

    chunks: list[Chunk] = []
    manifest_sections = []
    for s in sections:
        sc = chunk_section(version, s, compounds, cap, tables)
        chunks += sc
        book_pages, pdf_pages = s.pages()
        manifest_sections.append(
            {
                "section": s.number,
                "title": s.title,
                "level": s.level,
                "in_toc": s.number in toc_numbers,
                "book_pages": book_pages,
                "pdf_pages": pdf_pages,
                "files": [
                    {
                        "file": c.path,
                        "part": c.part,
                        "tokens": c.tokens,
                        "paragraphs": len(c.paragraphs),
                        "examples": len(c.examples),
                        **({"floats": list(c.floats)} if c.floats else {}),
                        **({"changed": c.changed} if c.changed else {}),
                        **({"underlined": c.underlined} if c.underlined else {}),
                    }
                    for c in sc
                ],
            }
        )

    manifest = {
        "version": version,
        "extractor_version": EXTRACTOR_VERSION,
        "source_pdf_sha256": sha256(pdf_path),
        "page_offset": ed.page_offset,
        "chapters": [ed.first_chapter, ed.last_chapter],
        "token_cap": cap,
        "token_estimate": "ceil(characters / 4), whole file incl. front matter",
        "conventions": {
            "paragraph": "¶n counts prose paragraphs (and small print not under an example) "
            "per numbered section; restarts at every numbered heading",
            "example": "En counts example blocks per numbered section; the small-print "
            "explanation after an example belongs to it",
            "page": "book page of the first line of the paragraph/example (book = PDF - offset)",
            "verbatim": "example lines are not normalised; stray spaces are only listed "
            "(front matter `stray_spaces`)",
            "float": "figure/table captions are labelled `[Fig. N p.X]` / `[Table N p.X]` and "
            "not counted as ¶; a table and its footnotes follow its caption",
            "table": "configured tables (curated/extract/tables/<version>.yaml) are Markdown "
            "tables built from word positions; front matter `tables` gives their review status",
            "underline": "an underlined character is followed by U+0332 COMBINING LOW LINE "
            "(e.g. `der(1` + U+0332 + `)`); front matter `underlined` lists the labels",
            "changed": "front matter `changed` lists labels next to a margin change bar "
            "(ISCN 2016 §1.6: changes vs. the previous edition are marked in the margin)",
            "legend": "FISH signal legends (`● = probe for ABL1`) belong to the example above",
        },
        "figure_pages": figure_pages,
        "tables": [
            {
                "id": spec.id,
                "kind": spec.kind,
                "book_pages": sorted({r.book_page for r in spec.regions}),
                "review": spec.review,
            }
            for spec in specs
        ],
        "glyphs": mapper.report(),
        "problems": report.problems,
        "warnings": report.warnings,
        "sections": manifest_sections,
    }

    out_dir.mkdir(parents=True, exist_ok=True)
    clear_output(out_dir)
    for c in chunks:
        write_text(out_dir / c.path, c.text)
    write_text(out_dir / "manifest.yaml", dump_yaml(manifest))

    report.sections = len(sections)
    report.chunks = len(chunks)
    report.max_tokens = max((c.tokens for c in chunks), default=0)
    return report
