"""Parsers for NCBI GDP ideogram files and UCSC cytoBand files."""

import gzip
from dataclasses import dataclass

CHROMS = [str(i) for i in range(1, 23)] + ["X", "Y"]
CHROM_INDEX = {c: i for i, c in enumerate(CHROMS)}


@dataclass(frozen=True)
class SourceBand:
    chrom: str
    arm: str  # p | q
    band: str  # "34.11"
    start: int  # bp, 1-based closed (as far as the source allows; see check tolerance)
    end: int
    stain: str  # gneg, gpos25..gpos100, acen, gvar, stalk

    @property
    def name(self) -> str:
        return f"{self.chrom}{self.arm}{self.band}"


def parse_ncbi_gdp(data: bytes) -> list[SourceBand]:
    """NCBI GDP: chromosome, arm, band, iscn_start, iscn_stop, bp_start, bp_stop, stain, density.

    bp coordinates are taken as given; the 850 files mix 0- and 1-based starts, so callers
    compare with a 1 bp tolerance and use UCSC for canonical 850 coordinates.
    """
    out = []
    for line in data.decode("utf-8").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        cols = line.split("\t")
        chrom, arm, band = cols[0], cols[1], cols[2]
        if chrom not in CHROM_INDEX:
            continue
        stain = cols[7] + (cols[8].strip() if len(cols) > 8 and cols[7] == "gpos" else "")
        out.append(SourceBand(chrom, arm, band, int(cols[5]), int(cols[6]), stain))
    return out


def parse_ucsc_cytoband(data: bytes) -> list[SourceBand]:
    """UCSC cytoBand(.txt.gz): chrom, chromStart (0-based), chromEnd, name, gieStain.

    Only primary chromosomes (chr1-22, X, Y); converted to 1-based closed coordinates.
    """
    if data[:2] == b"\x1f\x8b":
        data = gzip.decompress(data)
    out = []
    for line in data.decode("utf-8").splitlines():
        if not line.strip():
            continue
        chrom, start, end, name, stain = line.split("\t")[:5]
        chrom = chrom.removeprefix("chr")
        if chrom not in CHROM_INDEX or not name:
            continue
        out.append(SourceBand(chrom, name[0], name[1:], int(start) + 1, int(end), stain))
    return out
