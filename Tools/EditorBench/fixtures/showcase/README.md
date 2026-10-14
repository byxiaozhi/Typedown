# Showcase document

`showcase.md` is a single Markdown document (about 1,100 lines) that contains every element this editor supports, the tricky cases around each one, and a section of deliberately broken input. It has two uses:

1. **Fixture**: `node showcase-check.js` (in `Tools/EditorBench`) loads it into the built editor headlessly and checks it.
2. **Side-by-side comparison**: open the same file in Typora, MarkText, Obsidian, VS Code's preview and on GitHub, and compare the results section by section.

`images/` holds the pictures it uses. They are small generated files (PNG, JPEG, SVG and a PNG with spaces in its name). The document uses no web images, so it renders the same offline and fetches nothing.

## Sections

Each section opens with an italic line saying what it checks. Items that this editor does not support are marked **(not supported here)** in the text.

| # | Section | Standard? | What it covers |
|---|---------|-----------|----------------|
| – | Front matter | extension | YAML between `---` lines at the top of the file |
| – | `[TOC]` | extension | Table-of-contents placeholder |
| 1 | Headings | CommonMark | ATX levels 1–6, setext `=`/`-`, inline formatting, closing `#`s, `#5`/`#hashtag`, escaped `#`, three-space indent, seven `#`s |
| 2 | Paragraphs and line breaks | CommonMark | soft breaks, two-space and backslash hard breaks, `<br>`, long lines, unbroken tokens, runs of blank lines |
| 3 | Scripts, languages and emoji | CommonMark + extension | CJK/Latin mix, Japanese, Korean, Arabic and Hebrew (RTL), combining marks, literal emoji with ZWJ sequences and flags, `:shortcode:` emoji (extension) |
| 4 | Emphasis and inline formatting | CommonMark/GFM + extension | nested, intraword and `***` emphasis, underscores in words, unclosed markers, `~~strike~~` (GFM), code spans containing backticks, `==highlight==` (extension), `<u>` `<ins>` `<sub>` `<sup>` `<mark>` `<kbd>` `<ruby>` |
| 5 | Links | CommonMark/GFM | inline links, titles, parentheses in URLs, `<...>` destinations, full/collapsed/shortcut references, autolinks, bare URLs/www/email (GFM), mailto, anchors to headings in this document |
| 6 | Images | CommonMark + HTML | relative paths, `./` paths, titles, SVG, spaces in paths (`%20` and `<...>`), reference images, sized and centred `<img>`, a missing file, an image inside a link |
| 7 | Block quotes | CommonMark | three levels of nesting, a heading, lists, code and tasks inside a quote, lazy continuation, a "**Note**" callout |
| 8 | Lists | CommonMark | `-` `*` `+`, start number 7, `1)` delimiter, all-`1.` numbering, tight and loose lists, mixed nesting, items with paragraphs, code and quotes |
| 9 | Task lists | GFM | checked, unchecked, `[X]`, nested tasks, ordered tasks |
| 10 | Code | CommonMark | js, ts, python, csharp, json, yaml, bash, powershell, sql, html, css, diff, markdown, an unknown language, no language, an info string with attributes, `~~~` and four-backtick fences containing ``` , indented code, a very long line, an empty block |
| 11 | Tables | GFM | alignment, inline formatting, maths, `\|` in text and code, `<br>` in cells, empty cells, a 12-column table, CJK, a one-column table |
| 12 | Footnotes | extension (GFM) | numbered and named footnotes, a repeated reference, a footnote with two paragraphs |
| 13 | Horizontal rules | CommonMark | `---` `***` `___` `- - -` `* * * * *` and long runs |
| 14 | Math | extension | inline `$…$`, display `$$…$$`, one-line `$$…$$`, `aligned`, matrices, `cases`, mhchem `\ce`, prices like $5 that are not maths, `\$` |
| 15 | Mermaid diagrams | extension | flowchart, sequence, class, state, Gantt, pie, ER, mindmap, timeline, gitGraph, and one diagram that is broken on purpose |
| 16 | Other diagrams | extension | `flowchart` (flowchart.js), `sequence` (js-sequence-diagrams), `vega-lite`, `plantuml` |
| 17 | Raw HTML | CommonMark | a styled `<div>`, `<details>`, an HTML `<table>` with colspan, comments, inline tags, an `<a id>` anchor target |
| 18 | Escapes and entities | CommonMark | every escapable punctuation character, escaped markup, named/decimal/hex entities, backslashes before letters |
| 19 | 中文段落 | CommonMark | Chinese prose, full-width punctuation, Chinese heading anchors, lists, quotes and a long unbroken Chinese paragraph |
| 20 | Syntax from other editors | not here | `^sup^`, `~sub~`, single-tilde strikethrough, `[[wiki links]]`, `%%comments%%`, `> [!NOTE]` alerts, definition lists, abbreviations, a ```` ```math ```` fence |
| 21 | Edge cases | CommonMark | empty headings, a heading right after a list, consecutive fences, a six-level list, a table interrupting a paragraph, a fence or quote with no blank line before it, whitespace-only lines |
| 22 | Malformed and hostile input | — | unclosed emphasis, links, code, maths, HTML and comments; an unclosed `<div>` and a stray `</div>`; misnested tags; harmless `<script>`/`onerror`/`onclick`/`javascript:`/`<iframe>`; tables with too many or too few cells or a bad delimiter row; missing, duplicate and unused references and footnotes; invalid formulas and diagrams (marked `BROKEN-ON-PURPOSE`); fake front matter in the middle; ten-digit list numbers; 15 nested quotes; zero-width, no-break, soft-hyphen, BOM, U+FFFD and RTL-override characters; an empty `$$` pair; an unclosed `$$`; an unclosed code fence at the end of the file |

