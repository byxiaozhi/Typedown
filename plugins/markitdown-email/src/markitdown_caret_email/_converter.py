"""The MarkItDown converter: an email file in, one clean Markdown thread out."""

import io
import json
import os
import re
from dataclasses import dataclass, field
from typing import Any, BinaryIO, Dict, List, Optional, Sequence, Tuple, Union

from markitdown import DocumentConverter, DocumentConverterResult, StreamInfo
from markitdown.converters import HtmlConverter

from ._eml import read_eml
from ._model import Address, Attachment, Email
from ._redact import Redactor
from ._rules import Rules, load_rules
from ._thread import Message, normalise_subject, split_thread

EML_EXTENSIONS = (".eml",)
MSG_EXTENSIONS = (".msg",)
EML_MIME_TYPES = ("message/rfc822",)
MSG_MIME_TYPES = ("application/vnd.ms-outlook",)

MAX_ATTACHMENT_BYTES = 25 * 1024 * 1024
GENERATOR = "markitdown-caret-email"


@dataclass
class Options:
    redact: bool = True
    names: List[str] = field(default_factory=list)
    attachments: bool = True
    keep_signatures: bool = False
    rules: List[str] = field(default_factory=list)

    @classmethod
    def resolve(cls, kwargs: Dict[str, Any]) -> "Options":
        """Options come from convert() keyword arguments, else environment variables.

        The environment variables are there for the ``markitdown`` command line, which
        has no way to pass options through to a plugin.
        """
        names = kwargs.get("email_names")
        if names is None:
            names = os.environ.get("CARET_EMAIL_NAMES") or []
        rules = kwargs.get("email_rules")
        if rules is None:
            rules = [p for p in os.environ.get("CARET_EMAIL_RULES", "").split(os.pathsep) if p]
        return cls(
            redact=_flag(kwargs.get("email_redact"), "CARET_EMAIL_REDACT", True),
            names=read_names(names),
            attachments=_flag(kwargs.get("email_attachments"), "CARET_EMAIL_ATTACHMENTS", True),
            keep_signatures=_flag(kwargs.get("email_keep_signatures"), "CARET_EMAIL_KEEP_SIGNATURES", False),
            rules=[rules] if isinstance(rules, str) else list(rules),
        )


def read_names(value: Union[str, os.PathLike, Sequence[str]]) -> List[str]:
    """A list of names, or the path of a text file with one name per line (# comments)."""
    if isinstance(value, (str, os.PathLike)):
        with open(value, "r", encoding="utf-8-sig") as f:
            lines = f.read().splitlines()
    else:
        lines = list(value)
    return [l.strip() for l in lines if l.strip() and not l.strip().startswith("#")]


def _flag(value: Any, env: str, default: bool) -> bool:
    if value is not None:
        return bool(value)
    raw = os.environ.get(env)
    if raw is None or raw.strip() == "":
        return default
    return raw.strip().lower() not in ("0", "false", "no", "off")


class EmailConverter(DocumentConverter):
    """Converts .eml and .msg files into a cleaned, optionally redacted thread."""

    def __init__(self, markitdown: Any = None):
        super().__init__()
        self._markitdown = markitdown  # used to convert attachments
        self._default_rules: Optional[Rules] = None

    def accepts(self, file_stream: BinaryIO, stream_info: StreamInfo, **kwargs: Any) -> bool:
        return _kind(stream_info) is not None

    def convert(self, file_stream: BinaryIO, stream_info: StreamInfo, **kwargs: Any) -> DocumentConverterResult:
        options = Options.resolve(kwargs)
        rules = self._rules(options)
        email = _read(file_stream, _kind(stream_info) or "eml")
        source = stream_info.filename or (os.path.basename(stream_info.local_path) if stream_info.local_path else None)
        markdown, title = render(email, rules, options, self._markitdown, source)
        return DocumentConverterResult(markdown=markdown, title=title)

    def _rules(self, options: Options) -> Rules:
        if options.rules:
            return load_rules(extra=options.rules)
        if self._default_rules is None:
            self._default_rules = load_rules()
        return self._default_rules


def _kind(stream_info: StreamInfo) -> Optional[str]:
    extension = (stream_info.extension or "").lower()
    mimetype = (stream_info.mimetype or "").lower()
    if extension in MSG_EXTENSIONS or mimetype.startswith(MSG_MIME_TYPES):
        return "msg"
    if extension in EML_EXTENSIONS or mimetype.startswith(EML_MIME_TYPES):
        return "eml"
    return None


