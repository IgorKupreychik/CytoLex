"""`kb samples`: batch sample files from the KB examples (T-038).

Reads the full KB (`build/kb.json`) and writes `tests/data/batch/<version>/`: one-string-per-line
text files per notation type, `all.csv`/`all.tsv` (one row per KB example or `corrected` form,
labelled with its rule) and `large.csv` (the same rows cycled to 1,200 for the 1,000+ batch run).
Book fragments (`inv(2)`, `…`) are left out. The `type` column is the segment types joined with
`+`; the C# tests check it against `Segmenter`.
"""

from __future__ import annotations

import csv
import io
import json
import re
from dataclasses import dataclass
from pathlib import Path

LARGE_ROWS = 1200
COLUMNS = ("id", "iscn", "type", "kb_rule", "kb_kind", "cite")

# The whole-string test of the KB-example harness
# (tests/CytoLex.Core.Tests/KbExamples/KbCatalog.cs).
_WHOLE_STRING_START = re.compile(
    r"^(?:\d+(?:[,~<(XY]|$)|//"
    r"|(?:mos|chi|end|ish|nuc ish|fib ish|rev ish|cgh|arr|rsa|seq"
    r"|ch[tr][gbe]|cht|sce|pvz|MII|AII|PI|MI|AI|fem|mal|spm|oom|lep|zyg|pac|dip|dit|dia)"
    r"(?![A-Za-z]))"
)

# Technique keywords, longest first, in the order of src/CytoLex.Core/Segmentation/Segmenter.cs.
_KEYWORDS = (
    (("nuc", "ish"), "nuc_ish"),
    (("fib", "ish"), "fib_ish"),
    (("rev", "ish"), "rev_ish"),
    (("ish", "cgh"), "cgh"),
    (("ish",), "ish"),
    (("cgh",), "cgh"),
    (("arr",), "arr"),
    (("rsa",), "rsa"),
    (("seq",), "seq"),
    # Breakage and meiotic notation by their leading symbol (T-018).
    *((("cht", x), "breakage") for x in ("g", "b", "e")),
    *((("chr", x), "breakage") for x in ("g", "b", "e")),
    *(((w,), "breakage") for w in ("cht", "sce", "pvz")),
    *(
        ((w,), "meiotic")
        for w in ("MII", "AII", "PI", "MI", "AI", "fem", "mal")
        + ("spm", "oom", "lep", "zyg", "pac", "dip", "dit", "dia")
    ),
)


@dataclass(frozen=True)
class Sample:
    iscn: str
    type: str
    kb_rule: str
    kb_kind: str
    cite: str


@dataclass(frozen=True)
class SamplesReport:
    out: Path
    rows: int
    unique: int
    per_file: dict[str, int]


def is_fragment(text: str) -> bool:
    """A book fragment rather than a whole notation string (same test as the harness)."""
    return "…" in text or not _WHOLE_STRING_START.match(text)


def segment_types(text: str) -> list[str]:
    """The KB names of the segment types, as `Segmenter.Split` detects them."""
    types: list[str] = []
    depth, start = 0, 0
    for i, ch in enumerate(text):
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth = max(0, depth - 1)
        elif ch == "." and depth == 0 and _next_is_letter(text, i + 1):
            types.append(_segment_type(text[start:i]))
            start = i + 1
    types.append(_segment_type(text[start:]))
    return types


def _next_is_letter(text: str, start: int) -> bool:
    rest = text[start:].lstrip()
    return bool(rest) and rest[0].isalpha()


def _segment_type(segment: str) -> str:
    s = segment.lstrip()
    # A donor-only chimera result puts // before the keyword (§13.3.1 E19).
    if s.startswith("//"):
        rest = s[2:].lstrip()
        if _keyword_type(rest) != "karyotype":
            s = rest
    return _keyword_type(s)


def _keyword_type(s: str) -> str:
    for words, name in _KEYWORDS:
        pos, ok = 0, True
        for w, word in enumerate(words):
            if w > 0:
                while pos < len(s) and s[pos] in " \t":
                    pos += 1
            if not s.startswith(word, pos):
                ok = False
                break
            pos += len(word)
        if ok and not (pos < len(s) and s[pos].isalpha()):
            return name
    return "karyotype"


def collect(kb: dict, version: str) -> list[Sample]:
    """Every whole-string KB example and `corrected` form of `version`, in KB order."""
    edition = kb["editions"].get(version)
    if edition is None:
        raise KeyError(f"ISCN {version} not in build/kb.json")
    samples: list[Sample] = []
    for rule in edition["rules"]:
        rule_cite = (rule.get("cite") or [{}])[0].get("rendered", "")
        examples = rule.get("examples") or {}
        for kind in ("valid", "invalid"):
            for ex in examples.get(kind) or []:
                cite = (ex.get("cite") or {}).get("rendered") or rule_cite
                forms = [(ex["text"], kind)]
                if ex.get("corrected"):
                    forms.append((ex["corrected"], "corrected"))
                for text, label in forms:
                    if is_fragment(text):
                        continue
                    types = "+".join(segment_types(text))
                    samples.append(Sample(text, types, rule["id"], label, cite))
    return samples


def _group(type_: str) -> str:
    if "+" in type_:
        return "combined"
    return type_ if type_ in ("karyotype", "arr") else "other"


def _table(rows: list[tuple[str, Sample]], delimiter: str) -> str:
    buf = io.StringIO()
    writer = csv.writer(buf, delimiter=delimiter, lineterminator="\n", quoting=csv.QUOTE_MINIMAL)
    writer.writerow(COLUMNS)
    for id_, s in rows:
        writer.writerow((id_, s.iscn, s.type, s.kb_rule, s.kb_kind, s.cite))
    return buf.getvalue()


def render(samples: list[Sample]) -> dict[str, str]:
    """File name -> content (LF line endings)."""
    files: dict[str, str] = {}
    unique: dict[str, list[str]] = {"karyotype": [], "arr": [], "combined": [], "other": []}
    seen: set[str] = set()
    for s in samples:
        if s.iscn not in seen:
            seen.add(s.iscn)
            unique[_group(s.type)].append(s.iscn)
    for group, texts in unique.items():
        if texts:  # e.g. no ish-only examples before T-015/T-016
            files[f"{group}.txt"] = "".join(t + "\n" for t in texts)

    rows = [(f"K{i:04d}", s) for i, s in enumerate(samples, 1)]
    files["all.csv"] = _table(rows, ",")
    files["all.tsv"] = _table(rows, "\t")
    if samples:
        large = [(f"E{i + 1:04d}", samples[i % len(samples)]) for i in range(LARGE_ROWS)]
        files["large.csv"] = _table(large, ",")
    return files


def run(version: str, kb_json: Path, out: Path) -> SamplesReport:
    if not kb_json.exists():
        raise FileNotFoundError(
            f"{kb_json} not found; run 'uv run --directory tools kb build' first"
        )
    kb = json.loads(kb_json.read_text(encoding="utf-8"))
    samples = collect(kb, version)
    files = render(samples)
    out.mkdir(parents=True, exist_ok=True)
    for name, content in files.items():
        (out / name).write_bytes(content.encode("utf-8"))
    per_file = {name: content.count("\n") for name, content in files.items()}
    return SamplesReport(out, len(samples), len({s.iscn for s in samples}), per_file)
