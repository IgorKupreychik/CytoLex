"""T-102: the probe table — CSV format, `kb build` checks, `kb probes` pre-fill, review report."""

import gzip
import hashlib
import json
import shutil
from pathlib import Path

import pytest

from cytolex_tools.kb.build import run as build_run
from cytolex_tools.probes import prefill, table
from cytolex_tools.probes.check import check
from cytolex_tools.probes.names import example_probe_names, probe_names
from cytolex_tools.probes.report import review_markdown

HEADER = ",".join(table.COLUMNS)
# Two bands with coordinates in both builds, one book-only band without coordinates
BANDS = {
    "2016": {
        "9q34.12": {"name": "9q34.12", "grch37": [133500001, 134000000], "grch38": [130600001, 131100000]},  # noqa: E501
        "9q34.13": {"name": "9q34.13", "grch37": [134000001, 135900000], "grch38": [131100001, 133100000]},  # noqa: E501
        "1q32.11": {"name": "1q32.11", "grch37": None, "grch38": None},
    }
}  # fmt: skip
ABL1 = "ABL1,ABL,gene,9,9q34.12,133588430-133763062,130713043-130887675,HGNC test (auto),no,,,"


def tab(*rows: str, tmp: Path | None = None) -> table.Table:
    return table.parse("\n".join([HEADER, *rows]) + "\n", (tmp or Path("probes.csv")))


def errors(t: table.Table) -> list[str]:
    return [f"{p.where}: {p.message}" for p in [*t.problems, *check(t, BANDS).errors]]


def conflicts(t: table.Table) -> list[str]:
    return [f"{c.kind}: {c.message}" for c in check(t, BANDS).conflicts]


# --- CSV format ----------------------------------------------------------------------------


def test_valid_row_has_no_problems():
    t = tab(ABL1)
    assert errors(t) == [] and conflicts(t) == []
    row = t.rows[0]
    assert (row.name, row.aliases, row.range("grch38")) == ("ABL1", ["ABL"], (130713043, 130887675))
    assert row.is_auto and not row.reviewed


def test_excel_bom_and_crlf_are_read_and_lf_is_written(tmp_path):
    path = tmp_path / "probes.csv"
    path.write_bytes(("﻿" + HEADER + "\r\n" + ABL1 + "\r\n").encode("utf-8"))
    t = table.read(path)
    assert t.problems == [] and t.rows[0].name == "ABL1"
    table.write(path, t.rows)
    assert path.read_bytes() == (HEADER + "\n" + ABL1 + "\n").encode("utf-8")
    table.write(path, table.read(path).rows)
    assert path.read_bytes() == (HEADER + "\n" + ABL1 + "\n").encode("utf-8")  # byte-identical


def test_wrong_header():
    t = table.parse("probe,kind\nABL1,gene\n", Path("p.csv"))
    assert t.rows == [] and "header must be exactly" in t.problems[0].message


def test_wrong_cell_count():
    assert "11 cells, expected 12" in errors(tab("ABL1,,gene,9,,,,src,no,,"))[0]


@pytest.mark.parametrize(
    ("row", "fragment"),
    [
        (",,gene,9,,,,src,no,,,", "row 2.probe: required"),
        ("X1,,probe,9,,,,src,no,,,", "row 2 (X1).kind: 'probe' is not one of"),
        ("X1,,gene,23,,,,src,no,,,", "'23' is not a chromosome 1-22, X or Y"),
        ("X1,,gene,9,9q34.12,1_2,,src,no,,,", "'1_2' is not start-end in bp"),
        ("X1,,gene,9,9 q34,,,src,no,,,", "is not a band such as 9q34.12"),
        ("X1,,gene,9,,,,,no,,,", "row 2 (X1).source: required"),
        ("X1,,gene,9,,,,src,maybe,,,", "'maybe' is not one of ['yes', 'no']"),
        ("X1,,gene,9,,,,src,yes,,2026-11-27,", "row 2 (X1).reviewer: required"),
        ("X1,,gene,9,,,,src,yes,A. Person,,", "row 2 (X1).date: required"),
        ("X1,,gene,9,,,,src,yes,A. Person,27.11.2026,", "is not a date YYYY-MM-DD"),
        ("X1,A B,gene,9,,,,src,no,,,", "names separated by ';'"),
    ],
)
def test_schema_faults(row, fragment):
    assert any(fragment in e for e in errors(tab(row))), errors(tab(row))


# --- checks against the band data ------------------------------------------------------------


def test_band_not_in_the_band_data():
    t = tab("X1,,gene,9,9q34.99,,,src,no,,,")
    assert errors(t) == ["row 2 (X1).band: 9q34.99: no such band in ISCN 2016"]


