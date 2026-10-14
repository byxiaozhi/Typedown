#!/usr/bin/env python3
"""The same scripted conversation with Typedown on every platform (automation plan, phase 4): Windows (named pipe)
and the Uno edition (Unix socket) must answer it alike. Prints one JSON transcript: every request and the raw reply.
compare.py normalizes two transcripts, checks each reply against docs/automation-schema/v1.json and diffs them.

    python scenario.py --examples DIR (--endpoint PIPE | --socket PATH) --workdir DIR > transcript.json

Run it against an instance in the visual mode (a fresh profile): in source mode the editor does not classify
normalization, so the default policy refuses the writes that are expected to succeed.
"""
import argparse, json, os, sys


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--examples", required=True, help="the folder holding typedown_client.py")
    ap.add_argument("--endpoint")
    ap.add_argument("--socket")
    ap.add_argument("--workdir", required=True, help="a folder for the scenario's files (emptied first)")
    args = ap.parse_args()
    sys.path.insert(0, args.examples)
    import typedown_client as td

    os.makedirs(args.workdir, exist_ok=True)
    for name in os.listdir(args.workdir):
        os.remove(os.path.join(args.workdir, name))
    def fixture(name, text):
        path = os.path.join(args.workdir, name)
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(text)
        return path

    client = td.Client.connect(args.endpoint, args.socket)
    transcript = []
    ids = {}

    def call(step, method, params=None):
        params = json.loads(json.dumps(params or {}).replace('"$A"', json.dumps(ids.get("A"))).replace('"$B"', json.dumps(ids.get("B"))))
        entry = {"step": step, "method": method, "params": params}
        try:
            entry["result"] = client.call(method, params)
        except td.TypedownError as e:
            entry["error"] = e.error
        transcript.append(entry)
        return entry.get("result")

    call("initialize", "system.initialize", {"apiVersion": 1, "client": {"id": "7a6f3b5e-9f0e-4d2c-8a1b-3c4d5e6f7a8b", "name": "equivalence", "version": "1"},
                                            "requestedScopes": ["app.read", "document.read", "document.write", "document.save", "window.focus", "settings.read", "settings.write"]})
    a_path = fixture("eq-a.md", "# Equivalence\n\nThe quick brown fox.\n\n- one\n- two\n")
    opened = call("open", "document.open", {"path": a_path, "reveal": "document"})
    ids["A"] = opened["documentId"]
    call("get latest with text and headings", "document.get", {"documentId": "$A", "consistency": "latest", "include": ["text", "headings"]})
    call("get without consistency", "document.get", {"documentId": "$A"})
    call("replace", "document.replace", {"documentId": "$A", "baseRevision": 0, "text": "# Equivalence\n\nThe quick red fox.\n\n- one\n- two\n"})
    call("replace on a stale revision", "document.replace", {"documentId": "$A", "baseRevision": 0, "text": "stale\n"})
    call("replaceText with the wrong count", "document.replaceText", {"documentId": "$A", "baseRevision": 1, "find": "o", "replacement": "0", "expectedCount": 1})
    call("replaceText", "document.replaceText", {"documentId": "$A", "baseRevision": 1, "find": "red", "replacement": "green", "expectedCount": 1})
    call("replaceText of nothing is no change", "document.replaceText", {"documentId": "$A", "baseRevision": 2, "find": "absent", "replacement": "x", "expectedCount": 0})
    call("text with a carriage return", "document.replace", {"documentId": "$A", "baseRevision": 2, "text": "a\r\nb\n"})
    call("formatting the editor would rewrite", "document.replace", {"documentId": "$A", "baseRevision": 2, "text": "Title\n=====\n\n* star\n"})
    call("the same, accepted", "document.replace", {"documentId": "$A", "baseRevision": 2, "text": "Title\n=====\n\n* star\n", "normalizationPolicy": "allowUnknown"})
    call("text the editor would lose", "document.replace", {"documentId": "$A", "baseRevision": 3, "text": "<div>\n&copy; kept\n</div>\n", "normalizationPolicy": "allowUnknown"})
    call("awaitPresentation without reveal", "document.replace", {"documentId": "$A", "baseRevision": 3, "text": "x\n", "awaitPresentation": True})
    call("undo", "document.undo", {"documentId": "$A", "baseRevision": 3})
    after_undo = call("get after undo", "document.get", {"documentId": "$A", "consistency": "latest", "include": ["text"]})
    call("redo", "document.redo", {"documentId": "$A", "baseRevision": after_undo["revision"] if after_undo else 4})
    now = call("get after redo", "document.get", {"documentId": "$A", "consistency": "latest", "include": ["text"]})
    call("save", "document.save", {"documentId": "$A", "baseRevision": now["revision"] if now else 5})
    with open(a_path, encoding="utf-8", newline="") as f:
        transcript.append({"step": "file on disk", "disk": f.read()})
    call("save on a stale revision", "document.save", {"documentId": "$A", "baseRevision": 0})
    created = call("create", "document.create", {"text": "# New\n", "normalizationPolicy": "allowUnknown"})
    ids["B"] = created["documentId"] if created else None
    call("get the new document", "document.get", {"documentId": "$B", "consistency": "latest", "include": ["text"]})
    call("save an untitled document", "document.save", {"documentId": "$B"})
    call("a document that does not exist", "document.get", {"documentId": "0123456789abcdef0123456789abcdef", "consistency": "snapshot"})
    call("missing documentId", "document.replace", {"baseRevision": 0, "text": "x\n"})
    call("unknown method", "document.teleport", {})
    call("focus A", "document.focus", {"documentId": "$A"})
    call("revealed write, presentation awaited", "document.replaceText", {"documentId": "$A", "baseRevision": now["revision"] if now else 5, "find": "star", "replacement": "starry", "expectedCount": 1, "reveal": "document", "awaitPresentation": True, "normalizationPolicy": "allowUnknown"})
    # Settings: the external ones only, each change against the settings revision read.
    call("settings.describe", "settings.describe")
    # The starting values are whatever this machine has: read them outside the transcript.
    got = client.call("settings.get", {"keys": ["editor.fontSize", "editor.textDirection"]})
    base = got["settingsRevision"] if got else 0
    font = got["values"]["editor.fontSize"] if got else 16
    # Fixed values, not relative ones: earlier runs may have left any font size behind.
    changed = call("settings.set fontSize", "settings.set", {"key": "editor.fontSize", "value": 20 if font != 20 else 21, "baseSettingsRevision": base})
    call("settings.set on a stale revision", "settings.set", {"key": "editor.fontSize", "value": 22, "baseSettingsRevision": base})
    after = changed["settingsRevision"] if changed else base
    call("settings.set out of range", "settings.set", {"key": "editor.fontSize", "value": 99, "baseSettingsRevision": after})
    call("settings.set not exposed", "settings.set", {"key": "editor.customCss", "value": "x", "baseSettingsRevision": after})
    call("settings.get after", "settings.get", {"keys": ["editor.fontSize"]})
    # Put the reader's size back; not part of the transcript, since it differs from machine to machine.
    try: client.call("settings.set", {"key": "editor.fontSize", "value": font, "baseSettingsRevision": after})
    except td.TypedownError: pass
    json.dump(transcript, sys.stdout, ensure_ascii=False, indent=1)
    client.close()


if __name__ == "__main__":
    main()
