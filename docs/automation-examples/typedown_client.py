#!/usr/bin/env python3
"""A direct client of Typedown's local automation API (docs/automation-api-spec.md), standard library only.

    python typedown_client.py list
    python typedown_client.py read DOCUMENT_ID
    python typedown_client.py replace-text DOCUMENT_ID FIND REPLACEMENT [--save]

The last one shows the pattern every writer should follow: read, write against the revision it read, and when the
document moved on meanwhile (revision_conflict) or the text no longer matches (match_count_mismatch), read again and
decide again from the new text - never resend an edit computed from old text.

Windows: connects to the named pipe of the current user (or --endpoint NAME). Elsewhere: --socket PATH.
"""
import argparse
import json
import socket
import subprocess
import sys
import uuid

API_VERSION = 1


class TypedownError(Exception):
    """A JSON-RPC error from Typedown: code, message and data (data['kind'] names it)."""

    def __init__(self, error):
        super().__init__(f"{error.get('message')} [{error.get('data', {}).get('kind')}]")
        self.error = error
        self.kind = error.get("data", {}).get("kind")
        self.data = error.get("data", {})


class Client:
    def __init__(self, stream):
        self._stream = stream
        self._next_id = 1

    @staticmethod
    def default_endpoint():
        # Typedown.Automation.v1.<SID of the current user>
        out = subprocess.run(["whoami", "/user", "/fo", "csv", "/nh"], capture_output=True, text=True, check=True).stdout
        sid = out.strip().split(",")[-1].strip('"')
        return "Typedown.Automation.v1." + "".join(c for c in sid if c.isalnum() or c == "-")

    @classmethod
    def connect(cls, endpoint=None, socket_path=None):
        if socket_path:
            sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
            sock.connect(socket_path)
            return cls(sock.makefile("rwb", buffering=0))
        return cls(open("\\\\.\\pipe\\" + (endpoint or cls.default_endpoint()), "r+b", buffering=0))

    def close(self):
        self._stream.close()

    # Content-Length framing, as in the Language Server Protocol.
    def _send(self, message):
        body = json.dumps(message, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self._stream.write(b"Content-Length: %d\r\n\r\n" % len(body) + body)

    def _receive(self):
        length = None
        while True:
            line = b""
            while not line.endswith(b"\r\n"):
                ch = self._stream.read(1)
                if not ch:
                    raise ConnectionError("Typedown closed the connection")
                line += ch
            if line == b"\r\n":
                break
            name, _, value = line.decode("ascii").partition(":")
            if name.strip().lower() == "content-length":
                length = int(value.strip())
        body = b""
        while len(body) < length:
            chunk = self._stream.read(length - len(body))
            if not chunk:
                raise ConnectionError("Typedown closed the connection")
            body += chunk
        return json.loads(body.decode("utf-8"))

    def call(self, method, params=None):
        request_id = self._next_id
        self._next_id += 1
        self._send({"jsonrpc": "2.0", "id": request_id, "method": method, "params": params or {}})
        while True:
            reply = self._receive()
            if reply.get("id") == request_id or (reply.get("id") is None and "error" in reply):
                if "error" in reply:
                    raise TypedownError(reply["error"])
                return reply["result"]

    def initialize(self, scopes, name="typedown_client.py"):
        return self.call("system.initialize", {
            "apiVersion": API_VERSION,
            "client": {"id": str(uuid.uuid5(uuid.NAMESPACE_URL, "typedown_client.py")), "name": name, "version": "1"},
            "requestedScopes": scopes,
        })


def replace_text_carefully(client, document_id, find, replacement, save=False, attempts=3):
    """Read, then write against that revision; on a conflict read again and decide again from the new text."""
    for _ in range(attempts):
        doc = client.call("document.get", {"documentId": document_id, "consistency": "latest", "include": ["text"]})
        count = doc["text"].count(find)
        if count == 0:
            raise SystemExit(f"'{find}' is not in the document (revision {doc['revision']}); nothing to do")
        try:
            return client.call("document.replaceText", {
                "documentId": document_id, "baseRevision": doc["revision"],
                "find": find, "replacement": replacement, "expectedCount": count, "save": save,
            })
        except TypedownError as e:
            if e.kind not in ("revision_conflict", "match_count_mismatch"):
                raise
            print(f"the document changed meanwhile ({e.kind}); reading it again", file=sys.stderr)
    raise SystemExit("the document keeps changing; giving up")


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--endpoint", help="named pipe name (Windows)")
    parser.add_argument("--socket", help="Unix domain socket path")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("list")
    read = sub.add_parser("read")
    read.add_argument("document_id")
    replace = sub.add_parser("replace-text")
    replace.add_argument("document_id")
    replace.add_argument("find")
    replace.add_argument("replacement")
    replace.add_argument("--save", action="store_true")
    args = parser.parse_args()

    client = Client.connect(args.endpoint, args.socket)
    try:
        scopes = ["document.read"] + (["document.write"] + (["document.save"] if getattr(args, "save", False) else [])
                                      if args.command == "replace-text" else [])
        client.initialize(scopes)
        if args.command == "list":
            for d in client.call("document.list")["documents"]:
                print(f"{d['documentId']}  r{d['revision']}  {'saved' if d['saved'] else 'unsaved'}  {d.get('path') or d['title']}")
        elif args.command == "read":
            sys.stdout.write(client.call("document.get", {"documentId": args.document_id, "consistency": "latest", "include": ["text"]})["text"])
        else:
            result = replace_text_carefully(client, args.document_id, args.find, args.replacement, args.save)
            print(json.dumps(result))
    except TypedownError as e:
        print(f"typedown: {e}", file=sys.stderr)
        return 1
    finally:
        client.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
