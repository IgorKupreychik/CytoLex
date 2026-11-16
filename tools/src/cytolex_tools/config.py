"""Local configuration: ISCN PDF paths from config/local.yaml, overridable by env vars."""

import os
from pathlib import Path

from ruamel.yaml import YAML

# tools/src/cytolex_tools/config.py -> repo root
REPO_ROOT = Path(__file__).resolve().parents[3]
LOCAL_CONFIG = REPO_ROOT / "config" / "local.yaml"


def iscn_pdf_path(version: str, config_file: Path = LOCAL_CONFIG) -> Path:
    """Return the ISCN PDF path for `version`.

    `CYTOLEX_ISCN_PDF_<VERSION>` (e.g. CYTOLEX_ISCN_PDF_2016) overrides config/local.yaml.
    """
    env = os.environ.get(f"CYTOLEX_ISCN_PDF_{version}")
    if env:
        return Path(env)
    if not config_file.exists():
        raise FileNotFoundError(
            f"{config_file} not found; copy config/local.example.yaml and set iscn_pdf paths"
        )
    data = YAML(typ="safe").load(config_file.read_text(encoding="utf-8")) or {}
    paths = data.get("iscn_pdf") or {}
    value = paths.get(version) or paths.get(int(version) if version.isdigit() else version)
    if not value:
        raise KeyError(f"iscn_pdf.{version!r} missing in {config_file}")
    return Path(value)
