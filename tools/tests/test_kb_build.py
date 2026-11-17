"""T-005: `kb build` — cross-file checks, chunk checks, SQLite/JSON outputs, review report."""

import json
import shutil
import sqlite3
from pathlib import Path

import pytest

from cytolex_tools.kb.build import run
from cytolex_tools.kb.chunks import load_sections, normalise
from cytolex_tools.kb.cite import render_cite

FIXTURES = Path(__file__).parent / "fixtures" / "kb"
REL = Path("2016") / "ch09" / "09.02.17.01-reciprocal-translocations.yaml"
VOCAB_REL = Path("2016") / "ch03" / "03-symbols-and-abbreviated-terms.yaml"


@pytest.fixture
def kb(tmp_path):
    """A copy of the sample KB + chunk excerpt; returns (kb_dir, source_text_root, out_dir)."""
    kb_dir = tmp_path / "kb"
    for sub in ("rules", "source-quotes", "source-text", "vocab"):
        shutil.copytree(FIXTURES / sub, kb_dir / sub)
    return kb_dir, kb_dir / "source-text", tmp_path / "build"


def build(kb, versions=None):
    return run(*kb, versions)


def edit(path: Path, old: str, new: str, count: int = 1) -> None:
    text = path.read_text(encoding="utf-8")
    assert old in text, old
    path.write_text(text.replace(old, new, count), encoding="utf-8")


def rules_file(kb) -> Path:
    return kb[0] / "rules" / REL


def quotes_file(kb) -> Path:
    return kb[0] / "source-quotes" / REL


def vocab_file(kb) -> Path:
    return kb[0] / "vocab" / VOCAB_REL


def assert_fails(report, kb, where: str, fragment: str):
    hits = [e for e in report.errors if e.where == where and fragment in e.message]
    assert hits, report.errors
    assert not kb[2].exists(), "nothing may be written on errors"


# --- success -----------------------------------------------------------------------------


def test_build_ok(kb):
    report = build(kb)
    assert report.errors == []
    assert report.warnings == []
    assert report.editions[0].chunks_checked
    out = kb[2]
    con = sqlite3.connect(out / "kb.sqlite")
    assert con.execute("SELECT count(*) FROM rules").fetchone() == (2,)
    assert con.execute("SELECT count(*) FROM quotes").fetchone() == (1,)
    hit = con.execute(
        "SELECT id FROM rules_fts WHERE rules_fts MATCH 'autosome' ORDER BY id"
    ).fetchall()
    assert hit == [("TRANSLOC.CHROMOSOME_ORDER",)]
    assert con.execute(
        "SELECT rule_id FROM quotes_fts WHERE quotes_fts MATCH 'translocations'"
    ).fetchall() == [("TRANSLOC.CHROMOSOME_ORDER",)]
    assert con.execute(
        "SELECT rendered FROM citations WHERE rule_id = 'TRANSLOC.CHROMOSOME_ORDER'"
    ).fetchall() == [("ISCN 2016 §9.2.17.1, p.75, ¶1",)]
    assert con.execute(
        "SELECT cite_rendered FROM examples WHERE kind = 'valid' ORDER BY ord"
    ).fetchall() == [("ISCN 2016 §9.2.17.1, p.75, ex. E1",), ("ISCN 2016 §9.2.17.1, p.75, ex. E3",)]
    assert dict(con.execute("SELECT key, value FROM meta").fetchall())["chunks_checked.2016"] == "1"
    con.close()

    data = json.loads((out / "kb.json").read_text(encoding="utf-8"))
    ed = data["editions"]["2016"]
    assert data["format_version"] == 2
    assert [r["id"] for r in ed["rules"]] == [
        "TRANSLOC.CHROMOSOME_ORDER",
        "TRANSLOC.THREE_WAY_ORDER",
    ]
    assert ed["rules"][0]["file"] == REL.relative_to("2016").as_posix()
    assert ed["rules"][0]["cite"][0]["rendered"] == "ISCN 2016 §9.2.17.1, p.75, ¶1"
    assert ed["quotes"][0]["rule"] == "TRANSLOC.CHROMOSOME_ORDER"
    sym = ed["vocab"]["symbols"]
    assert sym["file"] == VOCAB_REL.relative_to("2016").as_posix()
    labels = [e["label"] for e in sym["entries"]]
    assert labels[:3] == ["brackets, square ([ ])", "decimal point (.)", "del"]
    assert sym["entries"][2]["cite"]["rendered"] == "ISCN 2016 §3, p.34, ¶2"

    review = (out / "review" / "kb-review.md").read_text(encoding="utf-8")
    assert "### `needs_review: true` (1)" in review
    assert "### `paraphrase_checked: false` (2)" in review
    assert "`symbols` (ch03/03-symbols-and-abbreviated-terms.yaml, 5 entries)" in review
    assert [r["id"] for _, _, r in report.needs_review] == ["TRANSLOC.CHROMOSOME_ORDER"]
    assert len(report.unchecked) == 2


