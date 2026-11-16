"""Per-edition layout profiles for the ISCN PDFs."""

from dataclasses import dataclass


@dataclass(frozen=True)
class Edition:
    version: str
    page_offset: int  # book page = PDF page - page_offset
    toc_pdf_pages: tuple[int, int]  # inclusive, 1-based PDF pages
    first_chapter: int
    last_chapter: int  # chapters after this one (references, index, ...) are not extracted
    footer_top: float  # lines whose top is below this y (pt) are running footers
    figure_fonts: tuple[str, ...]  # fonts used only for ideogram labels; dropped
    body_size: float  # main text size (pt)
    small_size: float  # example explanations, tables (pt)
    heading_font: str  # substring of the heading font name
    text_right: float  # right edge of the text block (pt); change bars are drawn beyond it


EDITIONS: dict[str, Edition] = {
    "2016": Edition(
        version="2016",
        page_offset=6,
        toc_pdf_pages=(3, 6),
        first_chapter=1,
        last_chapter=16,
        footer_top=715.0,
        figure_fonts=("TTE2340D80t00",),
        body_size=10.0,
        small_size=8.2,
        heading_font="MyriadPro-Bold",
        text_right=525.0,
    ),
}


def get_edition(version: str) -> Edition:
    try:
        return EDITIONS[version]
    except KeyError:
        known = ", ".join(sorted(EDITIONS))
        raise KeyError(f"no extraction profile for ISCN {version} (known: {known})") from None