## What differs between editors

- **GitHub** renders maths, Mermaid, footnotes, task lists (ordered ones too), `> [!NOTE]` alerts, `:shortcode:` emoji and single-tilde strikethrough. It shows front matter as a table and `[TOC]` as text. It has no `==highlight==`. It shows `flowchart`, `sequence`, `vega-lite` and `plantuml` fences as code, and removes `style`, `<script>`, `<iframe>` and event handlers.
- **Typora** renders front matter, `[TOC]`, highlight, maths, Mermaid, flowchart.js and js-sequence. `^sup^` and `~sub~` work once enabled in its settings. It has no Vega-Lite or PlantUML.
- **MarkText**, which this editor is based on, behaves much like this editor. With its extension options on, it also renders `^sup^`/`~sub~`.
- **Obsidian** renders maths, Mermaid, highlight, footnotes, wiki links, `%%comments%%` and alerts (callouts). Front matter appears as Properties. It does not render `[TOC]`, flowchart.js, js-sequence, Vega-Lite or PlantUML (only through plugins), and it does not render `:shortcode:` emoji.
- **VS Code's built-in preview** renders CommonMark/GFM plus front matter (hidden) and maths. Mermaid, emoji shortcodes, highlight and footnotes need extensions. It shows the other diagram fences as code.
- Most editors use their own Mermaid version, so newer diagram types (mindmap, timeline) may fail in older ones.
- PlantUML is **off by default in this editor**: drawing a diagram sends its source to a PlantUML server, so the block shows a notice until the setting is turned on.

## This editor: what to expect

- The editor shows these as text: backslash hard breaks, lazy quote continuation lines (the editor ends the quote), ordered task lists, and everything in section 20.
- `[TOC]` shows as text while you edit. The HTML/PDF export replaces it with a table of contents.
- `\ce{…}` (mhchem) renders, inline and as a display block, in the editor and in the HTML/PDF export.
- A reference-style image (`![…][ref]`) appears when the document opens, like an inline one.
- A table row with more cells than the header widens the table: the header gets empty cells, so the surplus cells are shown and kept (GFM ignores them).
- An empty `$$` / `$$` pair is not an empty block here. The editor needs at least one character between the lines, so the opening `$$` pairs with the next `$$` line further down. Section 22.8 is written so this is visible: the sentence after the empty pair ends up inside a formula.
- In section 22, every block marked `BROKEN-ON-PURPOSE` shows an "Invalid … Codes/Formula" notice, and the empty Mermaid block shows "Empty Mermaid Block". The script, `onerror` and `onclick` do not run, and the `<iframe>` is removed.

## Normalisation this editor applies

Until the first edit, the editor writes the file back byte for byte. On the first edit it rewrites the whole document from its own model. `showcase-check.js` compares that rewrite (`getMarkdown()`) with the file. These differences come up, and none of them loses text:

- Closing `#`s are removed from headings, the three-space indent before a heading is removed, and empty headings get a trailing space (`# `).
- Table delimiter rows become `| --- |:---:| ---:|` and cell padding is removed. A missing cell is added as an empty cell.
- `~~~` fences are rewritten as backtick fences, one longer than any fence inside. An unclosed fence gets a closing fence. Empty fenced blocks get one blank line inside.
- A blank line is inserted between reference definitions, between a paragraph and a following table, fence or quote, between a list and a following heading, and between two adjacent fences.
- Empty quote lines become `> ` (with a trailing space), and a blank `> ` line is inserted between a quote paragraph and a nested quote.
- Ordered lists numbered `1. 1. 1.` are renumbered `1. 2. 3.`, and `[X]` becomes `[x]`.
- Footnote definitions move to the end of the document. A multi-paragraph footnote starts with `[^long]:` on its own line and has its first paragraph indented below it.
- Whitespace-only lines become empty lines.
- These change how the document renders elsewhere, but still keep the text:
  - The lazy continuation line is written as its own paragraph after the quote.
  - The `*` and `+` lists in section 8 come back loose: the blank line between the two lists moves to between their items.
  - A blank line is inserted after a stray `$$`.
  - The 22.3 table whose row has surplus cells gets two more columns (empty header cells and `---` delimiters), so GFM then shows `surplus1` and `surplus2` as well.

## Running the check

```bash
cd Tools/EditorBench
npm ci                       # once; or reuse an existing node_modules
node showcase-check.js       # PASS/FAIL, render counts, a short diff of the rewrite
node showcase-check.js --full-diff
node showcase-check.js path/to/other.md
```

The check needs the built editor in `Dev/Typedown/Resources/Statics` and Chrome at `/opt/google/chrome/chrome` (or set `CHROME`). It fails in any of these cases:

- the page throws an error;
- a diagram or formula fails, other than those marked `BROKEN-ON-PURPOSE`;
- a block marked `BROKEN-ON-PURPOSE` renders;
- a local image fails to load or never finishes loading, other than `does-not-exist*`;
- section 22's script or event handlers run;
- a word or invisible character from the file is missing from the rewrite;
- the unedited flush differs from the file.

Normalisation differences are printed but do not fail the check.
