"""Validate curated KB files (YAML, and the probe CSV) against the JSON Schemas in `kb/schema/`.

A file's kind is taken from its top-level key: `rules` → rules.schema.json,
`quotes` → quotes.schema.json, `exceptions` → band-exceptions.schema.json (T-007),
`entries` → vocab.schema.json (T-008), `templates` → translation-templates.schema.json and
`golden` → translation-golden.schema.json (T-057); `corpus` → corpus.schema.json (test corpus
in curated/corpus/, T-062). The probe table (`curated/probes/probes.csv`, T-102) is CSV:
each row is checked against probes.schema.json (`probes.table.validate_csv`).
Only the schema is checked here; cross-file checks
(unique IDs, quote ↔ rule links, version vs directory) belong to `kb build` (T-005).
"""

import json
from collections.abc import Iterable, Iterator
from dataclasses import dataclass
from functools import cache
from pathlib import Path

from jsonschema import Draft202012Validator, ValidationError
from referencing import Registry, Resource
from ruamel.yaml import YAML
from ruamel.yaml.error import YAMLError

from ..config import REPO_ROOT

SCHEMA_DIR = REPO_ROOT / "kb" / "schema"
SCHEMA_FILES = {
    "rules": "rules.schema.json",
    "quotes": "quotes.schema.json",
    "exceptions": "band-exceptions.schema.json",  # curated/bands/<version>/exceptions.yaml (T-007)
    "entries": "vocab.schema.json",  # curated/vocab/<version>/*.yaml (T-008)
    "templates": "translation-templates.schema.json",  # curated/translation/<version>/ (T-057)
    "golden": "translation-golden.schema.json",  # curated/translation/<version>/golden.yaml
    "corpus": "corpus.schema.json",  # curated/corpus/<type>.yaml (T-062)
}
YAML_SUFFIXES = {".yaml", ".yml"}


@dataclass(frozen=True)
class Problem:
    file: Path
    where: str  # JSON path inside the file, e.g. rules[0].cite[1].para; "" = whole file
    message: str

    def __str__(self) -> str:
        loc = f"{self.file}:{self.where}" if self.where else str(self.file)
        return f"{loc}: {self.message}"


@cache
def _registry(schema_dir: Path) -> Registry:
    resources = []
    for f in sorted(schema_dir.glob("*.schema.json")):
        contents = json.loads(f.read_text(encoding="utf-8"))
        res = Resource.from_contents(contents)
        resources.append((contents["$id"], res))
    return Registry().with_resources(resources)


@cache
def schema_validator(file_name: str, schema_dir: Path = SCHEMA_DIR) -> Draft202012Validator:
    """Validator for the schema `file_name`, with `$ref`s to sibling schemas resolved."""
    schema = json.loads((schema_dir / file_name).read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    return Draft202012Validator(schema, registry=_registry(schema_dir))


def validator(kind: str, schema_dir: Path = SCHEMA_DIR) -> Draft202012Validator:
    """Validator for `kind` (a key of SCHEMA_FILES)."""
    return schema_validator(SCHEMA_FILES[kind], schema_dir)


def _where(error: ValidationError) -> str:
    out = ""
    for part in error.absolute_path:
        out += f"[{part}]" if isinstance(part, int) else (f".{part}" if out else str(part))
    return out


def _message(error: ValidationError) -> str:
    """Rewrite the few jsonschema messages that don't say what to change."""
    schema_path = list(error.schema_path)
    if error.validator == "anyOf" and set(error.validator_value[0]) == {"required"}:
        keys = [next(iter(alt["required"])) for alt in error.validator_value]
        return f"citation needs at least one of: {', '.join(keys)}"
    if error.validator == "not" and "else" in schema_path:
        return "'kind' and 'auto' are only allowed with fix level 1"
    if error.validator == "pattern" and isinstance(error.schema, dict):
        expected = error.schema.get("x-expected")
        if expected:
            return f"{error.instance!r} is not {expected}"
    if error.validator == "additionalProperties":
        return error.message.replace("Additional properties are not allowed", "unknown field(s)")
    return error.message


_KINDS = ", ".join(f"'{k}'" for k in SCHEMA_FILES)


def _sort_key(e: ValidationError) -> tuple:
    return (tuple(str(p) for p in e.absolute_path), e.message)


def validate_data(data: object, file: Path, schema_dir: Path = SCHEMA_DIR) -> list[Problem]:
    if not isinstance(data, dict):
        return [Problem(file, "", f"expected a mapping with a top-level {_KINDS} list")]
    kinds = [k for k in SCHEMA_FILES if k in data]
    if len(kinds) != 1:
        return [Problem(file, "", f"needs exactly one top-level key: {_KINDS}")]
    v = validator(kinds[0], schema_dir)
    errors = sorted(v.iter_errors(data), key=_sort_key)
    return [Problem(file, _where(e), _message(e)) for e in errors]


def load_yaml(file: Path) -> object:
    yaml = YAML(typ="safe")
    yaml.allow_duplicate_keys = False
    return yaml.load(file.read_text(encoding="utf-8"))


def validate_file(file: Path, schema_dir: Path = SCHEMA_DIR) -> list[Problem]:
    try:
        data = load_yaml(file)
    except YAMLError as exc:
        mark = getattr(exc, "problem_mark", None)
        where = f"line {mark.line + 1}" if mark else ""
        what = getattr(exc, "problem", None) or str(exc).splitlines()[0]
        return [Problem(file, where, f"YAML error: {what}")]
    return validate_data(data, file, schema_dir)


def iter_yaml_files(paths: Iterable[Path], suffixes=YAML_SUFFIXES) -> Iterator[Path]:
    """Expand directories (recursively) into their files with `suffixes` (default: YAML), in
    sorted order; files given directly are kept."""
    for p in paths:
        if p.is_dir():
            yield from sorted(f for f in p.rglob("*") if f.suffix in suffixes)
        else:
            yield p
