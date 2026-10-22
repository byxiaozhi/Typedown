"""Splitting one mail body into the messages of its thread, and removing the clutter.

A reply carries the whole conversation below it. Mail clients introduce each quoted
message in one of two ways, and we recognise both in every language in the rules:

* a header block, as Outlook writes it::

      ________________________________
      De : Anna Nowak <anna@example.com>
      Envoyé : lundi 3 mars 2025 10:12
      À : Jan Kowalski
      Objet : RE: Budget

* a "wrote" line followed by ``>``-quoted text, as Gmail, Apple Mail and most others do::

      On Mon, 3 Mar 2025 at 10:12, Anna Nowak <anna@example.com> wrote:
      > ...

Everything here is plain rules; nothing is sent anywhere and nothing is guessed by a model.
"""

import re
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple

from ._dates import parse_date
from ._model import Address, parse_address, parse_address_list
from ._rules import Rules, fold

_HEADER_LINE = re.compile(r"^\s*\**\s*([^\W\d_][\w .\-/]{0,24}?)\s*\**\s*:\s*\**\s*(.*?)\s*\**\s*$")
_SEPARATOR = re.compile(r"^\s*[_\-=]{10,}\s*$")
_BLANK_RUNS = re.compile(r"\n{3,}")
_TIME_IN_TEXT = re.compile(r"\d{1,2}[:h]\d{2}(?:\s*[AaPp]\.?\s?[Mm]\b\.?)?")
_NAME_REST = re.compile(r"^[\w'’\-.]*$")
_BARE_LINK = re.compile(r"^\s*(?:\[[^\]\n]*\]\([^)\s]*\)|<?(?:https?://|www\.)\S+?>?)\s*$")


@dataclass
class Message:
    sender: Optional[Address] = None
    to: List[Address] = field(default_factory=list)
    cc: List[Address] = field(default_factory=list)
    bcc: List[Address] = field(default_factory=list)
    date: Optional[str] = None
    subject: str = ""
    body: str = ""
    forwarded: bool = False


@dataclass
class _Boundary:
    start: int  # first line that belongs to the introduction (marker, separator, header)
    body_start: int  # first line of the quoted message's own text
    headers: Dict[str, str]
    forwarded: bool = False
    quoted: bool = False  # ">" style: the quoted text needs one level of ">" removed


def split_thread(body: str, rules: Rules, keep_signatures: bool = False) -> List[Message]:
    """Split a body into messages, newest first (the order they appear in)."""
    lines = _normalise(body).split("\n")
    messages: List[Message] = []
    headers: Dict[str, str] = {}
    forwarded = False

    while True:
        boundary = _find_boundary(lines, rules)
        if boundary is None:
            messages.append(_message(headers, lines, forwarded, rules, keep_signatures))
            break

        messages.append(_message(headers, lines[: boundary.start], forwarded, rules, keep_signatures))
        rest = lines[boundary.body_start :]
        if boundary.quoted:
            quoted, after = _take_quoted(rest)
            if after:  # text written below the quote belongs to the newer message
                messages[-1].body = (messages[-1].body + "\n\n" + clean_body("\n".join(after), rules, keep_signatures)).strip()
            rest = quoted
        lines = rest
        headers, forwarded = boundary.headers, boundary.forwarded

    return messages


def _normalise(text: str) -> str:
    text = text.replace("\r\n", "\n").replace("\r", "\n").replace(" ", " ")
    return "\n".join(line.rstrip() for line in text.split("\n"))


def _find_boundary(lines: List[str], rules: Rules) -> Optional[_Boundary]:
    for i, line in enumerate(lines):
        stripped = line.strip()
        if not stripped:
            continue

        # "On ... wrote:", possibly wrapped over two lines by the sending client
        for joined, used in ((stripped, 1), (stripped + " " + _line(lines, i + 1), 2)):
            if len(joined) < 400 and any(p.match(joined) for p in rules.wrote_patterns):
                if used == 2 and not _line(lines, i + 1):
                    continue
                return _Boundary(i, i + used, _parse_wrote(joined), quoted=True)

        header_start = i
        forwarded = False
        if rules.is_reply_marker(stripped) or _SEPARATOR.match(stripped):
            forwarded = rules.is_forward_marker(stripped)
            header_start = _next_nonblank(lines, i + 1)
            if header_start is None:
                if rules.is_reply_marker(stripped):
                    return _Boundary(i, len(lines), {}, forwarded)
                continue

        block = _header_block(lines, header_start, rules)
        if block is not None:
            headers, end = block
            return _Boundary(i, end, headers, forwarded)
        if rules.is_reply_marker(stripped):
            return _Boundary(i, i + 1, {}, forwarded)
    return None


