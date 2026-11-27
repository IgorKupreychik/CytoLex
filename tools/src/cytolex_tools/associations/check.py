"""Checks of the association rules that need the band data (T-108); run by `kb build`.

Errors fail the build: duplicate IDs (case-insensitive), a band not in every edition's band
data, band groups that don't pair with the chromosomes of an `exact` form, a `fusion` of a band
with itself, a `copy` region with start after end or a band on another chromosome, a rule that
matches by `not` alone, and any conflict on a reviewed rule. Conflicts (a `copy` region outside
its band) are warnings on unreviewed rules and are listed in the review report.
"""

from dataclasses import dataclass, field
from pathlib import Path

from ..kb.schema import Problem
from .forms import FormError, band_chrom, band_range_names, exact_bands, leaves, only_negative


@dataclass
class Conflict:
    rule: dict
    where: str
    message: str


@dataclass
class CheckResult:
    errors: list[Problem] = field(default_factory=list)
    conflicts: list[Conflict] = field(default_factory=list)


def check(path: Path, rules: list[dict], band_sets: dict[str, dict[str, dict]]) -> CheckResult:
    """`band_sets`: edition -> band name -> band entry of that edition's bands.yaml."""
    res = CheckResult()
    seen: dict[str, int] = {}
    for i, rule in enumerate(rules):
        at = f"associations[{i}]"
        key = rule["id"].casefold()
        if key in seen:
            res.errors.append(
                Problem(
                    path, f"{at}.id", f"{rule['id']} is already used by associations[{seen[key]}]"
                )
            )
        else:
            seen[key] = i
        if only_negative(rule["match"]):
            res.errors.append(
                Problem(
                    path,
                    f"{at}.match",
                    "the rule matches by 'not' alone, so it would apply to nearly every result; "
                    "combine the 'not' with a leaf under 'all'",
                )
            )
        for leaf in leaves(rule["match"]):
            where = f"{at}.{leaf.where}"
            if leaf.kind == "exact":
                try:
                    bands = exact_bands(leaf.value)
                except FormError as e:
                    res.errors.append(Problem(path, where, str(e)))
                    continue
                _bands_exist(bands, band_sets, path, where, res)
            elif leaf.kind == "fusion":
                a, b = leaf.value
                if a == b:
                    res.errors.append(Problem(path, where, f"{a} is joined with itself"))
                _bands_exist([a, b], band_sets, path, where, res)
            else:
                _check_copy(rule, leaf.value, band_sets, path, where, res)
    for c in res.conflicts:
        if c.rule["review"]["reviewed"]:
            res.errors.append(
                Problem(
                    path,
                    c.where,
                    f"reviewed rule with a conflict: {c.message}; fix the rule, or set reviewed "
                    "to false until it is resolved",
                )
            )
    return res


def _bands_exist(bands, band_sets, path, where, res) -> bool:
    ok = True
    for version, known in sorted(band_sets.items()):
        missing = [b for b in bands if b not in known]
        if missing:
            ok = False
            res.errors.append(
                Problem(path, where, f"{', '.join(missing)}: no such band in ISCN {version}")
            )
    return ok


def _check_copy(rule, copy, band_sets, path, where, res) -> None:
    start, end = copy["grch38"]
    if start > end:
        res.errors.append(Problem(path, f"{where}.grch38", f"start {start} > end {end}"))
        return
    if not copy.get("band"):
        return
    names = band_range_names(copy["band"])
    if {band_chrom(n) for n in names} != {copy["chrom"]}:
        res.errors.append(
            Problem(
                path, f"{where}.band", f"band {copy['band']} is not on chromosome {copy['chrom']}"
            )
        )
        return
    if not _bands_exist(names, band_sets, path, f"{where}.band", res):
        return
    for version, known in sorted(band_sets.items()):
        ranges = [known[n].get("grch38") for n in names]
        if any(r is None for r in ranges):
            continue  # book-only band without coordinates
        lo, hi = min(r[0] for r in ranges), max(r[1] for r in ranges)
        if not (start <= hi and lo <= end):
            res.conflicts.append(
                Conflict(
                    rule,
                    where,
                    f"GRCh38 {start}-{end} is outside {copy['band']} ({lo}-{hi}, ISCN {version} "
                    "band data)",
                )
            )
        return  # coordinates don't differ per edition: one edition suffices
