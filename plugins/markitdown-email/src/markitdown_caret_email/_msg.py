"""Reading Outlook .msg files (the file you get by dragging a mail out of Outlook).

A .msg is an OLE compound file: each MAPI property is either its own stream, named
``__substg1.0_<tag><type>``, or (for fixed-width values such as dates and numbers) an
entry in the ``__properties_version1.0`` stream. Recipients and attachments are
sub-storages with the same layout.
"""

import codecs
import struct
from datetime import datetime, timedelta, timezone
from email.parser import Parser
from email.utils import parsedate_to_datetime
from typing import Any, Callable, Dict, List, Optional

from ._dates import format_datetime
from ._model import Address, Attachment, Email, parse_address_list

PROPERTIES = "__properties_version1.0"
PT_LONG = 0x0003
PT_SYSTIME = 0x0040

# Property ids (PidTag...)
SUBJECT = "0037"
CLIENT_SUBMIT_TIME = 0x0039
MESSAGE_DELIVERY_TIME = 0x0E06
TRANSPORT_HEADERS = "007D"
SENDER_NAME = "0C1A"
SENDER_EMAIL = "0C1F"
SENDER_SMTP = "5D01"
DISPLAY_TO = "0E04"
DISPLAY_CC = "0E03"
BODY = "1000"
BODY_HTML = "1013"
MESSAGE_CODEPAGE = 0x3FFD
INTERNET_CODEPAGE = 0x3FDE

RECIPIENT_TYPE = 0x0C15  # 1 To, 2 Cc, 3 Bcc
RECIPIENT_NAME = "3001"
RECIPIENT_EMAIL = "3003"
RECIPIENT_SMTP = "39FE"

ATTACH_DATA = "3701"
ATTACH_FILENAME = "3704"
ATTACH_LONG_FILENAME = "3707"
ATTACH_MIME = "370E"
ATTACH_CONTENT_ID = "3712"
ATTACH_DISPLAY_NAME = "3001"

FILETIME_EPOCH = datetime(1601, 1, 1, tzinfo=timezone.utc)


def is_msg(ole: Any) -> bool:
    names = {"/".join(entry) for entry in ole.listdir(streams=True, storages=False)}
    return PROPERTIES in names and any(n.startswith("__substg1.0_") for n in names)


EMBEDDED = "__substg1.0_3701000D"  # an Outlook item attached to the mail ("Forward as attachment")
MAX_DEPTH = 3


def read_msg(ole: Any, html_to_text: Callable[[str], str], root: str = "", depth: int = 0) -> Email:
    """Read an opened olefile.OleFileIO (or anything with the same two methods).

    ``root`` is the storage of an attached Outlook item, whose layout is the same as a
    top-level message except for a shorter property stream header.
    """
    reader = _Reader(ole, root)
    top = reader.fixed("", header_size=24 if root else 32)
    codec = _codec(top.get(MESSAGE_CODEPAGE)) or _codec(top.get(INTERNET_CODEPAGE))
    body_codec = _codec(top.get(INTERNET_CODEPAGE)) or codec

    email = Email(subject=reader.string("", SUBJECT, codec) or "")

    name = reader.string("", SENDER_NAME, codec) or ""
    address = reader.string("", SENDER_SMTP, codec) or _smtp(reader.string("", SENDER_EMAIL, codec))
    if name or address:
        email.sender = Address(name, address or "")

    email.to, email.cc, email.bcc = _recipients(reader, codec)
    if not email.to and not email.cc and not email.bcc:
        email.to = parse_address_list(reader.string("", DISPLAY_TO, codec) or "")
        email.cc = parse_address_list(reader.string("", DISPLAY_CC, codec) or "")

    email.date = _date(reader, top, codec)

    body = reader.string("", BODY, body_codec)
    if not body:
        html = reader.binary("", BODY_HTML)
        if html:
            body = html_to_text(_decode(html, body_codec))
        else:
            body = html_to_text(reader.string("", BODY_HTML, body_codec) or "")
    email.body = body or ""

    email.attachments = _attachments(reader, codec, html_to_text, depth)
    return email


def _recipients(reader: "_Reader", codec: Optional[str]):
    to: List[Address] = []
    cc: List[Address] = []
    bcc: List[Address] = []
    for storage in reader.storages("__recip_version1.0_#"):
        kind = reader.fixed(storage, header_size=8).get(RECIPIENT_TYPE, 1)
        address = Address(
            reader.string(storage, RECIPIENT_NAME, codec) or "",
            reader.string(storage, RECIPIENT_SMTP, codec)
            or _smtp(reader.string(storage, RECIPIENT_EMAIL, codec))
            or "",
        )
        if kind == 1:
            to.append(address)
        elif kind == 2:
            cc.append(address)
        elif kind == 3:
            bcc.append(address)
    return to, cc, bcc


