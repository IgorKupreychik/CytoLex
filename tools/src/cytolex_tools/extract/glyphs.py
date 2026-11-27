"""Font-aware glyph repair.

The ISCN 2016 PDF draws several notation symbols with symbol fonts whose text layer maps to
unrelated ASCII characters (e.g. the arrow `→` extracts as `]`). The same character in the body
font is genuine (`[25]`), so the mapping is keyed by (font, char). The mapping itself is
person-checked data in `curated/extract/glyphs.yaml`.
"""

from collections import Counter, defaultdict
from pathlib import Path

from ruamel.yaml import YAML

from ..config import CURATED_DIR

GLYPHS_FILE = CURATED_DIR / "extract" / "glyphs.yaml"


def load_glyphs(
    path: Path = GLYPHS_FILE,
) -> tuple[dict[tuple[str, str], str], set[tuple[str, str]]]:
    """(font base name, extracted char) -> real char, and the mappings marked `uncertain`."""
    data = YAML(typ="safe").load(path.read_text(encoding="utf-8"))
    mapping, uncertain = {}, set()
    for g in data["glyphs"]:
        key = (str(g["font"]), str(g["char"]))
        mapping[key] = str(g["to"])
        if g.get("uncertain"):
            uncertain.add(key)
    return mapping, uncertain


GLYPH_MAP, UNCERTAIN = load_glyphs()  # UNCERTAIN: not yet checked against the page image

# Fonts whose characters can't be trusted unless they are in GLYPH_MAP.
SYMBOL_FONT_MARKERS = ("Math", "Symbol", "Pi-")


def font_base(fontname: str) -> str:
    """`CLORKZ+TimesSqu-Roman` -> `TimesSqu-Roman`."""
    return fontname.split("+", 1)[-1]


def is_symbol_font(base: str) -> bool:
    return any(m in base for m in SYMBOL_FONT_MARKERS)


class GlyphMapper:
    """Maps characters and records what it did (for the manifest)."""

    def __init__(self) -> None:
        self.mapped: Counter[tuple[str, str, str]] = Counter()
        self.unmapped: defaultdict[tuple[str, str], set[int]] = defaultdict(set)
        self.phantom_spaces = 0  # zero-width spaces dropped by the layout pass
        self.reanchored = 0  # glyphs with broken vertical metrics moved back onto their line

    def map(self, char: str, fontname: str, book_page: int) -> str:
        base = font_base(fontname)
        key = (base, char)
        if key in GLYPH_MAP:
            out = GLYPH_MAP[key]
            self.mapped[(base, char, out)] += 1
            return out
        if is_symbol_font(base) and not char.isspace():
            self.unmapped[key].add(book_page)
        return char

    def report(self) -> dict:
        mapped = [
            {
                "font": f,
                "from": f"U+{ord(c):04X} {c!r}",
                "to": o,
                "count": n,
                **({"uncertain": True} if (f, c) in UNCERTAIN else {}),
            }
            for (f, c, o), n in sorted(self.mapped.items())
        ]
        unmapped = [
            {"font": f, "char": f"U+{ord(c):04X} {c!r}", "book_pages": sorted(pages)}
            for (f, c), pages in sorted(self.unmapped.items())
        ]
        return {
            "mapped": mapped,
            "unmapped": unmapped,
            "phantom_spaces_dropped": self.phantom_spaces,
            "glyphs_reanchored": self.reanchored,
        }
