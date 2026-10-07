"""The email as the readers see it, before any cleanup."""

import re
from dataclasses import dataclass, field
from typing import List, Optional


@dataclass
class Address:
    name: str = ""
    email: str = ""

    def __str__(self) -> str:
        if self.name and self.email and self.name.lower() != self.email.lower():
            return f"{self.name} <{self.email}>"
        return self.name or self.email


@dataclass
class Attachment:
    filename: str
    data: Optional[bytes]  # None when it can't be extracted (e.g. an attached Outlook item)
    content_type: str = ""
    inline: bool = False  # an image shown in the body, usually a signature logo
    email: Optional["Email"] = None  # an Outlook item attached to a .msg, already read


@dataclass
class Email:
    subject: str = ""
    sender: Optional[Address] = None
    to: List[Address] = field(default_factory=list)
    cc: List[Address] = field(default_factory=list)
    bcc: List[Address] = field(default_factory=list)  # only in a sender's own copy
    date: Optional[str] = None  # "YYYY-MM-DD HH:MM", or the text as written
    body: str = ""  # plain text; HTML-only mail is converted before it gets here
    attachments: List[Attachment] = field(default_factory=list)


_ANGLE = re.compile(
    r"^(?P<name>.*?)\s*[<\[]\s*(?:mailto:)?(?P<email>[^<>\[\]\s]+@[^<>\[\]\s]+)\s*[>\]]\s*$"
)
_NAME_PART = re.compile(r"^[A-ZÀ-ÖØ-ÞĀ-Ž][\w'’\-]*(?: [A-ZÀ-ÖØ-ÞĀ-Ž][\w'’\-]*)?$")
_BARE = re.compile(r"^[^@\s]+@[^@\s]+$")


def parse_address(text: str) -> Address:
    """Parse one address as mail clients write it in quoted headers.

    Handles "Anna Nowak <anna@x.pl>", "Nowak, Anna <anna@x.pl>" (the comma that trips
    up email.utils), "Anna Nowak [mailto:anna@x.pl]" (older Outlook), bare addresses
    and bare names.
    """
    text = text.strip().strip(";,").strip()
    text = re.sub(r"\(\s*(<[^<>]+>)\s*\)", r"\1", text)  # Gmail in Spanish: "Ana (<ana@x.es>)"
    m = _ANGLE.match(text)
    if m:
        return Address(_clean_name(m.group("name")), m.group("email").strip())
    if _BARE.match(text):
        return Address("", text)
    return Address(_clean_name(text), "")


def parse_address_list(text: str) -> List[Address]:
    """Parse a To/Cc line. Outlook separates with ';', everyone else with ','."""
    text = (text or "").strip()
    if not text:
        return []
    if ";" in text:
        parts = text.split(";")
    elif "<" in text or "[mailto:" in text:
        # Split on commas that follow a closing bracket, so "Nowak, Anna <a@x>" stays whole.
        parts = re.split(r"(?<=[>\]])\s*,", text)
    else:
        # Commas outside quotes: "Jan Kowalski, anna@x.fr". (email.utils.getaddresses would
        # keep only "Jan" of a bare "Jan Kowalski", which Outlook often writes.)
        parts = re.split(r',(?=(?:[^"]*"[^"]*")*[^"]*$)', text)
    return [a for a in (parse_address(p) for p in parts) if a.name or a.email]


def _clean_name(name: str) -> str:
    name = re.sub(r"\s+", " ", name.strip().strip("'\"").strip())
    return flip_name(name)


def flip_name(name: str) -> str:
    """'Nowak, Anna' -> 'Anna Nowak' (the order of Outlook's company directory).

    Only when both sides look like names, so "ACME, Inc." stays as it is.
    """
    if name.count(",") == 1:
        last, first = (p.strip() for p in name.split(","))
        if _NAME_PART.match(last) and _NAME_PART.match(first) and " " not in last:
            return f"{first} {last}"
    return name