def _attachments(
    reader: "_Reader", codec: Optional[str], html_to_text: Callable[[str], str], depth: int
) -> List[Attachment]:
    result = []
    for storage in reader.storages("__attach_version1.0_#"):
        filename = (
            reader.string(storage, ATTACH_LONG_FILENAME, codec)
            or reader.string(storage, ATTACH_FILENAME, codec)
            or reader.string(storage, ATTACH_DISPLAY_NAME, codec)
            or "attachment"
        )
        content_type = reader.string(storage, ATTACH_MIME, codec) or ""
        data = reader.binary(storage, ATTACH_DATA)  # None for an attached Outlook item
        inline = bool(reader.string(storage, ATTACH_CONTENT_ID, codec)) and (
            content_type.startswith("image/")
            or filename.lower().endswith((".png", ".jpg", ".jpeg", ".gif", ".bmp"))
        )
        attachment = Attachment(filename, data, content_type, inline)
        if data is None and reader.has_storage(f"{storage}/{EMBEDDED}") and depth < MAX_DEPTH:
            attachment.email = read_msg(reader.ole, html_to_text, reader.path(f"{storage}/{EMBEDDED}"), depth + 1)
        result.append(attachment)
    return result


def _date(reader: "_Reader", top: Dict[int, Any], codec: Optional[str]) -> Optional[str]:
    headers = reader.string("", TRANSPORT_HEADERS, codec)
    if headers:
        raw = Parser().parsestr(headers, headersonly=True).get("Date")
        if raw:
            try:
                return format_datetime(parsedate_to_datetime(raw))
            except (TypeError, ValueError, IndexError):
                pass
    for tag in (CLIENT_SUBMIT_TIME, MESSAGE_DELIVERY_TIME):
        value = top.get(tag)
        if isinstance(value, datetime):
            # Stored in UTC; show it in this computer's time zone, like Outlook does.
            return format_datetime(value.astimezone())
    return None


def _smtp(address: Optional[str]) -> Optional[str]:
    # Exchange-internal senders carry an X.500 path ("/O=EXCHANGELABS/OU=...") here.
    if address and "@" in address and not address.startswith("/"):
        return address
    return None


def _codec(codepage: Optional[int]) -> Optional[str]:
    if not codepage:
        return None
    name = {20127: "ascii", 28591: "iso8859-1", 28592: "iso8859-2", 65001: "utf-8"}.get(
        codepage, f"cp{codepage}"
    )
    try:
        return codecs.lookup(name).name
    except LookupError:
        return None


def _decode(data: bytes, codec: Optional[str]) -> str:
    data = data.rstrip(b"\x00")
    for candidate in (codec, "utf-8", "cp1252"):
        if candidate:
            try:
                return data.decode(candidate)
            except (UnicodeDecodeError, LookupError):
                continue
    return data.decode("utf-8", errors="replace")


class _Reader:
    def __init__(self, ole: Any, root: str = ""):
        self.ole = ole
        self.root = root
        names = ("/".join(e) for e in ole.listdir(streams=True, storages=False))
        if root:
            names = (n[len(root) + 1 :] for n in names if n.startswith(root + "/"))
        self.names = set(names)

    def path(self, relative: str) -> str:
        return f"{self.root}/{relative}" if self.root else relative

    def has_storage(self, relative: str) -> bool:
        return any(n.startswith(relative + "/") for n in self.names)

    def _read(self, path: str) -> Optional[bytes]:
        if path not in self.names:
            return None
        try:
            return self.ole.openstream(self.path(path)).read()
        except Exception:
            return None

    def storages(self, prefix: str) -> List[str]:
        # Only direct children of the root: an attached Outlook item has its own
        # recipients and attachments nested deeper, which belong to that item.
        found = {n.split("/")[0] for n in self.names if n.startswith(prefix) and n.count("/") == 1}
        return sorted(found)

    def string(self, storage: str, tag: str, codec: Optional[str]) -> Optional[str]:
        base = f"{storage}/__substg1.0_{tag}" if storage else f"__substg1.0_{tag}"
        data = self._read(base + "001F")  # PT_UNICODE
        if data is not None:
            text = data.decode("utf-16-le", errors="replace").rstrip("\x00").strip()
            return text or None
        data = self._read(base + "001E")  # PT_STRING8, in the message's code page
        if data is not None:
            return _decode(data, codec).strip() or None
        return None

    def binary(self, storage: str, tag: str) -> Optional[bytes]:
        base = f"{storage}/__substg1.0_{tag}" if storage else f"__substg1.0_{tag}"
        return self._read(base + "0102")

    def fixed(self, storage: str, header_size: int) -> Dict[int, Any]:
        path = f"{storage}/{PROPERTIES}" if storage else PROPERTIES
        data = self._read(path) or b""
        values: Dict[int, Any] = {}
        for offset in range(header_size, len(data) - 15, 16):
            tag, _flags = struct.unpack_from("<II", data, offset)
            kind, prop = tag & 0xFFFF, tag >> 16
            if kind == PT_LONG:
                values[prop] = struct.unpack_from("<I", data, offset + 8)[0]
            elif kind == PT_SYSTIME:
                ticks = struct.unpack_from("<Q", data, offset + 8)[0]
                if ticks:
                    values[prop] = FILETIME_EPOCH + timedelta(microseconds=ticks // 10)
        return values
