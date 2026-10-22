"""Language rules: the phrase lists and patterns that drive thread splitting and cleanup.

The rules live in plain JSON files (``rules/<lang>.json``) rather than in code so that
people can add their own company disclaimer or a new language without touching Python,
and so that a future C# port inside Caret can read exactly the same files.

All languages are applied at once: a French reply to an English mail forwarded from
Poland is ordinary in a European company, so there is no language detection step.
"""

import json
import re
from dataclasses import dataclass, field
from importlib import resources
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Pattern, Union

BUILTIN_LANGUAGES = ("en", "fr", "es", "pl", "pt")

# Header fields we understand in a quoted header block, in the order they are shown.
HEADER_FIELDS = ("from", "sent", "to", "cc", "bcc", "subject", "other")


def fold(text: str) -> str:
    """Lowercase and unify the characters that differ between mail clients.

    Outlook writes typographic apostrophes and non-breaking spaces (French puts one
    before every colon), so phrase matching compares folded text on both sides.
    """
    return (
        text.replace("’", "'")
        .replace(" ", " ")
        .replace(" ", " ")
        .lower()
    )


@dataclass
class Rules:
    header_keys: Dict[str, str] = field(default_factory=dict)  # folded label -> field
    reply_markers: List[str] = field(default_factory=list)  # folded, dashes stripped
    forward_markers: List[str] = field(default_factory=list)
    wrote_patterns: List[Pattern[str]] = field(default_factory=list)
    subject_prefixes: List[str] = field(default_factory=list)
    mobile_signatures: List[str] = field(default_factory=list)  # folded prefixes
    closings: List[str] = field(default_factory=list)  # folded
    greetings: List[str] = field(default_factory=list)  # folded, may be several words
    titles: List[str] = field(default_factory=list)  # folded, without the dot
    not_names: List[str] = field(default_factory=list)  # folded: words that are not a person
    disclaimer_phrases: List[str] = field(default_factory=list)  # folded
    banner_phrases: List[str] = field(default_factory=list)  # folded
    months: Dict[str, int] = field(default_factory=dict)

    def merge(self, data: dict) -> None:
        for name, labels in data.get("header_keys", {}).items():
            if name not in HEADER_FIELDS:
                continue
            for label in labels:
                self.header_keys[fold(label)] = name
        self.reply_markers += [_marker(m) for m in data.get("reply_markers", [])]
        self.forward_markers += [_marker(m) for m in data.get("forward_markers", [])]
        self.wrote_patterns += [
            re.compile(p, re.IGNORECASE) for p in data.get("wrote_patterns", [])
        ]
        self.subject_prefixes += data.get("subject_prefixes", [])
        self.mobile_signatures += [fold(s) for s in data.get("mobile_signatures", [])]
        self.closings += [fold(s) for s in data.get("closings", [])]
        self.greetings += [fold(s) for s in data.get("greetings", [])]
        self.titles += [fold(s) for s in data.get("titles", [])]
        self.not_names += [fold(s) for s in data.get("not_names", [])]
        self.disclaimer_phrases += [fold(s) for s in data.get("disclaimer_phrases", [])]
        self.banner_phrases += [fold(s) for s in data.get("banner_phrases", [])]
        self.months.update({fold(k): v for k, v in data.get("months", {}).items()})

    def is_reply_marker(self, line: str) -> bool:
        return _marker(line) in self.reply_markers

    def is_forward_marker(self, line: str) -> bool:
        return _marker(line) in self.forward_markers


def _marker(text: str) -> str:
    # "-----Original Message-----", "---------- Forwarded message ---------" and
    # "Original Message" all compare equal.
    return fold(text).strip(" -_*\t")


def load_rules(
    languages: Iterable[str] = BUILTIN_LANGUAGES,
    extra: Optional[Iterable[Union[str, Path, dict]]] = None,
) -> Rules:
    """Load the built-in rule files, then any extra files or dicts on top of them."""
    rules = Rules()
    package = resources.files(__package__).joinpath("rules")
    for lang in languages:
        with package.joinpath(f"{lang}.json").open("r", encoding="utf-8") as f:
            rules.merge(json.load(f))
    for item in extra or []:
        if isinstance(item, dict):
            rules.merge(item)
        else:
            with open(item, "r", encoding="utf-8-sig") as f:
                rules.merge(json.load(f))
    return rules
