# Editor benchmark

Drives the built editor bundle (`Dev/Typedown/Resources/Statics`) in headless Chrome with the WebView2 host
stubbed out, loads a large document, types into it and reports per-keystroke cost plus a CPU profile.

```bash
cd Tools/EditorBench
npm init -y && npm i puppeteer-core source-map          # once
# build the editor first (GENERATE_SOURCEMAP=true gives readable profiles)
node bench.js ../../Dev/Typedown/Resources/Statics big.md label
node prof-summary.js profile-label.cpuprofile ../../Dev/Typedown/Resources/Statics/static/js/main.*.js.map
```

`bench.js` expects Chrome at `/opt/google/chrome/chrome`; edit `executablePath` otherwise. The harness itself
costs ~20 ms per keystroke (CDP round trips), so compare runs against each other, not against zero.

Reference (80k-char document, 1287 blocks, 4-core Linux box): before the 2026-09-18 fixes 83–107 ms per
keystroke, after 45–53 ms.

`modeswitch-check.js` reproduces upstream #20/#61 (blank lines appended on every source-mode switch): the
markdown printed after "back to muya" must be identical to the value shown before the switch.

Setup once with `npm ci` in this directory (Chrome at `/opt/google/chrome/chrome`, or set `CHROME`).
`npm run check:source` runs the source-text mapping checks that gate automation writes (stage 0a of
`docs/automation-api-analysis-plan.md`).

Other checks (all take `STATICS=<dir>` or default to the built editor):

- `source-stability-check.js` — source-text mapping stability over a wide fixture set (GFM tables, raw HTML,
  footnotes, maths, Mermaid, fence info strings, emoji and ZWJ sequences, combining characters, task lists, front
  matter, links/images/reference definitions, a 50k slice of `doc300k.md`, and every tenth CommonMark example;
  `--full` runs all 652 examples and the whole 300k document). Each document goes reading → visual → source
  twice and must come back byte for byte with no edit reported. It proves the mapping back to the original
  source holds, not that the first real visual edit serializes faithfully. CR never reaches the editor (the host
  normalizes line endings), so there is no CRLF fixture: CodeMirror would rewrite one and report an edit.
- `first-edit-check.js` — the same fixtures, each with a paragraph `EDITHERE` appended, loaded, switched to
  visual mode and edited for real (click, then type `X`). The flushed text must be Muya's export with only that
  keystroke added, and must match what `window.__typedownPendingNormalization()` (`services/normalization.ts`)
  predicted before the edit. Each fixture's verdict (`none` / `unknown` / `unsafe`) is kept in
  `first-edit-known.json`; a worse verdict fails, `--update` rewrites the baseline. `unsafe` entries are text a
  single keystroke would lose (today: an HTML entity in an HTML block, blank lines inserted into a multi-line HTML
  attribute; fence info strings and tabs are kept since the lexer fix). `harness.js` holds the fixtures and the stub-host page both use.
- `protected-payload-check.js` — the preserve documents of `docs/automation-fixtures/protected-payload.json` (links,
  images, reference definitions, autolinks, fence info strings and bodies, footnotes, task states, raw HTML
  attributes, front matter, maths, diagram blocks) each get one real keystroke in visual mode. Any `mustKeep` string
  missing from what Muya writes back must have been predicted `unsafe` before the edit; losses are found by substring
  search, not by the classifier, so a blind spot in the classifier cannot hide itself. Each document is also written
  through `ApplyDocumentEdit` and must get the verdict predicted on load.
- `origin-check.js` — every `MarkdownChange` carries `origin`: `user` after a real key, pointer, paste or drop
  event or a host editing command, `editor` for a change nothing the reader did caused (`services/changeOrigin.ts`).
  Loads, mode switches and late renders report nothing; an action counts for one report; a load forgets it.
