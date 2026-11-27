"""`kb tables`: render each configured table region to PNG, for checking the extracted tables.

Writes `<out>/<table>-<n>.png` (the page area of each region) and `<out>/index.md`, which lists
every table with its review status and the chunk file holding its extracted Markdown. A person
compares the image with that Markdown and records the result in
`curated/extract/tables/<version>.yaml`.
"""

import re
from pathlib import Path

import pdfplumber
from ruamel.yaml import YAML

from .editions import get_edition
from .tables import load_tables

MARGIN = 6.0  # pt around the region
RESOLUTION = 130  # dpi


def file_stem(table_id: str, n: int) -> str:
    return f"{re.sub(r'[^a-z0-9]+', '-', table_id.lower()).strip('-')}-{n}"


def chunk_files(source_text: Path) -> dict[str, list[str]]:
    """Table id -> chunk files that contain it (front matter `tables`, written by `kb extract`)."""
    found: dict[str, list[str]] = {}
    for p in sorted(source_text.glob("ch*/*.md")):
        front = YAML(typ="safe").load(p.read_text(encoding="utf-8").split("---\n", 2)[1]) or {}
        for table_id in front.get("tables") or {}:
            found.setdefault(table_id, []).append(p.relative_to(source_text).as_posix())
    return found


def render(version: str, pdf_path: Path, out_dir: Path, source_text: Path) -> list[Path]:
    ed = get_edition(version)
    specs = load_tables(version)
    files = chunk_files(source_text)
    out_dir.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    index = [f"# ISCN {version} tables: images for review", ""]
    with pdfplumber.open(pdf_path) as pdf:
        for spec in specs:
            where = ", ".join(files.get(spec.id, [])) or "(run `kb extract` first)"
            index += [f"## {spec.id}", "", f"- review: {spec.review}", f"- chunk: {where}", ""]
            for n, r in enumerate(spec.regions, 1):
                page = pdf.pages[r.book_page + ed.page_offset - 1]
                box = (
                    max(0.0, r.columns[0] - MARGIN),
                    max(0.0, r.top - MARGIN),
                    min(float(page.width), ed.text_right + MARGIN),
                    min(float(page.height), r.bottom + 2 * MARGIN),
                )
                path = out_dir / f"{file_stem(spec.id, n)}.png"
                page.crop(box).to_image(resolution=RESOLUTION).save(path)
                written.append(path)
                index += [f"![{spec.id} part {n}, p.{r.book_page}]({path.name})", ""]
    (out_dir / "index.md").write_text("\n".join(index), encoding="utf-8")
    return written