def _header_block(lines: List[str], start: int, rules: Rules) -> Optional[Tuple[Dict[str, str], int]]:
    """Read a quoted header block starting at ``start``, or return None if it isn't one."""
    first = _header(lines[start], rules) if start < len(lines) else None
    if first is None or first[0] != "from":
        return None

    headers: Dict[str, str] = {}
    last_key = None
    i = start
    while i < len(lines) and i < start + 16:
        line = lines[i]
        if not line.strip():
            # Outlook's HTML, turned into text, can leave a blank line inside the block (after
            # "From:"); the block goes on if the next line is another field of it
            following = _next_nonblank(lines, i + 1)
            field_ = _header(lines[following], rules) if following is not None and following < start + 16 else None
            if field_ is None or field_[0] in headers:
                break
            i = following
            continue
        parsed = _header(line, rules)
        if parsed is not None and parsed[0] not in headers:
            last_key = parsed[0]
            headers[last_key] = parsed[1]
        elif last_key in ("to", "cc", "bcc") and parsed is None:
            headers[last_key] += " " + line.strip()  # a long recipient list, wrapped
        else:
            break
        i += 1

    if len({"sent", "to", "subject"} & headers.keys()) < 2:
        return None
    return headers, i


def _header(line: str, rules: Rules) -> Optional[Tuple[str, str]]:
    m = _HEADER_LINE.match(line)
    if not m:
        return None
    key = rules.header_keys.get(fold(m.group(1)).strip())
    return (key, m.group(2)) if key else None


def _parse_wrote(text: str) -> Dict[str, str]:
    """Pull the sender and date out of an "On <date>, <sender> wrote:" line."""
    body = re.sub(r"\s*\S+\s*:\s*$", "", text)  # drop the trailing "wrote:" word
    body = re.sub(r"\s*\b(?:a|napisał(?:\(a\)|a)?)\s*$", "", body)  # "a écrit", "napisał(a)"
    body = re.sub(r"^\s*(?:On|Le|El|W dniu)\s+", "", body, flags=re.IGNORECASE)
    time = None
    for time in _TIME_IN_TEXT.finditer(body):
        pass
    if time is None:
        return {"from": body.strip(" ,")}
    return {"sent": body[: time.end()].strip(" ,"), "from": body[time.end() :].strip(" ,")}


def _take_quoted(lines: List[str]) -> Tuple[List[str], List[str]]:
    """Split ``>``-quoted lines (one level removed) from the unquoted text after them."""
    quoted: List[str] = []
    i = 0
    while i < len(lines) and (lines[i].startswith(">") or not lines[i].strip()):
        line = lines[i]
        quoted.append(line[2:] if line.startswith("> ") else line[1:])
        i += 1
    after = lines[i:]
    if not any(l.strip() for l in quoted):
        return after, []  # "wrote:" with the earlier message below it, not quoted
    return quoted, after if any(l.strip() for l in after) else []


def _message(
    headers: Dict[str, str], lines: List[str], forwarded: bool, rules: Rules, keep_signatures: bool
) -> Message:
    sender = parse_address(headers["from"]) if headers.get("from") else None
    return Message(
        sender=sender,
        to=parse_address_list(headers.get("to", "")),
        cc=parse_address_list(headers.get("cc", "")),
        bcc=parse_address_list(headers.get("bcc", "")),
        date=parse_date(headers.get("sent"), rules.months) or headers.get("sent") or None,
        subject=headers.get("subject", ""),
        body=clean_body("\n".join(lines), rules, keep_signatures),
        forwarded=forwarded,
    )


def clean_body(text: str, rules: Rules, keep_signatures: bool = False) -> str:
    """Remove banners at the top and signatures and disclaimers at the bottom."""
    paragraphs = [p.strip("\n") for p in re.split(r"\n\s*\n", _normalise(text))]
    paragraphs = [p for p in paragraphs if p.strip()]

    # External-sender and "you don't often get email from" banners, at the top
    while paragraphs and _contains(paragraphs[0], rules.banner_phrases):
        paragraphs.pop(0)

    paragraphs = _strip_trailing(paragraphs, rules)
    if not keep_signatures:
        text = _cut_signature("\n\n".join(paragraphs), rules)
        paragraphs = _strip_trailing([p for p in re.split(r"\n\s*\n", text) if p.strip()], rules)

    text = "\n\n".join(paragraphs).strip()
    return _BLANK_RUNS.sub("\n\n", text)


def _strip_trailing(paragraphs: List[str], rules: Rules) -> List[str]:
    """Drop disclaimers, mobile signatures and separators from the end.

    Only from the end, so that a sentence in the message itself that mentions
    confidentiality is never touched.
    """
    paragraphs = list(paragraphs)
    while paragraphs:
        last = paragraphs[-1]
        if _contains(last, rules.disclaimer_phrases) or _SEPARATOR.match(last):
            paragraphs.pop()
            continue
        # The company's website under its disclaimer ("www.example.com")
        if (
            len(paragraphs) >= 2
            and _BARE_LINK.match(last)
            and (_contains(paragraphs[-2], rules.disclaimer_phrases) or _SEPARATOR.match(paragraphs[-2]))
        ):
            paragraphs.pop()
            continue
        lines = last.split("\n")
        kept = [l for l in lines if not _is_mobile_signature(l, rules) and not _SEPARATOR.match(l)]
        if len(kept) != len(lines):
            if any(l.strip() for l in kept):
                paragraphs[-1] = "\n".join(kept)
            else:
                paragraphs.pop()
            continue
        break
    return paragraphs