- `apply-edit-check.js` — the page's half of an automation write (`ApplyDocumentEdit` → `DocumentEditApplied`): a
  stale `baseContentHash`, including typing the host has not heard of yet, is refused; otherwise the exact text is
  applied in visual, source and reading mode with no `MarkdownChange`, and the reply carries the SHA-256 of the
  source and the normalization verdict (`notEvaluated` in source mode, which has no Muya).

- `reading-source-check.js` — preserves exact source through reading/source/visual mode switches and
  save flushes, checks a list followed immediately by a heading and fenced code, checks a GFM table
  interrupting a paragraph, verifies a source-mode edit survives a full mode round trip, and verifies pending
  visual edits survive a mode switch. `handshake-check.js` also checks original source in `FileLoaded`.
- `security-check.js` — sends raw HTML, SVG, event-handler, `javascript:` URL and embedded-content payloads
  through the complete Markdown render path, then exercises KaTeX, Mermaid, Vega-Lite, flowchart.js and the
  sequence renderer with hostile labels; none may execute or leave active attributes/elements in the editor.
- `spec-check.js` — every CommonMark 0.31.2 example (652) imported, exported, imported and exported again. The
  second export must equal the first (importing what we just wrote must not keep changing the document), and the
  first must equal `spec-known.json`, the recorded behaviour of this editor. Muya is not a CommonMark renderer,
  so the baseline records what we do, not what the spec says; its point is that no editor change can quietly
  alter the document model of 652 documents. `--update` re-records, `--section <name>` or example numbers narrow
  the run. Three examples still do not survive a second import; they are marked in the baseline and are left
  alone on purpose, because none of them loses text and each settles after one more round:

  - **7 (Tabs)** `-\t\tfoo` — tabs indenting content into a code block inside a list item. Everyday tab use is
    fine (tab-indented nested lists, `-` followed by a tab, a tab-indented second paragraph all round-trip with
    the tabs turned into spaces, once). Fixing this one means expanding tabs by column width in the parser,
    which touches every indentation decision there is.
  - **96 (Setext headings)** `---` used as a rule with a setext underline on the next line. The writing itself is
    ambiguous — the spec uses it to probe that boundary — so changing it means re-deciding what those three
    dashes mean.
  - **117 (Indented code blocks)** an indented code block padded with blank lines; one leading blank line is
    dropped. The code itself is untouched.
- `style-check.js` — lays out `style-fixture.md` (headings, lists, tables, code, quote, maths, footnote, raw
  html) and measures every element: position, size, font, weight, colour, background. A block passes if it is
  within 2px of where it was and within 1px of its size; fonts and colours must match exactly. `--update`
  records, `--theme dark` keeps its own baseline, and `--against <dir>` compares two editor builds directly
  instead of against a baseline — that is how the Windows and Uno bundles are checked against each other:

      node style-check.js --against ../../../Typedown-Uno/Typedown.Uno/Assets/Editor

  The maths in the fixture is measured twice over: KaTeX writes a visible `.katex-html` tree and a hidden
  `.katex-mathml` one for screen readers. Since a whole-document load builds the page from an HTML string
  (see `docs/editor-performance.md`), the hidden tree is parsed as real MathML instead of as elements that
  merely happen to be called `math` — one element fewer, different metrics, and nothing visible changes. That
  is what the baseline was re-recorded for.
- `outline-follow-check.js` — reading mode has no caret, so the outline follows the page. Two things have
  to hold and it checks both: the heading reported moves with the page and comes back, and reporting it
  does not move the page. Its fake host is hostile on purpose — it scrolls to whatever `cur` a state report
  carries, which is what the first attempt fed it, so a heading routed back through that report starts the
  loop and the check sees the page moving on its own.
- `code-font-check.js` — code follows the font size setting and the tab size setting: in a code block, in
  inline code and in source mode, measured before and after a settings change. Source mode kept its own
  font size once created (CodeMirror 5) and never heard of the tab size; the Prism themes drew tabs two
  columns wide whatever the setting said. Two Store reviews.
