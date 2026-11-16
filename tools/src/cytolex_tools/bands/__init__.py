"""Band data generator (T-006): NCBI GDP ideograms + UCSC cytoBand -> kb/bands/<version>/."""

from .generate import GENERATOR_VERSION, Report, run

__all__ = ["GENERATOR_VERSION", "Report", "run"]
