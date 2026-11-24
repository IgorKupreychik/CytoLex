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
    for sub in ("bands", "rules", "source-quotes", "source-text", "vocab"):
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
    assert "`symbols` (ch03/03-symbols-and-abbreviated-terms.yaml, 6 entries)" in review
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


def _typed_example(kb) -> None:
    """E1 gets a printed minus; its valid example uses the typed hyphen-minus (T-063)."""
    chunk = kb[1] / "2016" / "ch09" / "09.02.17.01-reciprocal-translocations.md"
    edit(chunk, "46,XY,t(2;5)(q21;q31)\n", "46,XY,t(2;5)(q21;q31),–7\n")
    edit(rules_file(kb), '- text: "46,XY,t(2;5)(q21;q31)"', '- text: "46,XY,t(2;5)(q21;q31),-7"')


def test_typed_form_of_equivalent_entry_is_not_a_mismatch(kb):
    _typed_example(kb)
    report = build(kb)
    assert report.errors == []
    assert not any("examples.valid[0]" in w for w in report.warnings), report.warnings


def test_typed_form_without_equivalent_forms_is_a_mismatch(kb):
    _typed_example(kb)
    edit(vocab_file(kb), "    equivalent_forms: true\n", "")
    report = build(kb)
    assert report.errors == []
    assert any("examples.valid[0]" in w and "E1" in w for w in report.warnings)


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
    names = ("kb.json", "kb-runtime.json")
    before = [(kb[2] / n).read_bytes() for n in names]
    edit(quotes_file(kb), "- rule: TRANSLOC.CHROMOSOME_ORDER", "- rule: TRANSLOC.NOPE")
    assert build(kb).errors
    assert [(kb[2] / n).read_bytes() for n in names] == before


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
    assert (
        "ISCN 2016: 2 rules, 1 quotes, 1 vocabularies (6 entries), 4 bands (chunk checks: on)"
    ) in out
    assert "2016 symbols (ch03/03-symbols-and-abbreviated-terms.yaml)" in out
    assert "TRANSLOC.CHROMOSOME_ORDER (ch09/09.02.17.01-reciprocal-translocations.yaml)" in out
    wrote = "build/kb.sqlite, build/kb.json, build/kb-runtime.json, build/review/kb-review.md"
    assert f"wrote {wrote}" in out

    edit(quotes_file(kb), "- rule: TRANSLOC.CHROMOSOME_ORDER", "- rule: TRANSLOC.NOPE")
    assert cli.main(["build"]) == 1
    err = capsys.readouterr().err
    assert "quotes[0].rule: orphan quote: TRANSLOC.NOPE" in err
    assert "FAILED, 1 problem(s); nothing written" in err


# --- vocab (T-008) -----------------------------------------------------------------------


def test_fix_auto_sqlite(kb):
    """T-097: the rule browser reads the autocorrection flag (`fix.auto`) from kb.sqlite."""
    edit(rules_file(kb), "      kind: reorder\n", "      kind: reorder\n      auto: true\n")
    report = build(kb)
    assert report.errors == []
    con = sqlite3.connect(kb[2] / "kb.sqlite")
    rows = con.execute("SELECT id, fix_auto FROM rules ORDER BY id").fetchall()
    con.close()
    assert rows == [("TRANSLOC.CHROMOSOME_ORDER", 1), ("TRANSLOC.THREE_WAY_ORDER", 0)]


def test_vocab_sqlite(kb):
    assert build(kb).errors == []
    con = sqlite3.connect(kb[2] / "kb.sqlite")
    assert con.execute("SELECT count(*) FROM vocab").fetchone() == (6,)
    assert con.execute(
        "SELECT label FROM vocab_tokens WHERE token = '.' ORDER BY label"
    ).fetchall() == [("decimal point (.)",), ("period (.)",)]
    assert con.execute(
        "SELECT tokens, pair_open, pair_close FROM vocab WHERE label LIKE 'brackets%'"
    ).fetchone() == ('["[", "]"]', "[", "]")
    assert con.execute(
        "SELECT tokens, note IS NOT NULL FROM vocab WHERE kind = 'typographic'"
    ).fetchone() == ("[]", 1)
    assert con.execute("SELECT label FROM vocab WHERE equivalent_forms = 1").fetchall() == [
        ("minus sign (–)",)
    ]
    # the vocab file's section, for the rule browser's section filter (T-059)
    assert con.execute("SELECT DISTINCT section FROM vocab").fetchall() == [("3",)]
    con.close()


