"""`kb build`: check the whole KB and compile it to SQLite (FTS5) + a JSON bundle.

Checks (errors fail the build; nothing is written then):
  1. schema (`kb.schema.validate_file`), and the right file kind per directory
  2. file `version` = edition directory; file name starts with the file's `section`
  3. unique rule IDs per edition
  4. `related` IDs exist in the same edition
  5. quote files pair with a rule file at the same relative path, with the same `section`,
     and quote `rule` IDs exist in that rule file
  6. (chunks present) every cite's section, label and page match the chunk front matter
  7. (chunks present) quote text occurs verbatim (whitespace-normalised) in a cited block
Warnings: chunks missing (6-7 skipped); a valid example's text not found in its cited E block.
"""

import json
import os
import re
import sqlite3
from collections.abc import Iterable
from dataclasses import dataclass, field
from pathlib import Path

from .chunks import Section, load_sections, normalise, squash
from .cite import render_cite
from .schema import Problem, iter_yaml_files, load_yaml, validate_file

FORMAT_VERSION = 1
EDITION_RE = re.compile(r"^[0-9]{4}$")
FILE_SECTION_RE = re.compile(r"^(\d+(?:\.\d+)*)-")
ANCHORS = ("para", "example", "float")


@dataclass
class KbFile:
    path: Path
    rel: str  # path relative to the edition directory (posix)
    data: dict


@dataclass
class Edition:
    version: str
    rule_files: list[KbFile] = field(default_factory=list)
    quote_files: list[KbFile] = field(default_factory=list)
    chunks_checked: bool = False


@dataclass
class BuildReport:
    editions: list[Edition] = field(default_factory=list)
    errors: list[Problem] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    outputs: list[Path] = field(default_factory=list)

    def rules(self) -> Iterable[tuple[str, KbFile, dict]]:
        for ed in self.editions:
            for f in ed.rule_files:
                for rule in f.data["rules"]:
                    yield ed.version, f, rule

    def quotes(self) -> Iterable[tuple[str, KbFile, dict]]:
        for ed in self.editions:
            for f in ed.quote_files:
                for q in f.data["quotes"]:
                    yield ed.version, f, q

    @property
    def unchecked(self) -> list[tuple[str, KbFile, dict]]:
        return [r for r in self.rules() if not r[2]["review"]["paraphrase_checked"]]

    @property
    def needs_review(self) -> list[tuple[str, KbFile, dict]]:
        return [r for r in self.rules() if r[2]["review"]["needs_review"]]


# --- loading -----------------------------------------------------------------------------


def find_editions(kb_dir: Path) -> list[str]:
    found = set()
    for sub in ("rules", "source-quotes"):
        d = kb_dir / sub
        if d.is_dir():
            found |= {p.name for p in d.iterdir() if p.is_dir() and EDITION_RE.match(p.name)}
    return sorted(found)


def _load_dir(directory: Path, kind: str, errors: list[Problem]) -> list[KbFile]:
    if not directory.is_dir():
        return []
    out = []
    for path in iter_yaml_files([directory]):
        problems = validate_file(path)
        if problems:
            errors.extend(problems)
            continue
        data = load_yaml(path)
        if kind not in data:
            other = "rules" if kind == "quotes" else "quotes"
            errors.append(Problem(path, "", f"a '{other}' file in the {kind} directory"))
            continue
        out.append(KbFile(path, path.relative_to(directory).as_posix(), data))
    return out


def _file_section(path: Path) -> str | None:
    m = FILE_SECTION_RE.match(path.name)
    return ".".join(str(int(p)) for p in m[1].split(".")) if m else None


def _check_file_header(f: KbFile, version: str, errors: list[Problem]) -> None:
    if f.data["version"] != version:
        errors.append(
            Problem(f.path, "version", f"{f.data['version']!r} != edition directory {version!r}")
        )
    named = _file_section(f.path)
    if named is None:
        errors.append(Problem(f.path, "", "file name must start with its section, e.g. 09.02-…"))
    elif named != f.data["section"]:
        errors.append(
            Problem(f.path, "section", f"{f.data['section']!r} != file name section {named!r}")
        )


# --- checks ------------------------------------------------------------------------------


