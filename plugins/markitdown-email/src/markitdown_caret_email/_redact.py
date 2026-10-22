"""Masking personal data before the text is pasted into an assistant.

Everything is pattern matching with checksums where the format has one (IBAN, card
numbers, PESEL, DNI/NIE, French NIR, and in Latin America the Chilean RUT, the Argentine CUIT/CUIL,
the Colombian NIT, the Mexican CURP and RFC, the Brazilian CPF and CNPJ), so a random order number is not mistaken for an
ID. People are masked by name when the name is known: from the mail's own headers,
from a greeting ("Hi Daniel,") or a sign-off ("Kind regards,\nAnna Nowak"), plus any
list the user supplies. A name that appears only in running text ("ask Marta from
finance") is not found; that would take a language model, which this tool
deliberately does not use.

The same value always gets the same placeholder within one document ("[PERSON-2]" is
the same person everywhere), so the thread stays readable.
"""

import re
from typing import Dict, Iterable, List, Tuple

from ._model import Address, flip_name
from ._rules import Rules
from ._thread import signature_names

EMAIL = re.compile(r"(?<![\w.+\-])[\w.%+\-']+@[\w\-]+(?:\.[\w\-]+)*\.[A-Za-z]{2,}(?![\w\-])")
IBAN = re.compile(r"\b[A-Z]{2}\d{2}(?:[ ]?[A-Z0-9]{4}){2,7}(?:[ ]?[A-Z0-9]{1,4})?\b")
CARD = re.compile(r"(?<![\w\-])\d(?:[ \-]?\d){12,18}(?![\w\-])")
PESEL = re.compile(r"(?<![\w\-])\d{11}(?![\w\-])")
NIR = re.compile(
    r"(?<![\w\-])[12][ ]?\d{2}[ ]?(?:0[1-9]|1[0-2]|[2-9]\d)[ ]?(?:\d{2}|2A|2B)[ ]?\d{3}[ ]?\d{3}[ ]?\d{2}(?![\w\-])"
)
DNI = re.compile(r"(?<![\w\-])\d{8}[ \-]?[A-Z](?![\w\-])")
NIE = re.compile(r"(?<![\w\-])[XYZ][ \-]?\d{7}[ \-]?[A-Z](?![\w\-])")
NINO = re.compile(
    r"(?<![\w\-])(?!BG|GB|NK|KN|TN|NT|ZZ)[A-CEGHJ-PR-TW-Z][A-CEGHJ-NPR-TW-Z] ?\d{2} ?\d{2} ?\d{2} ?[A-D](?![\w\-])"
)
PHONE_INTERNATIONAL = re.compile(r"(?<![\w+])(?:\+|00)\d{1,3}(?:[ .\-]?\(?\d{1,4}\)?){2,6}(?![\w])")
# "100 000 000 EUR" is an amount written with thousands separators, not a phone number
_NOT_AMOUNT = r"(?![ \u00a0]?(?:€|EUR|PLN|zł|USD|\$|£|GBP|CHF|[.,]\d))"
PHONE_NATIONAL = [
    re.compile(r"(?<![\w+\-./])0[1-9](?:[ .\-]?\d{2}){4}(?![\w\-])"),  # FR 01 23 45 67 89
    re.compile(r"(?<![\w+\-./])0\d{2}[ ]?\d{4}[ ]?\d{4}(?![\w\-])"),  # UK 020 7946 0958
    re.compile(r"(?<![\w+\-./])0\d{3,4}[ ]?\d{3}[ ]?\d{3,4}(?![\w\-])"),  # UK 07700 900123, 0161 496 0000
    re.compile(r"(?<![\w+\-./])\d{3}[ \-]\d{3}[ \-]\d{3}(?![\w\-])" + _NOT_AMOUNT),  # PL/ES 612 345 678
    re.compile(r"(?<![\w+\-./])[6-9]\d{2}[ ]\d{2}[ ]\d{2}[ ]\d{2}(?![\w\-])"),  # ES 912 34 56 78
    re.compile(r"(?<![\w+\-./(])\(?\d{2,3}\)?[ \-]\d{3,4}[ \-]\d{4}(?![\w\-])" + _NOT_AMOUNT),  # CO 300 123 4567, AR 011 4123-4567, MX 55 1234 5678
    re.compile(r"(?<![\w+\-./])9[ ]\d{4}[ ]\d{4}(?![\w\-])"),  # CL 9 1234 5678
    re.compile(r"(?<![\w+\-./(])\(?\d{2}\)?[ ]?9\d{4}[ \-]\d{4}(?![\w\-])"),  # BR (11) 91234-5678, 11 91234 5678
]
# Anything after a phone label, whatever its format
PHONE_LABELLED = re.compile(
    r"(?i)\b(?:tel|tél|phone|mobile|mob|cell|móvil|movil|teléfono|telefono|telf|tlf|cel|celular|whatsapp|whats|wsp|zap|fone|contato|fono|fijo|kom|komórka|tel\. kom)"
    r"\.?\s*[:.]?\s*(\+?\d[\d ().\-/]{6,20}\d)"
)