def _cut_signature(text: str, rules: Rules) -> str:
    lines = text.split("\n")

    # The standard "-- " delimiter: everything after it is signature
    for i in range(len(lines) - 1, -1, -1):
        if lines[i] in ("-- ", "--"):
            return "\n".join(lines[:i]).rstrip()

    # A closing ("Best regards", "Cordialement", "Pozdrawiam") near the end, followed by
    # the sender's name. Only then is the rest cut, and only if it looks like a signature
    # block: short lines, no questions. Anything less certain is left alone.
    nonblank = [i for i, l in enumerate(lines) if l.strip()]
    for i in reversed(nonblank[-25:]):
        closing, name_in_line = _closing(lines[i], rules)
        if not closing:
            continue
        if name_in_line:
            keep_until = i
        else:
            following = [j for j in nonblank if j > i]
            if not following or not _looks_like_name(lines[following[0]]):
                return text
            keep_until = following[0]
        rest = [lines[j] for j in nonblank if j > keep_until]
        if len(rest) > 15 or any(l.rstrip().endswith("?") or len(l) > 100 for l in rest):
            return text
        return "\n".join(lines[: keep_until + 1]).rstrip()
    return text


def _closing(line: str, rules: Rules) -> Tuple[bool, bool]:
    """Is this line a closing, and does it already carry the name ("Thanks, Anna")?"""
    folded = fold(line).strip().rstrip("!.,;: ")
    if folded in rules.closings:
        return True, False
    # Bilingual closings: "Pozdrawiam / With Regards", "Cordialement / Best regards"
    if "/" in folded and any(p.strip().rstrip("!.,;: ") in rules.closings for p in folded.split("/")):
        return True, False
    rest = _closing_rest(line, rules)
    if rest and _looks_like_name(rest):
        return True, True
    return False, False


def _closing_rest(line: str, rules: Rules) -> str:
    """What follows a closing on the same line ("Thanks, Anna!" gives "Anna"), or ""."""
    folded = fold(line).strip().rstrip("!.,;: ")
    for closing in rules.closings:
        if folded.startswith(closing + ",") or folded.startswith(closing + " -"):
            return line.strip()[len(closing) + 1 :].strip(" ,-!.;:")
    return ""


def signature_names(text: str, rules: Rules) -> List[str]:
    """The names written under a closing ("Kind regards,\nAnna Nowak" or "Thanks, Anna")."""
    lines = text.split("\n")
    names: List[str] = []
    for i, line in enumerate(lines):
        closing, name_in_line = _closing(line, rules)
        if not closing:
            continue
        if name_in_line:
            candidate = _closing_rest(line, rules)
        else:
            following = next((l for l in lines[i + 1 :] if l.strip()), "")
            candidate = following if _looks_like_name(following) else ""
        candidate = " ".join(w.strip(".,") for w in candidate.strip().strip("*_").split())
        if candidate and _looks_like_name(candidate):
            names.append(candidate)
    return names


def _looks_like_name(line: str) -> bool:
    # HTML mail often has the name in bold: "**Anna Nowak**"
    words = line.strip().strip("*_").split()
    return 1 <= len(words) <= 4 and all(_is_name_word(w) for w in words)


def _is_name_word(word: str) -> bool:
    """Capitalised, in any alphabet ("Anna", "Łukasz", "Ольга")."""
    return word[:1].isupper() and bool(_NAME_REST.match(word[1:]))


def _is_mobile_signature(line: str, rules: Rules) -> bool:
    folded = fold(line).strip()
    return len(folded) < 90 and any(folded.startswith(s) for s in rules.mobile_signatures)


def _contains(text: str, phrases: List[str]) -> bool:
    folded = fold(text)
    return any(p in folded for p in phrases)


def _line(lines: List[str], i: int) -> str:
    return lines[i].strip() if i < len(lines) else ""


def _next_nonblank(lines: List[str], start: int) -> Optional[int]:
    for i in range(start, len(lines)):
        if lines[i].strip():
            return i
    return None


def normalise_subject(subject: str, rules: Rules) -> str:
    """Strip RE:/TR:/RV:/Odp:/[EXTERNAL] prefixes, however many are stacked up."""
    prefixes = sorted({fold(p) for p in rules.subject_prefixes}, key=len, reverse=True)
    subject = subject.strip()
    changed = True
    while changed:
        changed = False
        folded = fold(subject)
        for p in prefixes:
            if p.startswith("[") or p.endswith(":"):
                if folded.startswith(p):
                    subject, changed = subject[len(p) :].lstrip(), True
                    break
            else:
                m = re.match(re.escape(p) + r"\s?:\s*", folded)
                if m:
                    subject, changed = subject[m.end() :], True
                    break
    return subject.strip()