def _iter_cites(f: KbFile) -> Iterable[tuple[str, dict]]:
    if "rules" in f.data:
        for i, rule in enumerate(f.data["rules"]):
            for j, c in enumerate(rule["cite"]):
                yield f"rules[{i}].cite[{j}]", c
            for kind in ("valid", "invalid"):
                for j, ex in enumerate(rule["examples"].get(kind) or []):
                    if "cite" in ex:
                        yield f"rules[{i}].examples.{kind}[{j}].cite", ex["cite"]
    else:
        for i, q in enumerate(f.data["quotes"]):
            yield f"quotes[{i}].cite", q["cite"]


def _check_cite(
    f: KbFile, where: str, cite: dict, sections: dict[str, Section], errors: list[Problem]
) -> Section | None:
    sec = sections.get(cite["section"])
    if sec is None:
        errors.append(Problem(f.path, where, f"§{cite['section']} not found in the chunks"))
        return None
    ok = True
    for key in ANCHORS:
        label = cite.get(key)
        if label is None:
            continue
        if label not in sec.labels:
            errors.append(Problem(f.path, where, f"{label} not found in §{sec.section}"))
            ok = False
        elif sec.labels[label] != cite["page"]:
            errors.append(
                Problem(
                    f.path,
                    where,
                    f"page {cite['page']} != p.{sec.labels[label]} of {label} in §{sec.section}",
                )
            )
            ok = False
    return sec if ok else None


def _check_edition(ed: Edition, source_dir: Path, report: BuildReport) -> None:
    errors = report.errors
    for f in ed.rule_files + ed.quote_files:
        _check_file_header(f, ed.version, errors)

    # 3. unique IDs; 4. related IDs exist
    seen: dict[str, str] = {}
    for f in ed.rule_files:
        for i, rule in enumerate(f.data["rules"]):
            here = f"{f.path}:rules[{i}]"
            if rule["id"] in seen:
                errors.append(
                    Problem(
                        f.path,
                        f"rules[{i}].id",
                        f"duplicate rule ID {rule['id']} (first at {seen[rule['id']]})",
                    )
                )
            else:
                seen[rule["id"]] = here
    for f in ed.rule_files:
        for i, rule in enumerate(f.data["rules"]):
            for j, rid in enumerate(rule.get("related") or []):
                if rid not in seen:
                    errors.append(
                        Problem(
                            f.path,
                            f"rules[{i}].related[{j}]",
                            f"unknown rule ID {rid} in ISCN {ed.version}",
                        )
                    )

    # 5. quote files pair with rule files
    by_rel = {f.rel: f for f in ed.rule_files}
    for qf in ed.quote_files:
        rf = by_rel.get(qf.rel)
        if rf is None:
            errors.append(
                Problem(qf.path, "", f"orphan quote file: no rule file rules/{ed.version}/{qf.rel}")
            )
            continue
        if qf.data["section"] != rf.data["section"]:
            errors.append(
                Problem(
                    qf.path,
                    "section",
                    f"{qf.data['section']!r} != rule file section {rf.data['section']!r}",
                )
            )
        ids = {r["id"] for r in rf.data["rules"]}
        for i, q in enumerate(qf.data["quotes"]):
            if q["rule"] not in ids:
                errors.append(
                    Problem(
                        qf.path,
                        f"quotes[{i}].rule",
                        f"orphan quote: {q['rule']} is not in {rf.path.name}",
                    )
                )

    # 6-7. against the chunks
    sections = load_sections(source_dir) if source_dir.is_dir() else {}
    if not sections:
        report.warnings.append(
            f"ISCN {ed.version}: no chunks in {source_dir}; citation and quote checks skipped "
            "(run `kb extract`)"
        )
        return
    ed.chunks_checked = True
    for f in ed.rule_files + ed.quote_files:
        for where, cite in _iter_cites(f):
            _check_cite(f, where, cite, sections, errors)
    for qf in ed.quote_files:
        for i, q in enumerate(qf.data["quotes"]):
            sec = sections.get(q["cite"]["section"])
            labels = [q["cite"][k] for k in ANCHORS if k in q["cite"]]
            if sec is None or any(lb not in sec.blocks for lb in labels):
                continue  # already reported by the cite check
            text = normalise(q["text"])
            if not any(text in normalise(sec.blocks[lb]) for lb in labels):
                errors.append(
                    Problem(
                        qf.path,
                        f"quotes[{i}].text",
                        f"text not found verbatim in §{sec.section} {'/'.join(labels)}",
                    )
                )
    for f in ed.rule_files:
        for i, rule in enumerate(f.data["rules"]):
            for j, ex in enumerate(rule["examples"].get("valid") or []):
                c = ex.get("cite") or {}
                sec = sections.get(c.get("section"))
                label = c.get("example")
                if sec is None or label not in sec.blocks:
                    continue
                if squash(ex["text"]) not in squash(sec.blocks[label]):
                    report.warnings.append(
                        f"{f.path}:rules[{i}].examples.valid[{j}]: {ex['text']!r} "
                        f"not found in §{sec.section} {label}"
                    )


