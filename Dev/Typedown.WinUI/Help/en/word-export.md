# Export to Word

**File** > **Export** > **Word Document (.docx)** writes the document as a real Word file. It is made on your PC: Word does not have to be installed, and nothing is uploaded.

## What becomes what

- Headings are Word headings, so the navigation pane, a table of contents and a screen reader find them. Lists, tables (the header row repeats on every page), quotes, code, links, bold, italic and the rest are kept.
- Pictures are put in the file, at the size they have on screen, with their alt text. They are read from your PC; a picture on the web is not downloaded, and a picture that cannot be added is listed in a message, with its description left in the text.
- Footnotes are Word footnotes. Formulas (`$x^2$`) are Word equations. Diagrams (mermaid, flowchart, sequence, vega-lite) are pictures; PlantUML needs a server, so it stays code.
- The front matter at the top of the file gives the title, author, subject, description and keywords of the Word file, and is not printed.
- A line with `[TOC]` becomes a table of contents. A link to a heading, such as `[see below](#budget)`, goes to that heading. Word fills in the page numbers when it opens the file.

## Review and speech

Comments and changes written as marks (see **Review: comments and changes**) become real Word comments and tracked changes, with their authors and days, so a colleague can accept or reject them in Word.

When you are tracking changes in the document, Caret asks whether to export them: **With tracked changes** shows each change since the tracking began, and **Current text only** exports the text as it is. Nothing is written into your document either way.

Speech marks (see **Speech marks**) are small gray notes in the file.

## Options

The options open first and are kept for next time.

- **Look**: Plain, Report, Business or Modern. They change fonts, sizes, colors and spacing; the structure is the same.
- **Page size**, **Orientation** and **Margins**.
- **Header text** and **Footer text**: `{title}` is replaced by the title of the document and `{date}` by today’s date. **Page numbers** are at the right of the footer.
- **Table of contents at the start**.
- **Template**: choose a Word file or template (`.docx`, `.dotx`) of your own with **Choose a template...**, and the export is written on it. Its styles, page, header and footer are used, and its own text is dropped; the look and page options do not apply. **Remove** goes back to the looks.
