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


# --- T-003: extraction review fixes ---------------------------------------------------------


def _chunk(out: Path, name: str) -> str:
    return (out / name).read_text(encoding="utf-8")


def test_all_captions_are_floats(extracted):
    out, _ = extracted
    labels = re.findall(
        r"^\[((?:Fig\.|Table) \d+[ab]?) p\.\d+\]",
        "\n".join(p.read_text(encoding="utf-8") for p in sorted(out.rglob("*.md"))),
        re.M,
    )
    expected = [f"Fig. {n}" for n in (1, 2, 3)] + ["Table 1", "Fig. 4", "Table 2", "Table 3"]
    expected += ["Fig. 5", "Fig. 6a", "Fig. 6b", "Fig. 7", "Fig. 8", "Table 4", "Fig. 9", "Fig. 10"]
    assert labels == expected
    for p in out.rglob("*.md"):  # no caption is left as a numbered paragraph or example note
        assert not re.search(
            r"^(\[¶\d+ p\.\d+\] |> )(Fig\.|Table) \d+\. ", p.read_text("utf-8"), re.M
        )


def test_homologue_underlining_is_kept(extracted):
    out, _ = extracted
    text = _chunk(out, "ch09/09.02.03-derivative-chromosomes.md")
    assert "46,XX,der(1)t(1;3)(p34.3;q21),der(1̲)t(1̲;3)(p34.3;q21)" in text
    text = _chunk(out, "ch09/09.02.17.01-reciprocal-translocations.md")
    assert "46,XX,t(2;7;7̲)(q21;q22;p13)" in text


def test_tables_are_markdown(extracted):
    out, _ = extracted
    text = _chunk(out, "ch02/02.05-molecular-basis-of-banding.md")
    assert "| Chromosome Number | Arm | Number of regions | Landmarks^a |" in text
    assert "|  | q | 4 | Proximal negative band (21) distal to variable region, median" in text
    text = _chunk(out, "ch11/11.01.06-unrelated-clones.md")
    assert "[Ploidy table p.90]" in text
    assert "| Near-haploidy (23±) | ≤34 | Near-pentaploidy (115±) | 104–126 |" in text
    symbols = _chunk(out, "ch03/03-symbols-and-abbreviated-terms.md")
    rows = [ln for ln in symbols.splitlines() if ln.startswith("| ")]
    assert len(rows) > 120 and all(ln.count(" | ") == 1 for ln in rows if ln.strip("| "))
    assert "| approximate sign (~) | Denotes intervals and boundaries" in symbols


def test_legends_paragraph_join_and_change_marks(extracted):
    out, _ = extracted
    fish = _chunk(out, "ch13/13.03.02-relative-position-of-signals.md")
    assert "nuc ish(ABL1,BCR)×2[400]\n```\n> ● = probe for ABL1\n> ○ = probe for BCR" in fish
    hgvs = _chunk(out, "ch16/16.02-general-principles.md")
    assert "existing HGVS recommendations have been extended:" in hgvs
    assert re.search(r"^changed: \[.+\]$", fish, re.M)  # p.109 carries a margin change bar