- `theme-contrast-check.js` — inline code, table borders and dividers have to be visible against the page in
  each bundled theme: colours are composited the way the browser paints them and compared as contrast
  ratios. Light inline code was white at 70% on a near-white page (ratio 1.05), dark table borders 1.25;
  two Store reviews. Inline code now has its own variable, `--inlineCodeBgColor`, falling back to the
  code-block colour for themes without it. The style baselines were re-recorded for this: the fixture's
  `==highlight==` renders as `<mark>` now and the inline-code fill changed, nothing else moved.
- `anchor-link-check.js` — a link to a place in the same document: Ctrl+click while editing, plain click in
  reading mode, and the heading the fragment names lands at the top. Fragments are slugs of the heading text
  the way the export names them, may be percent-encoded, and duplicates number the same way. Two things
  used to defeat it: headings carry block keys as ids, not slugs, so `querySelector('#fragment')` found
  nothing; and the caret-follow rule scrolled back up on the Ctrl key-up, which Muya reports as a selection
  change. Five Store reviews.
- `inline-mark-check.js` — `==text==` is highlight: it renders as `<mark>`, survives a round trip through the
  editor unchanged, exports as `<mark>`, is stripped from the outline's heading text, and HTML with `<mark>`
  imports as `==text==`. Three Store reviews asked for it; the markers used to be plain text.
- `void-html-check.js` — a void HTML element (`<br>`, `<hr>`, `<img>`, `<wbr>` …) has no close tag, so a stray
  `</br>` in `<br></br>` is literal text, not the element's close. Pairing them dropped a character as it was
  typed and destroyed the editor on the next render (a reading-mode round trip). Each case must load back
  unchanged and survive a reading-mode toggle with the editor intact.
- `caret-check.js` — edit mode has to have a caret after every load: the editor focused, a selection inside
  it, and a typed key landing in the text. Checked for the first load and for a load arriving as a tab
  switch. The page side passes; a caret that is missing on Windows is the host holding XAML focus elsewhere.
- `export-tilde-check.js` — a single `~` is text, not strikethrough: a range like `1~6` or `2~5` (common in
  Chinese) survives export intact, and only `~~text~~` strikes through. The export lexer accepted one tilde,
  so an exported PDF struck through the middle of a number range. Checks the editor render and the export.
- `mhchem-check.js` — chemistry with mhchem's `\ce`, inline and as a display block, renders in the editor and in
  the exports (RenderXhtml). The extension was imported as its UMD file, which registers `\ce` on the CommonJS
  copy of KaTeX while the editor and the export render with the ES module copy, so every `\ce` was invalid.
- `reference-image-check.js` — a reference-style picture (`![alt][ref]`) loads when the document opens: its source
  text is not left marked failed once the picture is drawn, and a reference to a missing file is still marked
  failed.
- `link-image-check.js` — a picture inside a link (`[![alt](src)](url)`) is drawn inside the link, in its own box.
  Its icons were `<a>` elements inside the link's `<a>`, so a small document built from the HTML closed the link
  early and left the box empty (a big document is drawn in pieces and was spared).
- `export-pdf-check.js` — the exported HTML carries what the PDF is made from: images by absolute path (so
  they survive being opened from a temp file) and KaTeX fonts by real address (so a formula keeps its font).
  A PDF is then produced from that HTML with a document outline, and the outline is checked to have an entry
  per heading. Three Store reviews: PDF loses images, PDF has no bookmarks, the formula font changes.
- `print-check.js` — printing prints the editor page itself, in its own theme. Under print media none of the
  editing chrome is visible (block icons, tool bars, drag handles, syntax markers), every block is laid out
  even past the off-screen rule (which would print blanks), the page is white on a dark theme, and a long
  code line wraps instead of printing a scrollbar and losing its end (a Store review from 2022). Printing
  used to render an export with the GitHub stylesheet, which is why "the print did not look like the
  document".
