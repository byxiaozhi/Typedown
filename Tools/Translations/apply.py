#!/usr/bin/env python3
"""Adds translated strings to the .resw files.

    python3 Tools/Translations/apply.py translations.json

The JSON is {lang: {key: text}}; the file a key belongs to is taken from en, and keys that
the language already has are left alone (this never overwrites an existing translation).
"""
import json, os, re, sys
from xml.sax.saxutils import escape

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Dev', 'Typedown.Core', 'Resources', 'Strings')
FILES = ['CommonResources.resw', 'DialogResources.resw', 'SettingsResources.resw', 'Resources.resw']

def strip_comments(text):
    """The .resw template carries sample entries (Name1, Bitmap1, Icon1) inside a comment."""
    return re.sub(r'<!--.*?-->', '', text, flags=re.S)



def keys_of(lang, name):
    path = os.path.join(ROOT, lang, name)
    if not os.path.exists(path):
        return set()
    return set(re.findall(r'<data name="([^"]+)"', strip_comments(open(path, encoding='utf-8').read())))


def main(path):
    data = json.load(open(path, encoding='utf-8'))
    home = {}  # key -> file, from the reference language
    for name in FILES:
        for key in keys_of('en', name):
            home[key] = name
    for lang, strings in data.items():
        added = 0
        for name in FILES:
            existing = keys_of(lang, name)
            new = {k: v for k, v in strings.items() if home.get(k) == name and k not in existing}
            if not new:
                continue
            target = os.path.join(ROOT, lang, name)
            text = open(target, encoding='utf-8').read()
            block = ''.join(f'  <data name="{k}" xml:space="preserve">\n    <value>{escape(v)}</value>\n  </data>\n'
                            for k, v in new.items())
            text = text.replace('</root>', block + '</root>')
            open(target, 'w', encoding='utf-8').write(text)
            added += len(new)
        print(f"{lang}: added {added}")


if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))
