"""``caret-email``: convert email files or whole folders of them from the command line."""

import argparse
import sys
from pathlib import Path
from typing import Iterable, List, Optional

from markitdown import MarkItDown

from . import PRIORITY, __version__
from ._converter import EmailConverter

EXTENSIONS = {".msg", ".eml"}

NO_AI_NOTICE = (
    "No AI is used: everything is done with fixed rules on this computer. "
    "People are masked only by the names in the From/To/Cc lines and in --names; "
    "a name that appears only in the message text is not masked. "
    "Read the result before sharing it."
)


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        prog="caret-email",
        description="Convert Outlook .msg and .eml emails to clean Markdown threads. "
        "Runs entirely on this computer; nothing is uploaded.",
        epilog=NO_AI_NOTICE,
    )
    parser.add_argument("inputs", nargs="+", help="email files or folders")
    parser.add_argument("-o", "--output", help="folder for the Markdown files (default: next to each email)")
    parser.add_argument("--stdout", action="store_true", help="print the Markdown instead of saving files")
    parser.add_argument("--no-redact", action="store_true", help="keep email addresses, phone numbers, IDs and names")
    parser.add_argument("--names", help="text file with extra names to mask, one per line")
    parser.add_argument("--rules", action="append", default=[], help="extra rules file (JSON), e.g. your company disclaimer")
    parser.add_argument("--no-attachments", action="store_true", help="list attachments without converting them")
    parser.add_argument("--keep-signatures", action="store_true", help="leave signatures in place")
    parser.add_argument("-r", "--recursive", action="store_true", help="include subfolders")
    parser.add_argument("--version", action="version", version=f"%(prog)s {__version__}")
    args = parser.parse_args(argv)
    if args.stdout and hasattr(sys.stdout, "reconfigure"):
        # Redirected output on Windows defaults to the ANSI code page, which can't
        # hold Polish letters; the saved .md files are UTF-8, so is this
        sys.stdout.reconfigure(encoding="utf-8")

    files = list(_collect(args.inputs, args.recursive))
    if not files:
        print("No .msg or .eml files found.", file=sys.stderr)
        return 1

    if not args.no_redact:
        print(NO_AI_NOTICE, file=sys.stderr)

    md = MarkItDown(enable_plugins=False)
    md.register_converter(EmailConverter(md), priority=PRIORITY)
    options = dict(
        email_redact=not args.no_redact,
        email_attachments=not args.no_attachments,
        email_keep_signatures=args.keep_signatures,
    )
    if args.names:
        options["email_names"] = args.names
    if args.rules:
        options["email_rules"] = args.rules

    out_dir = Path(args.output) if args.output else None
    if out_dir:
        out_dir.mkdir(parents=True, exist_ok=True)

    failures = 0
    for path in files:
        try:
            result = md.convert(str(path), **options)
        except Exception as e:
            print(f"{path}: {e}", file=sys.stderr)
            failures += 1
            continue
        if args.stdout:
            sys.stdout.write(result.markdown)
            if len(files) > 1:
                sys.stdout.write("\n")
            continue
        target = _free_path((out_dir or path.parent) / (path.stem + ".md"))
        target.write_text(result.markdown, encoding="utf-8")
        print(f"{path} -> {target}", file=sys.stderr)
    return 1 if failures else 0


def _collect(inputs: Iterable[str], recursive: bool) -> Iterable[Path]:
    for item in inputs:
        path = Path(item)
        if path.is_dir():
            pattern = "**/*" if recursive else "*"
            yield from sorted(p for p in path.glob(pattern) if p.suffix.lower() in EXTENSIONS and p.is_file())
        elif path.is_file():
            yield path
        else:
            print(f"{item}: not found", file=sys.stderr)


def _free_path(path: Path) -> Path:
    """Never overwrite: 'mail.md', then 'mail (2).md', 'mail (3).md', like Caret does."""
    if not path.exists():
        return path
    n = 2
    while True:
        candidate = path.with_name(f"{path.stem} ({n}){path.suffix}")
        if not candidate.exists():
            return candidate
        n += 1


if __name__ == "__main__":
    sys.exit(main())
