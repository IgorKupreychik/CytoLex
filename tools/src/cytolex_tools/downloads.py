"""Pinned downloads shared by `kb bands` and `kb probes`: cache, sha256 and pin rewriting.

A sources file (person-approved, under `curated/`) lists each download with its `id`, `url`
and `sha256`. A download is cached once under `build/cache/…`; a sha256 mismatch means the
upstream file changed, and the person re-pins deliberately (`--update-pins`) after reviewing
the resulting data diff.
"""

import hashlib
import urllib.request
from pathlib import Path

from ruamel.yaml import YAML

USER_AGENT = "cytolex-tools (KB data generator)"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def cache_name(source_id: str, url: str) -> str:
    """`ncbi-grch38` + `…genomic.gff.gz` -> `ncbi-grch38.gff.gz`; plain files get `.tsv`."""
    if url.endswith(".gz"):
        inner = url.removesuffix(".gz").rsplit(".", 1)
        ext = inner[1] if len(inner) == 2 and "/" not in inner[1] else "txt"
        return f"{source_id}.{ext}.gz"
    return f"{source_id}.tsv"


def fetch(source_id: str, url: str, cache_dir: Path, name: str, cache_only: bool = False) -> bytes:
    """Return the raw bytes of `url`, from `cache_dir/name` or downloaded into it."""
    cached = cache_dir / name
    if cached.exists():
        return cached.read_bytes()
    if cache_only:
        raise FileNotFoundError(f"{source_id}: not in cache ({cached}) and --cache-only given")
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=300) as resp:
        data = resp.read()
    cache_dir.mkdir(parents=True, exist_ok=True)
    cached.write_bytes(data)
    return data


def pin_problems(
    pins: dict[str, str], shas: dict[str, str], cache_dir: Path, command: str, names: dict[str, str]
) -> list[str]:
    """One problem per source without a pin or with a different sha256."""
    out = []
    for sid, pinned in pins.items():
        if not pinned:
            out.append(f"{sid}: no sha256 pin; run `{command} --update-pins`")
        elif pinned != shas[sid]:
            out.append(
                f"{sid}: sha256 mismatch (pinned {pinned[:12]}…, got {shas[sid][:12]}…); "
                f"upstream changed or the cache is stale. Delete {cache_dir / names[sid]} "
                "to re-download, or re-pin with --update-pins and review the diff"
            )
    return out


def update_pins(path: Path, shas: dict[str, str]) -> None:
    """Rewrite the sha256 pins of the `files` list in `path` (round-trip, comments kept)."""
    yaml = YAML()
    yaml.preserve_quotes = True
    yaml.width = 120
    yaml.indent(mapping=2, sequence=4, offset=2)
    data = yaml.load(path.read_text(encoding="utf-8"))
    for f in data["files"]:
        if f["id"] in shas:
            f["sha256"] = shas[f["id"]]
    with path.open("w", encoding="utf-8", newline="\n") as fh:
        yaml.dump(data, fh)