def test_band_on_another_chromosome():
    t = tab("X1,,gene,1,9q34.12,,,src,no,,,")
    assert errors(t) == ["row 2 (X1).band: band 9q34.12 is not on chromosome 1"]


def test_start_after_end():
    t = tab("X1,,gene,9,,200-100,,src,no,,,")
    assert errors(t) == ["row 2 (X1).grch37: start 200 > end 100"]


def test_duplicate_name_and_alias():
    t = tab(ABL1, "abl1,,gene,9,,,,src,no,,,", "BCR,ABL,gene,22,,,,src,no,,,")
    assert errors(t) == [
        "row 3 (abl1).probe: 'abl1' is already used by row 2 (ABL1)",
        "row 4 (BCR).aliases: 'ABL' is already used by row 2 (ABL1)",
    ]


@pytest.mark.parametrize("name", ["9-Sep", "1-Mar", "Sep-09", "2024-09-09", "09/09/2024"])
def test_date_mangled_names(name):
    t = tab(f"{name},,gene,9,,,,src,no,,,")
    assert "looks like a date a spreadsheet made of a name" in errors(t)[0]


def test_real_names_are_not_dates():
    t = tab("SEPTIN9,SEPT9,gene,17,,,,src,no,,,", "MARCHF1,MARCH1,gene,4,,,,src,no,,,")
    assert errors(t) == []


def test_conflict_band_vs_coordinates():
    t = tab("X1,,gene,9,9q34.12,1-100,1-100,src (auto),no,,,")
    assert [c.split(":")[0] for c in conflicts(t)] == ["band-coordinates", "band-coordinates"]
    assert "GRCh37 1-100 is outside 9q34.12 (133500001-134000000" in conflicts(t)[0]
    assert errors(t) == []  # unreviewed: a warning, not an error


def test_conflict_builds_disagree():
    t = tab("X1,,gene,9,9q34.12,133588430-133763062,1-100,src,no,,,")
    assert conflicts(t) == [
        "builds-disagree: GRCh37 and GRCh38 disagree: GRCh38 1-100 is outside 9q34.12 "
        "(130600001-131100000, ISCN 2016 band data)"
    ]


def test_band_range_spans_both_bands():
    t = tab("X1,,gene,9,9q34.12-q34.13,133900000-134100000,131000000-131200000,src,no,,,")
    assert errors(t) == [] and conflicts(t) == []


def test_book_only_band_has_no_coordinate_check():
    t = tab("X1,,gene,1,1q32.11,1-100,1-100,src,no,,,")
    assert errors(t) == [] and conflicts(t) == []


def test_conflict_on_a_reviewed_row_is_an_error():
    t = tab("X1,,gene,9,9q34.12,1-100,1-100,src,yes,A. Person,2026-11-27,")
    assert errors(t) and all("reviewed row with a conflict" in e for e in errors(t))


# --- names in the KB examples ------------------------------------------------------------


def test_probe_names_in_example_strings():
    assert probe_names("nuc ish(ABL1conBCR×2)[200]") == {"ABL1", "BCR"}
    assert probe_names("ish del(22)(q11.2q11.2)(HIRA-)") == {"HIRA"}
    assert probe_names("nuc ish 22q11.2q11.2D22S75(x2)") == {"D22S75"}
    assert probe_names("ish 17p11.2(RAI1enh)") == {"RAI1"}
    assert probe_names("46,XY.ish 22q11.2(RP11-265P11×2)") == {"RP11-265P11"}
    assert probe_names("46,XX.ish(D13Z1/D21Z1×2)") == {"D13Z1", "D21Z1"}
    assert probe_names("46,XY,t(9;22)(q34;q11.2)") == set()  # no in situ part


def test_example_probe_names_reads_valid_invalid_and_corrected():
    rules = [{"examples": {"valid": [{"text": "nuc ish(MYC×3)"}],
                           "invalid": [{"text": "nuc ish(TP53 ×1)", "corrected": "nuc ish(TP53×1)"}]}}]  # fmt: skip  # noqa: E501
    assert example_probe_names(rules) == {"MYC", "TP53"}


# --- `kb probes` -------------------------------------------------------------------------

HGNC = (
    "hgnc_id\tsymbol\tname\tstatus\tlocation\tprev_symbol\tentrez_id\n"
    "HGNC:76\tABL1\tABL proto-oncogene 1\tApproved\t9q34.12\tABL\t25\n"
    "HGNC:7132\tKMT2A\tlysine methyltransferase 2A\tApproved\t11q23.3\tMLL|ALL1\t4297\n"
    "HGNC:1\tGENEA\tgene a\tApproved\t1p36.33\tOLD\t100\n"
    "HGNC:2\tGENEB\tgene b\tApproved\t1p36.33\tOLD\t200\n"
    "HGNC:3\tWD\twithdrawn\tEntry Withdrawn\t\t\t\n"
)


