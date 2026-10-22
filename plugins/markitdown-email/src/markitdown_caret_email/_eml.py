"""Reading .eml files (RFC 822 / MIME) with the standard library only."""

from email import policy
from email.message import EmailMessage
from email.parser import BytesParser
from email.utils import parsedate_to_datetime
from typing import BinaryIO, Callable, Optional

from ._dates import format_datetime
from ._model import Attachment, Email, parse_address, parse_address_list


def read_eml(stream: BinaryIO, html_to_text: Callable[[str], str]) -> Email:
    msg: EmailMessage = BytesParser(policy=policy.default).parse(stream)  # type: ignore[assignment]

    email = Email(
        subject=_header(msg, "Subject"),
        sender=parse_address(_header(msg, "From")) if msg["From"] else None,
        to=parse_address_list(_header(msg, "To")),
        cc=parse_address_list(_header(msg, "Cc")),
        bcc=parse_address_list(_header(msg, "Bcc")),
        date=_date(msg),
    )

    body_part = msg.get_body(preferencelist=("plain", "html"))
    if body_part is not None and body_part.get_content_subtype() == "plain" and not _text(body_part).strip():
        # An empty plain-text alternative next to the real HTML one: use the HTML
        body_part = msg.get_body(preferencelist=("html",)) or body_part
    if body_part is not None:
        content = _text(body_part)
        if body_part.get_content_subtype() == "html":
            content = html_to_text(content)
        email.body = content

    for part in msg.iter_attachments():
        filename = part.get_filename() or ""
        content_type = part.get_content_type()
        if content_type == "message/rfc822":
            inner = part.get_payload()
            inner = inner[0] if isinstance(inner, list) and inner else inner
            data = inner.as_bytes() if hasattr(inner, "as_bytes") else None
            if not filename:
                filename = (_header(inner, "Subject") if data else "") or "attached message"
                filename += ".eml"
        else:
            try:
                data = part.get_content()
            except (KeyError, LookupError, ValueError):
                data = part.get_payload(decode=True)
            if isinstance(data, str):
                data = data.encode("utf-8")
        inline = part.get_content_disposition() == "inline" or (
            bool(part["Content-ID"]) and content_type.startswith("image/")
        )
        email.attachments.append(
            Attachment(filename or "attachment", data, content_type, inline)
        )

    return email


def _header(msg, name: str) -> str:
    value = msg.get(name)
    return str(value).strip() if value is not None else ""


def _date(msg) -> Optional[str]:
    raw = msg.get("Date")
    if not raw:
        return None
    try:
        return format_datetime(parsedate_to_datetime(str(raw)))
    except (TypeError, ValueError, IndexError):
        return str(raw)


def _text(part) -> str:
    try:
        return part.get_content()
    except (LookupError, ValueError):
        # Unknown or wrong charset label: decode leniently rather than fail the mail.
        payload = part.get_payload(decode=True) or b""
        return payload.decode("utf-8", errors="replace")
