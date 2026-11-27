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
  8. vocab files (T-008): unique vocabulary names, labels and tokens (except `shared_tokens`),
     `pair` tokens listed in `tokens`, `equivalent_forms` only on non-pair entries with 2+
     tokens; (chunks present) the labels equal the first-column
     cells of the `source` tables, and each entry's label is in its own cited block
  9. (T-060) `kb/bands/<version>/bands.yaml`: `version` = edition, every band has the keys the
     runtime bundle carries, unique band names; (T-084) `curated/bands/<version>/exceptions.yaml`,
     if present, matches its schema (its exceptions go into `kb.sqlite` for the rule browser)
 10. (T-057) `curated/translation/<version>/`: `templates.<language>.yaml` (keys are existing
     rules or vocab items, unique keys and constructs, every construct covered, placeholders are
     declared slots, variant names unique and required with several forms) and `golden.yaml` (unique
     inputs, spans match the input, KB references exist, cites match the chunks)
 11. (T-102) `curated/probes/probes.csv`, if present: header and row schema, then the checks of
     `probes.check` against every edition's band data (the table is edition-independent)
 12. (T-108) `curated/associations/associations.yaml`, if present: schema, then the checks of
     `associations.check` (unique IDs, bands in every edition's band data, copy regions vs bands)
Warnings: chunks missing (6-7 skipped); a valid example's text not found in its cited E block
(whitespace ignored; typed forms of `equivalent_forms` vocab entries count as printed, T-053);
no `bands.yaml` for an edition (its runtime bundle then has `bands: null`); a template slot no
form uses; a conflict in an unreviewed probe-table row or association rule.

Inputs: the person-maintained files under `curated/` (rules, source-quotes, vocab, translation,
band exceptions, probe table, association rules) and the generated `kb/bands/<version>/bands.yaml`.

Outputs: `kb.sqlite` (rule browser), `kb.json` (full KB, also read by the KB-example tests),
`kb-runtime.json` (lean runtime bundle embedded in `CytoLex.Core`, T-060) and
`review/kb-review.md` (+ `review/probes-review.md` with a probe table,
`review/associations-review.md` with association rules).
"""

import json
import os
import re
import sqlite3
from collections.abc import Iterable
from dataclasses import dataclass, field
from pathlib import Path

from ..associations.check import CheckResult as AssociationCheck
from ..associations.check import check as check_associations
from ..associations.report import review_markdown as associations_review
from ..probes import table as probe_table
from ..probes.check import CheckResult
from ..probes.check import check as check_probes
from ..probes.names import example_probe_names
from ..probes.report import review_markdown as probes_review
from .chunks import Section, load_sections, normalise, squash
from .cite import render_cite
from .schema import SCHEMA_DIR, Problem, iter_yaml_files, load_yaml, validate_file

FORMAT_VERSION = 2  # 2: `vocab` (T-008)
RUNTIME_FORMAT_VERSION = 1  # kb-runtime.json (T-060); independent of FORMAT_VERSION
BAND_KEYS = ("name", "chrom", "arm", "parent", "levels", "derived", "book", "grch37", "grch38")
RUNTIME_BAND_KEYS = (*BAND_KEYS, "stain")  # stain added without a format bump (T-101: ideograms)
EDITION_RE = re.compile(r"^[0-9]{4}$")
FILE_SECTION_RE = re.compile(r"^(\d+(?:\.\d+)*)-")
SEPARATOR_RE = re.compile(r"^\|(\s*:?-+:?\s*\|)+$")
PIPE_RE = re.compile(r"(?<!\\)\|")
ANCHORS = ("para", "example", "float")
KIND_DIRS = {"rules": "rules", "quotes": "source-quotes", "entries": "vocab"}
TRANSLATION_DIR = "translation"  # templates.<language>.yaml + golden.yaml (T-057)
TEMPLATES_FILE_RE = re.compile(r"^templates\.([a-z]{2})\.ya?ml$")
PLACEHOLDER_RE = re.compile(r"\{([^{}]*)\}")
SLOT_RE = re.compile(r"^[a-z][a-z0-9_]*$")


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
    vocab_files: list[KbFile] = field(default_factory=list)
    template_files: list[KbFile] = field(default_factory=list)  # T-057
    golden_file: KbFile | None = None  # T-057
    bands: dict | None = None  # parsed kb/bands/<version>/bands.yaml
    band_exceptions: list[dict] = field(default_factory=list)  # exceptions.yaml (T-084)
    chunks_checked: bool = False
    sections: dict[str, Section] = field(default_factory=dict)  # the chunks, when present


@dataclass
class BuildReport:
    editions: list[Edition] = field(default_factory=list)
    errors: list[Problem] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    outputs: list[Path] = field(default_factory=list)
    probes: probe_table.Table | None = None  # curated/probes/probes.csv (T-102)
    probe_check: CheckResult | None = None
    associations: list[dict] | None = None  # curated/associations/associations.yaml (T-108)
    association_check: AssociationCheck | None = None

    def rules(self) -> Iterable[tuple[str, KbFile, dict]]:
        for ed in self.editions:
            for f in ed.rule_files:
                for rule in f.data["rules"]:
                    yield ed.version, f, rule

    def vocab_files(self) -> Iterable[tuple[str, KbFile]]:
        for ed in self.editions:
            for f in ed.vocab_files:
                yield ed.version, f

    @property
    def vocab_unchecked(self) -> list[tuple[str, KbFile]]:
        return [
            (v, f)
            for v, f in self.vocab_files()
            if f.data["review"]["needs_review"] or not f.data["review"]["paraphrase_checked"]
        ]

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
    for sub in (*KIND_DIRS.values(), TRANSLATION_DIR):
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
            other = next(k for k in KIND_DIRS if k in data)
            errors.append(Problem(path, "", f"a '{other}' file in the {KIND_DIRS[kind]} directory"))
            continue
        out.append(KbFile(path, path.relative_to(directory).as_posix(), data))
    return out


def _load_translation(directory: Path, ed: Edition, errors: list[Problem]) -> None:
    """Template and golden files of one edition (T-057), schema-checked."""
    if not directory.is_dir():
        return
    for path in iter_yaml_files([directory]):
        problems = validate_file(path)
        if problems:
            errors.extend(problems)
            continue
        data = load_yaml(path)
        f = KbFile(path, path.relative_to(directory).as_posix(), data)
        if "templates" in data:
            ed.template_files.append(f)
        elif "golden" in data:
            ed.golden_file = f
        else:
            other = next(k for k in data if k != "version")
            errors.append(Problem(path, "", f"a '{other}' file in the {TRANSLATION_DIR} directory"))


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
    elif "quotes" in f.data:
        for i, q in enumerate(f.data["quotes"]):
            yield f"quotes[{i}].cite", q["cite"]
    else:
        for i, c in enumerate(f.data["source"]):
            yield f"source[{i}]", c
        for i, e in enumerate(f.data["entries"]):
            yield f"entries[{i}].cite", e["cite"]


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


def _block_key(cite: dict) -> tuple:
    return (cite["section"], *(cite.get(k) for k in ANCHORS))


def first_column(block: str) -> list[str]:
    """First-column cells of the Markdown table in a chunk block (rows above `|---|` are header)."""
    rows: list[str] = []
    for line in block.splitlines():
        s = line.strip()
        if len(s) < 2 or not (s.startswith("|") and s.endswith("|")):
            continue
        if SEPARATOR_RE.match(s):
            rows = []
            continue
        rows.append(PIPE_RE.split(s[1:-1])[0].strip().replace("\\|", "|"))
    return rows


def _check_vocab_tokens(f: KbFile, errors: list[Problem]) -> dict[str, int]:
    """Unique labels and tokens, pair tokens in `tokens`; returns label -> entry index."""
    labels: dict[str, int] = {}
    owners: dict[str, list[int]] = {}
    for i, e in enumerate(f.data["entries"]):
        if e["label"] in labels:
            errors.append(
                Problem(
                    f.path,
                    f"entries[{i}].label",
                    f"duplicate label {e['label']!r} (first at entries[{labels[e['label']]}])",
                )
            )
        labels.setdefault(e["label"], i)
        for tok in e["tokens"]:
            owners.setdefault(tok, []).append(i)
        for k, tok in (e.get("pair") or {}).items():
            if tok not in e["tokens"]:
                errors.append(Problem(f.path, f"entries[{i}].pair.{k}", f"{tok!r} not in tokens"))
        if e.get("equivalent_forms") and ("pair" in e or len(e["tokens"]) < 2):
            errors.append(
                Problem(
                    f.path,
                    f"entries[{i}].equivalent_forms",
                    "needs 2+ tokens and no pair (forms of one symbol)",
                )
            )
    shared = set(f.data.get("shared_tokens") or [])
    for tok, idx in owners.items():
        if len(idx) > 1 and tok not in shared:
            errors.append(
                Problem(
                    f.path,
                    f"entries[{idx[1]}].tokens",
                    f"token {tok!r} also in entries[{idx[0]}] "
                    "(list it in shared_tokens if intended)",
                )
            )
    for tok in sorted(shared):
        if len(owners.get(tok, [])) < 2:
            errors.append(Problem(f.path, "shared_tokens", f"{tok!r} is not shared by two entries"))
    return labels


def _check_vocab_coverage(
    f: KbFile, labels: dict[str, int], sections: dict[str, Section], errors: list[Problem]
) -> None:
    """The entry labels equal the first-column cells of the `source` blocks."""
    printed: dict[str, tuple] = {}  # book row -> block it is printed in
    for i, c in enumerate(f.data["source"]):
        sec = sections.get(c["section"])
        names = [c[k] for k in ANCHORS if k in c]
        if sec is None or any(n not in sec.blocks for n in names):
            continue  # reported by the cite check
        cells = [x for n in names for x in first_column(sec.blocks[n])]
        if not cells:
            errors.append(Problem(f.path, f"source[{i}]", "no table rows in the cited block"))
        for x in cells:
            printed.setdefault(x, _block_key(c))
    for x in printed:
        if x not in labels:
            errors.append(Problem(f.path, "entries", f"book row {x!r} has no entry (coverage)"))
    for x, i in labels.items():
        cite = f.data["entries"][i]["cite"]
        if x not in printed:
            errors.append(
                Problem(f.path, f"entries[{i}].label", f"{x!r} is not a row of the source table")
            )
        elif printed[x] != _block_key(cite):
            errors.append(
                Problem(f.path, f"entries[{i}].cite", f"{x!r} is printed in another source block")
            )


def _check_vocab_files(ed: Edition, sections: dict[str, Section], errors: list[Problem]) -> None:
    names: dict[str, Path] = {}
    for f in ed.vocab_files:
        name = f.data["vocabulary"]
        if name in names:
            errors.append(
                Problem(f.path, "vocabulary", f"duplicate vocabulary {name!r} (also {names[name]})")
            )
        names.setdefault(name, f.path)
        labels = _check_vocab_tokens(f, errors)
        if sections:
            _check_vocab_coverage(f, labels, sections, errors)


def typed_form_fold(ed: Edition):
    """Map typed forms of `equivalent_forms` vocab entries to the printed (first) token."""
    pairs = [
        (tok, e["tokens"][0])
        for f in ed.vocab_files
        for e in f.data["entries"]
        if e.get("equivalent_forms")
        for tok in e["tokens"][1:]
    ]
    pairs.sort(key=lambda p: -len(p[0]))  # `->` before `-`
    if not pairs:
        return lambda s: s
    pattern = re.compile("|".join(re.escape(tok) for tok, _ in pairs))
    table = dict(pairs)
    return lambda s: pattern.sub(lambda m: table[m.group(0)], s)


def _load_bands(ed: Edition, bands_dir: Path, exceptions_dir: Path, report: BuildReport) -> None:
    """Load and check `bands.yaml` (generated by `kb bands`); missing → warning."""
    path = bands_dir / ed.version / "bands.yaml"
    if not path.is_file():
        report.warnings.append(
            f"ISCN {ed.version}: no {path.as_posix()}; "
            "runtime bundle without bands (run `kb bands`)"
        )
        return
    data = load_yaml(path)
    errors = report.errors
    if not isinstance(data, dict) or not isinstance(data.get("bands"), list):
        errors.append(Problem(path, "bands", "missing band list"))
        return
    if str(data.get("version")) != ed.version:
        errors.append(
            Problem(path, "version", f"{data.get('version')!r} != edition {ed.version!r}")
        )
    seen: dict[str, int] = {}
    for i, b in enumerate(data["bands"]):
        missing = [k for k in BAND_KEYS if not isinstance(b, dict) or k not in b]
        if missing:
            errors.append(Problem(path, f"bands[{i}]", f"missing key(s) {', '.join(missing)}"))
            continue
        if b["name"] in seen:
            errors.append(
                Problem(
                    path,
                    f"bands[{i}].name",
                    f"duplicate band {b['name']!r} (first at bands[{seen[b['name']]}])",
                )
            )
        seen.setdefault(b["name"], i)
    ed.bands = data
    _load_band_exceptions(ed, exceptions_dir / ed.version / "exceptions.yaml", report)


def _load_band_exceptions(ed: Edition, path: Path, report: BuildReport) -> None:
    """The cited book-vs-NCBI band decisions (T-007) for the rule browser (T-084); optional."""
    if not path.is_file():
        return
    problems = validate_file(path)
    if problems:
        report.errors.extend(problems)
        return
    data = load_yaml(path)
    if str(data.get("version")) != ed.version:
        report.errors.append(
            Problem(path, "version", f"{data.get('version')!r} != edition {ed.version!r}")
        )
        return
    ed.band_exceptions = data["exceptions"]


def _check_edition(ed: Edition, source_dir: Path, report: BuildReport) -> None:
    errors = report.errors
    for f in ed.rule_files + ed.quote_files + ed.vocab_files:
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
    ed.sections = sections
    _check_vocab_files(ed, sections, errors)
    if not sections:
        report.warnings.append(
            f"ISCN {ed.version}: no chunks in {source_dir}; citation and quote checks skipped "
            "(run `kb extract`)"
        )
        return
    ed.chunks_checked = True
    for f in ed.rule_files + ed.quote_files + ed.vocab_files:
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
    fold = typed_form_fold(ed)
    for f in ed.rule_files:
        for i, rule in enumerate(f.data["rules"]):
            for j, ex in enumerate(rule["examples"].get("valid") or []):
                c = ex.get("cite") or {}
                sec = sections.get(c.get("section"))
                label = c.get("example")
                if sec is None or label not in sec.blocks:
                    continue
                if fold(squash(ex["text"])) not in fold(squash(sec.blocks[label])):
                    report.warnings.append(
                        f"{f.path}:rules[{i}].examples.valid[{j}]: {ex['text']!r} "
                        f"not found in §{sec.section} {label}"
                    )


# --- translation (T-057) -----------------------------------------------------------------


def template_constructs() -> list[str]:
    """The constructs every template set must cover (the schema's `construct` enum)."""
    schema = json.loads(
        (SCHEMA_DIR / "translation-templates.schema.json").read_text(encoding="utf-8")
    )
    return list(schema["$defs"]["construct"]["enum"])


def _kb_refs(ed: Edition) -> tuple[set[str], set[str]]:
    """The edition's rule and vocab references (`rule:ID`, `vocab:key/label`), and its bands."""
    refs = {f"rule:{r['id']}" for f in ed.rule_files for r in f.data["rules"]}
    for f in ed.vocab_files:
        refs |= {f"vocab:{f.data['vocabulary']}/{e['label']}" for e in f.data["entries"]}
    bands = {f"band:{b['name']}" for b in (ed.bands or {}).get("bands", [])}
    return refs, bands


def _check_version(f: KbFile, version: str, errors: list[Problem]) -> None:
    if f.data["version"] != version:
        errors.append(
            Problem(f.path, "version", f"{f.data['version']!r} != edition directory {version!r}")
        )


def _check_placeholders(
    f: KbFile, where: str, text: str, slots: set[str], used: set[str], errors: list[Problem]
) -> None:
    for name in PLACEHOLDER_RE.findall(text):
        used.add(name)
        if not SLOT_RE.match(name) or name not in slots:
            errors.append(Problem(f.path, where, f"placeholder {{{name}}} is not a declared slot"))
    rest = PLACEHOLDER_RE.sub("", text)
    if "{" in rest or "}" in rest:
        errors.append(Problem(f.path, where, "unbalanced { or }"))


def _check_template(
    f: KbFile, i: int, t: dict, refs: set[str], report: BuildReport, version: str
) -> None:
    errors = report.errors
    here = f"templates[{i}]"
    if t["key"] not in refs:
        errors.append(
            Problem(f.path, f"{here}.key", f"unknown KB entry {t['key']} in ISCN {version}")
        )
    slots = set(t.get("slots") or [])
    used: set[str] = set()
    whens: set[str] = set()
    for j, form in enumerate(t["forms"]):
        at = f"{here}.forms[{j}]"
        if "when" in form:
            if form["when"] in whens:
                errors.append(Problem(f.path, f"{at}.when", f"duplicate when {form['when']}"))
            whens.add(form["when"])
        elif len(t["forms"]) > 1:
            errors.append(Problem(f.path, at, "'when' is required with several forms"))
        _check_placeholders(f, f"{at}.text", form["text"], slots, used, errors)
        for ctx, text in (form.get("readings") or {}).items():
            _check_placeholders(f, f"{at}.readings.{ctx}", text, slots, used, errors)
    for name in sorted(slots - used):
        report.warnings.append(f"{f.path}:{here}: slot {name} is not used by any form")


def _check_templates(ed: Edition, refs: set[str], report: BuildReport) -> None:
    errors = report.errors
    for f in ed.template_files:
        _check_version(f, ed.version, errors)
        m = TEMPLATES_FILE_RE.match(f.path.name)
        if m is None:
            errors.append(Problem(f.path, "", "file must be named templates.<language>.yaml"))
        elif m[1] != f.data["language"]:
            errors.append(
                Problem(
                    f.path, "language", f"{f.data['language']!r} != file name language {m[1]!r}"
                )
            )
        first: dict[tuple[str, str], int] = {}
        for i, t in enumerate(f.data["templates"]):
            for name in ("key", "construct"):
                seen = first.setdefault((name, t[name]), i)
                if seen != i:
                    errors.append(
                        Problem(
                            f.path,
                            f"templates[{i}].{name}",
                            f"duplicate {name} {t[name]} (first at templates[{seen}])",
                        )
                    )
            _check_template(f, i, t, refs, report, ed.version)
        covered = {t["construct"] for t in f.data["templates"]}
        missing = [c for c in template_constructs() if c not in covered]
        if missing:
            errors.append(
                Problem(f.path, "templates", f"no template for construct(s): {', '.join(missing)}")
            )


def _utf16_slice(text: str, start: int, length: int) -> str:
    """`length` UTF-16 code units of `text` from `start` (the core's offsets)."""
    units = text.encode("utf-16-le")
    return units[2 * start : 2 * (start + length)].decode("utf-16-le", errors="replace")


def _check_golden(ed: Edition, refs: set[str], bands: set[str], report: BuildReport) -> None:
    f = ed.golden_file
    if f is None:
        return
    errors = report.errors
    _check_version(f, ed.version, errors)
    if f.path.stem != "golden":
        errors.append(Problem(f.path, "", "file must be named golden.yaml"))
    first: dict[str, int] = {}
    for i, entry in enumerate(f.data["golden"]):
        here = f"golden[{i}]"
        text = entry["input"]
        seen = first.setdefault(text, i)
        if seen != i:
            errors.append(
                Problem(f.path, f"{here}.input", f"duplicate input (first at golden[{seen}])")
            )
        if "cite" in entry and ed.sections:
            _check_cite(f, f"{here}.cite", entry["cite"], ed.sections, errors)
        for j, item in enumerate(entry["items"]):
            start, expected = item["span"]["start"], item["span"]["text"]
            length = len(expected.encode("utf-16-le")) // 2
            if _utf16_slice(text, start, length) != expected:
                errors.append(
                    Problem(
                        f.path,
                        f"{here}.items[{j}].span",
                        f"{expected!r} is not at offset {start} of {text!r}",
                    )
                )
            for k, ref in enumerate(item["kb"]):
                if ref not in (bands if ref.startswith("band:") else refs):
                    errors.append(
                        Problem(
                            f.path,
                            f"{here}.items[{j}].kb[{k}]",
                            f"unknown KB entry {ref} in ISCN {ed.version}",
                        )
                    )


def _check_translation(ed: Edition, report: BuildReport) -> None:
    refs, bands = _kb_refs(ed)
    _check_templates(ed, refs, report)
    _check_golden(ed, refs, bands, report)


def _translation(ed: Edition, golden: bool) -> dict | None:
    """`translation` of an edition in the bundles: templates per language (+ the golden file)."""
    if not ed.template_files and ed.golden_file is None:
        return None
    files = sorted(ed.template_files, key=lambda f: f.data["language"])
    out: dict = {"templates": {f.data["language"]: f.data["templates"] for f in files}}
    if golden:
        out["golden"] = ed.golden_file.data["golden"] if ed.golden_file else []
    return out


# --- outputs -----------------------------------------------------------------------------

SCHEMA_SQL = """
CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE rules (
  version TEXT NOT NULL, id TEXT NOT NULL, file TEXT NOT NULL, section TEXT NOT NULL,
  title TEXT NOT NULL, severity TEXT NOT NULL, statement TEXT NOT NULL,
  fix_level INTEGER, fix_kind TEXT, fix_hint TEXT,
  paraphrase_checked INTEGER NOT NULL, needs_review INTEGER NOT NULL, review_note TEXT,
  fix_auto INTEGER NOT NULL DEFAULT 0,
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
CREATE TABLE vocab (
  version TEXT, vocabulary TEXT, file TEXT, ord INTEGER, label TEXT, kind TEXT,
  tokens TEXT, pair_open TEXT, pair_close TEXT, equivalent_forms INTEGER, meanings TEXT,
  note TEXT, cite_rendered TEXT, section TEXT,
  PRIMARY KEY (version, vocabulary, label));
CREATE TABLE vocab_tokens (version TEXT, vocabulary TEXT, token TEXT, label TEXT);
CREATE INDEX vocab_tokens_token ON vocab_tokens (version, token);
CREATE TABLE bands (
  version TEXT NOT NULL, name TEXT NOT NULL, ord INTEGER NOT NULL, chrom TEXT NOT NULL,
  arm TEXT NOT NULL, parent TEXT, levels TEXT NOT NULL, derived INTEGER NOT NULL, stain TEXT,
  book INTEGER NOT NULL, grch37_start INTEGER, grch37_end INTEGER,
  grch38_start INTEGER, grch38_end INTEGER,
  PRIMARY KEY (version, name));
CREATE INDEX bands_parent ON bands (version, parent);
CREATE TABLE band_exceptions (
  version TEXT, ord INTEGER, level INTEGER, region TEXT, resolution TEXT,
  book TEXT, ncbi TEXT, keep TEXT, note TEXT, cite_rendered TEXT);
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
            if e.bands is not None:
                levels = e.bands.get("levels_available") or []
                meta[f"bands.levels_available.{e.version}"] = ",".join(str(n) for n in levels)
        con.executemany("INSERT INTO meta VALUES (?, ?)", sorted(meta.items()))
        for v, f, r in report.rules():
            fix, review = r.get("fix") or {}, r["review"]
            con.execute(
                "INSERT INTO rules VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
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
                    int(bool(fix.get("auto"))),  # fix.auto (T-096), rule browser badge (T-097)
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
        for v, f in report.vocab_files():
            name = f.data["vocabulary"]
            for i, e in enumerate(f.data["entries"]):
                pair = e.get("pair") or {}
                con.execute(
                    "INSERT INTO vocab VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                    (
                        v,
                        name,
                        f.rel,
                        i,
                        e["label"],
                        e["kind"],
                        json.dumps(e["tokens"], ensure_ascii=False),
                        pair.get("open"),
                        pair.get("close"),
                        int(bool(e.get("equivalent_forms"))),
                        json.dumps(e["meanings"], ensure_ascii=False),
                        e.get("note"),
                        render_cite(v, e["cite"]),
                        f.data["section"],
                    ),
                )
                for tok in e["tokens"]:
                    con.execute(
                        "INSERT INTO vocab_tokens VALUES (?,?,?,?)", (v, name, tok, e["label"])
                    )
        for e in report.editions:
            _write_bands(con, e)
        con.commit()
    finally:
        con.close()
    os.replace(tmp, path)


def _range(r: list | None) -> tuple:
    return (r[0], r[1]) if r else (None, None)


def _write_bands(con: sqlite3.Connection, ed: Edition) -> None:
    """The edition's bands and band exceptions for the rule browser (T-084)."""
    v = ed.version
    for i, b in enumerate((ed.bands or {}).get("bands") or []):
        con.execute(
            "INSERT INTO bands VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (
                v,
                b["name"],
                i,
                str(b["chrom"]),
                b["arm"],
                b["parent"],
                json.dumps(b["levels"]),
                int(bool(b["derived"])),
                b.get("stain"),
                int(bool(b["book"])),
                *_range(b["grch37"]),
                *_range(b["grch38"]),
            ),
        )
    for i, x in enumerate(ed.band_exceptions):
        con.execute(
            "INSERT INTO band_exceptions VALUES (?,?,?,?,?,?,?,?,?,?)",
            (
                v,
                i,
                x["level"],
                x["region"],
                x["resolution"],
                json.dumps(x["book"]),
                json.dumps(x["ncbi"]),
                json.dumps(x.get("keep") or []),
                x["note"].strip(),
                render_cite(v, x["cite"]),
            ),
        )


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
        vocab = {}
        for f in ed.vocab_files:
            d = f.data
            vocab[d["vocabulary"]] = {
                **{k: d[k] for k in d if k not in ("version", "vocabulary", "entries")},
                "file": f.rel,
                "source": [_with_rendered(ed.version, c) for c in d["source"]],
                "entries": [
                    {**e, "cite": _with_rendered(ed.version, e["cite"])} for e in d["entries"]
                ],
            }
        editions[ed.version] = {
            "chunks_checked": ed.chunks_checked,
            "rules": rules,
            "quotes": quotes,
            "vocab": vocab,
        }
        translation = _translation(ed, golden=True)
        if translation is not None:
            editions[ed.version]["translation"] = translation
    out = {"format_version": FORMAT_VERSION, "editions": editions}
    probes = probes_bundle(report, full=True)
    if probes is not None:
        out["probes"] = probes  # edition-independent (T-102)
    associations = associations_bundle(report, full=True)
    if associations is not None:
        out["associations"] = associations  # edition-independent (T-108)
    return out


RUNTIME_RULE_KEYS = (
    "id",
    "section",
    "title",
    "severity",
    "statement",
    "applies_to",
    "context",
    "related",
    "fix",
)
RUNTIME_VOCAB_ENTRY_KEYS = ("label", "kind", "tokens", "pair", "equivalent_forms", "meanings")


def runtime_bundle(report: BuildReport) -> dict:
    """Lean bundle for the core library (docs/design/core.md §6): rule metadata, vocabularies
    and bands per edition; no quotes, examples or source tables."""
    editions = {}
    for ed in report.editions:
        v = ed.version
        rules = []
        for f in ed.rule_files:
            for r in f.data["rules"]:
                src = {**r, "section": f.data["section"]}
                out = {k: src[k] for k in RUNTIME_RULE_KEYS if src.get(k) is not None}
                review = {"needs_review": r["review"]["needs_review"]}
                if r["review"].get("note"):
                    review["note"] = r["review"]["note"]
                out["review"] = review
                out["cite"] = [_with_rendered(v, c) for c in r["cite"]]
                rules.append(out)
        vocab = {}
        for f in ed.vocab_files:
            d = f.data
            entries = []
            for e in d["entries"]:
                entry = {k: e[k] for k in RUNTIME_VOCAB_ENTRY_KEYS if e.get(k) is not None}
                entry["cite"] = render_cite(v, e["cite"])
                entries.append(entry)
            shared = {"shared_tokens": d["shared_tokens"]} if d.get("shared_tokens") else {}
            vocab[d["vocabulary"]] = {**shared, "entries": entries}
        bands = None
        if ed.bands is not None:
            bands = {
                "levels_available": ed.bands.get("levels_available") or [],
                "bands": [{k: b.get(k) for k in RUNTIME_BAND_KEYS} for b in ed.bands["bands"]],
            }
        editions[v] = {"rules": rules, "vocab": vocab, "bands": bands}
        translation = _translation(ed, golden=False)
        if translation is not None:
            editions[v]["translation"] = translation
    out = {"format_version": RUNTIME_FORMAT_VERSION, "editions": editions}
    probes = probes_bundle(report, full=False)
    if probes is not None:
        out["probes"] = probes  # edition-independent, added without a format bump (T-102)
    associations = associations_bundle(report, full=False)
    if associations is not None:
        out["associations"] = associations  # edition-independent, no format bump (T-108)
    return out


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
        vocab = [f for v, f in report.vocab_files() if v == ed.version]
        if vocab:
            lines += ["", f"### Vocabularies ({len(vocab)})", ""]
            for f in vocab:
                rv = f.data["review"]
                state = (
                    "paraphrase checked" if rv["paraphrase_checked"] else "paraphrase not checked"
                )
                if rv["needs_review"]:
                    state += f"; needs review: {rv['note']}"
                n = len(f.data["entries"])
                lines.append(f"- `{f.data['vocabulary']}` ({f.rel}, {n} entries): {state}")
        translation = [*ed.template_files, *([ed.golden_file] if ed.golden_file else [])]
        if translation:
            lines += ["", "### Translation (T-057)", ""]
            for f in translation:
                items = f.data.get("templates") or f.data.get("golden") or []
                todo = sum(not x["review"]["checked"] for x in items)
                lines.append(f"- `{f.rel}`: {len(items)} entries, {todo} not reviewed")
        lines.append("")
    return "\n".join(lines)


def _write_text(path: Path, text: str) -> None:
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(text, encoding="utf-8", newline="\n")
    os.replace(tmp, path)


# --- entry point -------------------------------------------------------------------------


def run(
    curated_dir: Path,
    source_text_root: Path,
    out_dir: Path,
    versions: list[str] | None = None,
    bands_dir: Path | None = None,
) -> BuildReport:
    """Check the KB (person-maintained files in `curated_dir`, generated band data in
    `bands_dir`, default `kb/bands`) and, when there are no errors, write `kb.sqlite`,
    `kb.json`, `kb-runtime.json` and `review/kb-review.md` (+ `review/probes-review.md`,
    `review/associations-review.md`) into `out_dir`."""
    from ..config import KB_DIR

    kb_dir = curated_dir
    bands_dir = bands_dir or KB_DIR / "bands"
    report = BuildReport()
    found = find_editions(kb_dir)
    for v in versions or found:
        if v not in found:
            report.warnings.append(f"ISCN {v}: no KB files under {kb_dir}")
        ed = Edition(v)
        ed.rule_files = _load_dir(kb_dir / "rules" / v, "rules", report.errors)
        ed.quote_files = _load_dir(kb_dir / "source-quotes" / v, "quotes", report.errors)
        ed.vocab_files = _load_dir(kb_dir / "vocab" / v, "entries", report.errors)
        _load_translation(kb_dir / TRANSLATION_DIR / v, ed, report.errors)
        report.editions.append(ed)
    if report.errors:  # cross-file checks need schema-valid files
        return report
    for ed in report.editions:
        _check_edition(ed, source_text_root / ed.version, report)
        _load_bands(ed, bands_dir, kb_dir / "bands", report)
        _check_translation(ed, report)
    _load_probes(kb_dir / "probes" / "probes.csv", report)
    _load_associations(kb_dir / "associations" / "associations.yaml", report)
    if report.errors:
        return report

    full = json.dumps(bundle(report), ensure_ascii=False, indent=1) + "\n"
    runtime = json.dumps(runtime_bundle(report), ensure_ascii=False, separators=(",", ":"))
    review = review_markdown(report)
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "review").mkdir(exist_ok=True)
    db, js, rt = out_dir / "kb.sqlite", out_dir / "kb.json", out_dir / "kb-runtime.json"
    md = out_dir / "review" / "kb-review.md"
    write_sqlite(report, db)
    _write_text(js, full)
    _write_text(rt, runtime + "\n")
    _write_text(md, review)
    report.outputs = [db, js, rt, md]
    if report.probes is not None and report.probe_check is not None:
        names = example_probe_names(r for _, _, r in report.rules())
        pr = out_dir / "review" / "probes-review.md"
        _write_text(pr, probes_review(report.probes, report.probe_check, names))
        report.outputs.append(pr)
    if report.associations is not None and report.association_check is not None:
        ar = out_dir / "review" / "associations-review.md"
        _write_text(ar, associations_review(report.associations, report.association_check))
        report.outputs.append(ar)
    return report


