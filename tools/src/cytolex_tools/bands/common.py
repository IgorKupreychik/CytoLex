"""Helpers shared by the band generator and the idiogram decoder: band nesting, YAML flow."""


def parent_band(band: str) -> str | None:
    """'34.11' -> '34.1' -> '34' -> None."""
    if "." not in band:
        return None
    head, tail = band.split(".", 1)
    return f"{head}.{tail[:-1]}" if len(tail) > 1 else head


def lineage(band: str) -> list[str]:
    """The band itself followed by its ancestors."""
    out = [band]
    while (p := parent_band(out[-1])) is not None:
        out.append(p)
    return out


def split_name(name: str) -> tuple[str, str, str]:
    """'9q34.1' -> ('9', 'q', '34.1')."""
    i = next(i for i, ch in enumerate(name) if ch in "pq")
    return name[:i], name[i], name[i + 1 :]


def scalar(v) -> str:
    if v is None:
        return "null"
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, int):
        return str(v)
    if isinstance(v, list):
        return "[" + ", ".join(scalar(x) for x in v) + "]"
    if isinstance(v, dict):
        return flow(v)
    s = str(v)
    plain = s and s[0].isalpha() and all(ch.isalnum() or ch in ".-_" for ch in s)
    return s if plain and s not in ("null", "true", "false", "yes", "no", "on", "off") else f"'{s}'"


def flow(d: dict) -> str:
    return "{" + ", ".join(f"{k}: {scalar(v)}" for k, v in d.items()) + "}"