def test_empty_kb_builds(tmp_path):
    report = run(tmp_path / "kb", tmp_path / "src", tmp_path / "out")
    assert report.errors == [] and report.editions == []
    con = sqlite3.connect(tmp_path / "out" / "kb.sqlite")
    assert con.execute("SELECT count(*) FROM rules").fetchone() == (0,)
    con.close()


def test_without_quotes(kb):
    shutil.rmtree(kb[0] / "source-quotes")
    report = build(kb)
    assert report.errors == []
    con = sqlite3.connect(kb[2] / "kb.sqlite")
    assert con.execute("SELECT count(*) FROM quotes").fetchone() == (0,)
    con.close()


def test_without_chunks_warns_and_skips(kb):
    shutil.rmtree(kb[1])
    edit(quotes_file(kb), "always specified first", "never specified")  # would fail check 7
    report = build(kb)
    assert report.errors == []
    assert any("checks skipped" in w for w in report.warnings)
    assert not report.editions[0].chunks_checked


def test_version_filter(kb):
    other = kb[0] / "rules" / "2020" / REL.relative_to("2016")
    other.parent.mkdir(parents=True)
    other.write_text(
        rules_file(kb).read_text(encoding="utf-8").replace('"2016"', '"2020"', 1), encoding="utf-8"
    )
    all_report = build(kb)
    assert [e.version for e in all_report.editions] == ["2016", "2020"]
    assert any("2020" in w and "checks skipped" in w for w in all_report.warnings)
    only = build(kb, ["2016"])
    assert [e.version for e in only.editions] == ["2016"]
    data = json.loads((kb[2] / "kb.json").read_text(encoding="utf-8"))
    assert list(data["editions"]) == ["2016"]


def test_example_text_mismatch_is_warning(kb):
    edit(rules_file(kb), '- text: "46,X,t(X;13)(q27;q12)"', '- text: "46,X,t(X;13)(q26;q12)"')
    report = build(kb)
    assert report.errors == []
    assert any("examples.valid[1]" in w and "E3" in w for w in report.warnings)


# --- errors ------------------------------------------------------------------------------


def test_schema_error(kb):
    edit(rules_file(kb), "severity: error", "severity: fatal")
    assert_fails(build(kb), kb, "rules[0].severity", "is not one of")


def test_quote_file_in_rules_dir(kb):
    shutil.copy(quotes_file(kb), rules_file(kb).with_name("09.02.17.02-x.yaml"))
    assert_fails(build(kb), kb, "", "'quotes' file in the rules directory")


def test_version_not_directory(kb):
    edit(rules_file(kb), 'version: "2016"', 'version: "2020"')
    assert_fails(build(kb), kb, "version", "'2020' != edition directory '2016'")


def test_section_not_file_name(kb):
    edit(rules_file(kb), 'section: "9.2.17.1"\nrules', 'section: "9.2.17"\nrules')
    assert_fails(build(kb), kb, "section", "!= file name section '9.2.17.1'")


def test_duplicate_rule_id(kb):
    edit(rules_file(kb), "- id: TRANSLOC.THREE_WAY_ORDER", "- id: TRANSLOC.CHROMOSOME_ORDER")
    assert_fails(build(kb), kb, "rules[1].id", "duplicate rule ID TRANSLOC.CHROMOSOME_ORDER")


def test_duplicate_rule_id_across_files(kb):
    second = rules_file(kb).with_name("09.02.17.01-copy.yaml")
    shutil.copy(rules_file(kb), second)
    report = build(kb)
    dups = [e for e in report.errors if "duplicate" in e.message]
    assert len(dups) == 2  # both rules of the later file (sorted: "-copy" < "-reciprocal")
    assert all(rules_file(kb).name == e.file.name for e in dups)
    assert all(f"first at {second}" in e.message for e in dups)


