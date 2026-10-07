"""Reading the dates that mail clients write in quoted headers, in any of the languages."""

import re
from datetime import datetime
from email.utils import parsedate_to_datetime
from typing import Dict, Optional

from ._rules import fold

_YEAR = re.compile(r"\b((?:19|20)\d{2})\b")
_TIME = re.compile(
    r"\b(\d{1,2})[:h.](\d{2})(?::\d{2})?\s*(a\.?\s?m\b\.?|p\.?\s?m\b\.?)?(?![\d])",
    re.IGNORECASE,
)
_NUMERIC = re.compile(r"\b(\d{1,2})[./-](\d{1,2})[./-]((?:19|20)\d{2})\b")
_ISO = re.compile(r"\b((?:19|20)\d{2})-(\d{2})-(\d{2})\b")
_MERIDIEM = re.compile(r"\d\s*[ap]\.?\s?m\b\.?", re.IGNORECASE)
_WORD = re.compile(r"[^\W\d_]+\.?", re.UNICODE)


def format_datetime(value: datetime) -> str:
    return value.strftime("%Y-%m-%d %H:%M")


def parse_date(text: Optional[str], months: Dict[str, int]) -> Optional[str]:
    """Return "YYYY-MM-DD HH:MM" (or "YYYY-MM-DD"), or None when it can't be read safely.

    Numeric dates like 03/04/2025 are only accepted when the day is unambiguous, since
    an English sender and a French one mean different days by them.
    """
    if not text:
        return None
    text = text.strip()

    if not _MERIDIEM.search(text):  # the RFC parser silently drops AM/PM
        try:  # RFC 2822, as in a real Date: header
            return format_datetime(parsedate_to_datetime(text))
        except (TypeError, ValueError, IndexError):
            pass

    year = month = day = None
    rest = text

    iso = _ISO.search(text)
    if iso:
        year, month, day = int(iso.group(1)), int(iso.group(2)), int(iso.group(3))
        rest = text[: iso.start()] + " " + text[iso.end() :]
    else:
        numeric = _NUMERIC.search(text)
        if numeric:
            a, b, year = int(numeric.group(1)), int(numeric.group(2)), int(numeric.group(3))
            if a > 12 >= b:
                day, month = a, b
            elif b > 12 >= a:
                month, day = a, b
            else:
                return None
            rest = text[: numeric.start()] + " " + text[numeric.end() :]
        else:
            y = _YEAR.search(text)
            if not y:
                return None
            year = int(y.group(1))
            for word in _WORD.findall(text):
                key = fold(word).rstrip(".")
                if key in months:
                    month = months[key]
                    break
            if month is None:
                return None
            without_year = text[: y.start()] + " " + text[y.end() :]
            without_time = _TIME.sub(" ", without_year)
            numbers = re.findall(r"\b(\d{1,2})(?:st|nd|rd|th|er|º|\.)?\b", without_time)
            days = [int(n) for n in numbers if 1 <= int(n) <= 31]
            if not days:
                return None
            day = days[0]
            rest = without_year

    try:
        date = datetime(year, month, day)
    except ValueError:
        return None

    t = _TIME.search(rest)
    if not t:
        return date.strftime("%Y-%m-%d")
    hour, minute = int(t.group(1)), int(t.group(2))
    meridiem = (t.group(3) or "").lower().replace(".", "").replace(" ", "")
    if meridiem == "pm" and hour < 12:
        hour += 12
    elif meridiem == "am" and hour == 12:
        hour = 0
    if hour > 23 or minute > 59:
        return date.strftime("%Y-%m-%d")
    return format_datetime(date.replace(hour=hour, minute=minute))
