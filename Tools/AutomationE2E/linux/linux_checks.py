#!/usr/bin/env python3
"""What only Linux can get wrong, checked against a running Uno edition through its socket (automation plan, phase 4):
case-sensitive names, symbolic links, CRLF files, paths with spaces and non-ASCII names, a large document.

    python linux_checks.py --examples DIR --socket PATH --workdir DIR
"""
import argparse, os, sys, time

ap = argparse.ArgumentParser()
ap.add_argument("--examples", required=True)
ap.add_argument("--socket", required=True)
ap.add_argument("--workdir", required=True)
args = ap.parse_args()
sys.path.insert(0, args.examples)
import typedown_client as td

ok = True
def check(cond, label):
    global ok
    print(("PASS " if cond else "FAIL ") + label)
    ok = ok and cond

w = args.workdir
os.makedirs(w, exist_ok=True)
def write(name, text):
    path = os.path.join(w, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    return path
def disk(path):
    with open(path, encoding="utf-8", newline="") as f:
        return f.read()

c = td.Client.connect(socket_path=args.socket)
c.initialize(["document.read", "document.write", "document.save", "window.focus"])
def open_doc(path): return c.call("document.open", {"path": path})["documentId"]
def get(doc): return c.call("document.get", {"documentId": doc, "consistency": "latest", "include": ["text"]})

# Names that differ only in case are two files on Linux.
lower = open_doc(write("case/a.md", "# lower\n"))
upper = open_doc(write("case/A.md", "# upper\n"))
check(lower != upper, "a.md and A.md are two documents")
check(get(upper)["text"] == "# upper\n" and get(lower)["text"] == "# lower\n", "each shows its own file")

# A link is the file it points to: one document, not two edited apart.
target = write("link/target.md", "# target\n")
link = os.path.join(w, "link", "alias.md")
if os.path.lexists(link): os.remove(link)
os.symlink(target, link)
check(open_doc(link) == open_doc(target), "a link and its target are one document")

# CRLF on disk: \n in the API, CRLF written back.
crlf_path = write("crlf.md", "# crlf\r\n\r\nline\r\n")
crlf = open_doc(crlf_path)
d = get(crlf)
check(d["text"] == "# crlf\n\nline\n" and d["lineEnding"] == "crlf", f"a CRLF file reads with \\n and says crlf ({d['lineEnding']})")
c.call("document.replace", {"documentId": crlf, "baseRevision": d["revision"], "text": "# crlf\n\nchanged\n", "save": True, "normalizationPolicy": "allowUnknown"})
check(disk(crlf_path) == "# crlf\r\n\r\nchanged\r\n", "saved back with CRLF")

# Spaces and non-ASCII in the path.
odd_path = write("dir with space/文档 é.md", "# 文档\n\nπ\n")
odd = open_doc(odd_path)
d = get(odd)
check(d["path"] == os.path.abspath(odd_path) and d["title"] == "文档 é.md", f"the path and title come back as they are ({d['path']})")
c.call("document.replaceText", {"documentId": odd, "baseRevision": d["revision"], "find": "π", "replacement": "τ", "expectedCount": 1, "save": True, "normalizationPolicy": "allowUnknown"})
check(disk(odd_path) == "# 文档\n\nτ\n", "and it saves there")

# A large document (the Uno edition shows it in source mode): a short write near the end, exact read back.
para = "Paragraph {} with some words, *emphasis* and `code` to make the line longer than it needs to be.\n\n"
big_text = "# Big\n\n" + "".join(para.format(i) for i in range(12000))
big_path = write("big.md", big_text)
start = time.time()
big = open_doc(big_path)
d = get(big)
opened = time.time() - start
check(d["text"] == big_text, f"a {len(big_text) // 1024} KB document reads back exactly ({opened:.1f} s to open and read)")
start = time.time()
r = c.call("document.replaceText", {"documentId": big, "baseRevision": d["revision"], "find": "Paragraph 11990 with", "replacement": "Paragraph eleven thousand nine hundred ninety with", "expectedCount": 1, "normalizationPolicy": "allowUnknown"})
written = time.time() - start
check(get(big)["text"] == big_text.replace("Paragraph 11990 with", "Paragraph eleven thousand nine hundred ninety with"), f"a short write near the end of it is exact ({written:.2f} s)")
c.close()
sys.exit(0 if ok else 1)
