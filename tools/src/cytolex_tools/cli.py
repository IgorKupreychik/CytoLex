"""`kb` command-line entry point: `uv run kb <subcommand> ...`."""

import argparse
import sys
from pathlib import Path

from .config import REPO_ROOT, iscn_pdf_path

# subcommand -> (help text, task that implements it)
SUBCOMMANDS = {
    "extract": ("Extract per-section source text from the ISCN PDF", "T-002"),
    "tables": ("Render the configured table regions to PNG for review", "T-003"),
    "check": ("Check KB and test-corpus YAML files against the schemas in kb/schema/", "T-004"),
    "build": ("Validate the KB and build SQLite (FTS5) + JSON bundle", "T-005"),
    "bands": ("Generate band data from NCBI GDP ideograms + UCSC cytoBand", "T-006"),
    "ideograms": ("Decode the band labels of the book's idiograms from the ISCN PDF", "T-007"),
    "samples": ("Write batch sample files from the KB examples to tests/data/batch/", "T-038"),
}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="kb", description="CytoLex KB tooling")
    sub = parser.add_subparsers(dest="command", required=True)
    for name, (help_text, _task) in SUBCOMMANDS.items():
        p = sub.add_parser(name, help=help_text, description=help_text)
        if name == "check":
            p.add_argument(
                "paths",
                nargs="*",
                type=Path,
                help="files or directories (default: kb/rules/, kb/source-quotes/ and kb/vocab/)",
            )
            continue
        if name == "build":
            p.add_argument(
                "--version", help="build only this ISCN edition (default: every edition in kb/)"
            )
            continue
        p.add_argument("--version", default="2016", help="ISCN edition (default: 2016)")
        if name in ("extract", "tables", "ideograms"):
            p.add_argument(
                "--pdf",
                type=Path,
                help="ISCN PDF (default: config/local.yaml or CYTOLEX_ISCN_PDF_<VERSION>)",
            )
            default_out = {
                "extract": "kb/source-text/<version>",
                "tables": "build/review/tables/<version>",
                "ideograms": "kb/bands/<version>",
            }[name]
            p.add_argument("--out", type=Path, help=f"output dir (default: {default_out})")
        if name == "bands":
            p.add_argument("--out", type=Path, help="output dir (default: kb/bands/<version>)")
            p.add_argument(
                "--cache",
                type=Path,
                help="download cache (default: build/cache/bands/<version>)",
            )
            p.add_argument(
                "--cache-only", action="store_true", help="don't download; use the cache only"
            )
            p.add_argument(
                "--update-pins",
                action="store_true",
                help="accept the current source files and rewrite their sha256 pins",
            )
            p.add_argument("--sources", type=Path, help=argparse.SUPPRESS)  # tests
            p.add_argument("--book", type=Path, help=argparse.SUPPRESS)  # tests
            p.add_argument("--review", type=Path, help=argparse.SUPPRESS)  # tests
        if name == "samples":
            p.add_argument(
                "--out", type=Path, help="output dir (default: tests/data/batch/<version>)"
            )
            p.add_argument("--kb", type=Path, help=argparse.SUPPRESS)  # tests
        if name == "ideograms":
            p.add_argument(
                "--review",
                type=Path,
                help="review crops dir (default: build/review/bands/<version>)",
            )
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


def cmd_check(args: argparse.Namespace) -> int:
    from .kb.schema import iter_yaml_files, validate_file

    # `uv run --directory tools` runs in tools/, so relative paths fall back to the repo root.
    paths = [p if p.is_absolute() or p.exists() else REPO_ROOT / p for p in args.paths] or [
        REPO_ROOT / "kb" / "rules",
        REPO_ROOT / "kb" / "source-quotes",
        REPO_ROOT / "kb" / "vocab",
        REPO_ROOT / "kb" / "translation",
        REPO_ROOT / "tests" / "data" / "corpus",
    ]
    missing = [p for p in paths if not p.exists() and args.paths]
    for p in missing:
        print(f"kb check: not found: {p}", file=sys.stderr)
    files = list(iter_yaml_files(p for p in paths if p.exists()))
    problems = [pr for f in files for pr in validate_file(f)]
    for pr in problems:
        print(f"  {pr}", file=sys.stderr)
    bad = len({pr.file for pr in problems})
    print(f"kb check: {len(files)} file(s), {bad} with problems, {len(problems)} problem(s)")
    return 1 if problems or missing else 0


