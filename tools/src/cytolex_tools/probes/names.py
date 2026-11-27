"""Probe names written in the in situ KB examples (T-102).

A heuristic, not a parser: it finds the names a person would look up in the probe table
(`ABL1`, `D21S65`, `RP11-265P11`) in the example strings of in situ/cgh rules. It feeds
`kb probes --from-kb-examples` and the "not in the table" list of the review report.
"""

import re
from collections.abc import Iterable, Iterator

# A segment that is in situ hybridization (ch. 13)
IN_SITU_RE = re.compile(r"\b(?:nuc |fib |rev )?ish\b|\bcgh\b")
TOKEN_RE = re.compile(r"[A-Za-z][A-Za-z0-9]*(?:-[A-Za-z0-9]*[0-9][A-Za-z0-9]*)*")
BAND_PREFIX_RE = re.compile(r"^(?:[pq][0-9]+(?:\.[0-9]+)?)+")
STATUS_SUFFIX_RE = re.compile(r"(?:con|sep|enh|dim|amp)$")
JOINED_RE = re.compile(r"(?<=[0-9A-Z])(?:con|sep)(?=[A-Z])")  # ABL1conBCR
# Probe names placed on their chromosome by the name alone (T-101, `ISH.LOCUS_DESIGNATION`)
D_NUMBER_RE = re.compile(r"^D([1-9]|1[0-9]|2[0-2]|X|Y)[ZSF][0-9]+$")
CLONE_RE = re.compile(r"^(?:RP[0-9]+|CTD|CTB|CTC|CH17|CH507|WI2)-[0-9A-Z]+$")
# Words of the notation that look like names
NOT_PROBES = {"XX", "XY", "XXX", "XXY", "XYY", "GRCh37", "GRCh38", "hg19", "hg38", "NCBI36"}


def in_situ_strings(rules: Iterable[dict]) -> Iterator[str]:
    """The example strings (valid, invalid, corrected) of `rules` that hold an in situ part."""
    for rule in rules:
        for kind in ("valid", "invalid"):
            for ex in (rule.get("examples") or {}).get(kind) or []:
                for text in (ex.get("text"), ex.get("corrected")):
                    if text and IN_SITU_RE.search(text):
                        yield text


def probe_names(text: str) -> set[str]:
    """Probe-like names in one string: uppercase-initial tokens without the band designation
    or signal status glued to them in the printed form."""
    out = set()
    for seg in re.split(r"(?=\b(?:nuc |fib |rev )?ish\b)", text):
        if not IN_SITU_RE.search(seg):
            continue
        for tok in TOKEN_RE.findall(seg):
            tok = BAND_PREFIX_RE.sub("", tok)
            for part in JOINED_RE.split(tok):
                part = STATUS_SUFFIX_RE.sub("", part) if part[:1].isupper() else part
                if len(part) >= 2 and part[0].isupper() and part not in NOT_PROBES:
                    out.add(part)
    return out


def example_probe_names(rules: Iterable[dict]) -> set[str]:
    return {n for s in in_situ_strings(rules) for n in probe_names(s)}


def placed_by_name(name: str) -> bool:
    """D-numbers name their chromosome (T-101); the table isn't needed to place them."""
    return bool(D_NUMBER_RE.match(name))