# --- outputs -----------------------------------------------------------------------------

SCHEMA_SQL = """
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE rules (
  version TEXT NOT NULL, id TEXT NOT NULL, file TEXT NOT NULL, section TEXT NOT NULL,
  title TEXT NOT NULL, severity TEXT NOT NULL, statement TEXT NOT NULL,
  fix_level INTEGER, fix_kind TEXT, fix_hint TEXT,
  paraphrase_checked INTEGER NOT NULL, needs_review INTEGER NOT NULL, review_note TEXT,
  PRIMARY KEY (version, id));
CREATE TABLE rule_applies_to (version TEXT, rule_id TEXT, type TEXT);
CREATE TABLE rule_context (version TEXT, rule_id TEXT, context TEXT);
CREATE TABLE rule_related (version TEXT, rule_id TEXT, related_id TEXT);
CREATE TABLE citations (
  version TEXT, rule_id TEXT, ord INTEGER, section TEXT, page INTEGER,
  para TEXT, example TEXT, float TEXT, rendered TEXT);
CREATE TABLE examples (
  version TEXT, rule_id TEXT, kind TEXT, ord INTEGER, text TEXT,
  why TEXT, corrected TEXT, note TEXT, cite_rendered TEXT);
CREATE TABLE quotes (
  version TEXT, rule_id TEXT, ord INTEGER, section TEXT, page INTEGER,
  para TEXT, example TEXT, float TEXT, rendered TEXT, text TEXT);
CREATE VIRTUAL TABLE rules_fts USING fts5(
  version UNINDEXED, id, title, statement, examples, tokenize = 'unicode61');
CREATE VIRTUAL TABLE quotes_fts USING fts5(
  version UNINDEXED, rule_id, text, tokenize = 'unicode61');
"""


def _cite_row(c: dict) -> tuple:
    return (c["section"], c["page"], c.get("para"), c.get("example"), c.get("float"))


def write_sqlite(report: BuildReport, path: Path) -> None:
    tmp = path.with_suffix(".tmp")
    tmp.unlink(missing_ok=True)
    con = sqlite3.connect(tmp)
    try:
        con.executescript(SCHEMA_SQL)
        meta = {
            "format_version": str(FORMAT_VERSION),
            "editions": ",".join(e.version for e in report.editions),
        }
        for e in report.editions:
            meta[f"chunks_checked.{e.version}"] = str(int(e.chunks_checked))
        con.executemany("INSERT INTO meta VALUES (?, ?)", sorted(meta.items()))
        for v, f, r in report.rules():
            fix, review = r.get("fix") or {}, r["review"]
            con.execute(
                "INSERT INTO rules VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                (
                    v,
                    r["id"],
                    f.rel,
                    f.data["section"],
                    r["title"],
                    r["severity"],
                    r["statement"],
                    fix.get("level"),
                    fix.get("kind"),
                    fix.get("hint"),
                    int(review["paraphrase_checked"]),
                    int(review["needs_review"]),
                    review.get("note"),
                ),
            )
            for t in r["applies_to"]:
                con.execute("INSERT INTO rule_applies_to VALUES (?,?,?)", (v, r["id"], t))
            for c in r.get("context") or []:
                con.execute("INSERT INTO rule_context VALUES (?,?,?)", (v, r["id"], c))
            for rid in r.get("related") or []:
                con.execute("INSERT INTO rule_related VALUES (?,?,?)", (v, r["id"], rid))
            for i, c in enumerate(r["cite"]):
                con.execute(
                    "INSERT INTO citations VALUES (?,?,?,?,?,?,?,?,?)",
                    (v, r["id"], i, *_cite_row(c), render_cite(v, c)),
                )
            texts = []
            for kind in ("valid", "invalid"):
                for i, ex in enumerate(r["examples"].get(kind) or []):
                    texts.append(ex["text"])
                    c = ex.get("cite")
                    con.execute(
                        "INSERT INTO examples VALUES (?,?,?,?,?,?,?,?,?)",
                        (
                            v,
                            r["id"],
                            kind,
                            i,
                            ex["text"],
                            ex.get("why"),
                            ex.get("corrected"),
                            ex.get("note"),
                            render_cite(v, c) if c else None,
                        ),
                    )
            con.execute(
                "INSERT INTO rules_fts VALUES (?,?,?,?,?)",
                (v, r["id"], r["title"], r["statement"], "\n".join(texts)),
            )
        counters: dict[tuple[str, str], int] = {}
        for v, _f, q in report.quotes():
            n = counters.get((v, q["rule"]), 0)
            counters[(v, q["rule"])] = n + 1
            c = q["cite"]
            con.execute(
                "INSERT INTO quotes VALUES (?,?,?,?,?,?,?,?,?,?)",
                (v, q["rule"], n, *_cite_row(c), render_cite(v, c), q["text"]),
            )
            con.execute("INSERT INTO quotes_fts VALUES (?,?,?)", (v, q["rule"], q["text"]))
        con.commit()
    finally:
        con.close()
    os.replace(tmp, path)


