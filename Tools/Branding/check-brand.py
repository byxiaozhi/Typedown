#!/usr/bin/env python3
"""Checks that the product's names come from the brand files only.

The names live in Dev/Typedown.Automation/Brand.cs (C#), Branding.props (MSBuild and the scripts),
Tools/Installer/brand.iss (the installer) and the package manifest. An edition under another name (Typeleaf) changes
those and nothing else; a name written anywhere else a person sees it is a leak - after a merge from Typedown it would
show the wrong product. This fails when:

  - the brand files disagree with each other;
  - a C# string literal, a XAML attribute, a resource string, the installer script, a build or check script or the CI
    workflow writes "Typedown", the edition's own name, its exe or its CLI where it should take them from the brand.

A line that has to say the name (a migration from Typedown's data, say) carries "brand-ok". Paths and identifiers
that merely contain the word (Dev/Typedown, Typedown.Core, typedown_client.py) are not names and are not reported.

    python3 Tools/Branding/check-brand.py        exit 0: clean, 1: leaks or disagreement (listed)
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
problems = []


def rel(path):
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def read(path):
    with open(path, encoding="utf-8-sig") as f:
        return f.read()


# ---- the brand files ------------------------------------------------------------------------------------------------

brand_cs = read(os.path.join(ROOT, "Dev/Typedown.Automation/Brand.cs"))
cs_name = re.search(r'const string Name = "([^"]+)"', brand_cs).group(1)
cs_cli = re.search(r'const string CliName = "([^"]+)"', brand_cs).group(1)
props = {e.tag: (e.text or "").strip() for e in ET.parse(os.path.join(ROOT, "Branding.props")).getroot().iter()}
iss = dict(re.findall(r'^#define (\w+) "([^"]*)"', read(os.path.join(ROOT, "Tools/Installer/brand.iss")), re.M))
manifest = read(os.path.join(ROOT, "Tools/Typedown.Package/Package.appxmanifest"))
display_names = re.findall(r'<DisplayName>([^<]*)</DisplayName>|DisplayName="([^"]*)"|<uap:DisplayName>([^<]*)</uap:DisplayName>', manifest)
display_names = [next(x for x in t if x) for t in display_names]

name, exe, cli = props["BrandName"], props["BrandExeName"], props["BrandCliName"]
for label, value, expected in [
    ("Brand.cs Name", cs_name, name),
    ("Brand.cs CliName", cs_cli, cli),
    ("brand.iss MyAppName", iss.get("MyAppName"), name),
    ("brand.iss MyAppExeName", iss.get("MyAppExeName"), exe + ".exe"),
] + [("Package.appxmanifest DisplayName", d, cli if d == cli else name) for d in display_names] + [
    # The command line's own entry: its exe beside the app's, and the name typed in a terminal.
    ("Package.appxmanifest execution alias", a, cli + ".exe") for a in re.findall(r'ExecutionAlias Alias="([^"]*)"', manifest)
] + [("Package.appxmanifest command line Executable", e, cli + ".exe") for e in re.findall(r'Executable="Typedown\\([^"]*)"', manifest)]:
    if value != expected:
        problems.append(f"brand files disagree: {label} is {value!r}, Branding.props says {expected!r}")

# ---- what counts as writing a name ----------------------------------------------------------------------------------

names = sorted({"Typedown", name}, key=len, reverse=True)
clis = sorted({"typedownctl", cli, "typedown-mcp", name.lower() + "-mcp"}, key=len, reverse=True)
exes = sorted({"Typedown.exe", exe + ".exe"})
# The product as a word: not part of a path, an identifier or a file name (Dev/Typedown, Typedown.Core,
# Typedown-Client.ps1, typedown_client) - but "Typedown.exe" is the exe's name.
word = re.compile(r"(?<![\w./\\:-])(?:%s)(?![\w-])(?!\.(?!exe\b)\w)" % "|".join(map(re.escape, names)))
tool = re.compile(r"(?<![\w.-])(?:%s)(?![\w-])" % "|".join(map(re.escape, clis + exes)))
# In scripts what breaks is finding a program by its file name; check labels that mention the CLI are prose.
exe_file = re.compile(r"(?<![\w.-])(?:%s)\.exe\b" % "|".join(map(re.escape, sorted({"Typedown", exe, "typedownctl", cli}))))
# Internal file names under the edition's own folder, not names a person reads.
INTERNAL = {"typedownctl-client-id", "typedown-mcp-client-id"}


def report(path, number, line, why):
    problems.append(f"{rel(path)}:{number}: {why}: {line.strip()[:160]}")


def scan(path, pattern_sets, literal_only=False, comment=None, skip=None):
    in_block = False
    for number, line in enumerate(read(path).splitlines(), 1):
        stripped = line.strip()
        # PowerShell's <# ... #> help blocks are comments too.
        if path.endswith(".ps1") and (in_block or stripped.startswith("<#")):
            in_block = "#>" not in stripped
            continue
        if "brand-ok" in line:
            continue
        if comment and stripped.startswith(comment):
            continue
        if skip and skip(stripped):
            continue
        parts = re.findall(r'"((?:[^"\\]|\\.)*)"', line) if literal_only else [line]
        for part in parts:
            if part in INTERNAL:
                continue
            for pattern, why in pattern_sets:
                if pattern.search(part):
                    report(path, number, line, why)
                    break
            else:
                continue
            break


def files(*patterns, exclude=()):
    out = []
    for p in patterns:
        out += glob.glob(os.path.join(ROOT, p), recursive=True)
    return sorted(f for f in set(out) if not any(x in rel(f) for x in exclude + ("/obj/", "/bin/", "node_modules")))


# C#: the app, the libraries it ships, the CLI and the MCP server; string literals only.
for f in files("Dev/**/*.cs", "Tools/Typedown.Cli/**/*.cs", "Tools/Typedown.Mcp/**/*.cs",
               exclude=("Dev/Typedown.Automation.Tests/", "Dev/Typedown.Automation.TestHost/", "Dev/Typedown.Editor/",
                        "Dev/Typedown.Automation/Brand.cs")):
    scan(f, [(word, "the product's name"), (tool, "an exe or CLI name")], literal_only=True, comment="//",
         skip=lambda s: s.startswith(("using ", "namespace ", "[assembly")))

# XAML: what a person sees; namespaces, classes and resource paths are not names.
for f in files("Dev/**/*.xaml"):
    scan(f, [(word, "the product's name")], skip=lambda s: any(k in s for k in ("xmlns", "using:", "x:Class", "ms-appx", "clr-namespace")))

# Resource strings, but AppName: nothing reads that key (window titles and About use Brand.Name).
for f in files("Dev/**/Resources.resw"):
    text = read(f)
    for m in re.finditer(r'<data name="([^"]+)"[^>]*>\s*<value>([^<]*)</value>', text):
        if m.group(1) != "AppName" and (word.search(m.group(2)) or tool.search(m.group(2))):
            report(f, text[:m.start()].count("\n") + 1, m.group(0).replace("\n", " "), "a resource string names the product")

# The installer script outside brand.iss.
scan(os.path.join(ROOT, "Tools/Installer/Typedown.iss"), [(word, "the product's name"), (tool, "an exe or CLI name")], comment=";")

# Build, packaging and check scripts, and CI: the app's exe and the CLI's by file name.
for f in files("Tools/Installer/*.ps1", "Tools/AutomationE2E/*.ps1", ".github/workflows/*.yml"):
    scan(f, [(exe_file, "a program by its file name")], comment="#")

# Typedown itself adds nothing at the edition hook (Dev/Typedown/Edition.cs): an implementation there is an edition's.
if name == "Typedown":
    for f in files("Dev/Typedown/Edition.*.cs", "Tools/AutomationE2E/Driver/Edition.*.cs"):
        problems.append(f"{rel(f)}: an edition's own code in Typedown - it belongs to the edition that uses it")
    for f in files("Dev/Typedown/*.cs", "Dev/Typedown/**/*.cs"):
        code = "\n".join(l for l in read(f).splitlines() if not l.lstrip().startswith("//"))  # not the doc's example
        if re.search(r"static\s+partial\s+void\s+(RegisterServices|Initialize)\s*\([^)]*\)\s*\{", code):
            problems.append(f"{rel(f)}: implements the edition hook - Typedown leaves it empty")

# What only an edition has stays in the edition's repository: its code (Dev/Edition), its Store listing and the
# scripts that build it (docs/store, Tools/Store), the private build and test scripts (Tools/dev). Typedown is public,
# and merges go from Typedown to an edition only (docs/editions.md in the edition's repository).
if name == "Typedown":
    for private in ("Dev/Edition", "docs/store", "Tools/Store", "Tools/dev"):
        if os.path.exists(os.path.join(ROOT, private)):
            problems.append(f"{private}: an edition's own material in Typedown - it belongs to the edition's repository")

if problems:
    print("\n".join(problems))
    print(f"\ncheck-brand: {len(problems)} problem(s); the names belong in Brand.cs, Branding.props, brand.iss and the manifest")
    sys.exit(1)
print(f"check-brand: {name} ({exe}.exe, {cli}) - the brand files agree and no other place writes a name")