- `outline-jump-check.js [--file doc.md]` — clicks every heading in the outline. Each has to land at the top
  of the window (16px below the edge, or as far as the page goes at the end of the document) and end up
  marked in the outline. The heading used to land 320px down, the caret's offset, so the section before it
  showed at the top and the follower marked that one: 3 of 40 at the top, 1 of 40 marked, before the fix.
- `open-position-check.js --file doc.md [--scroll N] [--line L] [--readonly 0|1] [--click "5.4.1"]` — opens a
  real document the way the host does, with a remembered caret and scroll offset, and records every move
  of the page and every outline report for the first seconds, then reads it with the wheel. The page has
  to settle where the host asked, the outline may not be left pointing at a place the page merely passed
  through, and no state report may arrive without a current heading. Written for a report that lost its
  outline highlight; what it found instead was the page leaving the remembered offset for the remembered
  caret a quarter of a second after landing — the load's own selection change, arriving through two timers,
  treated as a caret movement. It failed on that bundle (settled at 97, not 6000) before the fix went in.
- `idle-quiet-check.js` — an editor left alone must send nothing and re-render nothing. A stream of reports
  with nobody typing shows up as an outline that flickers and collapses, a selection that cannot be dragged
  because it dies on every render, and eventually a crash.
- `tab-stress-check.js` — twelve switches in a row, the way a tab bar is actually used: one editor element
  and one editable root throughout, and nothing sent once the switching stops.
- `tab-state-check.js` — what the host is told after a switch. The outline, the word count and the caret
  all reach the host through StateChange, and restoring a document already built produces no change of its
  own, so the report has to be asked for — at the wrong moment it goes to an editor that is no longer
  listening and the outline goes on describing the previous document, which is what shipped.
- `scroll-yield-check.js` — the hold that keeps a long document at its offset has to let go the moment the
  reader moves the page, by wheel, key, pointer or touch. Each gesture gets a page of its own: sharing one
  meant only the first met a live hold, and the check passed against a build with the guard removed.
- `tab-switch-check.js` — switching tabs between a long document and an ordinary one, both directions.
  `--big`, `--small`, `--rounds`. It reports **blocked time**, not only when the content appears: what a
  reader feels is how long the window stops answering, and work that lands after the content does still
  counts — measuring up to first paint once had me reporting a win that could not be felt. It switches with
  the text the editor reports back, not the file as written, because that is what the host stores and hands
  over, and anything else is a load of a different document.
- `legacy-issues.js` — the issues reported against the original Typedown, in one run: raw html renders with its
  inline CSS, a code block is in the saved markdown the moment it is typed, a table cell whose text is selected
  and deleted still takes input, PageUp/PageDown keep moving.
- `roundtrip-check.js '"md..."' ...` — import each markdown string into Muya and print blocks + exported markdown.
- `handshake-check.js` — LoadFile → FileLoaded handshake: FileLoaded carries the original source text and the
  load id, a superseded load leaks nothing, an edit afterwards reports MarkdownChange (exit code 1 on failure).
- `scroll-flash-check.js` — a tab switch hands the document over with the offset it was left at; this samples
  where the page actually is over the frames that follow. Laying out a long document goes on well past the
  first paint and the browser can drop the position with nobody scrolling, which showed as the top of the
  document appearing for a moment. `--trace` records every scroll call with its stack and watches the editor
  root for replacement — useful when hunting, misleading when timing, so it is off by default.
- `scroll-restore-check.js` — a load that carries `scrollTop` (remembered offset, read-only mode) lands at that offset instead of at the caret; a load without it starts at the top.
- `export-check.js '"md..."'` — run the HTML export pipeline and print the body (upstream #48 emphasis case by default).
- `scrollup-check.js` — Up arrow must scroll the caret back into view (upstream #51).