def _with_rendered(v: str, c: dict) -> dict:
    return {**c, "rendered": render_cite(v, c)}


def bundle(report: BuildReport) -> dict:
    editions = {}
    for ed in report.editions:
        rules, quotes = [], []
        for f in ed.rule_files:
            for r in f.data["rules"]:
                out = {**r, "file": f.rel, "section": f.data["section"]}
                out["cite"] = [_with_rendered(ed.version, c) for c in r["cite"]]
                out["examples"] = {
                    kind: [
                        {**ex, "cite": _with_rendered(ed.version, ex["cite"])}
                        if "cite" in ex
                        else ex
                        for ex in exs
                    ]
                    for kind, exs in r["examples"].items()
                }
                rules.append(out)
        for f in ed.quote_files:
            for q in f.data["quotes"]:
                quotes.append({**q, "file": f.rel, "cite": _with_rendered(ed.version, q["cite"])})
        editions[ed.version] = {
            "chunks_checked": ed.chunks_checked,
            "rules": rules,
            "quotes": quotes,
        }
    return {"format_version": FORMAT_VERSION, "editions": editions}


def review_markdown(report: BuildReport) -> str:
    lines = ["# KB review status", "", "Generated by `kb build`; do not edit.", ""]
    for ed in report.editions:
        rows = [r for r in report.rules() if r[0] == ed.version]
        needs = [r for r in rows if r[2]["review"]["needs_review"]]
        unchecked = [r for r in rows if not r[2]["review"]["paraphrase_checked"]]
        lines += [
            f"## ISCN {ed.version}",
            "",
            f"{len(rows)} rules · {len(needs)} need review · "
            f"{len(unchecked)} paraphrase not checked",
            "",
        ]
        lines += [f"### `needs_review: true` ({len(needs)})", ""]
        lines += [f"- `{r['id']}` ({f.rel}): {r['review'].get('note', '')}" for _, f, r in needs]
        lines += ["", f"### `paraphrase_checked: false` ({len(unchecked)})", ""]
        lines += [f"- `{r['id']}` ({f.rel})" for _, f, r in unchecked]
        lines.append("")
    return "\n".join(lines)


def _write_text(path: Path, text: str) -> None:
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(text, encoding="utf-8", newline="\n")
    os.replace(tmp, path)


# --- entry point -------------------------------------------------------------------------


def run(
    kb_dir: Path,
    source_text_root: Path,
    out_dir: Path,
    versions: list[str] | None = None,
) -> BuildReport:
    """Check the KB and, when there are no errors, write `kb.sqlite`, `kb.json` and
    `review/kb-review.md` into `out_dir`."""
    report = BuildReport()
    found = find_editions(kb_dir)
    for v in versions or found:
        if v not in found:
            report.warnings.append(f"ISCN {v}: no KB files under {kb_dir}")
        ed = Edition(v)
        ed.rule_files = _load_dir(kb_dir / "rules" / v, "rules", report.errors)
        ed.quote_files = _load_dir(kb_dir / "source-quotes" / v, "quotes", report.errors)
        report.editions.append(ed)
    if report.errors:  # cross-file checks need schema-valid files
        return report
    for ed in report.editions:
        _check_edition(ed, source_text_root / ed.version, report)
    if report.errors:
        return report

    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "review").mkdir(exist_ok=True)
    db, js, md = out_dir / "kb.sqlite", out_dir / "kb.json", out_dir / "review" / "kb-review.md"
    write_sqlite(report, db)
    _write_text(js, json.dumps(bundle(report), ensure_ascii=False, indent=1) + "\n")
    _write_text(md, review_markdown(report))
    report.outputs = [db, js, md]
    return report
