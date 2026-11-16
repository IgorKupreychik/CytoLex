"""Source list (sources/<version>.yaml), download cache and sha256 pinning."""

import hashlib
import urllib.request
from dataclasses import dataclass
from pathlib import Path

from ruamel.yaml import YAML

SOURCES_DIR = Path(__file__).parent / "sources"
USER_AGENT = "cytolex-tools (band data generator)"


@dataclass(frozen=True)
class Source:
    id: str
    kind: str  # ncbi_gdp | ucsc_cytoband
    assembly: str  # GRCh37 | GRCh38
    level: int
    url: str
    sha256: str

    @property
    def cache_name(self) -> str:
        return self.id + (".txt.gz" if self.url.endswith(".gz") else ".tsv")


def sources_file(version: str) -> Path:
    return SOURCES_DIR / f"{version}.yaml"


def load_sources(version: str, path: Path | None = None) -> tuple[list[Source], dict]:
    """Return the sources and the raw config for `version`."""
    path = path or sources_file(version)
    if not path.exists():
        raise FileNotFoundError(f"no band sources for ISCN {version}: {path}")
    data = YAML(typ="safe").load(path.read_text(encoding="utf-8"))
    files = [
        Source(f["id"], f["kind"], f["assembly"], int(f["level"]), f["url"], f.get("sha256") or "")
        for f in data["files"]
    ]
    return files, data


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def fetch(src: Source, cache_dir: Path, cache_only: bool = False) -> bytes:
    """Return the raw bytes of `src`, from `cache_dir` or downloaded into it."""
    cached = cache_dir / src.cache_name
    if cached.exists():
        return cached.read_bytes()
    if cache_only:
        raise FileNotFoundError(f"{src.id}: not in cache ({cached}) and --cache-only given")
    req = urllib.request.Request(src.url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=60) as resp:
        data = resp.read()
    cache_dir.mkdir(parents=True, exist_ok=True)
    cached.write_bytes(data)
    return data


def update_pins(path: Path, shas: dict[str, str]) -> None:
    """Rewrite the sha256 pins in `path` (round-trip, comments kept)."""
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