def test_unknown_related(kb):
    edit(rules_file(kb), "related: [TRANSLOC.CHROMOSOME_ORDER]", "related: [TRANSLOC.NOPE]")
    assert_fails(build(kb), kb, "rules[1].related[0]", "unknown rule ID TRANSLOC.NOPE")


def test_orphan_quote_file(kb):
    quotes_file(kb).rename(quotes_file(kb).with_name("09.02.17.02-other.yaml"))
    edit(
        quotes_file(kb).with_name("09.02.17.02-other.yaml"),
        '"9.2.17.1"\nquotes',
        '"9.2.17.2"\nquotes',
    )
    assert_fails(build(kb), kb, "", "orphan quote file")


def test_orphan_quote_rule(kb):
    edit(quotes_file(kb), "- rule: TRANSLOC.CHROMOSOME_ORDER", "- rule: TRANSLOC.NOPE")
    assert_fails(build(kb), kb, "quotes[0].rule", "orphan quote: TRANSLOC.NOPE")


def test_cite_unknown_section(kb):
    edit(
        rules_file(kb),
        '{section: "9.2.17.1", page: 75, para: "¶1"}',
        '{section: "9.2.99", page: 75, para: "¶1"}',
    )
    assert_fails(build(kb), kb, "rules[0].cite[0]", "§9.2.99 not found in the chunks")


def test_cite_unknown_label(kb):
    edit(rules_file(kb), "page: 75, example: E3}", "page: 75, example: E9}")
    assert_fails(build(kb), kb, "rules[0].examples.valid[1].cite", "E9 not found in §9.2.17.1")


def test_cite_wrong_page(kb):
    edit(quotes_file(kb), "page: 75", "page: 76")
    assert_fails(build(kb), kb, "quotes[0].cite", "page 76 != p.75 of ¶1")


def test_quote_text_not_verbatim(kb):
    edit(quotes_file(kb), "always specified first", "usually specified first")
    assert_fails(build(kb), kb, "quotes[0].text", "text not found verbatim in §9.2.17.1 ¶1")


def test_failed_build_keeps_previous_outputs(kb):
    assert build(kb).errors == []
    before = (kb[2] / "kb.json").read_text(encoding="utf-8")
    edit(quotes_file(kb), "- rule: TRANSLOC.CHROMOSOME_ORDER", "- rule: TRANSLOC.NOPE")
    assert build(kb).errors
    assert (kb[2] / "kb.json").read_text(encoding="utf-8") == before


# --- helpers -----------------------------------------------------------------------------


@pytest.mark.parametrize(
    "cite, text",
    [
        ({"section": "9.2.17.1", "page": 75, "para": "¶1"}, "ISCN 2016 §9.2.17.1, p.75, ¶1"),
        (
            {"section": "9.2.17.1", "page": 75, "para": "¶2", "example": "E3"},
            "ISCN 2016 §9.2.17.1, p.75, ¶2, ex. E3",
        ),
        ({"section": "9.2.17.1", "page": 75, "example": "E3"}, "ISCN 2016 §9.2.17.1, p.75, ex. E3"),
        ({"section": "2.2", "page": 12, "float": "Table 1"}, "ISCN 2016 §2.2, p.12, Table 1"),
    ],
)
def test_render_cite(cite, text):
    assert render_cite("2016", cite) == text


def test_chunk_blocks_and_parts(tmp_path):
    for part, body in (
        (1, "[¶1 p.5] First\nline.\n\n#### Sub\n\n[E1 p.5]\n```\nA\n```\n"),
        (2, "[E2 p.6]\n```\nB\n```\n> Note.\n"),
    ):
        labels = "paragraphs: {¶1: 5}\nexamples: {E1: 5}" if part == 1 else "examples: {E2: 6}"
        (tmp_path / f"01.02-x-p{part}.md").write_text(
            f"---\nsection: '1.2'\n{labels}\n---\n{body}", encoding="utf-8"
        )
    sec = load_sections(tmp_path)["1.2"]
    assert sec.labels == {"¶1": 5, "E1": 5, "E2": 6}
    assert normalise(sec.blocks["¶1"]) == "First line."  # heading ends the block
    assert normalise(sec.blocks["E2"]) == "B Note."


