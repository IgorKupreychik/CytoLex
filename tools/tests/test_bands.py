"""Band data generator (T-006, T-007). No network: chr22 fixture slices, the committed band
file, and the committed book idiograms (chr22 labels) for the book/exception tests."""

import shutil
from pathlib import Path

import pytest
from ruamel.yaml import YAML

from cytolex_tools.bands import run
from cytolex_tools.bands.common import flow, lineage, parent_band
from cytolex_tools.bands.fetch import fetch, load_sources
from cytolex_tools.bands.lookup import load
from cytolex_tools.bands.parse import parse_ncbi_gdp, parse_ucsc_cytoband
from cytolex_tools.cli import main
from cytolex_tools.config import REPO_ROOT
from cytolex_tools.kb.schema import validate_data

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
    assert data["generator_version"] == 2
    assert data["levels_available"] == [300, 400, 550, 700, 850]
    assert data["levels_unavailable"] == []
    assert len(data["sources"]) == 10
    assert all(len(s["sha256"]) == 64 for s in data["sources"])
    assert [s["kind"] for s in data["sources"][-2:]] == ["book_ideograms", "exceptions"]
    names = [b["name"] for b in data["bands"]]
    assert len(names) == len(set(names)) == data["counts"]["bands"]
    by_name = {b["name"]: b for b in data["bands"]}
    assert all(b["parent"] is None or b["parent"] in by_name for b in data["bands"])


# --- book idiograms + exceptions (T-007) ------------------------------------------------------

BOOK = REPO_ROOT / "kb" / "bands" / "2016"
CITE = {"section": "2.5", "page": 28, "float": "Fig. 5"}
SUB = ["22q13.31", "22q13.32", "22q13.33"]


def _write_book(book: Path, labels: list[dict], exceptions: list[dict]):
    head = "version: '2016'\nsource: {figure: Fig. 5, section: '2.5'}\nlabels:\n"
    body = "".join(f"  - {flow(e)}\n" for e in labels)
    (book / "ideograms.yaml").write_text(head + body, encoding="utf-8")
    items = "".join(f"  - {flow(x)}\n" for x in exceptions) or "  []\n"
    (book / "exceptions.yaml").write_text(f"version: '2016'\nexceptions:\n{items}", "utf-8")


@pytest.fixture
def book_env(env, tmp_path):
    """Fixture sources with the book enabled; book dir = chr22 labels of the real idiograms."""
    cache, sources, out = env
    sources.write_text(sources.read_text("utf-8") + "ideograms: {figure: Fig. 5}\n", "utf-8")
    book = tmp_path / "book"
    book.mkdir()
    data = YAML(typ="safe").load((BOOK / "ideograms.yaml").read_text(encoding="utf-8"))
    _write_book(book, [e for e in data["labels"] if e["chrom"] == "22"], [])
    return cache, sources, out, book


def _labels(book: Path) -> list[dict]:
    return YAML(typ="safe").load((book / "ideograms.yaml").read_text("utf-8"))["labels"]


def _relabel(labels, levels, old: list[str], new: list[str]) -> list[dict]:
    """Replace the q-arm labels `old` by `new` (where the first one was) at `levels`."""
    out = []
    for e in labels:
        if e["level"] in levels and e["arm"] == "q" and e["label"] in old:
            if e["label"] == old[0]:
                out += [e | {"label": x} for x in new]
            continue
        out.append(e)
    return out


def _book_run(book_env):
    cache, sources, out, book = book_env
    return run("2016", out, cache, cache_only=True, sources_path=sources, book_dir=book,
               review_dir=out.parent / "review")  # fmt: skip


def _bands(report) -> dict[str, dict]:
    data = YAML(typ="safe").load(report.out.read_text(encoding="utf-8"))
    return {b["name"]: b for b in data["bands"]}


def _exc(level, region, book, ncbi, resolution="book", **kw):
    return {"level": level, "region": region, "book": book, "ncbi": ncbi,
            "resolution": resolution, **kw, "cite": CITE, "note": "test"}  # fmt: skip


def _coarse_q13_3(book_env, levels, exceptions):
    """Book shows 22q13.3 undivided at `levels`; NCBI 850 has 22q13.31–33."""
    book = book_env[3]
    _write_book(book, _relabel(_labels(book), levels, ["13.31", "13.32", "13.33"], ["13.3"]),
                exceptions)  # fmt: skip
    return _book_run(book_env)


def _split_q13_2(book_env, exceptions):
    """Book subdivides 22q13.2 into 22q13.21/22 at 850; NCBI 850 has 22q13.2."""
    book = book_env[3]
    _write_book(book, _relabel(_labels(book), {850}, ["13.2"], ["13.21", "13.22"]), exceptions)
    return _book_run(book_env)


def test_book_without_differences_adds_300_700_and_flags(book_env):
    report = _book_run(book_env)
    assert report.problems == []
    bands = _bands(report)
    assert bands["22q11.2"]["levels"] == [300, 400, 550]
    assert bands["22q13.31"]["levels"] == [700, 850]
    assert all(b["book"] for b in bands.values())
    assert report.counts["level_300"] == 8 and report.counts["not_in_book"] == 0