# Latin America. A number with a check digit is found on its own; one without (the Colombian cédula, the DNI of Argentina
# and Peru, the Peruvian RUC) only after its label, so that a random number is not taken for an ID.
RUT = re.compile(r"(?<![\w\-.])(?:\d{1,2}(?:\.\d{3}){2}|\d{7,8})-[\dKk](?![\w\-])")  # Chile 12.345.678-5
CUIT = re.compile(r"(?<![\w\-])(?:20|23|24|27|30|33|34)-\d{8}-\d(?![\w\-])")  # Argentina 20-12345678-6
NIT = re.compile(r"(?<![\w\-.])(?:\d{1,3}(?:\.\d{3}){2,3}|\d{6,10})-\d(?![\w\-])")  # Colombia 800.197.268-4
CURP = re.compile(
    r"(?<![\w\-])[A-Z][AEIOUX][A-Z]{2}\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])[HMX]"
    r"(?:AS|BC|BS|CC|CL|CM|CS|CH|DF|DG|GT|GR|HG|JC|MC|MN|MS|NT|NL|OC|PL|QT|QR|SP|SL|SR|TC|TS|TL|VZ|YN|ZS|NE)"
    r"[B-DF-HJ-NP-TV-Z]{3}[A-Z\d]\d(?![\w\-])"
)  # Mexico HEGG560427MVZRRL04
RFC = re.compile(
    r"(?<![\w\-&])[A-ZÑ&]{3,4}\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])[A-Z\d]{2}[A\d](?![\w\-&])"
)  # Mexico GODE561231GR8
HONDURAS_ID = re.compile(r"(?<![\w\-])(?:0[1-9]|1[0-8])\d{2}-(?:19|20)\d{2}-\d{5,6}(?![\w\-])")  # 0801-1990-12345, RTN 0801-1990-123456
CPF = re.compile(r"(?<![\w\-./])\d{3}\.\d{3}\.\d{3}-\d{2}(?![\w\-])")  # Brazil 529.982.247-25 (without its points it is found after its label)
CNPJ = re.compile(r"(?<![\w\-./])\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}(?![\w\-])")  # Brazil 11.222.333/0001-81
_ID_NUMBER = r"(\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}|\d{1,3}(?:[. ]\d{3}){1,3}(?:-[\dKkXx]{1,2})?|\d{6,14}(?:-[\dKkXx]{1,2})?)(?!\w)"
ID_LABELLED = re.compile(
    r"(?<!\w)(?:(?-i:DNI|D\.N\.I\.?|CC|C\.C\.?|CE|C\.E\.?|NUIP|CUIT|CUIL|RUC|NIT|RUT|RFC|CURP|CPF|CNPJ|RG|CNH|PIS|PASEP|NIS)"
    r"|(?i:c[eé]dula(?: de (?:ciudadan[ií]a|extranjer[ií]a|identidad))?|documento(?: (?:nacional )?de identidad)?|pasaporte|passaporte|carteira de (?:identidade|motorista|habilita[cç][aã]o)|t[ií]tulo de eleitor))"
    r"(?!\w)\.?\s*(?i:n[°º]|no\.?|num\.?|n[uú]mero|#)?\s*[:.\-]?\s*" + _ID_NUMBER
)

