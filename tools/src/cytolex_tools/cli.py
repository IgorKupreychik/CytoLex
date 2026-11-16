"""`kb` command-line entry point: `uv run kb <subcommand> ...`."""

import argparse
import sys
from pathlib import Path

from .config import REPO_ROOT, iscn_pdf_path

# subcommand -> (help text, task that implements it)
SUBCOMMANDS = {
    "extract": ("Extract per-section source text from the ISCN PDF", "T-002"),
    "tables": ("Render the configured table regions to PNG for review", "T-003"),
    "build": ("Validate the KB and build SQLite (FTS5) + JSON bundle", "T-005"),
    "bands": ("Generate band data from UCSC cytoBand", "T-006"),
}
IMPLEMENTED = {"extract", "tables"}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="kb", description="CytoLex KB tooling")
    sub = parser.add_subparsers(dest="command", required=True)
    for name, (help_text, _task) in SUBCOMMANDS.items():
        p = sub.add_parser(name, help=help_text, description=help_text)
        p.add_argument("--version", default="2016", help="ISCN edition (default: 2016)")
        if name in ("extract", "tables"):
            p.add_argument(
                "--pdf",
                type=Path,
                help="ISCN PDF (default: config/local.yaml or CYTOLEX_ISCN_PDF_<VERSION>)",
            )
            default_out = (
                "kb/source-text/<version>" if name == "extract" else "build/review/tables/<version>"
            )
            p.add_argument("--out", type=Path, help=f"output dir (default: {default_out})")
    return parser


def cmd_extract(args: argparse.Namespace) -> int:
    from .extract import run  # imports pdfplumber; keep `kb --help` fast

    pdf = args.pdf or iscn_pdf_path(args.version)
    if not pdf.exists():
        print(f"kb extract: PDF not found: {pdf}", file=sys.stderr)
        return 1
    out = args.out or REPO_ROOT / "kb" / "source-text" / args.version
    report = run(args.version, pdf, out)
    print(
        f"kb extract: ISCN {args.version}: {report.sections} sections -> {report.chunks} chunks "
        f"in {out} (max {report.max_tokens} tokens)"
    )
    for w in report.warnings:
        print(f"  warning: {w}")
    for p in report.problems:
        print(f"  PROBLEM: {p}", file=sys.stderr)
    if report.problems:
        print(f"kb extract: {len(report.problems)} TOC problem(s), see above", file=sys.stderr)
        return 1
    return 0


def cmd_tables(args: argparse.Namespace) -> int:
    from .extract.review import render  # imports pdfplumber

    pdf = args.pdf or iscn_pdf_path(args.version)
    if not pdf.exists():
        print(f"kb tables: PDF not found: {pdf}", file=sys.stderr)
        return 1
    out = args.out or REPO_ROOT / "build" / "review" / "tables" / args.version
    written = render(args.version, pdf, out, REPO_ROOT / "kb" / "source-text" / args.version)
    print(f"kb tables: ISCN {args.version}: {len(written)} images + index.md in {out}")
    return 0


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if args.command == "extract":
        return cmd_extract(args)
    if args.command == "tables":
        return cmd_tables(args)
    task = SUBCOMMANDS[args.command][1]
    print(f"kb {args.command}: not implemented yet ({task})", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
