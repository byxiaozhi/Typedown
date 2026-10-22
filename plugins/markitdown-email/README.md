# markitdown-caret-email

A [MarkItDown](https://github.com/microsoft/markitdown) plugin that turns Outlook `.msg` and `.eml` emails into **one clean Markdown thread**: every message in order, without the repeated quotes, signatures, legal disclaimers and "external sender" banners, and with personal data masked before you paste it into Copilot or any other assistant.

It runs entirely on your computer and uses **no AI**: only fixed rules, so the same email always gives the same result and nothing is uploaded anywhere.

> [!IMPORTANT]
> **No AI, and what that means for masking.** Because nothing here understands language, a person is masked only when the tool knows their name: from the From, To and Cc lines of the thread, from a greeting ("Hi Daniel,") or a sign-off ("Kind regards," then "Anna Nowak"), or from a names list you give it (`--names`). Someone mentioned only inside a sentence ("ask Marta from finance") stays visible, and so does a lowercase mention of a single first or last name. Signature and disclaimer removal follow phrase lists, so an unusual company disclaimer may stay until you add it to your rules. **Read the result before you share anything sensitive.** Details under [Redaction](#redaction).

Part of [Caret](../../README.md). Caret has the same conversion built in (**Outlook emails** in the sidebar, no Python needed): a C# port in `Dev/Typedown.WinUI/Services/Conversion/Email` that reads these same JSON rule files, so a rule added here applies to both.

## What it does

| Step | How |
| --- | --- |
| **Reads the mail** | `.msg` (drag a mail out of Outlook into a folder) and `.eml` (Outlook on the web, Thunderbird, Gmail, Apple Mail). HTML-only mail is converted to text. |
| **Splits the thread** | A reply contains the whole conversation below it. Each quoted message becomes its own section, oldest first, with sender, date and recipients. Both Outlook header blocks (`From: / Sent: / To: / Subject:`) and `On … wrote:` quotes are recognised. |
| **Removes clutter** | Signatures after a closing ("Best regards", "Cordialement", "Un saludo", "Pozdrawiam"), `-- ` signatures, "Sent from my iPhone", confidentiality disclaimers, "you don't often get email from" and external-sender banners, and `RE: / TR: / RV: / Odp: / [EXTERNAL]` prefixes on the subject. |
| **Converts attachments** | Word, Excel, PowerPoint, PDF, CSV... through MarkItDown, inline under **Attachments**. An email attached to an email is rendered as a thread too. Signature logos are skipped. |
| **Masks personal data** | On by default. Email addresses, phone numbers, IBANs, card numbers, national IDs, and the names of everyone in the thread, each replaced by a numbered placeholder. |

Languages: **English, French, Spanish, Polish and Brazilian Portuguese**, all applied together, so a French reply to a Polish mail forwarded from Spain works.

### Example

```markdown
---
type: "email-thread"
subject: "Budget 2026"
started: "2025-02-28 11:00"
last: "2025-03-04 09:30"
messages: 3
participants: ["[PERSON-1] <[EMAIL-1]>", "[PERSON-2] <[EMAIL-2]>", "[PERSON-3] <[EMAIL-3]>"]
attachments: ["budget.csv"]
redacted: true
generator: "markitdown-caret-email"
---

# Budget 2026

## 1. [PERSON-1] <[EMAIL-1]> · 2025-02-28 11:00

*To: [PERSON-2] <[EMAIL-2]>*

Hola [PERSON-2],

Te adjunto el presupuesto. Mi DNI es [ID-1].

Un saludo,
[PERSON-1]

## 2. [PERSON-2] <[EMAIL-2]> · 2025-03-03 16:05
...
```

The front matter is the same shape for every email, so a folder of converted mail can be searched, sorted and summarised as a knowledge base.

## Install

Needs Python 3.10 or later. From this folder:

```powershell
pip install .
# to also convert Word, Excel, PowerPoint and PDF attachments:
pip install ".[attachments]"
```

The only dependencies are `markitdown` and `olefile` (which MarkItDown itself uses for `.msg`).

## Use

### From the command line

```powershell
caret-email mail.msg                    # writes mail.md next to it
caret-email "C:\Mail\Project X" -o notes # a whole folder, into .\notes
caret-email mail.msg --stdout           # print instead of saving
```

Existing files are never overwritten: the second conversion is saved as `mail (2).md`, like Caret does.

| Option | |
| --- | --- |
| `-o`, `--output FOLDER` | Where to save the Markdown (default: next to each email) |
| `-r`, `--recursive` | Include subfolders |
| `--stdout` | Print instead of saving |
| `--no-redact` | Keep addresses, phone numbers, IDs and names |
| `--names FILE` | Extra names to mask, one per line (`#` starts a comment): customers, colleagues mentioned only in the text |
| `--rules FILE` | Extra rules, e.g. your company's disclaimer (see below). Can be repeated |
| `--no-attachments` | List attachments without converting them |
| `--keep-signatures` | Leave signatures in place |

### With MarkItDown

```powershell
markitdown --use-plugins mail.msg
```

The plugin takes over `.msg` and `.eml` from MarkItDown's basic Outlook converter. Since the `markitdown` command can't pass options to plugins, use environment variables:

| Variable | Meaning |
| --- | --- |
| `CARET_EMAIL_REDACT=0` | Don't mask personal data |
| `CARET_EMAIL_NAMES=C:\path\names.txt` | Extra names to mask |
| `CARET_EMAIL_RULES=C:\path\rules.json` | Extra rules (several separated by `;`) |
| `CARET_EMAIL_ATTACHMENTS=0` | Don't convert attachments |
| `CARET_EMAIL_KEEP_SIGNATURES=1` | Keep signatures |

### From Python

```python
from markitdown import MarkItDown

md = MarkItDown(enable_plugins=True)
result = md.convert("mail.msg", email_redact=True, email_names=["ACME Corp", "Marie Curie"])
print(result.markdown)
```

## Redaction

| Kind | Placeholder | Recognised |
| --- | --- | --- |
| Email addresses | `[EMAIL-n]` | any |
| Phone numbers | `[PHONE-n]` | international (`+33…`, `0048…`, `+57…`, `+54…`, `+56…`, `+52…`, `+51…`, `+504…`, `+55…`), French, Spanish, Polish, UK and Latin American national formats (Colombia `300 123 4567`, Argentina `011 4123-4567`, Chile `9 1234 5678`, Mexico `55 1234 5678`, Brazil `(11) 91234-5678`), and anything after *Tel*, *Tél*, *Mobile*, *Móvil*, *Cel*, *Celular*, *WhatsApp*, *Zap*, *Fone*, *Contato*, *Fono*, *Kom.* |
| Bank accounts | `[IBAN-n]` | IBANs, checked with their check digits |
| Card numbers | `[CARD-n]` | 13–19 digits passing the Luhn check |
| National IDs | `[ID-n]` | French NIR, Spanish DNI and NIE, Polish PESEL, Chilean RUT, Argentine CUIT/CUIL, Colombian NIT, Mexican CURP and RFC, Brazilian CPF and CNPJ (all checksum-verified), UK National Insurance numbers, Honduran identity number and RTN (by their shape), and a number after its label where the country has no check digit (*DNI*, *Cédula de ciudadanía*, *C.C.*, *CE*, *RUC*, *RG*, *CNH*, *PIS*, *Título de eleitor*, *Pasaporte*) |
| People | `[PERSON-n]` | everyone in the From/To/Cc lines of any message in the thread, anyone greeted ("Hi Daniel and Emma,") or signing off, plus your `--names` list, in the forms "Anna Nowak", "Nowak, Anna", "Nowak Anna", and first or last name alone |

The same value gets the same placeholder throughout the file, so the conversation still makes sense.

**Limits, stated plainly.** Without a language model, a person is found only by a name the tool has been given or can read from a fixed place: the headers, a capitalised name in a greeting, or the name under a closing. Greetings and closings come from the rule files, so other languages and phrasings can be added. Someone mentioned only inside a sentence ("ask Marta from finance") and not in any of those places or in your names list is **not** masked. Single first or last names are matched only when capitalised, so the name "Will" doesn't swallow the verb "will", but a lowercase mention is missed. Read the result before you share anything sensitive.

## Adding your own rules

The built-in rules are in [`src/markitdown_caret_email/rules`](src/markitdown_caret_email/rules), one file per language. To add your company's disclaimer, a closing your team uses, or the headers of another language, create a file with only what you want to add and pass it with `--rules`:

```json
{
  "disclaimer_phrases": ["the information in this email is the property of acme"],
  "closings": ["see you"],
  "banner_phrases": ["[acme external]"]
}
```

Phrases are matched without regard to case, and typographic apostrophes (`’`) match plain ones. Disclaimers are only removed at the end of a message, so a sentence in the message itself that mentions confidentiality is kept.

| Key | What it is |
| --- | --- |
| `header_keys` | Labels of quoted header lines, by field: `from`, `sent`, `to`, `cc`, `bcc`, `subject`, `other` |
| `reply_markers`, `forward_markers` | Lines like `-----Original Message-----` |
| `wrote_patterns` | Regular expressions for `On … wrote:` lines |
| `subject_prefixes` | `RE`, `TR`, `[EXTERNAL]`... |
| `mobile_signatures` | Line starts like "Sent from my " |
| `closings` | Whole lines that end a message before the signature |
| `greetings`, `titles`, `not_names` | Words that open a message ("Hi", "Dear", "Bonjour"), titles that come before a name ("Mr", "Mme"), and words that look like a name but aren't one ("all", "team", "à tous"), for finding the people greeted in a message |
| `disclaimer_phrases`, `banner_phrases` | Text that marks a disclaimer paragraph or a warning banner |
| `months` | Month names and abbreviations, for reading dates |

## How signatures are removed

Carefully, since cutting real text is worse than leaving a signature in. The tool looks for a closing line near the end ("Best regards", "Bien cordialement", "Saludos", "Z poważaniem"...) followed by a line that looks like a name, and removes what comes after the name only if it looks like a signature block: at most 15 short lines, none of them a question. Otherwise the message is left as it is.

## Development

```powershell
pip install -e ".[attachments]" pytest
pytest
```

## Privacy

Everything happens on your computer. The plugin makes no network connections and writes nothing except the Markdown files you ask for.
