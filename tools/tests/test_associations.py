"""T-108: association rules — schema, `kb build` checks, bundles, review report, committed seed."""

import copy
import json
import shutil
from pathlib import Path

import pytest
from ruamel.yaml import YAML

from cytolex_tools.associations.check import check
from cytolex_tools.associations.forms import FormError, exact_bands, leaves, only_negative
from cytolex_tools.associations.report import describe, review_markdown
from cytolex_tools.config import CURATED_DIR, KB_DIR
from cytolex_tools.kb.build import run as build_run
from cytolex_tools.kb.schema import load_yaml, validate_data

P = Path("associations.yaml")
BANDS = {
    "2016": {
        "9q34": {"name": "9q34", "grch38": [127500001, 138394717]},
        "22q11.2": {"name": "22q11.2", "grch38": [17400001, 25500000]},
        "22q11.21": {"name": "22q11.21", "grch38": [17400001, 21700000]},
        "22q11.23": {"name": "22q11.23", "grch38": [23100001, 25500000]},
        "16p13.1": {"name": "16p13.1", "grch38": [10400001, 16700000]},
        "16q22": {"name": "16q22", "grch38": [66600001, 74100000]},
    }
}  # fmt: skip
RULE = {
    "id": "ASSOC.T9_22_CML",
    "condition": {"name": "Chronic myeloid leukaemia", "ids": [{"system": "orphanet", "code": "ORPHA:521"}]},  # noqa: E501
    "context": "neoplastic",
    "strength": "characteristic",
    "source": "WHO-HAEM5",
    "citation": "myeloproliferative neoplasms: CML",
    "match": {"any": [{"exact": "t(9;22)(q34;q11.2)"}, {"fusion": ["9q34", "22q11.2"]}]},
    "review": {"reviewed": False, "note": "seed"},
}  # fmt: skip
COPY = {
    "copy": {"effect": "loss", "chrom": "22", "grch38": [18912231, 21465672], "band": "22q11.21"}
}  # noqa: E501


def rule(**changes) -> dict:
    out = copy.deepcopy(RULE)
    out.update(changes)
    return out


def schema(*rules: dict) -> list[str]:
    return [f"{p.where}: {p.message}" for p in validate_data({"associations": list(rules)}, P)]


def errors(*rules: dict) -> list[str]:
    return [f"{p.where}: {p.message}" for p in check(P, list(rules), BANDS).errors]


# --- schema ----------------------------------------------------------------------------------


def test_valid_rule_has_no_problems():
    assert schema(RULE, rule(id="ASSOC.DEL22Q11", context="constitutional", match=COPY)) == []
    assert errors(RULE) == []


@pytest.mark.parametrize(
    ("changes", "fragment"),
    [
        ({"context": "meiotic"}, "context: 'meiotic' is not one of"),
        ({"strength": "strong"}, "strength: 'strong' is not one of"),
        ({"id": "cml"}, "is not a valid rule ID"),
        ({"match": {"exact": "t(9;22)", "fusion": ["9q34", "22q11.2"]}}, "match:"),
        ({"match": {"exact": "t 9;22"}}, "is not a short form"),
        ({"match": {"fusion": ["9q34"]}}, "match.fusion"),
        ({"match": {"fusion": ["q34", "22q11.2"]}}, "is not a full band name"),
        ({"match": {"any": []}}, "match.any"),
        ({"review": {"reviewed": True}}, "review: 'reviewer' is a required property"),
        ({"review": {"reviewed": True, "reviewer": "", "date": ""}}, "review.reviewer"),
        ({"source": "OMIM"}, "source: 'OMIM' is not one of"),
    ],
)
def test_schema_faults(changes, fragment):
    assert any(fragment in p for p in schema(rule(**changes))), schema(rule(**changes))


@pytest.mark.parametrize(
    "form", ["+21", "-7", "+X", "del(5)(q13q33)", "inv(16)(p13.1q22)", "t(16;16)(p13.1;q22)",
             "i(17)(q10)", "der(1;7)(q10;p10)", "t(4;11)(q21;q23)", "r(X)"],
)  # fmt: skip
def test_exact_forms_accepted(form):
    assert schema(rule(match={"exact": form})) == []


