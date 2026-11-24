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
SAMPLE_VOCAB = FIXTURES / "vocab" / "2016" / "ch03" / "03-symbols-and-abbreviated-terms.yaml"


@pytest.mark.parametrize("name", ["common", "rules", "quotes", "vocab"])
def test_schemas_are_valid_draft_2020_12(name):
    schema = json.loads((SCHEMA_DIR / f"{name}.schema.json").read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)


@pytest.mark.parametrize(
    "path", [SAMPLE_RULES, SAMPLE_QUOTES, SAMPLE_VOCAB], ids=["rules", "quotes", "vocab"]
)
def test_samples_validate(path):
    assert validate_file(path) == []


# broken file -> (where, message fragment); each file has exactly one defect
EXPECTED = {
    "bad-rule-id.yaml": ("rules[0].id", "is not a valid rule ID"),
    "missing-why.yaml": ("rules[0].examples.invalid[0]", "'why' is a required property"),
    "bad-para-label.yaml": ("rules[0].cite[0].para", "is not a paragraph label"),
    "unknown-field.yaml": ("rules[0]", "unknown field(s) ('severty' was unexpected)"),
    "cite-no-anchor.yaml": ("rules[0].cite[0]", "needs at least one of: para, example, float"),
    "kind-on-level-3.yaml": ("rules[1].fix", "'kind' and 'auto' are only allowed with fix level 1"),
    "duplicate-key.yaml": ("line 31", 'duplicate key "needs_review"'),
    "vocab-bad.yaml": ("entries[0].tokens", "should be non-empty"),
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


@pytest.mark.parametrize(
    ("fix", "ok"),
    [
        ({"level": 1, "kind": "delete", "hint": "h", "auto": True}, True),
        ({"level": 1, "kind": "delete", "hint": "h", "auto": False}, True),
        ({"level": 3, "hint": "h", "auto": True}, False),
    ],
    ids=["level-1-auto", "level-1-not-auto", "level-3-auto"],
)
def test_fix_auto_only_on_level_1(fix, ok):
    """T-096: `fix.auto` (autocorrection) is a level-1 flag."""
    rule = {
        "id": "A.B",
        "title": "t",
        "applies_to": ["general"],
        "severity": "error",
        "statement": "s",
        "cite": [{"section": "1", "page": 1, "para": "¶1"}],
        "examples": {"valid": []},
        "fix": fix,
        "review": {"paraphrase_checked": False, "needs_review": False},
    }
    problems = validate_data({"version": "2016", "section": "1", "rules": [rule]}, Path("r.yaml"))
    if ok:
        assert problems == []
    else:
        assert [(p.where, p.message) for p in problems] == [
            ("rules[0].fix", "'kind' and 'auto' are only allowed with fix level 1")
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
    assert "'rules', 'quotes', 'exceptions', 'entries'" in problems[0].message


def test_kb_check_cli_ok(capsys):
    assert main(["check", str(SAMPLE_RULES.parents[2]), str(SAMPLE_QUOTES.parents[2])]) == 0
    assert "2 file(s), 0 with problems" in capsys.readouterr().out


def test_kb_check_cli_broken(capsys):
    assert main(["check", str(BROKEN)]) == 1
    captured = capsys.readouterr()
    assert "bad-rule-id.yaml:rules[0].id" in captured.err
    assert "10 file(s), 10 with problems" in captured.out


def test_kb_check_cli_missing_path(tmp_path, capsys):
    assert main(["check", str(tmp_path / "nope")]) == 1
    assert "not found" in capsys.readouterr().err


def _vocab(**entry) -> dict:
    base = {
        "label": "underlining (single)",
        "tokens": [],
        "kind": "typographic",
        "meanings": [{"text": "x", "sections": ["4.1"]}],
        "cite": {"section": "3", "page": 35, "para": "¶3"},
    }
    return {
        "version": "2016",
        "section": "3",
        "vocabulary": "symbols",
        "source": [{"section": "3", "page": 35, "para": "¶3"}],
        "review": {"paraphrase_checked": False, "needs_review": False},
        "entries": [{**base, **entry}],
    }


def test_vocab_typographic_may_have_no_tokens():
    assert validate_data(_vocab(), Path("v.yaml")) == []


@pytest.mark.parametrize(
    ("entry", "where"),
    [
        ({"kind": "glyph", "tokens": ["x"]}, "entries[0].kind"),
        ({"tokens": ["a b"], "kind": "abbreviation"}, "entries[0].tokens[0]"),
        ({"pair": {"open": "("}}, "entries[0].pair"),
        ({"meanings": []}, "entries[0].meanings"),
        ({"symbol": "x"}, "entries[0]"),
    ],
)
def test_vocab_schema_rejects(entry, where):
    problems = validate_data(_vocab(**entry), Path("v.yaml"))
    assert [p.where for p in problems] == [where], problems


# --- translation files (T-057) -----------------------------------------------------------


@pytest.mark.parametrize("name", ["translation-templates", "translation-golden"])
def test_translation_schemas_are_valid_draft_2020_12(name):
    schema = json.loads((SCHEMA_DIR / f"{name}.schema.json").read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)


def _template(**over) -> dict:
    t = {
        "construct": "deletion",
        "key": "vocab:symbols/del",
        "slots": ["chromosome"],
        "forms": [{"text": "deletion of chromosome {chromosome}"}],
        "review": {"checked": False},
    }
    return {"version": "2016", "language": "en", "templates": [t | over]}


def _golden(**over) -> dict:
    e = {
        "input": "47,XY,+21",
        "origin": "synthetic",
        "context": "unknown",
        "summary": "s",
        "items": [{"span": {"start": 6, "text": "+21"}, "sentence": "x", "kb": ["band:21q22.3"]}],
        "review": {"checked": False},
    }
    return {"version": "2016", "golden": [{k: v for k, v in (e | over).items() if v is not None}]}


def test_translation_samples_validate():
    assert validate_data(_template(), Path("templates.en.yaml")) == []
    assert validate_data(_golden(), Path("golden.yaml")) == []


@pytest.mark.parametrize(
    ("data", "where"),
    [
        (_template(construct="unknown_thing"), "templates[0].construct"),
        (_template(key="DEL.FORMAT"), "templates[0].key"),
        (_template(forms=[]), "templates[0].forms"),
        (_template(review={"checked": "no"}), "templates[0].review.checked"),
        (_golden(context="acquired"), "golden[0].context"),
        (_golden(origin=None), "golden[0]"),
        (_golden(cite={"section": "4.1", "page": 37, "example": "E1"}), "golden[0]"),
        (
            _golden(items=[{"span": {"start": 6, "text": "+21"}, "sentence": "x", "kb": ["21q"]}]),
            "golden[0].items[0].kb[0]",
        ),
    ],
    ids=[
        "construct",
        "key",
        "no-forms",
        "review",
        "context",
        "no-cite-or-origin",
        "cite-and-origin",
        "kb-ref",
    ],
)
def test_translation_schema_rejects(data, where):
    problems = validate_data(data, Path("x.yaml"))
    assert [p.where for p in problems] == [where], problems


# --- Test corpus (T-062) -------------------------------------------------------------------------

CORPUS_DIR = SCHEMA_DIR.parents[1] / "tests" / "data" / "corpus"


def test_corpus_schema_is_valid_draft_2020_12():
    schema = json.loads((SCHEMA_DIR / "corpus.schema.json").read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)


def _corpus(**over) -> dict:
    e = {
        "id": "K-001",
        "iscn": "47,XY,+21",
        "types": ["karyotype"],
        "features": ["trisomy"],
        "rules": ["KARYO.GAIN_LOSS_SIGN"],
        "source": "synthetic",
        "expected": {"2016": {"verdict": "valid", "context": "constitutional"}},
        "review": {"checked": False},
    }
    return {
        "format": "cytolex-corpus",
        "version": 1,
        "corpus": [{k: v for k, v in (e | over).items() if v is not None}],
    }


def test_corpus_sample_validates():
    assert validate_data(_corpus(), Path("karyotype.yaml")) == []
    invalid = {"2016": {"verdict": "invalid", "fires": ["SYM.NO_OTHER_SPACE"]}}
    assert validate_data(_corpus(expected=invalid), Path("x.yaml")) == []
    cite = {"section": "4.1", "page": 37, "example": "E1"}
    assert validate_data(_corpus(source="book-adapted", cite=cite), Path("x.yaml")) == []


@pytest.mark.parametrize(
    ("data", "where"),
    [
        (_corpus(source="lab"), "corpus[0].source"),
        (_corpus(expected=None), "corpus[0]"),
        (_corpus(expected={"2016": {"verdict": "invalid"}}), "corpus[0].expected.2016"),
        (
            _corpus(expected={"2016": {"verdict": "valid", "fires": ["SYM.NO_OTHER_SPACE"]}}),
            "corpus[0].expected.2016",
        ),
        (_corpus(expected={"16": {"verdict": "valid"}}), "corpus[0].expected"),
        (_corpus(source="book-adapted"), "corpus[0]"),
        (_corpus(id="K1"), "corpus[0].id"),
        (_corpus(types=["fish"]), "corpus[0].types[0]"),
        (_corpus(rules=["del"]), "corpus[0].rules[0]"),
    ],
    ids=[
        "lab-source",
        "no-expected",
        "invalid-without-fires",
        "valid-with-fires",
        "edition-key",
        "book-adapted-without-cite",
        "id",
        "type",
        "rule-id",
    ],
)
def test_corpus_schema_rejects(data, where):
    problems = validate_data(data, Path("x.yaml"))
    assert [p.where for p in problems] == [where], problems


def test_committed_corpus_files_validate():
    files = sorted(CORPUS_DIR.glob("*.yaml"))
    assert files, f"no corpus files in {CORPUS_DIR}"
    for f in files:
        assert validate_file(f) == [], f
