"""Book idiogram decoder (T-007): unit tests on synthetic glyphs; PDF tests skip without the PDF."""

from pathlib import Path

import pytest
from ruamel.yaml import YAML

from cytolex_tools.bands.ideograms import (
    Label,
    Report,
    check,
    column_for,
    expand_range,
    group_labels,
    load_config,
    merge_intervals,
    run,
    split_at,
    split_candidates,
)
from cytolex_tools.config import REPO_ROOT, iscn_pdf_path

COMMITTED = REPO_ROOT / "kb" / "bands" / "2016" / "ideograms.yaml"
GLYPHS = {"": "3", "": ".", "": "2", "": "1", "": "–"}
CODE = {v: k for k, v in GLYPHS.items()}


def chars(text: str, x0: float, top: float, width: float = 2.8) -> list[dict]:
    """Glyph dicts for `text`, each starting where the previous one ends."""
    out = []
    for ch in text:
        w = 1.4 if ch == "." else width
        out.append({"text": CODE[ch], "x0": x0, "x1": x0 + w, "top": top, "size": 5.04})
        x0 += w
    return out


def lab(text: str, top: float) -> Label:
    return Label(text, 0.0, 10.0, top, 5.0)


# --- grouping -------------------------------------------------------------------------------


def test_group_labels_decodes_and_splits_by_line():
    stream = chars("13", 100, 10) + chars("12", 100, 16) + chars("11.2", 96, 22)
    labels, problems = group_labels(stream, GLYPHS)
    assert problems == []
    assert [lb.text for lb in labels] == ["13", "12", "11.2"]


def test_group_labels_separates_touching_leader_label():
    # p.28, chr22 700: '12.1' at baseline 314.79; the leader-line label '12.2' ends exactly
    # where '12.1' starts but sits on another baseline (317.3). Word grouping merged them.
    stream = chars("12.1", 382.73, 314.79) + chars("12.2", 370.72, 317.3)
    labels, _ = group_labels(stream, GLYPHS)
    assert [(lb.text, round(lb.top, 1)) for lb in labels] == [("12.1", 314.8), ("12.2", 317.3)]


def test_group_labels_reports_unmapped_glyph():
    stream = [{"text": "", "x0": 0, "x1": 2, "top": 0, "size": 5}]
    _, problems = group_labels(stream, GLYPHS)
    assert problems == ["unmapped glyph U+F0FF"]


# --- columns and arms -----------------------------------------------------------------------


def test_merge_intervals_and_nearest_column_to_the_right():
    cols = merge_intervals([(138, 166), (139, 165), (231, 254), (232, 255), (312, 333)])
    assert cols == [(138, 166), (231, 255), (312, 333)]
    assert column_for(Label("12", 127, 133, 0, 5), cols) == 0
    # A leader-line label further left still belongs to the next idiogram on its right.
    assert column_for(Label("12.2", 200, 210, 0, 5), cols) == 1
    assert column_for(Label("12", 400, 410, 0, 5), cols) is None


def test_split_unique_for_acrocentric():
    labels = ["13", "12", "11.2", "11.1", "11.1", "11.2", "12", "13"]
    assert split_candidates(labels) == [4]


def test_split_ambiguous_at_centromere_resolved_by_drawing():
    # chr5: p ends 11, q starts 11.1 — '11' could close p or open q.
    texts = ["15", "14", "13", "12", "11", "11.1", "11.2", "12"]
    assert split_candidates(texts) == [4, 5]
    labels = [lab(t, 10.0 * i) for i, t in enumerate(texts)]
    # Label tops 0, 10, …; '11' (top 40) has its middle at 42.5.
    assert split_at(labels, cen=43.0) == (5, [4, 5])  # '11' above the centromere → p11
    assert split_at(labels, cen=38.0)[0] == 4  # below → q11
    assert split_at(labels, cen=None)[0] is None


def test_split_impossible_order():
    assert split_candidates(["13", "21", "12", "22"]) == []


# --- ranges and checks ----------------------------------------------------------------------


def test_expand_range_uses_finer_bands_at_the_same_depth():
    finer = ["21", "22", "23", "24", "22.1", "22–24", "25"]
    assert expand_range("22–24", finer) == ["22", "23", "24"]
    assert expand_range("11.2–11.4", ["11.3"]) == ["11.2", "11.3", "11.4"]
    assert expand_range("36.3", finer) == ["36.3"]


def _entries(chrom, level, arm, labels):
    return [{"chrom": chrom, "level": level, "arm": arm, "label": x, "page": 1} for x in labels]


def test_check_duplicates_and_nesting():
    report = Report()
    entries = _entries("22", 300, "q", ["12–14", "12–14"]) + _entries("22", 400, "q", ["12–14"])
    check(entries, report)
    assert any("duplicate labels ['q12–14']" in p for p in report.problems)

    report = Report()
    check(_entries("22", 300, "q", ["13"]) + _entries("22", 400, "q", ["12"]), report)
    assert "chr22: 300-level q13 has no 400-level equivalent" in report.warnings


def test_config_glyph_map_is_complete():
    cfg = load_config("2016")
    assert sorted(cfg.glyphs.values()) == sorted("0123456789"[1:] + ".–pq")
    assert (cfg.first_page, cfg.last_page) == (16, 29)


# --- real PDF -------------------------------------------------------------------------------


def _pdf() -> Path | None:
    try:
        path = iscn_pdf_path("2016")
    except FileNotFoundError, KeyError:
        return None
    return path if path.exists() else None


PDF = _pdf()
needs_pdf = pytest.mark.skipif(PDF is None, reason="ISCN 2016 PDF not configured")


@pytest.fixture(scope="module")
def decoded(tmp_path_factory):
    out = tmp_path_factory.mktemp("ide")
    return out, run("2016", PDF, out)


@needs_pdf
def test_pdf_decodes_every_chromosome_and_matches_committed(decoded):
    out, report = decoded
    assert report.problems == [] and report.warnings == []
    assert (out / "ideograms.yaml").read_bytes() == COMMITTED.read_bytes()


@needs_pdf
def test_pdf_chr22_850(decoded):
    out, _ = decoded
    data = YAML(typ="safe").load((out / "ideograms.yaml").read_text(encoding="utf-8"))
    got = [
        e["arm"] + e["label"] for e in data["labels"] if e["chrom"] == "22" and e["level"] == 850
    ]
    assert got == [
        "p13", "p12", "p11.2", "p11.1", "q11.1", "q11.21", "q11.22", "q11.23",
        "q12.1", "q12.2", "q12.3", "q13.1", "q13.2", "q13.31", "q13.32", "q13.33",
    ]  # fmt: skip


def test_committed_ideograms_cover_every_chromosome_and_level():
    data = YAML(typ="safe").load(COMMITTED.read_text(encoding="utf-8"))
    seen = {(e["chrom"], e["level"]) for e in data["labels"]}
    chroms = [str(i) for i in range(1, 23)] + ["X", "Y"]
    assert seen == {(c, lvl) for c in chroms for lvl in (300, 400, 550, 700, 850)}