_DNI_LETTERS = "TRWAGMYFPDXBNJZSQVHLCKE"
# Mail from these addresses is from a system or a team, and its display name is a
# product or company ("Microsoft Azure"): mask the full name, not each of its words.
_ROLE_ADDRESS = re.compile(
    r"(?i)^(?:[\w.\-]*[.\-])?(?:no-?reply|do-?not-?reply|mailer-daemon|postmaster|notifications?|"
    r"newsletters?|news|info|support|alerts?|marketing|hello|team|contact|service|admin|bounce)"
    r"(?:[.\-][\w.\-]*)?@"
)
_NAME_TOKEN = re.compile(r"^[^\W\d_][\w'’\-]*$")
_GREETED_WORD = re.compile(r"[^\W\d_][\w'’\-]+")
_CONNECTORS = {"&", "and", "et", "y", "e", "i", "oraz"}
# A name isn't part of a longer word or a hyphenated name, and neither is it a segment of an identifier
# ("user_Anna_id"), but Markdown emphasis ("_Emma_") is not a word
_EDGE_BEFORE = r"(?<![^\W_])(?<!-)(?<![^\W_]_)"
_EDGE_AFTER = r"(?![^\W_])(?!-)(?!_[^\W_])"


def _greeted_names(who: str) -> List[str]:
    """The names in "Daniel", "Anna-Maria", "John Smith" or "Daniel and Emma"; [] for anything else.

    A name is one to three capitalised words, in any alphabet ("Ольга", "Νίκος")."""
    names: List[str] = []
    words: List[str] = []
    for token in who.split():
        if token in _CONNECTORS:
            if not words:
                return []
            names.append(" ".join(words))
            words = []
        elif token[0].isupper() and _GREETED_WORD.fullmatch(token):
            words.append(token)
            if len(words) > 3:
                return []
        else:
            return []
    if not words:
        return []
    return names + [" ".join(words)]