# --- forms -----------------------------------------------------------------------------------


def test_exact_bands():
    assert exact_bands("+21") == []
    assert exact_bands("t(9;22)(q34;q11.2)") == ["9q34", "22q11.2"]
    assert exact_bands("inv(16)(p13.1q22)") == ["16p13.1", "16q22"]
    assert exact_bands("i(17)(q10)") == []
    assert exact_bands("del(9)(p21qter)") == ["9p21"]
    with pytest.raises(FormError):
        exact_bands("t(9;22)(q34)")


def test_leaves_and_negation():
    m = {"all": [{"exact": "+8"}, {"not": {"fusion": ["9q34", "22q11.2"]}}]}
    assert [(lf.kind, lf.where) for lf in leaves(m)] == [
        ("exact", "match.all[0].exact"),
        ("fusion", "match.all[1].not.fusion"),
    ]
    assert not only_negative(m)
    assert only_negative({"not": {"exact": "+8"}})
    assert only_negative({"any": [{"exact": "+8"}, {"not": {"exact": "+21"}}]})


# --- checks ----------------------------------------------------------------------------------


def test_duplicate_ids():
    assert errors(RULE, rule(id="ASSOC.t9_22_cml")) == [
        "associations[1].id: ASSOC.t9_22_cml is already used by associations[0]"
    ]


def test_unknown_bands():
    out = errors(
        rule(match={"any": [{"exact": "t(9;22)(q34;q11.9)"}, {"fusion": ["9q99", "22q11.2"]}]})
    )  # noqa: E501
    assert out == [
        "associations[0].match.any[0].exact: 22q11.9: no such band in ISCN 2016",
        "associations[0].match.any[1].fusion: 9q99: no such band in ISCN 2016",
    ]


def test_band_groups_must_pair_with_chromosomes():
    assert "1 band group(s)" in errors(rule(match={"exact": "t(9;22)(q34)"}))[0]


def test_fusion_with_itself():
    assert errors(rule(match={"fusion": ["9q34", "9q34"]})) == [
        "associations[0].match.fusion: 9q34 is joined with itself"
    ]


def test_lone_not():
    assert "'not' alone" in errors(rule(match={"not": {"exact": "+21"}}))[0]


def test_copy_checks():
    c = COPY["copy"]
    assert errors(rule(match=COPY)) == []
    assert errors(rule(match={"copy": {**c, "grch38": [5, 1]}})) == [
        "associations[0].match.copy.grch38: start 5 > end 1"
    ]
    assert errors(rule(match={"copy": {**c, "band": "9q34"}})) == [
        "associations[0].match.copy.band: band 9q34 is not on chromosome 22"
    ]
    assert errors(rule(match={"copy": {**c, "band": "22q11.21-q11.23"}})) == []
    assert errors(rule(match={"copy": {**c, "band": "22q12"}})) == [
        "associations[0].match.copy.band: 22q12: no such band in ISCN 2016"
    ]


def test_copy_outside_its_band_warns_unless_reviewed():
    outside = {"copy": {**COPY["copy"], "grch38": [1, 100]}}
    res = check(P, [rule(match=outside)], BANDS)
    assert res.errors == []
    assert [c.message for c in res.conflicts] == [
        "GRCh38 1-100 is outside 22q11.21 (17400001-21700000, ISCN 2016 band data)"
    ]
    reviewed = rule(
        match=outside, review={"reviewed": True, "reviewer": "A. Person", "date": "2026-11-27"}
    )  # noqa: E501
    assert "reviewed rule with a conflict" in errors(reviewed)[0]


# --- report ----------------------------------------------------------------------------------


def test_describe_in_plain_words():
    assert describe(RULE["match"]) == (
        "the translocation t(9;22)(q34;q11.2), or any rearrangement that joins band 9q34 and "
        "band 22q11.2"
    )
    assert describe({"exact": "+21"}) == "an extra chromosome 21 (+21)"
    assert (
        describe(COPY) == "a loss overlapping chr22:18,912,231–21,465,672 (GRCh38, band 22q11.21)"
    )
    assert describe({"all": [{"exact": "+8"}, {"not": {"exact": "-7"}}]}) == (
        "all of: an extra chromosome 8 (+8); not a missing chromosome 7 (-7)"
    )


