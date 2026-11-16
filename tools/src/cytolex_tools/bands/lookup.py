"""Read generated band data: `load(version).resolve('9q34.1')`."""

from dataclasses import dataclass
from pathlib import Path

from ruamel.yaml import YAML

from ..config import REPO_ROOT


@dataclass
class BandSet:
    version: str
    bands: dict[str, dict]

    def resolve(self, name: str) -> dict | None:
        """The band entry for an exact band name (e.g. '9q34.1'), or None."""
        return self.bands.get(name)

    def is_valid(self, name: str) -> bool:
        return name in self.bands


def load(version: str = "2016", path: Path | None = None) -> BandSet:
    path = path or REPO_ROOT / "kb" / "bands" / version / "bands.yaml"
    data = YAML(typ="safe").load(path.read_text(encoding="utf-8"))
    return BandSet(str(data["version"]), {b["name"]: b for b in data["bands"]})