def _html_to_text(html: str) -> str:
    return HtmlConverter().convert_string(html).markdown


def _read(stream: BinaryIO, kind: str) -> Email:
    if kind == "eml":
        return read_eml(stream, _html_to_text)

    import olefile  # a MarkItDown dependency through its [outlook] extra; we require it

    from ._msg import read_msg

    ole = olefile.OleFileIO(stream)
    try:
        return read_msg(ole, _html_to_text)
    finally:
        ole.close()


# --------------------------------------------------------------------------- rendering


def render(
    email: Email,
    rules: Rules,
    options: Options,
    markitdown: Any = None,
    source: Optional[str] = None,
) -> Tuple[str, str]:
    """Return the Markdown for one email file and its title."""
    people: List[Address] = []
    body, subject, messages = _render_thread(email, rules, options, markitdown, people, depth=0)

    dates = [m.date for m in messages if m.date and re.match(r"^\d{4}-\d{2}-\d{2}", m.date)]
    attachments = [a.filename for a in email.attachments if not a.inline]
    front = {
        "type": "email-thread",
        "subject": subject,
        "started": min(dates) if dates else None,
        "last": max(dates) if dates else None,
        "messages": len(messages),
        "participants": [str(p) for p in _unique(people)],
        "attachments": attachments,
        "source": source,
        "redacted": options.redact,
        "generator": GENERATOR,
    }
    lines = ["---"]
    for key, value in front.items():
        if value is None or value == []:
            continue
        lines.append(f"{key}: {json.dumps(value, ensure_ascii=False)}")  # JSON is valid YAML
    lines.append("---")
    markdown = "\n".join(lines) + "\n\n" + body

    if options.redact:
        # Every form seen, not just one per address: an attached mail may show the
        # same address under another display name, and that name must go too
        redactor = Redactor(people=people, names=options.names)
        redactor.learn_names(markdown, rules)
        markdown = redactor.redact(markdown)
        subject = redactor.redact(subject)
    return markdown.strip() + "\n", subject


def _render_thread(
    email: Email,
    rules: Rules,
    options: Options,
    markitdown: Any,
    people: List[Address],
    depth: int,
    title: bool = True,
) -> Tuple[str, str, List[Message]]:
    messages = split_thread(email.body, rules, options.keep_signatures)
    top = messages[0]
    top.sender, top.to, top.cc, top.bcc, top.date = email.sender, email.to, email.cc, email.bcc, email.date
    top.subject = email.subject
    messages = _dedupe(messages)
    messages.reverse()  # oldest first, the order people read a conversation in

    subject = normalise_subject(email.subject or next((m.subject for m in messages if m.subject), ""), rules)
    for m in messages:
        people.extend(a for a in [m.sender, *m.to, *m.cc, *m.bcc] if a is not None)

    h = _heading(depth + 1)
    out = [f"{h} {subject or 'Email'}", ""] if title else []
    for i, m in enumerate(messages, 1):
        who = str(m.sender) if m.sender else "Unknown sender"
        heading = f"{_heading(depth + 2)} {i}. {who}"
        if m.date:
            heading += f" · {m.date}"
        if m.forwarded:
            heading += " (forwarded)"
        out.append(heading)
        details = []
        if m.to:
            details.append("To: " + "; ".join(str(a) for a in m.to))
        if m.cc:
            details.append("Cc: " + "; ".join(str(a) for a in m.cc))
        if m.bcc:
            details.append("Bcc: " + "; ".join(str(a) for a in m.bcc))
        if details:
            out += ["", "*" + " · ".join(details) + "*"]
        out += ["", m.body or "*(no text)*", ""]

    attachments = [a for a in email.attachments if not a.inline]
    if attachments:
        out += [f"{_heading(depth + 2)} Attachments", ""]
        for a in attachments:
            out += [f"{_heading(depth + 3)} {a.filename}", "", _attachment(a, rules, options, markitdown, people, depth), ""]
    return "\n".join(out).rstrip() + "\n", subject, messages