def gff(rows: list[tuple[str, int, int, str]]) -> bytes:
    lines = ["##gff-version 3"]
    for seq, start, end, gid in rows:
        lines.append(f"{seq}\tBestRefSeq\tgene\t{start}\t{end}\t.\t+\t.\tID=g;Dbxref=GeneID:{gid}")
    lines.append("NT_187361.1\tBestRefSeq\tgene\t1\t2\t.\t+\t.\tDbxref=GeneID:25")  # alt contig
    return gzip.compress(("\n".join(lines) + "\n").encode("utf-8"), mtime=0)


@pytest.fixture
def env(tmp_path):
    cache = tmp_path / "cache"
    cache.mkdir()
    files = {
        "hgnc.tsv": HGNC.encode("utf-8"),
        "ncbi-grch38.gff.gz": gff([("NC_000009.12", 130713043, 130887675, "25"),
                                   ("NC_000011.10", 118436492, 118526832, "4297")]),
        "ncbi-grch37.gff.gz": gff([("NC_000009.11", 133588430, 133763062, "25"),
                                   ("NC_000011.9", 118307207, 118397547, "4297")]),
    }  # fmt: skip
    for name, data in files.items():
        (cache / name).write_bytes(data)
    sha = {n: hashlib.sha256(d).hexdigest() for n, d in files.items()}
    sources = tmp_path / "sources.yaml"
    sources.write_text(
        "files:\n"
        f"  - {{id: hgnc, kind: hgnc, label: HGNC T, url: 'https://x/hgnc.tsv', sha256: '{sha['hgnc.tsv']}'}}\n"  # noqa: E501
        f"  - {{id: ncbi-grch38, kind: ncbi_gff, build: GRCh38, label: N38, url: 'https://x/a.gff.gz', sha256: '{sha['ncbi-grch38.gff.gz']}'}}\n"  # noqa: E501
        f"  - {{id: ncbi-grch37, kind: ncbi_gff, build: GRCh37, label: N37, url: 'https://x/b.gff.gz', sha256: '{sha['ncbi-grch37.gff.gz']}'}}\n",  # noqa: E501
        encoding="utf-8",
    )  # fmt: skip
    rules = tmp_path / "rules"
    rules.mkdir()
    (rules / "r.yaml").write_text(
        "rules:\n  - examples:\n      valid:\n        - text: 'nuc ish(MLL×2),(ABL1×3)'\n",
        encoding="utf-8",
    )
    return tmp_path / "probes.csv", cache, sources, rules


def probes(env, add=(), from_examples=False):
    path, cache, sources, rules = env
    return prefill.run(list(add), from_examples, path, cache, True, False, sources, rules)


def test_prefill_adds_genes_from_examples(env):
    report = probes(env, from_examples=True)
    assert report.problems == [] and report.added == ["ABL1", "KMT2A"]
    rows = {r.name: r for r in table.read(env[0]).rows}
    abl1, kmt2a = rows["ABL1"], rows["KMT2A"]
    assert (abl1["chromosome"], abl1["band"], abl1["grch37"], abl1["grch38"]) == (
        "9", "9q34.12", "133588430-133763062", "130713043-130887675")  # fmt: skip
    assert abl1["source"] == "HGNC T + N38 + N37 (auto)" and abl1["reviewed"] == "no"
    assert kmt2a.aliases == ["MLL", "ALL1"]  # previous symbols; the book writes MLL
    assert "MLL: previous symbol of KMT2A" in report.notes


def test_prefill_drops_ambiguous_previous_symbols(env):
    report = probes(env, add=["OLD", "GENEA"])
    assert "previous symbol of several genes (GENEA, GENEB)" in " ".join(report.notes)
    row = table.read(env[0]).rows[0]
    assert row.name == "GENEA" and row.aliases == []  # OLD is shared: not an alias
    assert "no GRCh37 position in NCBI" in row["note"]


def test_prefill_reports_unknown_names(env):
    assert probes(env, add=["NOPE"]).not_found == ["NOPE"]


def test_prefill_refreshes_auto_rows_only(env):
    path = env[0]
    reviewed = "ABL1,,gene,9,9q34.12,1-2,1-2,datasheet X,yes,A. Person,2026-11-27,checked"
    stale = "KMT2A,,gene,11,11q23.3,1-2,1-2,HGNC old (auto),no,,,"
    path.write_text(f"{HEADER}\n{reviewed}\n{stale}\n", encoding="utf-8")
    report = probes(env, add=["ABL1"])
    lines = path.read_text(encoding="utf-8").splitlines()
    assert lines[1] == reviewed  # reviewed row untouched
    assert lines[2].startswith("KMT2A,MLL;ALL1,gene,11,11q23.3,118307207-118397547")
    assert report.refreshed == ["KMT2A"] and report.kept == ["ABL1"] and report.added == []