def test_unexplained_difference_fails_and_writes_draft(book_env):
    report = _coarse_q13_3(book_env, {850}, [])
    assert report.out is None
    assert report.problems == [
        "unexplained difference at 850, 22q13.3: book ['22q13.3'] vs NCBI "
        "['22q13.31', '22q13.32', '22q13.33']; add it to exceptions.yaml"
    ]
    draft = YAML(typ="safe").load(report.draft.read_text(encoding="utf-8"))
    assert draft["exceptions"][0]["region"] == "22q13.3"
    assert draft["exceptions"][0]["cite"] == CITE
    assert validate_data(draft, report.draft) == []  # schema-valid, ready to copy


def test_book_resolution_drops_names_no_book_level_shows(book_env):
    report = _coarse_q13_3(book_env, {700, 850}, [_exc(850, "22q13.3", ["22q13.3"], SUB)])
    assert report.problems == []
    bands = _bands(report)
    assert 850 in bands["22q13.3"]["levels"]
    assert not any(n in bands for n in SUB)


def test_book_resolution_only_moves_level_when_book_shows_name_elsewhere(book_env):
    report = _coarse_q13_3(book_env, {850}, [_exc(850, "22q13.3", ["22q13.3"], SUB)])
    assert report.problems == []
    bands = _bands(report)
    assert [bands[n]["levels"] for n in SUB] == [[700]] * 3  # still shown at 700
    assert all(bands[n]["book"] for n in SUB)


def test_keep_retains_ncbi_names_flagged(book_env):
    exc = _exc(850, "22q13.3", ["22q13.3"], SUB, keep=SUB)
    report = _coarse_q13_3(book_env, {700, 850}, [exc])
    assert report.problems == []
    bands = _bands(report)
    assert [(bands[n]["levels"], bands[n]["book"]) for n in SUB] == [([850], False)] * 3
    assert bands["22q13.3"]["book"] is True and report.counts["not_in_book"] == 3


def test_book_only_names_get_no_coordinates(book_env):
    report = _split_q13_2(book_env, [_exc(850, "22q13.2", ["22q13.21", "22q13.22"], ["22q13.2"])])
    assert report.problems == []
    bands = _bands(report)
    new = bands["22q13.21"]
    assert (new["grch38"], new["stain"], new["parent"], new["levels"]) == (
        None, None, "22q13.2", [850]
    )  # fmt: skip
    assert 850 not in bands["22q13.2"]["levels"]
    order = list(bands)
    assert order.index("22q13.2") < order.index("22q13.21") < order.index("22q13.22")
    assert order.index("22q13.22") < order.index("22q13.3")


def test_ncbi_resolution_keeps_ncbi_level(book_env):
    exc = _exc(850, "22q13.2", ["22q13.21", "22q13.22"], ["22q13.2"], resolution="ncbi")
    report = _split_q13_2(book_env, [exc])
    assert report.problems == []
    bands = _bands(report)
    assert 850 in bands["22q13.2"]["levels"] and "22q13.21" not in bands


def test_stale_and_mismatched_exceptions_fail(book_env):
    book = book_env[3]
    _write_book(book, _labels(book), [_exc(850, "22q13.3", ["22q13.3"], SUB)])
    report = _book_run(book_env)
    assert report.problems == ["stale exception 850, 22q13.3: no such difference any more"]

    report = _coarse_q13_3(book_env, {850}, [_exc(850, "22q13.3", ["22q13.3"], SUB[:1])])
    assert report.problems[0].startswith("exception 850, 22q13.3 doesn't match the data")


def test_keep_requires_book_resolution(book_env):
    exc = _exc(850, "22q13.3", ["22q13.3"], SUB, resolution="ncbi", keep=SUB)
    report = _coarse_q13_3(book_env, {850}, [exc])
    assert "`keep` needs resolution book" in report.problems[0]


def test_invalid_exceptions_file_fails(book_env):
    book = book_env[3]
    _write_book(book, _labels(book), [_exc(300, "22q13", [], [])])
    assert "exceptions[0].level" in _book_run(book_env).problems[0]


def test_cli_bands_reports_draft(book_env, capsys):
    cache, sources, out, book = book_env
    _write_book(book, _relabel(_labels(book), {850}, ["13.2"], ["13.21", "13.22"]), [])
    argv = ["bands", "--cache-only", "--cache", str(cache), "--out", str(out)]
    argv += ["--sources", str(sources), "--book", str(book), "--review", str(out.parent / "r")]
    assert main(argv) == 1
    assert "exceptions-draft.yaml" in capsys.readouterr().err


# --- committed book data (acceptance) ---------------------------------------------------------


def test_every_book_name_resolves():
    from cytolex_tools.bands.book import book_levels

    data = YAML(typ="safe").load((BOOK / "ideograms.yaml").read_text(encoding="utf-8"))
    bands = load()
    assert [n for n in book_levels(data["labels"]) if not bands.is_valid(n)] == []


@pytest.mark.parametrize(
    ("name", "level"),
    [("1p36.3", 300), ("9q34", 300), ("2p14", 300), ("22q13.31", 700), ("15q26.1", 400)],
)
def test_book_levels_in_committed_bands(name, level):
    assert level in load().resolve(name)["levels"]


def test_committed_exceptions_outcomes():
    bands = load()
    assert bands.resolve("9q34.12")["book"] is False  # kept for array notation (§15.3)
    assert 850 in bands.resolve("9q34.1")["levels"]
    assert bands.resolve("1q32.11")["grch38"] is None  # book-only sub-band
    not_in_book = sorted(n for n, b in bands.bands.items() if not b["book"])
    assert not_in_book == ["6p24.1", "6p24.2", "6p24.3", "9q34.11", "9q34.12", "9q34.13"]