class Redactor:
    def __init__(self, people: Iterable[Address] = (), names: Iterable[str] = ()):
        self._numbers: Dict[str, Dict[str, int]] = {}
        self._people: List[Tuple[str, str]] = []  # (canonical key, variant) pairs
        for person in people:
            self.add_person(person.name, person.email)
        for name in names:
            self.add_person(name.strip(), "")

    def add_person(self, name: str, email: str = "") -> None:
        name = flip_name(name.strip())
        if not name and not email:
            return
        key = email.lower() or name.lower()
        # Someone known by email may also have been listed by name; keep one placeholder
        for existing_key, variant in self._people:
            if name and variant.lower() == name.lower():
                key = existing_key
                break
        role = bool(email) and bool(_ROLE_ADDRESS.match(email))
        for variant in _variants(name, whole_only=role):
            self._people.append((key, variant))

    def learn_names(self, text: str, rules: Rules) -> None:
        """Add the people who are greeted ("Hi Daniel and Emma,") or who sign a message.

        Only a name in one of those two places is found, and only when it is written with
        capitals; that is enough to catch the recipients and senders a mail's headers
        leave out, without guessing at names inside sentences.
        """
        names: List[str] = []
        if rules.greetings:
            greetings = "|".join(re.escape(g).replace(r"\ ", r"[ \t]+") for g in sorted(rules.greetings, key=len, reverse=True))
            titles = "|".join(re.escape(t) for t in sorted(rules.titles, key=len, reverse=True))
            title_part = rf"(?:[*_]*(?i:{titles})\.?[ \t]+)*" if titles else ""
            line = re.compile(rf"^[ \t>*_]*(?i:{greetings})[ \t]+{title_part}(?P<who>[^\n,:;!]*?)[ \t*_]*(?:[,:;!]|$)", re.MULTILINE)
            for m in line.finditer(text):
                # Markdown emphasis around a name ("Hi **Sofia**,") isn't part of it
                who = re.sub(r"[*_]+", "", m.group("who")).strip()
                names += _greeted_names(who)
        names += signature_names(text, rules)
        skip = set(rules.not_names) | set(rules.titles)
        for name in names:
            if any(w.strip(".").lower() in skip for w in name.split()):
                continue
            self.add_person(name)

    def placeholder(self, kind: str, value: str) -> str:
        table = self._numbers.setdefault(kind, {})
        if value not in table:
            table[value] = len(table) + 1
        return f"[{kind}-{table[value]}]"

    def redact(self, text: str) -> str:
        # Find everything first, then number by position, so [ID-1] is the first ID
        # in the text whichever pattern found it. Earlier patterns win overlaps.
        spans: List[Tuple[int, int, str, str]] = []
        for pattern, kind, normalise, valid, group in _PATTERNS:
            for m in pattern.finditer(text):
                value = m.group(group)
                if valid is not None and not valid(value):
                    continue
                start, end = m.span(group)
                if any(start < e and s < end for s, e, _, _ in spans):
                    continue
                spans.append((start, end, kind, normalise(value)))
        out, last = [], 0
        for start, end, kind, value in sorted(spans):
            out += [text[last:start], self.placeholder(kind, value)]
            last = end
        out.append(text[last:])
        return self._names("".join(out))

    def _names(self, text: str) -> str:
        if not self._people:
            return text
        variants = sorted(self._people, key=lambda kv: len(kv[1]), reverse=True)
        by_variant = {}
        for key, variant in variants:
            by_variant.setdefault(_lookup(variant), key)
        # Full names match in any case; single words ("Anna") only as capitalised words,
        # so the name "Will" does not swallow the verb "will".
        multi = [v for _, v in variants if " " in v or "," in v]
        single = [v for _, v in variants if " " not in v and "," not in v]
        people_keys: Dict[str, str] = {}

        def replace(m: "re.Match[str]") -> str:
            key = by_variant.get(_lookup(m.group(0)))
            if key is None:
                return m.group(0)
            if key not in people_keys:
                people_keys[key] = self.placeholder("PERSON", key)
            return people_keys[key]

        if multi:
            pattern = _EDGE_BEFORE + "(?:" + "|".join(_flexible(v) for v in multi) + ")" + _EDGE_AFTER
            text = re.sub(pattern, replace, text, flags=re.IGNORECASE)
        if single:
            pattern = _EDGE_BEFORE + "(?:" + "|".join(re.escape(v) for v in single) + ")" + _EDGE_AFTER
            text = re.sub(pattern, replace, text)
        return text


def _lookup(text: str) -> str:
    return re.sub(r"[\s\-]+", " ", text).lower()


def _flexible(variant: str) -> str:
    # A name wrapped over two lines, written with a double space or with a hyphen between the
    # first names ("Anna-Maria" in a signature, "Anna Maria" in the address book) is still the name
    return r"[\s\-]+".join(re.escape(part) for part in variant.split(" "))


def _variants(name: str, whole_only: bool = False) -> List[str]:
    if not name or "@" in name:
        return []
    if whole_only:
        return [name]
    words = [w for w in name.split() if _NAME_TOKEN.match(w)]
    result = [name]
    if len(words) >= 2:
        result += [f"{words[-1]}, {' '.join(words[:-1])}", f"{words[-1]} {' '.join(words[:-1])}"]
        if len(words) >= 3:
            result.append(" ".join(words[:-1]))  # "Anna Maria", also written "Anna-Maria"
        for w in words:
            if len(w) >= 3:
                result.append(w)
                # Company directories write surnames in capitals ("Anna NOWAK"); the text doesn't
                if not any(c.islower() for c in w):
                    result.append(w[0] + w[1:].lower())
    return result


def _digits(value: str) -> str:
    return re.sub(r"\D", "", value)


def _compact(value: str) -> str:
    return re.sub(r"[\s\-]", "", value).upper()


def _valid_iban(value: str) -> bool:
    iban = _compact(value)
    if not 15 <= len(iban) <= 34:
        return False
    rearranged = iban[4:] + iban[:4]
    number = "".join(str(int(c, 36)) for c in rearranged)
    return int(number) % 97 == 1