# --- probe table (T-102) -----------------------------------------------------------------


def _load_probes(path: Path, report: BuildReport) -> None:
    """Read and check the probe table, if there is one; conflicts of unreviewed rows warn."""
    if not path.is_file():
        return
    tab = probe_table.read(path)
    report.errors.extend(tab.problems)
    if tab.problems:
        return
    band_sets = {
        ed.version: {b["name"]: b for b in ed.bands["bands"]}
        for ed in report.editions
        if ed.bands is not None
    }
    result = check_probes(tab, band_sets)
    report.errors.extend(result.errors)
    for c in result.conflicts:
        if not c.row.reviewed:
            report.warnings.append(f"{path}:{c.row.where()}: {c.message}")
    report.probes, report.probe_check = tab, result


def _probe_entry(row: probe_table.Row, full: bool) -> dict:
    r37, r38 = row.range("grch37"), row.range("grch38")
    out = {
        "name": row.name,
        "aliases": row.aliases,
        "kind": row["kind"],
        "chrom": row["chromosome"],
        "band": row["band"] or None,
        "grch37": list(r37) if r37 else None,
        "grch38": list(r38) if r38 else None,
        "source": row["source"],
        "reviewed": row.reviewed,
    }
    if full:
        out |= {k: row[k] or None for k in ("reviewer", "date", "note")}
    return out