def test_vocab_missing_book_row(kb):
    edit(vocab_file(kb), "  - label: del\n", "  - label: dl\n")
    report = build(kb)
    assert_fails(report, kb, "entries", "book row 'del' has no entry")
    assert_fails(report, kb, "entries[2].label", "'dl' is not a row of the source table")


def test_vocab_source_block_missing(kb):
    edit(vocab_file(kb), '  - {section: "3", page: 35, para: "¶3"}\n', "")
    report = build(kb)
    assert_fails(report, kb, "entries[4].label", "'period (.)' is not a row of the source table")


def test_vocab_entry_cites_wrong_block(kb):
    old = '"16.3.1"]}\n    cite: {section: "3", page: 34, para: "¶2"}'
    new = '"16.3.1"]}\n    cite: {section: "3", page: 35, para: "¶3"}'
    edit(vocab_file(kb), old, new)
    assert_fails(build(kb), kb, "entries[2].cite", "'del' is printed in another source block")


def test_vocab_duplicate_token_needs_shared_tokens(kb):
    edit(vocab_file(kb), 'shared_tokens: ["."]\n', "")
    assert_fails(build(kb), kb, "entries[4].tokens", "token '.' also in entries[1]")


def test_vocab_equivalent_forms_needs_two_tokens_and_no_pair(kb):
    pair = '    pair: {open: "[", close: "]"}\n'
    edit(vocab_file(kb), '    tokens: ["–", "-"]', '    tokens: ["–"]')
    edit(vocab_file(kb), pair, pair + "    equivalent_forms: true\n")
    report = build(kb)
    assert_fails(report, kb, "entries[0].equivalent_forms", "no pair")
    assert_fails(report, kb, "entries[3].equivalent_forms", "2+ tokens")


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


# --- runtime bundle (T-060) --------------------------------------------------------------


def bands_file(kb) -> Path:
    return kb[0] / "bands" / "2016" / "bands.yaml"


def runtime(kb) -> dict:
    return json.loads((kb[2] / "kb-runtime.json").read_text(encoding="utf-8"))


def test_runtime_bundle_content(kb):
    report = build(kb)
    assert report.errors == [] and report.warnings == []
    data = runtime(kb)
    assert data["format_version"] == 1
    ed = data["editions"]["2016"]
    assert set(ed) == {"rules", "vocab", "bands"}  # no quotes
    rule = ed["rules"][0]
    assert rule["id"] == "TRANSLOC.CHROMOSOME_ORDER"
    assert set(rule) <= {
        "id",
        "section",
        "title",
        "severity",
        "statement",
        "applies_to",
        "context",
        "related",
        "fix",
        "review",
        "cite",
    }
    assert {"id", "section", "title", "severity", "statement", "applies_to"} <= set(rule)
    assert rule["section"] == "9.2.17.1"
    assert rule["review"]["needs_review"] is True and rule["review"]["note"]
    assert set(rule["review"]) == {"needs_review", "note"}
    assert rule["cite"][0]["rendered"] == "ISCN 2016 §9.2.17.1, p.75, ¶1"
    assert rule["cite"][0]["page"] == 75
    entries = ed["vocab"]["symbols"]["entries"]
    labels = [e["label"] for e in entries]
    assert labels[:3] == ["brackets, square ([ ])", "decimal point (.)", "del"]
    assert entries[2]["cite"] == "ISCN 2016 §3, p.34, ¶2"
    assert all({"label", "kind", "tokens", "meanings", "cite"} <= set(e) for e in entries)
    assert not any("note" in e for e in entries)
    assert set(ed["vocab"]["symbols"]) <= {"entries", "shared_tokens"}
    bands = ed["bands"]
    assert bands["levels_available"] == [300, 400, 550, 700, 850]
    by_name = {b["name"]: b for b in bands["bands"]}
    assert list(by_name) == ["1p36", "1p36.3", "1q32.11", "6p24.1"]
    assert by_name["1p36.3"] == {
        "name": "1p36.3",
        "chrom": "1",
        "arm": "p",
        "parent": "1p36",
        "levels": [300, 400, 550, 700],
        "derived": False,
        "book": True,
        "grch37": [1, 7200000],
        "grch38": [1, 7100000],
    }
    assert by_name["6p24.1"]["book"] is False
    assert by_name["1q32.11"]["grch37"] is None and by_name["1q32.11"]["grch38"] is None
    full = json.loads((kb[2] / "kb.json").read_text(encoding="utf-8"))
    assert full["format_version"] == 2  # the full KB keeps its own format