def _valid_card(value: str) -> bool:
    digits = _digits(value)
    if not 13 <= len(digits) <= 19:
        return False
    total = 0
    for i, d in enumerate(reversed(digits)):
        n = int(d)
        if i % 2 == 1:
            n = n * 2 - 9 if n > 4 else n * 2
        total += n
    return total % 10 == 0


def _valid_pesel(value: str) -> bool:
    d = [int(c) for c in _digits(value)]
    weights = (1, 3, 7, 9, 1, 3, 7, 9, 1, 3)
    return (10 - sum(a * b for a, b in zip(d, weights)) % 10) % 10 == d[10]


def _valid_nir(value: str) -> bool:
    v = _compact(value)
    body, key = v[:13], v[13:]
    body = body.replace("2A", "19").replace("2B", "18")
    return body.isdigit() and key.isdigit() and 97 - int(body) % 97 == int(key)


def _valid_dni(value: str) -> bool:
    v = _compact(value)
    return _DNI_LETTERS[int(v[:8]) % 23] == v[8]


def _valid_nie(value: str) -> bool:
    v = _compact(value)
    number = "XYZ".index(v[0]).__str__() + v[1:8]
    return _DNI_LETTERS[int(number) % 23] == v[8]


def _id_key(value: str) -> str:
    return re.sub(r"[\s.\-/]", "", value).upper()


def _valid_rut(value: str) -> bool:
    """Chile: the digits from the right times 2, 3, 4, 5, 6, 7, 2, 3...; 11 minus the sum mod 11 (10 is K, 11 is 0)."""
    key = _id_key(value)
    body, dv = key[:-1], key[-1]
    if not 7 <= len(body) <= 8 or not body.isdigit():
        return False
    r = 11 - sum(int(c) * (2 + i % 6) for i, c in enumerate(reversed(body))) % 11
    return dv == ("0" if r == 11 else "K" if r == 10 else str(r))


_CUIT_WEIGHTS = (5, 4, 3, 2, 7, 6, 5, 4, 3, 2)


def _cuit_remainder(first_ten: str) -> int:
    return 11 - sum(int(a) * b for a, b in zip(first_ten, _CUIT_WEIGHTS)) % 11


def _valid_cuit(value: str) -> bool:
    """Argentina (CUIT and CUIL): weights 5 4 3 2 7 6 5 4 3 2; 11 minus the sum mod 11 (11 is 0)."""
    key = _id_key(value)
    if len(key) != 11 or not key.isdigit():
        return False
    r = _cuit_remainder(key[:10])
    if key[10] == str(0 if r == 11 else r):
        return True
    # When the digit would have been 10, 23 or 24 stands in for 20 or 27, with 9 (men) or 4 (women) as the digit
    if key[:2] in ("23", "24") and key[10] in ("9", "4"):
        return any(_cuit_remainder(base + key[2:10]) == 10 for base in ("20", "27"))
    return False


_NIT_WEIGHTS = (3, 7, 13, 17, 19, 23, 29, 37, 41, 43)


def _valid_nit(value: str) -> bool:
    """Colombia (DIAN): weights 3 7 13 17 19 23 29 37 41 43 from the right; the sum mod 11 if that is 0 or 1, else 11 minus it."""
    key = _id_key(value)
    body, dv = key[:-1], key[-1]
    if not key.isdigit() or not 6 <= len(body) <= 10:
        return False
    r = sum(int(c) * w for c, w in zip(reversed(body), _NIT_WEIGHTS)) % 11
    return dv == str(r if r < 2 else 11 - r)


_CURP_CHARS = "0123456789ABCDEFGHIJKLMNÑOPQRSTUVWXYZ"


def _valid_curp(value: str) -> bool:
    """Mexico: the first 17 characters by their place in 0-9 A-N Ñ O-Z, times 18 down to 2; 10 minus the sum mod 10."""
    key = _id_key(value)
    if len(key) != 18 or any(c not in _CURP_CHARS for c in key):
        return False
    total = sum(_CURP_CHARS.index(c) * (18 - i) for i, c in enumerate(key[:17]))
    return key[17] == str((10 - total % 10) % 10)


