"""Render structured KB citations as text: `ISCN 2016 §9.2.17.1, p.75, ¶2, ex. E3`."""

from collections.abc import Mapping


def render_cite(version: str, cite: Mapping) -> str:
    parts = [f"ISCN {version} §{cite['section']}", f"p.{cite['page']}"]
    if cite.get("para"):
        parts.append(cite["para"])
    if cite.get("example"):
        parts.append(f"ex. {cite['example']}")
    if cite.get("float"):
        parts.append(cite["float"])
    return ", ".join(parts)


def anchor(cite: Mapping) -> str | None:
    """The most specific label of a citation: example, then float, then paragraph."""
    return cite.get("example") or cite.get("float") or cite.get("para")
