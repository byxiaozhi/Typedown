# Word export: design

Status: 2026-10-10. The approach (a C# Markdown parser, Markdig, and the Open XML SDK) and the phases were confirmed by the owner the same day. All five phases are built and tested (section 8); the version is 2.5.0.0. The target is version 2.5.0.0; nothing of it goes into 2.0.1.

## 1. What it is for

Markdown is where a document is written; Word is where it is sent. Colleagues, customers and managers ask for a `.docx`. Today a Caret user can export HTML, PDF and plain text, and has to paste into Word by hand for the rest. File > Export > Word (.docx) closes the loop that Convert to Markdown opened: Word to Markdown (an existing feature) and Markdown back to Word.

## 2. Rules that do not change

| Rule | What it means here |
|---|---|
| No AI, no network, no keys | The file is written by rules, on the PC. Remote pictures are not downloaded. |
| Word is not needed | The `.docx` is written with the Open XML SDK (already in the app, MIT). A PC without Office produces the same file. |
| Only on request | Nothing is exported unless the user picks the command. The Markdown file is never changed by an export. |
| Real Word structure | Headings are heading styles, lists are real lists, tables are real tables, links are real links. No text boxes, no spaces used for layout. A screen reader and the Word navigation pane see the structure. |
| Round trip | What Caret exports, the existing Word converter (`WordConverter`) reads back to the same Markdown. This is the main automated test. |
| Five languages | Every UI string exists in English, French, Spanish, Polish and Brazilian Portuguese; `LocalizationTests` checks the files agree. |
| Tests with every phase | xunit in `Dev/Caret.ConverterTests`, a `CHANGES.md` entry, a check in the real app, and the file opened in the real Word. |

## 3. How it works

```
Markdown text + folder of the file + options
        |  Markdig (parse to a tree)
        v
   WordExporter  (plain .NET, no WinUI)
        |  Open XML SDK
        v
     .docx bytes
```

- `Services/Export/WordExporter.cs` is plain .NET, like the converters, and the test project compiles it as it is. No editor and no window are needed, so the same code can later serve the Explorer right-click menu and the command line.
- The parser is **Markdig 1.4.0** (BSD-2-Clause, added to THIRD-PARTY-NOTICES.md). The pipeline is built to match the editor (Muya): GitHub-style tables, task lists, strikethrough, sub- and superscript, automatic links, footnotes (phase 2), front matter (phase 2), math (phase 2). The marks of CriticMarkup use `{==` and `{++`, so the extension that reads `==x==` as a highlight is **not** switched on; it would eat them.
- The dialect is checked, not assumed: the tests feed sample files with every construct the editor writes to the exporter, and anything read differently is listed in section 9.

## 4. What becomes what

| Markdown | Word | Phase |
|---|---|---|
| `# ` to `###### ` | Heading 1 to 6 | 1 |
| Paragraph, hard line break | Normal paragraph, line break | 1 |
| bold, italic, strikethrough, inline code | Bold, italic, strikethrough, a Code character style | 1 |
| `[text](url)`, `<https://...>`, bare URLs | Hyperlink (the Hyperlink character style) | 1 |
| Bulleted, numbered and nested lists | Real Word lists (a numbering definition); every numbered list starts again at 1 | 1 |
| Task list `- [x]` | A list item that starts with a check box character | 1 |
| Block quote | Quote style paragraphs, indented, nested levels | 1 |
| Fenced and indented code | Paragraphs in a Code paragraph style (monospace, light background) | 1 |
| `---` | A paragraph with a bottom border | 1 |
| Table (with `:---:` alignment) | A real table, header row repeated across pages, column alignment | 1 |
| Picture `![alt](file)` | An embedded picture (PNG, JPEG, GIF, BMP), the alt text as the description, scaled to the page width | 1 |
| Sub- and superscript | Vertical alignment of the run | 1 |
| HTML in the Markdown | The text of the HTML, without the tags (a `<br>` is a line break) | 1 |
| Footnote `[^1]` | A real Word footnote (its links and pictures belong to the footnotes part) | 2 |
| Front matter | Document properties (title, author, subject, description, keywords or tags) and the language (lang); never printed | 2 |
| Math, inline and block | A Word equation (OMML) from the LaTeX: fractions, roots, powers and indices, sums, products and integrals with limits, limits, brackets, matrices, cases, aligned lines, Greek letters, accents, symbols, function names; what is not known is written as text | 2 |
| Mermaid, flowchart, sequence and vega-lite diagrams | A picture drawn by the editor, at the size it has on screen, the source as the alt text; PlantUML needs a server, so it stays code | 2 |
| A line with `[TOC]` | A Word table of contents field, its entries and links already written; Word fills in the page numbers when it opens the file | 2 |
| Review marks `{++added++}`, `{--deleted--}`, `{~~old~>new~~}` | A tracked insertion and deletion; the author and the day are those of the stamp that follows the mark (`{>>@Name 2026-10-10<<}`), or the reviewer given to the exporter | 3 |
| `{==text==}{>>@Name 2026-10-10: note<<}`, `{>>note<<}`, `{==text==}` | A comment on the range (author, initials, day, words), a comment at its place, a yellow highlight | 3 |
| The live review (changes since tracking began) | Offered by the command when the document is tracked: the baseline compared with the text, as tracked changes, without writing anything into the document | 3 |
| Speech marks of the built-in words, such as `{pause 2s}` and `{slow}...{/slow}` | Small gray notes as written, or left out (an option of the exporter; the command keeps them as notes). Marks a document defines itself are not known to the exporter and stay text | 3 |
| `[text](#heading-text)` | A link to the bookmark of the heading (GitHub anchors; every heading is a bookmark) | 2 |
| Page numbers | The number, centered, in the footer (the command turns them on; an option of the exporter) | 2 |
| Look (Plain, Report, Business, Modern) | The fonts, sizes, colors and spacing of the styles; the structure is the same in every look | 4 |
| Page size (A4, Letter, Legal), orientation, margins (normal, narrow, wide) | The page; the text, tables and pictures follow its width | 4 |
| Header text, footer text, page numbers, a table of contents at the start | A centered header; a footer with the text at the left and the number at the right; {title} and {date} are replaced | 4 |
| A template of the user (.docx or .dotx) | The file is written on it: its styles, page, headers and footers are used, ours are added where it has none (by id, then by name), its lists and ours do not collide | 4 |

### The marks

The marks are found with the tokenizer the app already uses to accept and reject them (`ReviewMarks.Scan`: none in code, none after a backslash, none across a paragraph), written as sentinel punctuation that goes through Markdig, and turned into Word elements by the builder. Nothing is silently dropped or silently accepted: a mark that is not complete stays the text it is.

## 5. The Word file

- Styles are Word built-in style ids (`Heading1`, `Title`, `Quote`, `ListParagraph`, `Hyperlink`, `TableGrid`) plus two of ours (`Code` paragraph style, `CodeChar` character style). The default font is Calibri 11 pt, headings follow Word defaults. Phase 4 lets the user choose another look.
- Language: the document language is the language of the Caret interface at the time of export, so Word proofs a French text as French. A later option can set it per file.
- Pictures: a path is read relative to the folder of the Markdown file. A `data:` picture is decoded. A remote `http(s)` picture is **not** fetched (no network); the alt text is written, as a link to the address. A missing file writes the alt text in brackets and the export reports it. SVG is not supported in phase 1 (Word needs a bitmap fallback): the alt text is written.
- Sizes: a picture is never wider than the text column; a picture without a stated size uses its own pixel size at 96 dpi.
- Core properties: title (the first heading, or the file name) and the date. Nothing about the machine.
- The file is written to a temporary name next to the target and moved into place, so a failed export never leaves a half file where a good one was.

## 6. In the application

- File > Export > Word (.docx), after HTML, PDF and Text. The command exports the text of the document on screen, with every edit the editor has made (the same flush the teleprompter uses).
- A save dialog for `.docx`, suggested name = the file name. After the export the user is told where it went.
- Failure (file open in Word, no right to write) shows a dialog with the reason. When only a picture failed, the rest is still exported and the dialog says which pictures were left out.
- Nothing is sent anywhere. The PRIVACY.md statement does not change.

## 7. Tests

1. **Round trip** (`WordExportTests`): sample Markdown documents are exported, read back with `WordConverter`, and compared to the expected Markdown. Where Word cannot say it exactly the same way (for example a hard-wrapped line), the expected text shows the agreed form.
2. **Structure**: the XML of the exported file is opened and checked (heading style ids, list numbering, table grid, relationship ids, picture parts present).
3. **Validation**: every sample is checked with the Open XML SDK validator, no errors.
4. **Real Word** (by hand, once per phase): the sample documents are opened in Word, saved as PDF, and looked at.
5. **Dialect**: a file with every construct the editor writes.
6. Localization, as for every feature.

## 8. Phases

| Phase | Content | State |
|---|---|---|
| 0 | This document | done |
| 1 | Core blocks and inlines, lists, tables, pictures, the menu command, tests, strings in five languages | built: the exporter, 34 tests, the menu command, five languages; checked in the real Word and in the real app |
| 2 | Footnotes, front matter, math, diagrams, table of contents, page numbers | built: 62 tests in all, checked in the real Word and in the real app |
| 3 | Review marks to tracked changes and comments; speech marks option; the live review | built: 76 tests for the export, checked in the real Word (revisions with their authors, a comment on its range) and in the real app (the tracked document exported with its changes) |
| 4 | Looks, page setup, header and footer, a template of the user | built: the options dialog of the command, four looks, the page, the header and the footer, a table of contents at the start, a .docx or .dotx template; 104 tests for the export, checked in the real Word and in the real app |
| 5 | Help topic, README, Store listing, version 2.5.0.0 | built: the help topic Export to Word (Help menu) and a line in Getting started in five languages, a section in each README, the five Store listings (what is new, the description, feature 18, a tenth screenshot, search terms), a sentence in the privacy policy, version 2.5.0.0 |

## 9. Open points

- The template of phase 4 and the built-in looks are both there: the looks for a user with no template, the template for a company style (it takes the place of the look, the page and the header and footer choices).
- Whether Export > Word should also be offered in the Explorer right-click menu for `.md` files (it needs no editor, so it can). Decide after phase 2.
- Differences between Markdig and the editor in reading the same Markdown: none known yet; the dialect test fills this list.
- A single line break in the Markdown is a space in Word, as in the HTML export. The exporter has an option that makes it a line break (SoftBreaksAsLineBreaks); the command does not offer it yet. Decide after the first real use.
