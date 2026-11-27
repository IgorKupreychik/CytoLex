"""Reading the match tree of an association rule (T-108): leaves and the bands they name.

An `exact` leaf is an abnormality's short form (`+21`, `t(9;22)(q34;q11.2)`, `inv(16)(p13.1q22)`).
`exact_bands` turns it into full band names (`9q34`, `22q11.2`) so `kb build` can check them
against the band data; `cen`/`pter`/`qter` and the centromere designations `p10`/`q10` name no
band and are skipped.
"""

import re
from collections.abc import Iterator
from dataclasses import dataclass

LEAVES = ("exact", "fusion", "copy")
EXACT_RE = re.compile(r"^(?P<sym>[a-z]+)\((?P<chroms>[^()]+)\)(?:\((?P<bands>[^()]+)\))?$")
BAND_RE = re.compile(r"[pq][0-9]+(?:\.[0-9]+)?|cen|pter|qter")
NO_BAND = {"cen", "pter", "qter", "p10", "q10"}


@dataclass(frozen=True)
class Leaf:
    kind: str  # exact | fusion | copy
    value: object
    where: str  # JSON path inside the rule, e.g. match.any[1].fusion


def leaves(node: dict, where: str = "match") -> Iterator[Leaf]:
    """Every leaf of a match tree, depth first, in written order."""
    ((key, value),) = node.items()
    if key in LEAVES:
        yield Leaf(key, value, f"{where}.{key}")
    elif key == "not":
        yield from leaves(value, f"{where}.not")
    else:
        for i, child in enumerate(value):
            yield from leaves(child, f"{where}.{key}[{i}]")


def only_negative(node: dict) -> bool:
    """True when the node can match without any leaf being present (a lone `not`, or `all`/`any`
    of such nodes): the rule would apply to nearly every result."""
    ((key, value),) = node.items()
    if key == "not":
        return True
    if key == "all":
        return all(only_negative(c) for c in value)
    if key == "any":
        return any(only_negative(c) for c in value)
    return False


class FormError(ValueError):
    pass


def exact_bands(form: str) -> list[str]:
    """Full band names of a short form; `+21` → []. Raises FormError when the band groups don't
    pair with the chromosomes (`t(9;22)(q34)`)."""
    m = EXACT_RE.match(form)
    if not m:
        return []  # +21, -7: no bands
    chroms = m["chroms"].split(";")
    if not m["bands"]:
        return []
    groups = m["bands"].split(";")
    if len(groups) != len(chroms):
        raise FormError(
            f"{form}: {len(chroms)} chromosome(s) but {len(groups)} band group(s); "
            "separate the bands of each chromosome with ';'"
        )
    out = []
    for chrom, group in zip(chroms, groups, strict=True):
        out += [chrom + b for b in BAND_RE.findall(group) if b not in NO_BAND]
    return out


def band_chrom(band: str) -> str:
    """`22q11.21` → `22`."""
    return band[: next(i for i, ch in enumerate(band) if ch in "pq")]


def band_range_names(cell: str) -> list[str]:
    """`22q11.21-q11.23` → ['22q11.21', '22q11.23']; `22q11.21` → ['22q11.21']."""
    head, _, tail = cell.partition("-")
    return [head, band_chrom(head) + tail] if tail else [head]
