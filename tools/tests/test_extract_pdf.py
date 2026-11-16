"""Integration tests on the real ISCN PDF; skipped when it isn't configured on this machine."""

import re
from pathlib import Path

import pytest
from ruamel.yaml import YAML

from cytolex_tools.config import iscn_pdf_path
from cytolex_tools.extract import run
from cytolex_tools.extract.chunker import TOKEN_CAP, estimate_tokens


def _pdf() -> Path | None:
    try:
        path = iscn_pdf_path("2016")
    except FileNotFoundError, KeyError:
        return None
    return path if path.exists() else None


PDF = _pdf()
pytestmark = pytest.mark.skipif(PDF is None, reason="ISCN 2016 PDF not configured")


@pytest.fixture(scope="module")
def extracted(tmp_path_factory):
    out = tmp_path_factory.mktemp("st") / "2016"
    report = run("2016", PDF, out)
    return out, report


def tree(root: Path) -> dict[str, bytes]:
    return {
        p.relative_to(root).as_posix(): p.read_bytes()
        for p in sorted(root.rglob("*"))
        if p.is_file()
    }


def test_toc_sections_all_found(extracted):
    _, report = extracted
    assert report.problems == []


def test_every_toc_section_ch2_to_16_has_a_chunk(extracted):
    out, _ = extracted
    manifest = YAML(typ="safe").load((out / "manifest.yaml").read_text(encoding="utf-8"))
    toc_sections = [s for s in manifest["sections"] if s["in_toc"]]
    chapters = {int(s["section"].split(".")[0]) for s in toc_sections}
    assert set(range(2, 17)) <= chapters
    for s in toc_sections:
        assert s["files"], s["section"]
        for f in s["files"]:
            assert (out / f["file"]).is_file()


def test_rerun_is_byte_identical(extracted, tmp_path):
    out, _ = extracted
    again = tmp_path / "2016"
    run("2016", PDF, again)
    assert tree(out) == tree(again)


def test_no_chunk_exceeds_cap(extracted):
    out, _ = extracted
    sizes = {p.name: estimate_tokens(p.read_text(encoding="utf-8")) for p in out.rglob("*.md")}
    assert max(sizes.values()) <= TOKEN_CAP


def test_manifest_maps_section_to_file_and_pages(extracted):
    out, _ = extracted
    manifest = YAML(typ="safe").load((out / "manifest.yaml").read_text(encoding="utf-8"))
    s = next(s for s in manifest["sections"] if s["section"] == "9.2.17.1")
    assert s["book_pages"][0] == 75
    assert s["files"][0]["file"] == "ch09/09.02.17.01-reciprocal-translocations.md"


def test_reciprocal_translocation_content(extracted):
    out, _ = extracted
    text = (out / "ch09/09.02.17.01-reciprocal-translocations.md").read_text(encoding="utf-8")
    assert "[¶1 p.75] In translocations (t) affecting two chromosomes" in text
    assert "46,XY,t(2;5)(2pter→2q21::5q31→5qter;5pter→5q31::2q21→2qter)" in text
    assert "]" not in re.sub(r"\[[^\]]*\]", "", text)  # no stray `]` left from arrows
