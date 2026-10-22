import io
import struct
from datetime import datetime, timezone
from email.message import EmailMessage
from typing import Dict, Iterable, Optional, Tuple

import pytest

from markitdown_caret_email import load_rules


@pytest.fixture(scope="session")
def rules():
    return load_rules()


def make_eml(
    body: str,
    subject: str = "RE: Budget 2026",
    sender: str = "Jan Kowalski <jan.kowalski@acme.pl>",
    to: str = "Anna Nowak <anna.nowak@client.fr>",
    cc: Optional[str] = None,
    date: str = "Tue, 04 Mar 2025 09:30:00 +0000",
    html: Optional[str] = None,
    attachments: Iterable[Tuple[str, bytes, str]] = (),
) -> bytes:
    m = EmailMessage()
    m["From"] = sender
    m["To"] = to
    if cc:
        m["Cc"] = cc
    m["Subject"] = subject
    m["Date"] = date
    if html is not None and not body:
        m.set_content(html, subtype="html")
    else:
        m.set_content(body)
        if html is not None:
            m.add_alternative(html, subtype="html")
    for filename, data, mime in attachments:
        if mime == "message/rfc822":
            from email import message_from_bytes, policy

            m.add_attachment(message_from_bytes(data, policy=policy.default), filename=filename)
        else:
            maintype, subtype = mime.split("/")
            m.add_attachment(data, maintype=maintype, subtype=subtype, filename=filename)
    return bytes(m)


class FakeOle:
    """Just enough of olefile.OleFileIO for the .msg reader: a dict of stream paths."""

    def __init__(self, streams: Dict[str, bytes]):
        self.streams = streams

    def listdir(self, streams=True, storages=False):
        return [p.split("/") for p in self.streams]

    def openstream(self, path):
        return io.BytesIO(self.streams[path])

    def close(self):
        pass


def unicode_prop(value: str) -> bytes:
    return value.encode("utf-16-le")


def properties(header_size: int, longs: Dict[int, int] = None, times: Dict[int, datetime] = None) -> bytes:
    data = b"\x00" * header_size
    for prop, value in (longs or {}).items():
        data += struct.pack("<IIIi", (prop << 16) | 0x0003, 6, value, 0)
    for prop, value in (times or {}).items():
        ticks = int((value - datetime(1601, 1, 1, tzinfo=timezone.utc)).total_seconds() * 10_000_000)
        data += struct.pack("<IIQ", (prop << 16) | 0x0040, 6, ticks)
    return data


def make_msg_streams(
    prefix: str = "",
    header_size: int = 32,
    subject: str = "RE: Offre",
    sender: Tuple[str, str] = ("Anna Nowak", "anna.nowak@client.fr"),
    recipients: Iterable[Tuple[str, str, int]] = (("Jan Kowalski", "jan.kowalski@acme.pl", 1),),
    body: Optional[str] = "Bonjour,\n\nVoici l'offre.",
    body8: Optional[Tuple[bytes, int]] = None,  # (bytes, code page) for a non-Unicode message
    sent: datetime = datetime(2025, 3, 3, 15, 5, tzinfo=timezone.utc),
) -> Dict[str, bytes]:
    p = f"{prefix}/" if prefix else ""
    longs = {}
    streams = {}
    if body8 is not None:
        longs[0x3FFD] = body8[1]
        streams[f"{p}__substg1.0_1000001E"] = body8[0]
    elif body is not None:
        streams[f"{p}__substg1.0_1000001F"] = unicode_prop(body)
    streams[f"{p}__properties_version1.0"] = properties(header_size, longs, {0x0039: sent})
    streams[f"{p}__substg1.0_0037001F"] = unicode_prop(subject)
    streams[f"{p}__substg1.0_0C1A001F"] = unicode_prop(sender[0])
    streams[f"{p}__substg1.0_5D01001F"] = unicode_prop(sender[1])
    for i, (name, address, kind) in enumerate(recipients):
        r = f"{p}__recip_version1.0_#{i:08X}"
        streams[f"{r}/__properties_version1.0"] = properties(8, {0x0C15: kind})
        streams[f"{r}/__substg1.0_3001001F"] = unicode_prop(name)
        streams[f"{r}/__substg1.0_39FE001F"] = unicode_prop(address)
    return streams