def cmd_build(args: argparse.Namespace) -> int:
    from .kb.build import run

    for stream in (sys.stdout, sys.stderr):  # ISCN glyphs (→, ¶) on a cp1252 console
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(errors="replace")
    report = run(
        REPO_ROOT / "kb",
        REPO_ROOT / "kb" / "source-text",
        REPO_ROOT / "build",
        [args.version] if args.version else None,
    )
    if not report.editions:
        print("kb build: no editions under kb/rules/, kb/source-quotes/ or kb/vocab/ (empty KB)")
    for ed in report.editions:
        n_rules = sum(len(f.data["rules"]) for f in ed.rule_files)
        n_quotes = sum(len(f.data["quotes"]) for f in ed.quote_files)
        n_vocab = sum(len(f.data["entries"]) for f in ed.vocab_files)
        checks = "on" if ed.chunks_checked else "skipped"
        bands = f"{len(ed.bands['bands'])} bands" if ed.bands else "no bands"
        print(
            f"kb build: ISCN {ed.version}: {n_rules} rules, {n_quotes} quotes, "
            f"{len(ed.vocab_files)} vocabularies ({n_vocab} entries), {bands} "
            f"(chunk checks: {checks})"
        )
    for w in report.warnings:
        print(f"  warning: {w}")
    for pr in report.errors:
        print(f"  {pr}", file=sys.stderr)
    if report.errors:
        print(
            f"kb build: FAILED, {len(report.errors)} problem(s); nothing written", file=sys.stderr
        )
        return 1
    needs, unchecked = report.needs_review, report.unchecked
    print(f"review: {len(needs)} rule(s) with needs_review: true")
    for v, f, r in needs:
        print(f"  {v} {r['id']} ({f.rel}): {r['review']['note']}")
    print(f"review: {len(unchecked)} rule(s) with paraphrase_checked: false")
    for v, f, r in unchecked:
        print(f"  {v} {r['id']} ({f.rel})")
    vocab = report.vocab_unchecked
    print(f"review: {len(vocab)} vocabulary file(s) not yet checked or needing review")
    for v, f in vocab:
        print(f"  {v} {f.data['vocabulary']} ({f.rel})")
    rel = ", ".join(p.relative_to(REPO_ROOT).as_posix() for p in report.outputs)
    print(f"kb build: wrote {rel}")
    return 0


def cmd_bands(args: argparse.Namespace) -> int:
    from .bands import run

    out = args.out or REPO_ROOT / "kb" / "bands" / args.version
    cache = args.cache or REPO_ROOT / "build" / "cache" / "bands" / args.version
    try:
        report = run(
            args.version,
            out,
            cache,
            args.cache_only,
            args.update_pins,
            args.sources,
            args.book,
            args.review,
        )
    except FileNotFoundError as exc:
        print(f"kb bands: {exc}", file=sys.stderr)
        return 1
    for pid in report.pinned:
        print(f"  pinned: {pid}")
    for w in report.warnings:
        print(f"  warning: {w}")
    for pr in report.problems:
        print(f"  PROBLEM: {pr}", file=sys.stderr)
    if report.problems:
        if report.draft:
            print(f"  draft with every difference: {report.draft}", file=sys.stderr)
        print(
            f"kb bands: FAILED, {len(report.problems)} problem(s); nothing written",
            file=sys.stderr,
        )
        return 1
    c = report.counts
    per_level = _per_level(c)
    print(
        f"kb bands: ISCN {args.version}: {c['bands']} band names "
        f"({per_level}, {c['derived']} derived) -> {report.out}"
    )
    return 0


def _per_level(counts: dict[str, int]) -> str:
    return ", ".join(
        f"{v} at {k.removeprefix('level_')}" for k, v in counts.items() if k.startswith("level_")
    )


def cmd_ideograms(args: argparse.Namespace) -> int:
    from .bands.ideograms import run

    out = args.out or REPO_ROOT / "kb" / "bands" / args.version
    review = args.review or REPO_ROOT / "build" / "review" / "bands" / args.version
    try:
        pdf = args.pdf or iscn_pdf_path(args.version)
        report = run(args.version, pdf, out, review)
    except (FileNotFoundError, KeyError) as exc:
        print(f"kb ideograms: {exc}", file=sys.stderr)
        return 1
    for w in report.warnings:
        print(f"  warning: {w}")
    for pr in report.problems:
        print(f"  PROBLEM: {pr}", file=sys.stderr)
    if report.problems:
        print(
            f"kb ideograms: FAILED, {len(report.problems)} problem(s); nothing written",
            file=sys.stderr,
        )
        return 1
    c = report.counts
    per_level = _per_level(c)
    print(
        f"kb ideograms: ISCN {args.version}: {c['labels']} labels ({per_level}; "
        f"{c['ranges']} ranges) -> {report.out}; review crops in {review}"
    )
    if c.get("split_by_centromere"):
        print(
            f"  {c['split_by_centromere']} idiograms had an ambiguous label order at the "
            "centromere (p11 | q11.1); split at the drawn centromere"
        )
    return 0


def cmd_samples(args: argparse.Namespace) -> int:
    from .kb.samples import run

    out = args.out or REPO_ROOT / "tests" / "data" / "batch" / args.version
    try:
        report = run(args.version, args.kb or REPO_ROOT / "build" / "kb.json", out)
    except (FileNotFoundError, KeyError) as exc:
        print(f"kb samples: {exc}", file=sys.stderr)
        return 1
    files = ", ".join(f"{name} ({n})" for name, n in report.per_file.items())
    print(
        f"kb samples: ISCN {args.version}: {report.rows} rows, {report.unique} unique strings "
        f"-> {report.out}: {files}"
    )
    return 0


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if args.command == "extract":
        return cmd_extract(args)
    if args.command == "tables":
        return cmd_tables(args)
    if args.command == "check":
        return cmd_check(args)
    if args.command == "build":
        return cmd_build(args)
    if args.command == "ideograms":
        return cmd_ideograms(args)
    if args.command == "samples":
        return cmd_samples(args)
    return cmd_bands(args)


if __name__ == "__main__":
    sys.exit(main())