def test_cli_build(kb, monkeypatch, capsys):
    from cytolex_tools import cli

    root = kb[0].parent
    assert kb[1] == root / "kb" / "source-text"  # same layout as the repo
    monkeypatch.setattr(cli, "REPO_ROOT", root)
    assert cli.main(["build"]) == 0
    out = capsys.readouterr().out
    assert "ISCN 2016: 2 rules, 1 quotes, 1 vocabularies (5 entries) (chunk checks: on)" in out
    assert "2016 symbols (ch03/03-symbols-and-abbreviated-terms.yaml)" in out
    assert "TRANSLOC.CHROMOSOME_ORDER (ch09/09.02.17.01-reciprocal-translocations.yaml)" in out
    assert "wrote build/kb.sqlite, build/kb.json, build/review/kb-review.md" in out

    edit(quotes_file(kb), "- rule: TRANSLOC.CHROMOSOME_ORDER", "- rule: TRANSLOC.NOPE")
    assert cli.main(["build"]) == 1
    err = capsys.readouterr().err
    assert "quotes[0].rule: orphan quote: TRANSLOC.NOPE" in err
    assert "FAILED, 1 problem(s); nothing written" in err


# --- vocab (T-008) -----------------------------------------------------------------------


def test_vocab_sqlite(kb):
    assert build(kb).errors == []
    con = sqlite3.connect(kb[2] / "kb.sqlite")
    assert con.execute("SELECT count(*) FROM vocab").fetchone() == (5,)
    assert con.execute(
        "SELECT label FROM vocab_tokens WHERE token = '.' ORDER BY label"
    ).fetchall() == [("decimal point (.)",), ("period (.)",)]
    assert con.execute(
        "SELECT tokens, pair_open, pair_close FROM vocab WHERE label LIKE 'brackets%'"
    ).fetchone() == ('["[", "]"]', "[", "]")
    assert con.execute(
        "SELECT tokens, note IS NOT NULL FROM vocab WHERE kind = 'typographic'"
    ).fetchone() == ("[]", 1)
    con.close()


def test_vocab_missing_book_row(kb):
    edit(vocab_file(kb), "  - label: del\n", "  - label: dl\n")
    report = build(kb)
    assert_fails(report, kb, "entries", "book row 'del' has no entry")
    assert_fails(report, kb, "entries[2].label", "'dl' is not a row of the source table")


def test_vocab_source_block_missing(kb):
    edit(vocab_file(kb), '  - {section: "3", page: 35, para: "¶3"}\n', "")
    report = build(kb)
    assert_fails(report, kb, "entries[3].label", "'period (.)' is not a row of the source table")


def test_vocab_entry_cites_wrong_block(kb):
    old = '"16.3.1"]}\n    cite: {section: "3", page: 34, para: "¶2"}'
    new = '"16.3.1"]}\n    cite: {section: "3", page: 35, para: "¶3"}'
    edit(vocab_file(kb), old, new)
    assert_fails(build(kb), kb, "entries[2].cite", "'del' is printed in another source block")


def test_vocab_duplicate_token_needs_shared_tokens(kb):
    edit(vocab_file(kb), 'shared_tokens: ["."]\n', "")
    assert_fails(build(kb), kb, "entries[3].tokens", "token '.' also in entries[1]")


def test_vocab_stale_shared_token(kb):
    edit(vocab_file(kb), 'shared_tokens: ["."]', 'shared_tokens: [".", "del"]')
    assert_fails(build(kb), kb, "shared_tokens", "'del' is not shared by two entries")


def test_vocab_pair_not_in_tokens(kb):
    edit(vocab_file(kb), 'pair: {open: "[", close: "]"}', 'pair: {open: "[", close: ")"}')
    assert_fails(build(kb), kb, "entries[0].pair.close", "')' not in tokens")


def test_vocab_duplicate_vocabulary(kb):
    f = vocab_file(kb)
    f.with_name("03-copy.yaml").write_text(f.read_text(encoding="utf-8"), encoding="utf-8")
    assert_fails(build(kb), kb, "vocabulary", "duplicate vocabulary 'symbols'")


def test_vocab_file_in_rules_dir(kb):
    shutil.copy(vocab_file(kb), kb[0] / "rules" / "2016" / "03-x.yaml")
    report = build(kb)
    assert any("'entries' file in the rules directory" in e.message for e in report.errors)


def test_first_column():
    from cytolex_tools.kb.build import first_column

    block = "\n|  |  |\n|---|---|\n| a (\\|) | x |\n| b | y \\| z |\n\n> note\n"
    assert first_column(block) == ["a (|)", "b"]
    assert first_column("| H | I |\n|---|---|\n| r | s |") == ["r"]