_RFC_CHARS = "0123456789ABCDEFGHIJKLMN&OPQRSTUVWXYZ Ñ"


def _valid_rfc(value: str) -> bool:
    """Mexico (SAT): a company has 12 characters and gets a space in front; each by its place in 0-9 A-N & O-Z space Ñ,
    times 13 down to 2; 11 minus the sum mod 11 (10 is A, 11 is 0)."""
    key = _id_key(value)
    body = key[:-1] if len(key) == 13 else " " + key[:-1] if len(key) == 12 else ""
    if not body or any(c not in _RFC_CHARS for c in body + key[-1]):
        return False
    r = 11 - sum(_RFC_CHARS.index(c) * (13 - i) for i, c in enumerate(body)) % 11
    return key[-1] == ("0" if r == 11 else "A" if r == 10 else str(r))

def _valid_cpf(value: str) -> bool:
    """Brazil: two check digits, each the sum of the digits times 10 (then 11) down to 2, times 10, mod 11, mod 10. All digits the same is no CPF."""
    d = [int(c) for c in re.sub(r"\D", "", value)]
    if len(d) != 11 or len(set(d)) == 1:
        return False
    for n in (9, 10):
        total = sum(d[i] * (n + 1 - i) for i in range(n))
        if (total * 10 % 11) % 10 != d[n]:
            return False
    return True


_CNPJ_WEIGHTS = (5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2)


def _valid_cnpj(value: str) -> bool:
    """Brazil: two check digits, weights 5 4 3 2 9 8 7 6 5 4 3 2 (and 6 in front for the second); 0 when the sum mod 11 is below 2, else 11 minus it."""
    d = [int(c) for c in re.sub(r"\D", "", value)]
    if len(d) != 14 or len(set(d)) == 1:
        return False
    for n, weights in ((12, _CNPJ_WEIGHTS), (13, (6,) + _CNPJ_WEIGHTS)):
        r = sum(a * b for a, b in zip(d[:n], weights)) % 11
        if (0 if r < 2 else 11 - r) != d[n]:
            return False
    return True


_PATTERNS = [
    # (pattern, placeholder kind, how to compare values, validity check, match group)
    (EMAIL, "EMAIL", str.lower, None, 0),
    (IBAN, "IBAN", _compact, _valid_iban, 0),
    # National IDs before card numbers: a 15-digit French NIR can pass the Luhn check
    (NIR, "ID", _compact, _valid_nir, 0),
    (PESEL, "ID", _digits, _valid_pesel, 0),
    (NIE, "ID", _compact, _valid_nie, 0),
    (DNI, "ID", _compact, _valid_dni, 0),
    (NINO, "ID", _compact, None, 0),
    (RUT, "ID", _id_key, _valid_rut, 0),
    (CUIT, "ID", _id_key, _valid_cuit, 0),
    (NIT, "ID", _id_key, _valid_nit, 0),
    (CURP, "ID", _id_key, _valid_curp, 0),
    (RFC, "ID", _id_key, _valid_rfc, 0),
    (HONDURAS_ID, "ID", _id_key, None, 0),
    (CNPJ, "ID", _id_key, _valid_cnpj, 0),
    (CPF, "ID", _id_key, _valid_cpf, 0),
    # Numbers with no check digit, only after their label ("DNI 12.345.678", "Cédula de ciudadanía No. 1.234.567.890")
    (ID_LABELLED, "ID", _id_key, lambda v: 6 <= len(_digits(v)) <= 14, 1),
    (CARD, "CARD", _digits, _valid_card, 0),
    (PHONE_LABELLED, "PHONE", _digits, None, 1),
    (PHONE_INTERNATIONAL, "PHONE", _digits, lambda v: 8 <= len(_digits(v)) <= 15, 0),
] + [(p, "PHONE", _digits, None, 0) for p in PHONE_NATIONAL]