def test_review_report_lists_unreviewed_first():
    done = rule(
        id="ASSOC.DONE", review={"reviewed": True, "reviewer": "A. Person", "date": "2026-11-27"}
    )  # noqa: E501
    rules = [done, RULE]
    text = review_markdown(rules, check(P, rules, BANDS))
    assert "2 rules · 1 reviewed · 1 not reviewed · 0 conflict(s)" in text
    assert text.index("`ASSOC.T9_22_CML`") < text.index("## Reviewed") < text.index("`ASSOC.DONE`")
    assert "**Chronic myeloid leukaemia** (ORPHA:521): neoplastic context, characteristic." in text
    assert "Reviewed by A. Person on 2026-11-27." in text
    assert "not a diagnosis" in text


# --- kb build --------------------------------------------------------------------------------

FIXTURES = Path(__file__).parent / "fixtures" / "kb"


@pytest.fixture
def kb(tmp_path):
    kb_dir = tmp_path / "kb"
    for sub in ("bands", "rules", "source-quotes", "source-text", "vocab"):
        shutil.copytree(FIXTURES / sub, kb_dir / sub)
    (kb_dir / "associations").mkdir()
    return kb_dir


def build(kb_dir: Path, *rules: dict):
    f = kb_dir / "associations" / "associations.yaml"
    with f.open("w", encoding="utf-8", newline="\n") as fh:
        YAML().dump({"associations": list(rules)}, fh)
    out = kb_dir.parent / "build"
    return build_run(kb_dir, kb_dir / "source-text", out, None, kb_dir / "bands"), out


def test_build_puts_the_rules_into_both_bundles(kb):
    r = rule(
        match={"exact": "+21"},
        review={"reviewed": True, "reviewer": "A. Person", "date": "2026-11-27", "note": "ok"},
    )
    report, out = build(kb, r)
    assert report.errors == []
    runtime = json.loads((out / "kb-runtime.json").read_text("utf-8"))["associations"]
    assert runtime == [{**{k: r[k] for k in r if k != "review"}, "review": {"reviewed": True}}]
    full = json.loads((out / "kb.json").read_text("utf-8"))["associations"][0]["review"]
    assert full == {"reviewed": True, "reviewer": "A. Person", "date": "2026-11-27", "note": "ok"}
    assert (out / "review" / "associations-review.md").exists()


def test_build_fails_on_a_rule_error(kb):
    report, out = build(kb, RULE, rule(id="ASSOC.T9_22_CML", match={"exact": "+21"}))
    assert any("already used" in e.message for e in report.errors)
    assert not (out / "kb-runtime.json").exists()


def test_build_fails_on_a_schema_error(kb):
    report, out = build(kb, rule(context="meiotic"))
    assert any("context" in e.where for e in report.errors)
    assert not (out / "kb-runtime.json").exists()


def test_build_without_a_file_has_no_associations(kb):
    out = kb.parent / "build"
    report = build_run(kb, kb / "source-text", out, None, kb / "bands")
    assert report.errors == []
    assert "associations" not in json.loads((out / "kb-runtime.json").read_text("utf-8"))


# --- committed seed --------------------------------------------------------------------------


def test_committed_seed_is_clean():
    """The seed passes the schema and the checks against every edition's real band data; every
    rule names its source and stays unreviewed until a person signs it off."""
    path = CURATED_DIR / "associations" / "associations.yaml"
    data = load_yaml(path)
    assert validate_data(data, path) == []
    band_sets = {
        d.name: {b["name"]: b for b in load_yaml(d / "bands.yaml")["bands"]}
        for d in (KB_DIR / "bands").iterdir()
        if (d / "bands.yaml").is_file()
    }
    res = check(path, data["associations"], band_sets)
    assert res.errors == [] and res.conflicts == []
    assert all(r["citation"] and r["source"] for r in data["associations"])
