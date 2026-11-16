"""`kb` command-line entry point: `uv run kb <subcommand> ...`."""

import argparse
import sys

# subcommand -> (help text, task that implements it)
SUBCOMMANDS = {
    "extract": ("Extract per-section source text from the ISCN PDF", "T-002"),
    "build": ("Validate the KB and build SQLite (FTS5) + JSON bundle", "T-005"),
    "bands": ("Generate band data from UCSC cytoBand", "T-006"),
}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="kb", description="CytoLex KB tooling")
    sub = parser.add_subparsers(dest="command", required=True)
    for name, (help_text, _task) in SUBCOMMANDS.items():
        p = sub.add_parser(name, help=help_text, description=help_text)
        p.add_argument("--version", default="2016", help="ISCN edition (default: 2016)")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    task = SUBCOMMANDS[args.command][1]
    print(f"kb {args.command}: not implemented yet ({task})", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
