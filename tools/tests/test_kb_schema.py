"""T-004: KB JSON Schemas, sample files and `kb check`."""

import json
from pathlib import Path

import pytest
from jsonschema import Draft202012Validator

from cytolex_tools.cli import main
from cytolex_tools.kb.schema import SCHEMA_DIR, validate_data, validate_file

FIXTURES = Path(__file__).parent / "fixtures" / "kb"
SAMPLE_RULES = FIXTURES / "rules" / "2016" / "ch09" / "09.02.17.01-reciprocal-translocations.yaml"
SAMPLE_QUOTES = SAMPLE_RULES.parents[3] / "source-quotes" / "2016" / "ch09" / SAMPLE_RULES.name
BROKEN = FIXTURES / "broken"


@pytest.mark.parametrize("name", ["common", "rules", "quotes"])
def test_schemas_are_valid_draft_2020_12(name):
    schema = json.loads((SCHEMA_DIR / f"{name}.schema.json").read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)


@pytest.mark.parametrize("path", [SAMPLE_RULES, SAMPLE_QUOTES], ids=["rules", "quotes"])
def test_samples_validate(path):
    assert validate_file(path) == []


# broken file -> (where, message fragment); each file has exactly one defect
EXPECTED = {
    "bad-rule-id.yaml": ("rules[0].id", "is not a valid rule ID"),
    "missing-why.yaml": ("rules[0].examples.invalid[0]", "'why' is a required property"),
    "bad-para-label.yaml": ("rules[0].cite[0].para", "is not a paragraph label"),
    "unknown-field.yaml": ("rules[0]", "unknown field(s) ('severty' was unexpected)"),
    "cite-no-anchor.yaml": ("rules[0].cite[0]", "needs at least one of: para, example, float"),
    "kind-on-level-3.yaml": ("rules[1].fix", "'kind' is only allowed with fix level 1"),
    "duplicate-key.yaml": ("line 31", 'duplicate key "needs_review"'),
}


@pytest.mark.parametrize("name", sorted(EXPECTED))
def test_broken_sample_fails_clearly(name):
    problems = validate_file(BROKEN / name)
    assert len(problems) == 1, problems
    where, fragment = EXPECTED[name]
    assert problems[0].where == where
    assert fragment in problems[0].message


def test_bad_vocabulary_reported_per_rule():
    problems = validate_file(BROKEN / "bad-applies-to.yaml")
    assert [p.where for p in problems] == ["rules[0].applies_to[0]", "rules[1].applies_to[0]"]
    assert all("'karyotyp' is not one of" in p.message for p in problems)


def test_broken_quote_file():
    problems = validate_file(BROKEN / "quote-bad.yaml")
    assert {p.where for p in problems} == {"quotes[0].rule", "quotes[0].text"}


def test_needs_review_requires_note():
    data = {
        "version": "2016",
        "section": "1",
        "quotes": [{"rule": "A.B", "cite": {"section": "1", "page": 1, "para": "¶1"}, "text": "x"}],
    }
    assert validate_data(data, Path("q.yaml")) == []
    rule = {
        "id": "A.B",
        "title": "t",
        "applies_to": ["general"],
        "severity": "info",
        "statement": "s",
        "cite": [{"section": "1", "page": 1, "float": "Fig. 6a"}],
        "examples": {"valid": []},
        "review": {"paraphrase_checked": False, "needs_review": True},
    }
    problems = validate_data({"version": "2016", "section": "1", "rules": [rule]}, Path("r.yaml"))
    assert [(p.where, p.message) for p in problems] == [
        ("rules[0].review", "'note' is a required property")
    ]


@pytest.mark.parametrize("label", ["Fig. 7", "Fig. 6a", "Table 4", "Ploidy table", "Fibre diagram"])
def test_float_labels_from_chunks_accepted(label):
    data = {
        "version": "2016",
        "section": "3",
        "quotes": [
            {"rule": "A.B", "cite": {"section": "3", "page": 1, "float": label}, "text": "x"}
        ],
    }
    assert validate_data(data, Path("q.yaml")) == []


def test_file_without_rules_or_quotes_key():
    problems = validate_data({"version": "2016"}, Path("x.yaml"))
    assert "'rules', 'quotes', 'exceptions'" in problems[0].message


def test_kb_check_cli_ok(capsys):
    assert main(["check", str(SAMPLE_RULES.parents[2]), str(SAMPLE_QUOTES.parents[2])]) == 0
    assert "2 file(s), 0 with problems" in capsys.readouterr().out


def test_kb_check_cli_broken(capsys):
    assert main(["check", str(BROKEN)]) == 1
    captured = capsys.readouterr()
    assert "bad-rule-id.yaml:rules[0].id" in captured.err
    assert "9 file(s), 9 with problems" in captured.out


def test_kb_check_cli_missing_path(tmp_path, capsys):
    assert main(["check", str(tmp_path / "nope")]) == 1
    assert "not found" in capsys.readouterr().err
