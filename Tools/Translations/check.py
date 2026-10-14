#!/usr/bin/env python3
"""Reports how complete each translation is, against en.

    python3 Tools/Translations/check.py            # summary, one line per language
    python3 Tools/Translations/check.py de ja      # the missing keys of those languages
    python3 Tools/Translations/check.py --missing  # every language's missing keys
    python3 Tools/Translations/check.py --strict   # exit 1 if any language has a gap (CI)

Strings live in Dev/Typedown.Core/Resources/Strings/<lang>/<file>.resw. A key that a
language does not have falls back to the project's default language at runtime, so a gap
shows up as a mixed-language interface rather than an error.
"""
import os, re, sys

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Dev', 'Typedown.Core', 'Resources', 'Strings')
FILES = ['CommonResources.resw', 'DialogResources.resw', 'SettingsResources.resw', 'Resources.resw']

def strip_comments(text):
    """The .resw template carries sample entries (Name1, Bitmap1, Icon1) inside a comment."""
    return re.sub(r'<!--.*?-->', '', text, flags=re.S)



def entries(lang, name):
    path = os.path.join(ROOT, lang, name)
    if not os.path.exists(path):
        return {}
    text = strip_comments(open(path, encoding='utf-8').read())
    return {m.group(1): m.group(2) for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', text, re.S)}


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('-')]
    show_missing = '--missing' in sys.argv or bool(args)
    reference = {f: entries('en', f) for f in FILES}
    total = sum(len(v) for v in reference.values())
    langs = sorted(d for d in os.listdir(ROOT) if os.path.isdir(os.path.join(ROOT, d)))
    if args:
        langs = [l for l in langs if l in args]
    rows = []
    for lang in langs:
        gaps = {f: [k for k in reference[f] if k not in entries(lang, f)] for f in FILES}
        have = total - sum(len(v) for v in gaps.values())
        rows.append((have / total, lang, have, gaps))
    for pct, lang, have, gaps in sorted(rows, reverse=True):
        print(f"{lang:10} {have:4}/{total}  {pct*100:5.1f}%")
        if show_missing:
            for f, keys in gaps.items():
                for key in keys:
                    print(f"    {f.replace('Resources.resw',''):9} {key} = {reference[f][key]}")
    incomplete = [r for r in rows if r[0] < 1]
    print(f"\n{len(rows) - len(incomplete)}/{len(rows)} complete; {len(incomplete)} with gaps")
    if '--strict' in sys.argv and incomplete:
        # A gap is not an error at runtime — the key falls back to English — so nothing fails until someone
        # sees one English line in their own language. This is what makes it fail earlier.
        print("run with --missing to see them, and add them to every language before merging")
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
