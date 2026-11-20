"""Tests of `kb samples` (T-038): batch sample files from the KB examples."""

import csv
import io
import json

from cytolex_tools.cli import main
from cytolex_tools.kb.samples import LARGE_ROWS, is_fragment, segment_types

KB = {
    "format_version": 1,
    "editions": {
        "2016": {
            "rules": [
                {
                    "id": "R.ONE",
                    "cite": [{"rendered": "ISCN 2016 §1, p.1, ¶1"}],
                    "examples": {
                        "valid": [
                            {"text": "46,XX"},
                            {"text": "inv(2)(p21q31)"},  # fragment
                            {"text": "46,XX.nuc ish(D20S108×1)[40/200]"},
                        ],
                        "invalid": [
                            {"text": "46XX", "corrected": "46,XX", "why": "comma"},
                            {"text": "47,XY,+…"},  # fragment
                        ],
                    },
                },
                {
                    "id": "R.TWO",
                    "cite": [{"rendered": "ISCN 2016 §2, p.2, ¶1"}],
                    "examples": {
                        "valid": [
                            {
                                "text": "arr[GRCh38] 7q11.23(73330452_74769740)×1",
                                "cite": {"rendered": "ISCN 2016 §14, p.9, ex. E1"},
                            }
                        ]
                    },
                },
            ]
        }
    },
}


def _run(tmp_path):
    kb = tmp_path / "kb.json"
    kb.write_text(json.dumps(KB, ensure_ascii=False), encoding="utf-8")
    out = tmp_path / "out"
    assert main(["samples", "--kb", str(kb), "--out", str(out)]) == 0
    return out


def test_writes_groups_tables_and_large(tmp_path):
    out = _run(tmp_path)
    assert sorted(p.name for p in out.iterdir()) == [
        "all.csv",
        "all.tsv",
        "arr.txt",
        "combined.txt",
        "karyotype.txt",
        "large.csv",
    ]
    assert (out / "karyotype.txt").read_text(encoding="utf-8") == "46,XX\n46XX\n"
    assert (out / "combined.txt").read_text(encoding="utf-8") == (
        "46,XX.nuc ish(D20S108×1)[40/200]\n"
    )

    rows = list(csv.DictReader(io.StringIO((out / "all.csv").read_text(encoding="utf-8"))))
    assert [(r["id"], r["iscn"], r["type"], r["kb_rule"], r["kb_kind"]) for r in rows] == [
        ("K0001", "46,XX", "karyotype", "R.ONE", "valid"),
        ("K0002", "46,XX.nuc ish(D20S108×1)[40/200]", "karyotype+nuc_ish", "R.ONE", "valid"),
        ("K0003", "46XX", "karyotype", "R.ONE", "invalid"),
        ("K0004", "46,XX", "karyotype", "R.ONE", "corrected"),
        ("K0005", "arr[GRCh38] 7q11.23(73330452_74769740)×1", "arr", "R.TWO", "valid"),
    ]
    assert rows[4]["cite"] == "ISCN 2016 §14, p.9, ex. E1"  # example cite wins
    assert rows[0]["cite"] == "ISCN 2016 §1, p.1, ¶1"

    tsv = list(
        csv.DictReader(io.StringIO((out / "all.tsv").read_text(encoding="utf-8")), delimiter="\t")
    )
    assert tsv == rows

    large = list(csv.DictReader(io.StringIO((out / "large.csv").read_text(encoding="utf-8"))))
    assert len(large) == LARGE_ROWS
    assert large[0]["id"] == "E0001" and large[5]["iscn"] == rows[0]["iscn"]


def test_output_is_lf_and_deterministic(tmp_path):
    out = _run(tmp_path)
    first = {p.name: p.read_bytes() for p in out.iterdir()}
    out2 = _run(tmp_path)
    assert {p.name: p.read_bytes() for p in out2.iterdir()} == first
    assert all(b"\r" not in data for data in first.values())


def test_missing_kb_exits_1(tmp_path, capsys):
    assert main(["samples", "--kb", str(tmp_path / "none.json"), "--out", str(tmp_path)]) == 1
    assert "kb build" in capsys.readouterr().err


def test_fragments_and_segment_types():
    assert is_fragment("inv(2)(p21q31)") and is_fragment("46,XX,…") and not is_fragment("46,XX")
    assert not is_fragment("arr(1-22)×2") and not is_fragment("mos 45,X[15]/46,XX[5]")
    assert segment_types("46,XX") == ["karyotype"]
    assert segment_types("46,XX.ish 5p15.2(D5S23×2).nucish(D5S23×2)") == [
        "karyotype",
        "ish",
        "nuc_ish",
    ]
    # sub-band points and decimals stay inside their segment
    assert segment_types("arr[GRCh38] 9q34.11(1_2)×1") == ["arr"]