def _attachment(
    a: Attachment, rules: Rules, options: Options, markitdown: Any, people: List[Address], depth: int
) -> str:
    if not options.attachments:
        return "*(attachment not converted)*"
    if a.email is not None:
        # A mail attached to a mail: render it here, so its people are masked too
        title = _title_needed(a, rules)
        body, _, _ = _render_thread(
            a.email, rules, options, markitdown, people, depth + (3 if title else 2), title=title
        )
        return body.rstrip()
    if a.data is None:
        return "*(attached Outlook item; save it separately to convert it)*"
    if len(a.data) > MAX_ATTACHMENT_BYTES:
        return "*(attachment too large to convert)*"

    extension = os.path.splitext(a.filename)[1].lower()
    if extension in EML_EXTENSIONS + MSG_EXTENSIONS or a.content_type in EML_MIME_TYPES:
        if depth >= 3:
            return "*(attached email nested too deeply)*"
        try:
            inner = _read(io.BytesIO(a.data), "msg" if extension in MSG_EXTENSIONS else "eml")
            title = _title_needed(Attachment(a.filename, None, email=inner), rules)
            body, _, _ = _render_thread(
                inner, rules, options, markitdown, people, depth + (3 if title else 2), title=title
            )
            return body.rstrip()
        except Exception as e:  # a damaged attachment must not lose the whole mail
            return f"*(attached email could not be read: {type(e).__name__})*"

    if markitdown is None:
        return "*(attachment not converted)*"
    try:
        result = markitdown.convert_stream(
            io.BytesIO(a.data),
            stream_info=StreamInfo(extension=extension or None, filename=a.filename, mimetype=a.content_type or None),
        )
    except Exception as e:
        return f"*(attachment could not be converted: {type(e).__name__})*"
    text = result.markdown.strip()
    return _demote(text, depth + 3) if text else "*(attachment is empty)*"


def _heading(level: int) -> str:
    return "#" * min(level, 6)  # Markdown has six levels; deeper nesting shares the last


def _title_needed(a: Attachment, rules: Rules) -> bool:
    """An attached mail is usually named after its subject; don't show the name twice."""
    name = os.path.splitext(a.filename)[0] if a.filename.lower().endswith((".msg", ".eml")) else a.filename
    subject = a.email.subject if a.email else ""
    return normalise_subject(name, rules) != normalise_subject(subject, rules)


def _demote(markdown: str, levels: int) -> str:
    """Push an attachment's headings below the email's own, outside code fences."""
    out, fence = [], None
    for line in markdown.split("\n"):
        stripped = line.lstrip()
        if stripped.startswith(("```", "~~~")):
            marker = stripped[:3]
            fence = None if fence == marker else (fence or marker)
        elif fence is None:
            m = re.match(r"^(#{1,6})(\s)", line)
            if m:
                line = "#" * min(6, len(m.group(1)) + levels) + line[len(m.group(1)) :]
        out.append(line)
    return "\n".join(out)


def _dedupe(messages: List[Message]) -> List[Message]:
    """Drop repeats (the same message quoted twice) and empty fragments.

    The first message is always kept: it is the mail itself, even when it is a
    forward with no text of its own.
    """
    seen = set()
    result = []
    for i, m in enumerate(messages):
        key = re.sub(r"\W+", " ", m.body).strip().lower()
        if i > 0 and (not key and m.sender is None or key and key in seen):
            continue
        if key:
            seen.add(key)
        result.append(m)
    return result


def _unique(people: List[Address]) -> List[Address]:
    """One entry per person, preferring the version that has both name and address."""
    by_key: Dict[str, Address] = {}
    order: List[str] = []
    names_to_key: Dict[str, str] = {}
    for p in people:
        key = (p.email or "").lower() or names_to_key.get(p.name.lower()) or p.name.lower()
        if not key:
            continue
        if p.name and p.email:
            # An earlier bare-name entry for the same person merges into this one
            earlier = names_to_key.get(p.name.lower())
            if earlier and earlier != key and earlier in by_key:
                # Its slot takes the new key, unless the key is already listed (an address-only
                # entry came first): then the slot goes, or the person would be listed twice
                if key in order:
                    order.remove(earlier)
                else:
                    order[order.index(earlier)] = key
                del by_key[earlier]
            names_to_key[p.name.lower()] = key
        elif p.name:
            names_to_key.setdefault(p.name.lower(), key)
        if key not in by_key:
            by_key[key] = p
            if key not in order:
                order.append(key)
        elif p.name and not by_key[key].name:
            by_key[key] = Address(p.name, by_key[key].email)
    return [by_key[k] for k in order if k in by_key]