def test_runtime_bundle_byte_identical(kb):
    assert build(kb).errors == []
    first = [(kb[2] / n).read_bytes() for n in ("kb.json", "kb-runtime.json")]
    assert build(kb).errors == []
    assert [(kb[2] / n).read_bytes() for n in ("kb.json", "kb-runtime.json")] == first
    assert first[1].endswith(b"\n") and b"\n" not in first[1][:-1]  # compact, one line


def test_missing_bands_warns(kb):
    shutil.rmtree(kb[0] / "bands")
    report = build(kb)
    assert report.errors == []
    assert any("no " in w and "bands.yaml" in w for w in report.warnings)
    assert runtime(kb)["editions"]["2016"]["bands"] is None


def test_bands_version_mismatch(kb):
    edit(bands_file(kb), "version: '2016'", "version: '2020'")
    report = build(kb)
    assert any(e.where == "version" and "edition '2016'" in e.message for e in report.errors)
    assert not kb[2].exists()


def test_bands_duplicate_name(kb):
    edit(bands_file(kb), "name: '1q32.11'", "name: '1p36.3'")
    assert_fails(build(kb), kb, "bands[2].name", "duplicate band '1p36.3' (first at bands[1])")


def test_bands_missing_key(kb):
    edit(bands_file(kb), ", book: false}", "}")
    assert_fails(build(kb), kb, "bands[3]", "missing key(s) book")


def exceptions_file(kb) -> Path:
    return kb[0] / "bands" / "2016" / "exceptions.yaml"


def test_sqlite_bands_and_exceptions(kb):
    """T-084: the rule browser reads the bands and the band exceptions from kb.sqlite."""
    assert build(kb).errors == []
    con = sqlite3.connect(kb[2] / "kb.sqlite")
    rows = con.execute(
        "SELECT name, ord, chrom, arm, parent, levels, derived, stain, book, "
        "grch37_start, grch37_end, grch38_start, grch38_end FROM bands ORDER BY ord"
    ).fetchall()
    assert [r[0] for r in rows] == ["1p36", "1p36.3", "1q32.11", "6p24.1"]
    assert rows[1] == (
        "1p36.3",
        1,
        "1",
        "p",
        "1p36",
        "[300, 400, 550, 700]",
        0,
        "gneg",
        1,
        1,
        7200000,
        1,
        7100000,
    )
    assert rows[0][4] is None and rows[0][6] == 1  # nesting ancestor
    assert rows[2][7:] == (None, 1, None, None, None, None)  # no coordinates
    assert rows[3][8] == 0  # not in the book
    assert con.execute(
        "SELECT value FROM meta WHERE key = 'bands.levels_available.2016'"
    ).fetchone() == ("300,400,550,700,850",)
    ex = con.execute(
        "SELECT ord, level, region, resolution, book, ncbi, keep, cite_rendered, note "
        "FROM band_exceptions"
    ).fetchall()
    assert len(ex) == 1
    assert ex[0][:7] == (
        0,
        850,
        "6p24",
        "book",
        '["6p24"]',
        '["6p24.3", "6p24.2", "6p24.1"]',
        '["6p24.3", "6p24.2", "6p24.1"]',
    )
    assert ex[0][7] == "ISCN 2016 §2.5, p.21, Fig. 5"
    assert ex[0][8].startswith("Fig. 5 shows 6p24 undivided")


