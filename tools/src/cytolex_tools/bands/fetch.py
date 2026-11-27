"""Source list (curated/bands/<version>/sources.yaml), download cache and sha256 pinning."""

from dataclasses import dataclass
from pathlib import Path

from ruamel.yaml import YAML

from .. import downloads
from ..config import CURATED_DIR
from ..downloads import sha256, update_pins

__all__ = ["Source", "fetch", "load_sources", "sha256", "sources_file", "update_pins"]


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
        return downloads.cache_name(self.id, self.url)


def sources_file(version: str) -> Path:
    return CURATED_DIR / "bands" / version / "sources.yaml"


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


def fetch(src: Source, cache_dir: Path, cache_only: bool = False) -> bytes:
    """Return the raw bytes of `src`, from `cache_dir` or downloaded into it."""
    return downloads.fetch(src.id, src.url, cache_dir, src.cache_name, cache_only)