def test_prefill_skips_a_name_a_person_row_has_as_alias(env):
    path = env[0]
    path.write_text(f"{HEADER}\nMLLprobe,ABL1,commercial,11,,,,datasheet,no,,,\n", "utf-8")
    report = probes(env, add=["ABL1"])
    assert report.added == [] and report.kept == ["ABL1"]


def test_prefill_fails_on_a_pin_mismatch(env):
    path, cache, sources, rules = env
    (cache / "hgnc.tsv").write_bytes(b"changed")
    report = prefill.run(["ABL1"], False, path, cache, True, False, sources, rules)
    assert "hgnc: sha256 mismatch" in report.problems[0] and not path.exists()


def test_prefill_update_pins(env):
    path, cache, sources, rules = env
    (cache / "hgnc.tsv").write_bytes(HGNC.encode("utf-8") + b"\n")
    report = prefill.run(["ABL1"], False, path, cache, True, True, sources, rules)
    assert report.pinned == ["hgnc"] and report.problems == []


# --- kb build + report ---------------------------------------------------------------------

FIXTURES = Path(__file__).parent / "fixtures" / "kb"


@pytest.fixture
def kb(tmp_path):
    kb_dir = tmp_path / "kb"
    for sub in ("bands", "rules", "source-quotes", "source-text", "vocab"):
        shutil.copytree(FIXTURES / sub, kb_dir / sub)
    (kb_dir / "probes").mkdir()
    return kb_dir


def build(kb_dir: Path, *rows: str):
    (kb_dir / "probes" / "probes.csv").write_text("\n".join([HEADER, *rows]) + "\n", "utf-8")
    out = kb_dir.parent / "build"
    return build_run(kb_dir, kb_dir / "source-text", out, None, kb_dir / "bands"), out


def test_build_puts_the_table_into_both_bundles(kb):
    row = "X1,A1,clone,9,,1-2,3-4,NCBI Clone DB,yes,A. Person,2026-11-27,typed in"
    report, out = build(kb, row)
    assert report.errors == []
    runtime = json.loads((out / "kb-runtime.json").read_text("utf-8"))
    assert runtime["probes"] == [{"name": "X1", "aliases": ["A1"], "kind": "clone", "chrom": "9",
                                  "band": None, "grch37": [1, 2], "grch38": [3, 4],
                                  "source": "NCBI Clone DB", "reviewed": True}]  # fmt: skip
    full = json.loads((out / "kb.json").read_text("utf-8"))["probes"][0]
    assert (full["reviewer"], full["date"], full["note"]) == ("A. Person", "2026-11-27", "typed in")
    assert (out / "review" / "probes-review.md").exists()


def test_build_fails_on_a_table_error(kb):
    report, out = build(kb, "9-Sep,,gene,9,,,,src,no,,,")
    assert any("looks like a date" in e.message for e in report.errors)
    assert not (out / "kb-runtime.json").exists()


def test_build_without_a_table_has_no_probes(kb):
    out = kb.parent / "build"
    report = build_run(kb, kb / "source-text", out, None, kb / "bands")
    assert report.errors == []
    assert "probes" not in json.loads((out / "kb-runtime.json").read_text("utf-8"))


def test_review_report():
    t = tab(ABL1, "X1,,gene,9,9q34.12,1-100,1-100,src,no,,,",
            "D1,,clone,9,,,,datasheet,yes,A. Person,2026-11-27,")  # fmt: skip
    text = review_markdown(t, check(t, BANDS), {"ABL", "BCR", "D21Z1", "RP11-1M1"})
    assert "3 rows · 1 reviewed · 2 not reviewed · 2 conflict(s)" in text
    assert "- row 3 (X1) [band-coordinates]: GRCh37 1-100 is outside 9q34.12" in text
    assert "- row 2 (ABL1) 9q34.12: HGNC test (auto)" in text
    need = text.split("without a row")[1].split("## Placed by name")[0]
    assert "`BCR`" in need and "`RP11-1M1`" in need and "`ABL`" not in need  # alias counts
    assert "`D21Z1`" in text.split("## Placed by name")[1]


def test_committed_probe_table_is_clean():
    """The committed seed passes the schema; every row names its source."""
    t = table.read()
    assert t.problems == [] and t.rows
    assert all(r["source"] for r in t.rows)