def test_sqlite_without_band_exceptions(kb):
    exceptions_file(kb).unlink()
    assert build(kb).errors == []
    con = sqlite3.connect(kb[2] / "kb.sqlite")
    assert con.execute("SELECT count(*) FROM bands").fetchone() == (4,)
    assert con.execute("SELECT count(*) FROM band_exceptions").fetchone() == (0,)


def test_band_exceptions_schema_error(kb):
    edit(exceptions_file(kb), "resolution: book", "resolution: maybe")
    report = build(kb)
    assert any("maybe" in e.message for e in report.errors), report.errors
    assert not kb[2].exists()


def test_band_exceptions_version_mismatch(kb):
    edit(exceptions_file(kb), "version: '2016'", "version: '2020'")
    report = build(kb)
    assert any(e.where == "version" and "edition '2016'" in e.message for e in report.errors)
    assert not kb[2].exists()


# --- translation (T-057) -----------------------------------------------------------------

TEMPLATES = """\
version: "2016"
language: en
templates:
  - construct: deletion
    key: "vocab:symbols/del"
    slots: [chromosome, from, to]
    forms:
      - when: terminal
        text: "terminal deletion of chromosome {chromosome} at {from}"
      - when: interstitial
        text: "interstitial deletion of chromosome {chromosome} between {from} and {to}"
    review: {checked: false}
  - construct: translocation
    key: "rule:TRANSLOC.CHROMOSOME_ORDER"
    slots: [chromosomes]
    forms:
      - text: "translocation between chromosomes {chromosomes}"
        readings: {neoplastic: "acquired translocation between chromosomes {chromosomes}"}
    review: {checked: true}
"""

GOLDEN = """\
version: "2016"
golden:
  - input: "46,XY,t(2;5)(q21;q31)"
    cite: {section: "9.2.17.1", page: 75, example: "E1"}
    context: unknown
    summary: "46 chromosomes; sex chromosomes XY; 1 abnormality."
    items:
      - span: {start: 6, text: "t(2;5)(q21;q31)"}
        sentence: "Translocation between chromosomes 2 and 5."
        kb: ["rule:TRANSLOC.CHROMOSOME_ORDER", "band:1p36.3"]
    review: {checked: false}
  - input: "47,XY,+21"
    origin: synthetic
    context: unknown
    summary: "47 chromosomes; sex chromosomes XY; 1 abnormality."
    items:
      - span: {start: 6, text: "+21"}
        sentence: "Gain of chromosome 21."
        readings: {constitutional: "An extra chromosome 21.", neoplastic: "Gain of chromosome 21."}
        kb: ["vocab:symbols/minus sign (–)"]
    review: {checked: false}
"""


@pytest.fixture
def tkb(kb, monkeypatch):
    """The sample KB plus translation files; the construct list cut to the two templated ones."""
    monkeypatch.setattr(
        "cytolex_tools.kb.build.template_constructs", lambda: ["deletion", "translocation"]
    )
    d = kb[0] / "translation" / "2016"
    d.mkdir(parents=True)
    (d / "templates.en.yaml").write_text(TEMPLATES, encoding="utf-8")
    (d / "golden.yaml").write_text(GOLDEN, encoding="utf-8")
    return kb


def templates_file(kb) -> Path:
    return kb[0] / "translation" / "2016" / "templates.en.yaml"


def golden_file(kb) -> Path:
    return kb[0] / "translation" / "2016" / "golden.yaml"


def test_translation_ok(tkb):
    report = build(tkb)
    assert report.errors == [] and report.warnings == []
    full = json.loads((tkb[2] / "kb.json").read_text(encoding="utf-8"))["editions"]["2016"]
    assert [t["construct"] for t in full["translation"]["templates"]["en"]] == [
        "deletion",
        "translocation",
    ]
    assert [g["input"] for g in full["translation"]["golden"]] == [
        "46,XY,t(2;5)(q21;q31)",
        "47,XY,+21",
    ]
    rt = runtime(tkb)["editions"]["2016"]["translation"]
    assert set(rt) == {"templates"}  # no golden file in the runtime bundle
    assert rt["templates"]["en"][1]["forms"][0]["readings"]["neoplastic"].startswith("acquired")
    review = (tkb[2] / "review" / "kb-review.md").read_text(encoding="utf-8")
    assert "- `templates.en.yaml`: 2 entries, 1 not reviewed" in review
    assert "- `golden.yaml`: 2 entries, 2 not reviewed" in review


