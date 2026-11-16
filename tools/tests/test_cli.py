import pytest

from cytolex_tools.cli import main
from cytolex_tools.config import iscn_pdf_path


def test_help_lists_subcommands(capsys):
    with pytest.raises(SystemExit) as exc:
        main(["--help"])
    assert exc.value.code == 0
    out = capsys.readouterr().out
    for name in ("extract", "tables", "build", "bands"):
        assert name in out


def test_extract_missing_pdf_exits_1(tmp_path, capsys):
    assert main(["extract", "--pdf", str(tmp_path / "missing.pdf"), "--out", str(tmp_path)]) == 1
    assert "PDF not found" in capsys.readouterr().err


def test_pdf_path_env_override(monkeypatch, tmp_path):
    monkeypatch.setenv("CYTOLEX_ISCN_PDF_2016", str(tmp_path / "x.pdf"))
    assert iscn_pdf_path("2016") == tmp_path / "x.pdf"


def test_pdf_path_from_config(monkeypatch, tmp_path):
    monkeypatch.delenv("CYTOLEX_ISCN_PDF_2016", raising=False)
    cfg = tmp_path / "local.yaml"
    cfg.write_text('iscn_pdf:\n  "2016": "C:/books/iscn2016.pdf"\n', encoding="utf-8")
    assert iscn_pdf_path("2016", cfg).as_posix() == "C:/books/iscn2016.pdf"


def test_tables_missing_pdf_exits_1(tmp_path, capsys):
    assert main(["tables", "--pdf", str(tmp_path / "missing.pdf"), "--out", str(tmp_path)]) == 1
    assert "PDF not found" in capsys.readouterr().err