def probes_bundle(report: BuildReport, full: bool) -> list[dict] | None:
    """The probe table for `kb.json` (`full`) or `kb-runtime.json`; None without a table."""
    if report.probes is None:
        return None
    return [_probe_entry(r, full) for r in report.probes.rows]


# --- association rules (T-108) -----------------------------------------------------------


def _load_associations(path: Path, report: BuildReport) -> None:
    """Read and check the association rules, if there are any; conflicts of unreviewed rules
    warn."""
    if not path.is_file():
        return
    problems = validate_file(path)
    report.errors.extend(problems)
    if problems:
        return
    rules = load_yaml(path)["associations"]
    band_sets = {
        ed.version: {b["name"]: b for b in ed.bands["bands"]}
        for ed in report.editions
        if ed.bands is not None
    }
    result = check_associations(path, rules, band_sets)
    report.errors.extend(result.errors)
    for c in result.conflicts:
        if not c.rule["review"]["reviewed"]:
            report.warnings.append(f"{path}:{c.where}: {c.message}")
    report.associations, report.association_check = rules, result


def _association_entry(rule: dict, full: bool) -> dict:
    out = {k: rule[k] for k in ("id", "condition", "context", "strength") if k in rule}
    if rule.get("defining"):
        out["defining"] = rule["defining"]
    out |= {"source": rule["source"], "citation": rule["citation"], "match": rule["match"]}
    rv = rule["review"]
    out["review"] = (
        {"reviewed": rv["reviewed"], **{k: rv.get(k) or None for k in ("reviewer", "date", "note")}}
        if full
        else {"reviewed": rv["reviewed"]}
    )
    return out


def associations_bundle(report: BuildReport, full: bool) -> list[dict] | None:
    """The association rules for `kb.json` (`full`) or `kb-runtime.json`; None without a file."""
    if report.associations is None:
        return None
    return [_association_entry(r, full) for r in report.associations]