def test_without_translation_no_bundle_key(kb):
    assert build(kb).errors == []
    assert "translation" not in runtime(kb)["editions"]["2016"]


def test_template_unknown_key(tkb):
    edit(templates_file(tkb), "vocab:symbols/del", "vocab:symbols/dl")
    assert_fails(build(tkb), tkb, "templates[0].key", "unknown KB entry vocab:symbols/dl")


def test_template_missing_construct(tkb, monkeypatch):
    monkeypatch.setattr(
        "cytolex_tools.kb.build.template_constructs",
        lambda: ["deletion", "translocation", "ring"],
    )
    assert_fails(build(tkb), tkb, "templates", "no template for construct(s): ring")


def test_template_duplicate_construct(tkb):
    edit(templates_file(tkb), "construct: translocation", "construct: deletion")
    assert_fails(build(tkb), tkb, "templates[1].construct", "duplicate construct deletion")


def test_template_undeclared_placeholder(tkb):
    edit(templates_file(tkb), "at {from}", "at {band}")
    assert_fails(build(tkb), tkb, "templates[0].forms[0].text", "{band} is not a declared slot")


def test_template_placeholder_in_reading(tkb):
    edit(templates_file(tkb), "acquired translocation between chromosomes {chromosomes}", "{x}")
    assert_fails(
        build(tkb), tkb, "templates[1].forms[0].readings.neoplastic", "{x} is not a declared slot"
    )


def test_template_unbalanced_brace(tkb):
    edit(templates_file(tkb), "at {from}", "at {from")
    report = build(tkb)
    assert any("unbalanced" in e.message for e in report.errors), report.errors


def test_template_several_forms_need_when(tkb):
    edit(templates_file(tkb), "      - when: interstitial\n        text:", "      - text:")
    assert_fails(build(tkb), tkb, "templates[0].forms[1]", "'when' is required")


def test_template_duplicate_when(tkb):
    edit(templates_file(tkb), "when: interstitial", "when: terminal")
    assert_fails(build(tkb), tkb, "templates[0].forms[1].when", "duplicate when terminal")


def test_template_unused_slot_warns(tkb):
    edit(templates_file(tkb), "slots: [chromosomes]", "slots: [chromosomes, extra]")
    report = build(tkb)
    assert report.errors == []
    assert any("slot extra is not used" in w for w in report.warnings)


def test_template_language_must_match_file_name(tkb):
    edit(templates_file(tkb), "language: en", "language: de")
    assert_fails(build(tkb), tkb, "language", "'de' != file name language 'en'")


def test_golden_span_must_match_input(tkb):
    edit(golden_file(tkb), 'start: 6, text: "t(2;5)', 'start: 5, text: "t(2;5)')
    assert_fails(build(tkb), tkb, "golden[0].items[0].span", "is not at offset 5")


def test_golden_unknown_band(tkb):
    edit(golden_file(tkb), "band:1p36.3", "band:1p37")
    assert_fails(build(tkb), tkb, "golden[0].items[0].kb[1]", "unknown KB entry band:1p37")


def test_golden_duplicate_input(tkb):
    edit(golden_file(tkb), '"47,XY,+21"', '"46,XY,t(2;5)(q21;q31)"')
    assert_fails(build(tkb), tkb, "golden[1].input", "duplicate input")


def test_golden_cite_checked_against_chunks(tkb):
    edit(golden_file(tkb), "page: 75", "page: 76")
    report = build(tkb)
    assert any(e.where == "golden[0].cite" for e in report.errors), report.errors


def test_golden_needs_cite_or_origin(tkb):
    edit(golden_file(tkb), "    origin: synthetic\n", "")
    report = build(tkb)
    assert any(e.where == "golden[1]" for e in report.errors), report.errors
