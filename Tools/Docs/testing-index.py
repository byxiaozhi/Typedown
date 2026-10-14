#!/usr/bin/env python3
"""The lists in docs/testing.md that come from the code: the Windows end-to-end cases (the Case(...) lines of
Tools/AutomationE2E/Driver/Program.cs, in the order they run), the test host's test.* methods (each with the comment
above it and the parameters it reads), and the editor checks the build runs (.github/workflows/build.yml).

    python3 Tools/Docs/testing-index.py           # fails, saying what differs, when the document is behind the code
    python3 Tools/Docs/testing-index.py --write   # brings the document up to date

Each list sits between <!-- BEGIN generated: NAME --> and <!-- END generated: NAME --> lines; the rest of the
document is written by hand. A test.* method without a comment of its own directly above it is an error: the
comment is what the document says it is for.
"""
import difflib, os, re, sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
DOC = "docs/testing.md"
DRIVER = "Tools/AutomationE2E/Driver/Program.cs"
HOOK_FILES = ["Dev/Typedown.Automation.TestHost/EditBarriers.cs", "Dev/Typedown/Services/Automation/TestHostHooks.cs"]
WORKFLOW = ".github/workflows/build.yml"

errors = []


def read(path):
    with open(os.path.join(ROOT, path), encoding="utf-8-sig") as f:
        return f.read()


def cell(text):
    return text.replace("|", "\\|")


def cases():
    found = re.findall(r'await Case\("((?:[^"\\]|\\.)*)",\s*(\w+)\)', read(DRIVER))
    if not found:
        errors.append(f"{DRIVER}: no Case(...) lines found")
    rows = ["| Case | What it checks |", "| --- | --- |"]
    for text, method in found:
        text = text.replace('\\"', '"').replace("\\\\", "\\")
        case_id, _, what = text.partition(" ")
        if case_id != method:
            errors.append(f"{DRIVER}: case {case_id!r} runs method {method}")
        rows.append(f"| {case_id} | {cell(what)} |")
    return rows


PARAM = re.compile(r'\b(Required|Optional)(\w*)\("(\w+)"')
RAW_PARAM = re.compile(r'Request\.Params as [\w.]+\)\?\["(\w+)"\]')


def hooks():
    rows = ["| Method | Parameters | What it is for | Where |", "| --- | --- | --- | --- |"]
    seen = set()
    for path in HOOK_FILES:
        lines = read(path).split("\n")
        starts = [i for i, line in enumerate(lines) if 'new MethodDescriptor("test.' in line]
        for n, start in enumerate(starts):
            name = re.search(r'MethodDescriptor\("(test\.[\w.]+)"', lines[start]).group(1)
            if name in seen:
                errors.append(f"{path}:{start + 1}: {name} is added twice")
            seen.add(name)
            comment = []
            i = start - 1
            while i >= 0 and lines[i].strip().startswith("//"):
                comment.insert(0, lines[i].strip()[2:].strip())
                i -= 1
            if not comment:
                errors.append(f"{path}:{start + 1}: {name} has no comment directly above it saying what it is for")
            end = starts[n + 1] if n + 1 < len(starts) else len(lines)
            body = "\n".join(lines[start:end])
            params = []
            for kind, _, param in PARAM.findall(body):
                label = param if kind == "Required" else param + "?"
                if label not in params:
                    params.append(label)
            for param in RAW_PARAM.findall(body):
                if param not in params:
                    params.append(param)
            shown = ", ".join(f"`{p}`" for p in params) or "-"
            rows.append(f"| `{name}` | {shown} | {cell(' '.join(comment))} | {path.split('/')[-1]} |")
    if not seen:
        errors.append("no test.* methods found")
    return rows


def editor_checks():
    workflow = read(WORKFLOW)
    step = re.search(r"- name: Test editor integration\n(.*?)\n      - ", workflow, re.S)
    if not step:
        errors.append(f"{WORKFLOW}: no 'Test editor integration' step")
        return []
    scripts = re.findall(r"^\s+node ([\w-]+\.js)\s*$", step.group(1), re.M)
    rows = []
    for script in scripts:
        # The first sentence of the comment the script starts with.
        comment = []
        for line in read(f"Tools/EditorBench/{script}").split("\n"):
            line = line.strip()
            if not line.startswith("//"):
                break
            text = line[2:].strip()
            if not text:
                break
            comment.append(text)
        if not comment:
            errors.append(f"Tools/EditorBench/{script}: no comment at the top saying what it checks")
            continue
        sentence = re.split(r"(?<=\.)\s", " ".join(comment), maxsplit=1)[0]
        rows.append(f"- `{script}`: {sentence}")
    return rows


def main():
    write = "--write" in sys.argv[1:]
    generated = {"e2e-cases": cases(), "test-methods": hooks(), "editor-checks": editor_checks()}
    if errors:
        for e in errors:
            print("ERROR " + e)
        return 1
    doc = read(DOC)
    updated = doc
    for name, rows in generated.items():
        pattern = re.compile(rf"(<!-- BEGIN generated: {name} -->\n).*?(<!-- END generated: {name} -->)", re.S)
        if not pattern.search(updated):
            print(f"ERROR {DOC}: no BEGIN/END generated: {name} markers")
            return 1
        updated = pattern.sub(lambda m: m.group(1) + "\n".join(rows) + "\n" + m.group(2), updated)
    if updated == doc:
        print(f"OK {DOC} matches the code")
        return 0
    if write:
        with open(os.path.join(ROOT, DOC), "w", encoding="utf-8", newline="\n") as f:
            f.write(updated)
        print(f"WROTE {DOC}")
        return 0
    print(f"FAIL {DOC} is behind the code; run python3 Tools/Docs/testing-index.py --write")
    sys.stdout.writelines(difflib.unified_diff(doc.splitlines(True), updated.splitlines(True), DOC, DOC + " (from the code)", n=0))
    return 1


if __name__ == "__main__":
    sys.exit(main())
