"""Band data generator (T-006). No network: chr22 fixture slices + the committed band file."""

import shutil
from pathlib import Path

import pytest
from ruamel.yaml import YAML

from cytolex_tools.bands import run
from cytolex_tools.bands.fetch import fetch, load_sources
from cytolex_tools.bands.generate import lineage, parent_band
from cytolex_tools.bands.lookup import load
from cytolex_tools.bands.parse import parse_ncbi_gdp, parse_ucsc_cytoband
from cytolex_tools.cli import main
from cytolex_tools.config import REPO_ROOT

FIX = Path(__file__).parent / "fixtures" / "bands"
COMMITTED = REPO_ROOT / "kb" / "bands" / "2016" / "bands.yaml"


@pytest.fixture
def env(tmp_path):
    """A writable copy of the fixture sources (cache dir = sources) with pins filled in."""
    cache = tmp_path / "cache"
    shutil.copytree(FIX, cache)
    sources = cache / "sources.yaml"
    report = run("2016", tmp_path / "pin-out", cache, cache_only=True, repin=True,
                 sources_path=sources)  # fmt: skip
    assert not report.problems, report.problems
    return cache, sources, tmp_path / "out"


def _run(env, **kw):
    cache, sources, out = env
    return run("2016", out, cache, cache_only=True, sources_path=sources, **kw)


# --- units -----------------------------------------------------------------------------------


def test_parent_and_lineage():
    assert parent_band("34.11") == "34.1"
    assert parent_band("34.1") == "34"
    assert parent_band("34") is None
    assert lineage("11.223") == ["11.223", "11.22", "11.2", "11"]


def test_parse_ncbi():
    bands = parse_ncbi_gdp((FIX / "ncbi-grch38-400.tsv").read_bytes())
    first = bands[0]
    assert (first.name, first.start, first.end, first.stain) == ("22p13", 1, 4300000, "gvar")
    assert any(b.stain == "gpos100" for b in bands)  # stain + density


def test_parse_ucsc_converts_to_one_based():
    line = b"chr22\t0\t4300000\tp13\tgvar\nchrUn_x\t0\t10\t\tgneg\n"
    (b,) = parse_ucsc_cytoband(line)
    assert (b.name, b.start, b.end) == ("22p13", 1, 4300000)


def test_fetch_cache_only_missing(tmp_path):
    (src, *_), _ = load_sources("2016", FIX / "sources.yaml")
    with pytest.raises(FileNotFoundError, match="--cache-only"):
        fetch(src, tmp_path, cache_only=True)


# --- generator ---------------------------------------------------------------------------------


def test_generate_fixture(env):
    report = _run(env)
    assert not report.problems, report.problems
    bands = load(path=report.out)
    q112 = bands.resolve("22q11.2")
    assert q112["levels"] == [400, 550] and q112["parent"] == "22q11"
    assert q112["grch38"] == [17400001, 25500000]  # union of q11.21-q11.23
    assert bands.resolve("22q11.21")["levels"] == [850]
    assert bands.resolve("22q11")["derived"] is True  # ancestor only
    assert not bands.is_valid("22q14")
    assert report.counts["level_850"] == 16


def test_output_is_byte_identical(env):
    first = _run(env).out.read_bytes()
    assert _run(env).out.read_bytes() == first


def test_sha_mismatch_fails_and_writes_nothing(env):
    cache, _, out = env
    p = cache / "ncbi-grch38-400.tsv"
    p.write_bytes(p.read_bytes() + b"\n")
    report = _run(env)
    assert any("sha256 mismatch" in pr for pr in report.problems)
    assert not out.exists()


def test_missing_pin_fails(tmp_path):
    report = run("2016", tmp_path / "out", FIX, cache_only=True, sources_path=FIX / "sources.yaml")
    assert any("no sha256 pin" in pr for pr in report.problems)


def _edit_and_repin(env, name, transform):
    cache, sources, _ = env
    p = cache / name
    p.write_text(transform(p.read_text(encoding="utf-8")), encoding="utf-8", newline="\n")
    run("2016", cache / "x", cache, cache_only=True, repin=True, sources_path=sources)


def test_name_mismatch_ncbi_vs_ucsc(env):
    _edit_and_repin(env, "ucsc-hg38.tsv", lambda s: s.replace("q13.33", "q13.34"))
    problems = _run(env).problems
    assert any("NCBI 850 vs UCSC band names differ" in pr and "22q13.34" in pr for pr in problems)


def test_coordinate_mismatch_ncbi_vs_ucsc(env):
    _edit_and_repin(env, "ucsc-hg38.tsv", lambda s: s.replace("\t50818468\t", "\t50818000\t"))
    assert any("coordinates differ" in pr for pr in _run(env).problems)


def test_coarse_band_without_sub_bands(env):
    _edit_and_repin(env, "ncbi-grch38-400.tsv", lambda s: s.replace("\tq\t13\t", "\tq\t14\t"))
    problems = _run(env).problems
    assert any("no 850 sub-band" in pr and "22q14" in pr for pr in problems)


def test_coarse_band_not_tiling(env):
    _edit_and_repin(env, "ncbi-grch37-550.tsv", lambda s: s.replace("\t29600000\t", "\t29700000\t"))
    assert any("don't match sub-bands" in pr for pr in _run(env).problems)


def test_cli_bands_on_fixtures(env, capsys):
    cache, sources, out = env
    argv = ["bands", "--cache-only", "--cache", str(cache), "--out", str(out)]
    assert main([*argv, "--sources", str(sources)]) == 0
    assert "band names" in capsys.readouterr().out


# --- committed data (acceptance) ---------------------------------------------------------------


@pytest.mark.parametrize("name", ["9q34", "9q34.1", "9q34.11", "22q11.2", "1p36.3", "Xp22.3"])
def test_known_bands_resolve(name):
    assert load().is_valid(name)


@pytest.mark.parametrize("name", ["9q35", "9q34.4", "22q14", "1p37", "23q11", "9q34.1.1"])
def test_invented_bands_do_not_resolve(name):
    assert not load().is_valid(name)


def test_committed_file_header():
    data = YAML(typ="safe").load(COMMITTED.read_text(encoding="utf-8"))
    assert data["levels_available"] == [400, 550, 850]
    assert data["levels_unavailable"] == [300, 700]
    assert len(data["sources"]) == 8 and all(len(s["sha256"]) == 64 for s in data["sources"])
    names = [b["name"] for b in data["bands"]]
    assert len(names) == len(set(names)) == data["counts"]["bands"]
    by_name = {b["name"]: b for b in data["bands"]}
    assert all(b["parent"] is None or b["parent"] in by_name for b in data["bands"])
