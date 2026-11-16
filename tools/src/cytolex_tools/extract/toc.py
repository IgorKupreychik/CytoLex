"""Table-of-contents parsing (the authoritative list of sections to check against)."""

import re
from dataclasses import dataclass

NUMBERED = re.compile(r"^(\d+(?:\.\d+)*)\s+(.+)$")
# "<title> ....... 81" (leaders may be broken by spaces or a trailing period of the title)
WITH_PAGE = re.compile(r"^(.*?)(?:\s*\.){2,}\s*(\d+)$")


@dataclass(frozen=True)
class TocEntry:
    number: str
    title: str
    book_page: int

    @property
    def chapter(self) -> int:
        return int(self.number.split(".")[0])


def clean_title(title: str) -> str:
    title = re.sub(r"\s+", " ", title.replace("|", " ")).strip()
    return title.rstrip(". ").strip()


def parse_toc(lines: list[str]) -> list[TocEntry]:
    """Parse TOC lines; a numbered line without a page number continues on the next line."""
    entries: list[TocEntry] = []
    pending: tuple[str, str] | None = None
    for raw in lines:
        line = re.sub(r"\s+", " ", raw.replace("|", " ")).strip()
        if pending is not None:
            number, head = pending
            m = WITH_PAGE.match(line)
            if m:
                entries.append(
                    TocEntry(number, clean_title(f"{head} {m.group(1)}"), int(m.group(2)))
                )
                pending = None
            else:
                pending = (number, f"{head} {line}")
            continue
        m = NUMBERED.match(line)
        if not m:
            continue
        number, rest = m.groups()
        mp = WITH_PAGE.match(rest)
        if mp:
            entries.append(TocEntry(number, clean_title(mp.group(1)), int(mp.group(2))))
        else:
            pending = (number, rest)
    return entries
