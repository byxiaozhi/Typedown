# Speech marks: design

Status: 2026-10-07. The design and phase 1 (steps 1a to 1f) were confirmed by the owner the same day. Step 1a is built (PR #67); the rest is not.

## 1. What it is for

A speaker prepares a talk as text. Text does not say where to go fast, where to slow down, where to wait, where the joke is, or how a sentence should sound. A transcript made by speech-to-text has the same gap: it keeps the words and loses the humour, the irony and the feel.

Speech marks put those decisions into the text itself, as plain characters in the `.md` file. Three things follow from that:

1. The speaker can see and plan the delivery while writing.
2. Caret can do arithmetic on the marks (how long the talk takes, where the pace changes), offline.
3. The marked text can be pasted into any AI assistant by the speaker, who then explains how they want to deliver the talk. Caret never talks to an AI.

It follows the review function (CriticMarkup marks, built in small steps, used only on request): same layers, same way of working.

## 2. Rules that do not change

| Rule | What it means here |
|---|---|
| No AI, no network, no keys | Every number comes from counting words and adding seconds. Nothing is "understood". The marks are text; the speaker pastes them to an AI themselves. |
| Only on request | Speech mode is switched on by the user and is off by default. Opening a file never turns it on, even if the file contains marks. Nothing is inserted, changed or removed unless the user presses a command. |
| Plain text survives | The `.md` file is the only storage. No sidecar is needed to read the marks. Any editor, git, email or chat shows them as readable text. |
| A file explains itself | Marks the user invents are defined **inside the document** (section 3.7), with their meaning in words. The user's own library lives in Settings, but a document never depends on it: on another PC, or in front of an AI, the file still says what every mark means. |
| Four languages | Every UI string exists in English, French, Spanish and Polish (`Strings/*/AppResources.resw`); `LocalizationTests` already checks the files agree. The mark keywords in the file are always English (section 3.5). |
| Undo works | Inserting or removing marks is typed into the page the way the review comment is, so Ctrl+Z works. |
| Tests with every step | jest for the editor, `Dev/Caret.ConverterTests` for the .NET rules, a `CHANGES.md` entry, and a check in the real app with the guarded UI script. |

## 3. The syntax

### 3.1 Choice: curly braces with a word

```
{pause 2s}              a pause of two seconds
{slow}...{/slow}        this part slowly
{tone: dry irony}...{/tone}
```

Why braces, and why a letter after the brace:

- Every CriticMarkup opener is `{` followed by a symbol: `{++`, `{--`, `{~~`, `{==`, `{>>`. A speech mark is `{` followed by a letter or `/`. The two can never be confused, and they can sit inside each other (a speech mark inside an added text, or a comment that mentions one).
- Square brackets were rejected: `[pause]` is a Markdown shortcut link. Parentheses were rejected: they are ordinary text in a speech. HTML comments (`<!-- pause -->`) were rejected: invisible in every renderer, long, and lost when text is copied from a rendered page. Symbols such as a pause sign were rejected: hard to type, hard for the speaker to remember, font problems.
- The keywords follow what speakers, directors and the W3C speech standard (SSML: break, prosody rate, prosody volume, emphasis) already call these things, so an AI recognises them without a legend. The export still carries a legend (section 6.3).

### 3.2 Grammar

```
mark      = "{" [ "/" ] name [ " " value ] [ ":" " " text ] "}"
name      = one of the words below, case does not matter
value     = a duration (2s, 1.5s, 1m, 1m30s) or a number (140)
            (a pause is 0.1 s to 10 min; a `{wpm}` is 40 to 400 words per minute, so the timing formula never divides by zero;
             a value outside its range makes the mark invalid, and an invalid mark stays plain text, see 3.4;
             `{wpm}` and `{budget}` need their value: without it they are not marks, there is no default;
             `{pause}` and `{wait}` may leave it out and use 1 s and 3 s; `{beat}` takes none, it is always 0.5 s)
text      = free words, no "{", no "}", no line break
```

A mark is inside one paragraph, as CriticMarkup marks are. Paired marks (`{slow}` ... `{/slow}`) open and close in the same paragraph. An opener with no closer in its paragraph runs to the end of that paragraph; a closer with no opener is ignored. A selection over several paragraphs gets one pair per paragraph.

### 3.3 The vocabulary

The rule behind the list: **what Caret can count has fixed words; what only a human or an AI can interpret is free text.** That keeps the arithmetic honest and the expression open.

| Group | Mark | Meaning | Counted by Caret? |
|---|---|---|---|
| Time | `{beat}` | very short pause, 0.5 s | yes |
| | `{pause}` / `{pause 3s}` | pause, 1 s by default, or the stated length | yes |
| | `{wait}` / `{wait 5s: laugh}` | time left for the audience (laugh, applause, a question); 3 s by default. Same as a pause for the arithmetic, but shown and reported as "audience" | yes |
| Pace | `{slow}...{/slow}` | slower than the baseline, speed x 0.75 | yes |
| | `{fast}...{/fast}` | faster than the baseline, speed x 1.25 | yes |
| Volume | `{loud}...{/loud}`, `{soft}...{/soft}` | louder, quieter | shape only (no time effect) |
| Stress | `{emphasis}...{/emphasis}` | stress this word or phrase | shape only |
| Tone | `{tone: dry irony}...{/tone}` | any tone or intent in the speaker's own words: joke, irony, warm, serious, urgent, humble | not counted; read by people and AI |
| Cue | `{cue: look at the back row}` | anything to do or remember: slide, gesture, prop, sip of water, breathe | not counted; shown in the cue list |
| Settings in the text | `{wpm 140}` | speaking speed in words per minute from here on | yes |
| | `{budget 3m}` | time allowed for this section (the heading it is under) | yes |
| Your own words | `{define very-slow pace 50%: half speed}` | defines a new mark for this document (section 3.7); the new word is then used like a built-in one | by its kind: pace and pause yes, span and note no |

Notes on what was left out of the first proposal and why:

- `{whisper}`, `{joke}`, `{smile}`, `{serious}`, `{sarcastic}`, `{breathe}`, `{slide 4}` as built-in fixed words. They are many near-synonyms, and every built-in word is one more thing everybody has to learn. `{soft}`, `{tone: ...}` and `{cue: ...}` carry all of them, with one-click presets that fill in the free text. A speaker who wants `{whisper}` as a word of their own makes it (section 3.7).
- Markdown bold and italic are **not** read as speech marks. They mean whatever the writer meant for slides or handouts; a delivery decision should not depend on how another tool draws bold.
- The built-in words and their numbers (slow 0.75, fast 1.25, beat 0.5 s, pause 1 s, wait 3 s) are the same for everybody, so two people's estimates of the same file agree. A document can change those numbers for itself with a definition line (section 3.7); Settings cannot, because a number that lives only on one PC would make the same file mean different things.

### 3.4 Escaping and unknown marks

- `\{pause\}` is plain text, as for any Markdown character (the editor already treats `\{` as an escaped brace).
- Marks are never read inside code spans or fenced code blocks, and not inside the note of a CriticMarkup comment (`{>>@Ann: put {pause} here<<}` stays a comment). They are read inside added or replaced CriticMarkup text.
- **Only the built-in words, and the words the document itself defines (section 3.7), are marks.** `{pauze 2s}` (a typo), `{width=50%}` (Pandoc image attributes), `{.red}` or `{{ liquid }}` are left alone as ordinary text, and keep their braces. A word in the user's Settings library that the document does not define is also left alone. A known word with a bad value (`{pause fast}`) is also left as text. The panel counts these near-misses (section 5.4) so a typo is found, but nothing changes them.
- Text a speaker types after a closer or inside a free note is never parsed as Markdown by the mark: the note is plain text, like a CriticMarkup comment.

### 3.5 Keywords are always English

The file format must be the same for everyone who exchanges files, so `{pause}` is never `{pauza}` or `{pausa}`. The speech itself can be in any language. The UI (chips, buttons, panel, hints) is translated in all four languages.

### 3.6 Clash check with other syntax

| Syntax | Clash? | Why |
|---|---|---|
| CriticMarkup `{++ -- ~~ == >>` | no | different second character (section 3.1) |
| Markdown, GFM | no | `{}` have no meaning; `\{` escapes |
| Pandoc attributes `{.x} {#id} {key=value}` | no | only the fixed words are marks |
| Kramdown `{: .x}`, Liquid `{{ }}`, `{% %}` | no | second character is not a letter |
| Math `$a_{i}$` | no | math is read first and consumed whole |
| HTML | no | `{}` is not special; tags are read first |
| MDX / JSX | **yes, if the file is fed to MDX** | MDX treats `{pause}` as a JavaScript expression and fails. Speeches are not MDX files; listed as a risk |
| Heading anchors | **yes** | `## Opening {budget 3m}` makes the generated anchor `opening-budget-3m` in sites that build anchors from headings. See risks |

### 3.7 Marks the user makes: definitions and recipes

The built-in set is deliberately small. Everything else is the user's to add, in two ways.

**A. New words, with a defined behaviour.** The user invents `very-slow`, `word-by-word`, `long-pause`, `whisper`, `wave`. The word is written in the document as a definition, one per line, normally in one paragraph at the top:

```
{define very-slow pace 50%: about half speed, every word clear}
{define word-by-word pace 60% +0.3s: one word at a time, a short beat after each}
{define long-pause pause 5s: the pause before the key line}
{define whisper span: barely audible, as if telling a secret}
{define wave note: wave to the room}
```

Grammar: `{define name kind [value]: meaning}`. Only four kinds exist, because these are the only things Caret can calculate or draw:

| Kind | Used as | Value | Effect on the time estimate |
|---|---|---|---|
| `pace` | pair `{name}...{/name}` | speed as a percentage of the baseline (`50%`), optionally `+0.3s` added after every word | slower or faster, plus the extra seconds per word |
| `pause` | single `{name}` | a length (`5s`) | adds those seconds |
| `span` | pair | none | none; it is a style and a meaning (a voice, a mood, a manner) |
| `note` | single | none | none; it is a cue |

Rules:

- A name is 2 to 24 characters: lower-case letters, digits, hyphen, starting with a letter. It cannot be `define`, and it cannot be a built-in word except in the override form below.
- The meaning (after the colon) is required for a new word, and is one line of the user's own words. It is what an AI reads and what the legend of "Copy for AI" prints, so the user is told to write it for a reader who has never seen the talk.
- Limits: speed 10 % to 300 %, extra per word up to 2 s, a pause 0.1 s to 10 min. A definition outside them is shown as text and listed in the hints.
- A definition applies to the whole document wherever it stands. If a name is defined twice, the first one is used and the hints list the second.
- A document may change the numbers of a built-in word for itself with a shorter line of the same kind: `{define slow pace 60%}`. This override form needs no meaning (the built-in word already has one) and is allowed only for `slow`, `fast`, `beat`, `pause` and `wait`, with their own kind.
- Colour and icon are **not** in the file. They come from the user's library; on a PC without it, a mark is drawn in a plain style for its kind, with a colour taken from its name, so the same name looks the same everywhere. The meaning, which is what matters for an AI, travels in words.
- A name is a mark in a document only if the document defines it. This keeps the clash analysis of section 3.6 true however many words the user invents.

**B. Recipes: several marks in one click.** A recipe is a template stored in Settings, for example "dramatic reveal": `{pause 5s}{soft}{emphasis}{text}{/emphasis}{/soft}{wait}`. `{text}` stands for the selected text (a recipe without it is inserted at the caret). Using a recipe writes the ordinary marks, and any definitions they need, into the document; nothing new is in the file, so recipes need no explanation to a reader or an AI. The "joke with room" structure of the first proposal is a built-in recipe. A recipe is checked when it is saved: it must contain only known marks, balanced pairs, and `{text}` at most once.

Removing, renaming or editing something in the library never changes a document; each document keeps its own definitions.

## 4. What Caret computes (rules only)

The input is the document text. Steps, in order:

1. Take the text as if all review changes were accepted (the existing `ReviewMarks.Resolve(text, Accept)`): deleted text is not spoken, added text is, comments are not.
2. Find the marks (section 3.2), ignoring code.
3. Decide what is spoken. **Spoken:** paragraphs, list items, quotes, link text. **Not spoken:** headings (they are labels, with a checkbox "headings are spoken" in the panel), code, images and their alt text, URLs, review comments, the text of `{cue}`/`{tone}` marks, and table cells (left out, section 7).
4. Count words: a run of letters or digits, with inner apostrophes and hyphens ("don't", "well-known") counted as one. A number such as `2026` counts as one word.
5. Time of a stretch of words = words / (baseline wpm x speed factor) x 60 seconds. Nested pace marks multiply (slow inside fast: 0.75 x 1.25). Marks the user defined as `pace` use their own percentage the same way, and add their seconds-per-word on top (`word-by-word` at 60 % with +0.3 s: ten words take 10 / 130 x 60 / 0.6 = 7.7 s plus 3 s).
6. Add the seconds of `{beat}`, `{pause}`, `{wait}` and of any user-defined `pause`.
7. Group by section: a section is a heading and everything up to the next heading of the same or higher level. Compare with its `{budget}`.

Defaults: baseline 130 words per minute until a `{wpm N}` mark or the panel field says otherwise. Budget light: green up to 100 % of the budget, amber up to 110 %, red above. The light is never colour only: the panel writes "over by 0:20".

**Accuracy, said plainly:** a word count is a rough measure of speaking time. Languages differ (Hungarian and Polish words are longer than English ones, so words per minute is lower), numbers and names take longer than they count, and every speaker is different. Expect within about 15 % once the speaker has set their own baseline; the first estimate before that can be further off. Step 3 improves this by measuring the speaker's own pace in a rehearsal and offering it as the baseline. Languages written without spaces (Chinese, Japanese) are not supported by this word count.

## 5. What the user sees

### 5.1 Speech mode

- A toolbar button (a speech-bubble icon; **not** a microphone, because Caret records no sound) and a View menu entry switch **Speech mode** on and off. The state is a setting, off by default, like Split.
- **Off:** the editor behaves exactly as today. `{pause 2s}` is ordinary text. This is deliberate: documents that never use the mode cannot be affected by it.
- **On:** the marks are recognised and drawn; the Speech card appears.
- Code view always shows the raw text. In Split, the preview follows Speech mode.

### 5.2 Chips and marked text in the View

Never raw braces while the caret is elsewhere; the raw mark shows, in gray, when the caret is inside it (the same behaviour as the review marks, so the text stays editable and the file is never rewritten).

| Mark | Drawn as |
|---|---|
| `{pause 2s}`, `{beat}`, `{wait}` | A small chip with an icon and the length: "pause 2 s", "audience 3 s". The chip is drawn with generated content, so it is not part of the text and does not disturb caret offsets |
| `{slow}` text | letter-spacing widened and a dotted underline |
| `{fast}` text | letter-spacing narrowed and a dashed underline |
| `{loud}` text | larger and heavier |
| `{soft}` text | smaller and lighter |
| `{emphasis}` text | accent colour with a solid underline |
| `{tone: x}` text | a soft coloured band with the tone word as a small label before it |
| `{cue: x}` | a chip with an icon and the first words; the full text in the tooltip |
| `{budget 3m}`, `{wpm 140}` | a quiet chip |
| A user's own `pace` mark | text drawn with letter-spacing that follows its speed (wider when slower, narrower when faster) and a dotted or dashed underline, in the colour and with the icon chosen in the library |
| A user's own `pause`, `span`, `note` | the same chip or band as their built-in cousins, in the colour and icon chosen in the library; a neutral style for the kind when the library does not have it |
| `{define ...}` lines | one quiet chip each, "very-slow = 50 % speed", with the meaning in the tooltip; the lines sit together in the first paragraph |

Accessibility: nothing relies on colour alone (every style has a shape, spacing or label as well); chip text meets 4.5:1 contrast in light, dark and Windows high-contrast themes (system colours are used in forced-colors mode); every chip has a text tooltip and an accessible name in the UI language; the Speech card and the ring are fully keyboard-operable (5.3, 5.4).

### 5.3 The Speech card: the full list, always on screen

The Speech card in the sidebar is the complete, plain way to add marks. It is also what a keyboard or screen-reader user starts from, and it is where the ring (5.4) falls back when a group has too many items. It stays on screen (a flyout would close after each insertion). Groups: Time, Pace, Volume and stress, Tone, Cue, Mine. The user's own marks and recipes appear in their group, in order of speed or length, next to the built-in ones.

- **Point marks** (pause, beat, wait, cue, a user's `pause` or `note`) are inserted at the caret.
- **Paired marks** wrap the selection. With nothing selected, `emphasis` wraps the word at the caret and the other pairs wrap the sentence at the caret.
- A selection that cannot be wrapped without cutting other syntax (it touches a link, an image, code or math) is not wrapped; the mark goes at the end of the paragraph instead, as the review comment does.
- Using a user-defined mark also writes its definition line into the document, if the document does not have it yet (one Undo step with the insertion); the chip that appears says so.
- Tone presets: joke, irony, warm, serious, urgent, humble. Cue presets: look at the audience, next slide, gesture, show an object, drink, breathe. They fill in the free text, which can be changed.
- **Joke** is a built-in recipe: `{tone: joke}setup {beat} punchline{/tone}{wait 3s: laugh}`, so room after the laugh is the default.
- **Add mark...** at the bottom opens Settings > Speech marks (5.5).
- **Edit > Remove all speech marks**: one step in Undo, removes marks and definitions and leaves the spoken text as it is. This is how a speaker gets a clean text for a handout or an article.
- Shortcuts: a few direct ones for the most used marks and for opening the ring, decided in the build after checking the editor's keymap. **Not** Ctrl+Alt+letter: on Polish, Hungarian and French keyboards that combination is AltGr and types characters, including `{`.

### 5.4 The Speech ring: right-click as a gesture

A long menu is slow to read and slow to hit. In Speech mode, right-click on text opens a **ring** around the pointer instead (an interactive mockup was shown with this proposal).

**Layout.** Six petals around the pointer: Time, Pace, Volume, Tone, Cue and Mine (the user's favourites and recipes). Hovering or clicking a petal grows an **outer arc** of its items. In the centre, a small round button "Cut Copy Paste" opens the ordinary right-click menu at the same spot.

**Direction means something.** Items on an arc are in a fixed order that follows what they mean: Pace from slowest to fastest clockwise, Time from shortest to longest, Volume from quiet to loud. A user's own marks land at their natural position automatically: add `very-slow` at 50 % and it sits left of `slow`, `very-fast` right of `fast`. After a few days the hand knows "flick left = slower" without reading.

**Live preview, from the same rules.** Hovering an item shows the selected text in place with that style (temporary, not written to the file), the meaning line (the user's own words from the definition), and the time before and after for the selected text: "0:05 -> 0:09". This is arithmetic (section 4), not a guess.

**It shows what is already there.** Items that are already applied to the selection or at the caret are lit. Clicking a lit item removes that mark. So the ring is also a read-out of "what is on this sentence", and combinations (slow + soft + a tone) are built by clicking several items while the ring stays open.

**Two ways to use it.** *Click:* the ring stays open until Esc, a click outside, or the middle button. *Flick:* hold the right button, move toward a petal and then an item, release to apply; the ring closes. The flick is the fast way once the hand knows it.

**It never takes the ordinary menu away.**
- The ring opens on plain text and on selected text. On a misspelled word, a review change or comment, a link, an image or a table, the ordinary menu opens (so spelling suggestions and Accept/Reject keep working) with one extra item, "Speech marks...", that opens the ring.
- **Shift + right-click** (or the centre button) is always the ordinary menu.
- When Speech mode is off, nothing changes at all.

**Keyboard and assistive technology.** The context-menu key and Shift+F10 open the ring at the caret. Arrow keys move around the petals, Enter opens an arc, arrows move along it, Enter applies, Esc closes; the digits 1 to 9 pick an item of an open arc. Every petal and item is a real button with a text name, and its state ("applied", "not applied") and meaning are announced. Nothing depends on colour: lit items are also bold with a check mark, and user items carry a star. Touch and pen: press and hold opens the ring.

**Looks.** The ring uses the app's theme colours and the system colours in high-contrast themes, and scales with display scaling and text size. It opens with a short grow (about 150 ms) unless Windows animations are off. It moves away from the window's edges so it is never cut off. An arc shows at most eight items; if a group has more, the eighth petal is "More...", which opens that group in the Speech card. The order is fixed, so positions can be learned.

**Why a ring and not something else.** A pie or ring menu puts every item at the same distance from the pointer and makes the targets large, and the direction of a movement can be remembered, which a list cannot offer. A honeycomb of hexagons was considered: it holds more items without crowding, but the direction-means-meaning idea is lost and it is less familiar. If testing with many user marks shows arcs overflowing, the honeycomb is the fallback for the groups that overflow; this is a layout change, not a change of the file format.

**How it is built.** Drawn in the editor page as an overlay outside the text (so the selection and focus are not disturbed), from the user's library sent by the host as plain data. The place of the selection is remembered as paragraph id plus text offsets, as in the review script. Applying a mark goes through the same insertion code as the Speech card. The centre button sends the page's ordinary "right-click report" to the host, which shows the ordinary menu as it does today.

### 5.5 Settings > Speech marks: "My marks"

A new page in Settings, in the same style as the others. It is where the user adds elements of their own, so the set is as flexible as they want.

- **My marks.** A list with Add, Edit, Duplicate, Delete and reordering. Per mark: *name* (the word in the file, checked against the rules of 3.7 and for duplicates), *kind* (pace, pause, span, note), *value* (speed % and extra seconds per word, or a length), *meaning* (one line, required), *group* (where it appears: Time, Pace, Volume, Tone or Cue; defaults from the kind), *colour* (a set of eight swatches that have been checked for contrast in light, dark and high-contrast themes; free colours are not offered because they can fail contrast), *icon* (a set of sixteen), and *Pin to Mine* (shows in the ring's Mine petal and gets a digit shortcut there). A live preview shows how it looks in a sentence and what it does to the time of a ten-word sentence. Errors appear next to the field ("That name is already used", "Pace must be between 10 % and 300 %").
- **Starter marks.** One click adds a set the user may want: very-slow (50 %), very-fast (160 %), word-by-word (60 %, +0.3 s a word), long-pause (5 s), whisper (span), sing-song (span), wave (note).
- **Recipes.** A list of templates (3.7 B) with a test button that shows the marks they produce on a sample sentence. Includes the built-in joke recipe, which can be copied but not changed.
- **Speed.** The default words per minute for documents that have no `{wpm}` mark.
- **Import / Export.** The library as a file on the PC, to move it to another PC or give it to a colleague. Local files only.
- The library is stored in the user's Settings like the other settings, never in a document and never sent anywhere. At most 60 marks and 20 recipes.

### 5.6 Timing panel and the shape of the talk

Part of the Speech card, next to the outline:

- Total estimated time against the total budget, the baseline wpm (editable; the edit writes or updates `{wpm N}` at the top of the document, as one Undo step), and the checkbox "headings are spoken".
- One row per section: title, words, estimated time, budget, light.
- **The shape strip:** the talk from top to bottom, one bar per paragraph, height in proportion to its time. Bar offset is pace against the baseline (left slower, right faster), fill is volume, ticks are pauses and audience time. A glance shows where it is all fast and where the quiet moments are. Clicking a bar scrolls the editor to that paragraph. This is drawn from the same numbers as the table; no extra analysis. (A strip aligned to the editor's scrollbar is possible later; the panel version is robust against the editor redrawing its paragraphs.)
- **Hints:** a short list of plain facts, never style advice: a section over its budget; a `{tone: joke}` with no `{wait}` or `{pause}` right after it; more than a minute of `{fast}` in a row; a pause longer than 10 s (probably a typo); a `{word}` that looks like a mark but is not one (a typo, or a word that is in the user's library but not defined in this document, with a one-click "add the definition"); a name defined twice (sections 3.4 and 3.7).

## 6. Phases

Built the way the review function was built: small pull requests, each with tests, a `CHANGES.md` entry, translations, a check in the real app (the guarded UI script, with the dev data and clipboard backed up and restored), Auto-fix on, and CodeRabbit comments settled before the owner merges.

### Step 1: marks, your own marks, the ring, timing (phase 1: confirmed by the owner on 2026-10-07; 1a built, 1b to 1f not started)

| PR | Content | Tests |
|---|---|---|
| 1a | Speech mode setting and View menu item; parser rule and renderer for the built-in marks **and for `{define ...}` lines and the marks they define**; chips and styles; labels sent from the host in the UI language | jest for the parser (every mark, every kind of definition, round trip text unchanged, code and escapes, inside CriticMarkup, near-misses, an undefined name is text); the page in a browser; real app: marks typed by hand in Code view appear as chips in View |
| 1b | The Speech card with the full list; insert/wrap page script (paragraph id and offsets, as the review script); writing a missing definition with the mark; Remove all speech marks; a few shortcuts | .NET tests for the markup builder, definition rules and removal; page script in a browser; real app |
| 1c | **Settings > Speech marks:** My marks, starter marks, recipes with test button, default speed, import/export; the library sent to the page and shown in the Speech card | .NET tests for the rules of names, kinds and limits, recipe checking, and the library file; the four `.resw` files; real app (add a mark, see it in the card) |
| 1d | **The Speech ring:** overlay in the page, petals and arcs in fixed meaningful order, lit state, click and flick, live style preview, keyboard and screen-reader behaviour, the centre button to the ordinary menu, the exceptions (misspelled word, review mark, link, image, table, Shift+right-click) | jest for the geometry and ordering (user marks land in place) and the keyboard model; the page in a browser; real app, with real mouse input and scan-code key presses |
| 1e | `SpeechTiming` (plain .NET, compiled into the tests like `ReviewMarks`) with built-in and user-defined kinds; timing panel; wpm and budget; traffic light; the ring's "before -> after" time | .NET tests for counting, pace maths, nesting, per-word seconds, sections, review text accepted first, locales (comma and dot decimals) |
| 1f | Shape strip; hints; **Copy for AI** (section 6.3) | .NET tests for the shape data, hints and export text; real app |

The ring comes before the timing on purpose: it is the part that makes the tool pleasant, and its style preview works without any timing. The time figures are added to it in 1e. Each PR is usable on its own: after 1b a speaker can already work; after 1c with their own marks; after 1d with the ring.

**Copy for AI is pulled forward into step 1** (it was step 3 in the first proposal): it is only the text, a short legend and the planned numbers, and it is what delivers the owner's main goal. After step 1 the speaker can already hand a marked talk to an AI.

### Step 2: teleprompter

Big text on a dark or light page, scrolling at the planned pace (each paragraph passes in its planned time), marks drawn as large cues, a visible countdown to the next pause, start/stop on Space, speed adjust, and a step-by-paragraph mode that does not scroll by itself (for people who prefer it, and when reduced motion is asked for in Windows). Page Up/Page Down work, because presentation clickers send them. Optional mirror mode for a glass prompter. The first decision of step 2 is a separate window for a second screen or an overlay in the main window; a second screen is the real use, so a separate window is likely. Still no network, no audio.

### Step 3: rehearsal capture and the AI export with numbers

- **Rehearse** is a mode of the teleprompter. The speaker reads aloud and taps a few keys: Right arrow or Page Down = on to the next paragraph (this gives the time per paragraph), Space = a pause started or ended (the real length of the pause taken), Up/Down = "slower"/"faster than planned here", L = "laugh happened", X = "stumbled, did it again".
- Caret records only **what the speaker pressed and when**, and writes planned against actual per paragraph into a plain Markdown file beside the speech, `name.rehearsal.md`, one block per run with the date: times, differences, extra pauses, taps, and the measured words per minute (with a button to adopt it as the baseline).
- **Copy for AI** then adds the planned and actual numbers.
- **Cue cards / speaker script** for printing: large text, marks as printed symbols and words.

What this does and does not give: the speaker is the sensor. The timing is as exact as their taps (about a second or two per paragraph), not per word. It carries the feel of a delivery (where it ran long, where the room laughed, where they stumbled) as text an AI can read, without any audio.

### 6.3 Copy for AI

Plain text put on the clipboard (the speaker pastes it where they want): a one-line header saying it is a speech script with delivery marks; a legend of the marks used in the document, one line each, in English (for the user's own marks, the meaning text from their definition lines); the baseline, planned total and planned section times; then the document text with the marks as they are in the file. An option writes the marks as words (`(pause, 2 seconds)`) for tools that dislike braces. Nothing is sent anywhere by Caret.

## 7. Deliberately left out, and what cannot work offline with rules

| Idea | Decision | Why |
|---|---|---|
| Microphone, voice analysis of volume or speed, detecting laughs | **Not built, and not planned** | Caret has no audio. A microphone permission also changes what the Store listing and the privacy policy can say, and it is not "rules and arithmetic". The rehearsal taps give the same facts that matter, supplied by the speaker |
| Judging humour, irony, tone or "how it sounds" | **Impossible with rules** | No rule can understand a joke. Caret stores what the speaker says they intend; reading it is for the speaker and, if they paste it, their AI |
| Speech-to-text of a rehearsal | **Not built** | needs a speech model; contradicts the no-AI promise |
| Reading the script aloud with a synthetic voice to hear the plan | Not now; an owner decision | Windows has local voices, so it would work offline, but it is speech technology and a possible conflict with the "no AI" wording of the Store pitch. Humour cannot be carried by it anyway |
| Automatic Speech mode, or suggestions to add marks | **Never** | "Only on request" |
| Table cells in the time count | Left out | tables are rarely spoken as written; the speaker can put the spoken text in a paragraph |
| CJK word counting | Left out | needs a dictionary, not a rule |
| Changing the numbers of the built-in words in Settings | Not offered | a number that lives on one PC makes the same file mean different things; a document can override them in itself (3.7) |
| Free colours and free icons for user marks | Not offered | colours can fail contrast in dark or high-contrast themes; a checked set of eight colours and sixteen icons is used |
| Marks with formulas, several numbers, or any behaviour other than pace, pause, style or note | Not offered | Caret can only calculate those four; anything richer is words in `{tone: ...}` or `{cue: ...}`, which people and AI read |
| User-made groups and petals | Later if asked | the six groups keep positions learnable; "Mine" is the user's own place |
| Sharing marks online, a library of marks from a server | **Never** | no network. Export and import of a local file only |
| A separate shortcut for every user mark | Not offered | the ring's Mine petal gives digits; direct shortcuts stay few (AltGr) |
| Slides, PowerPoint speaker notes | Not in this feature | the Convert hub already reads PowerPoint notes into Markdown; marking them up afterwards works with this feature as it is |
| SSML export | Not planned | possible later from the same marks; only useful with a voice engine |

## 8. Risks

| Risk | What could go wrong | What we do |
|---|---|---|
| Clash with other syntax | A mark read where it was not meant, or another tool's `{...}` read as a mark | Built-in words and words defined in the document only (a name in the user's library is not a mark in a document that lacks its definition); letter after the brace; not read in code; escape `\{`; Speech mode off by default so existing documents are untouched. MDX is the one known tool that fails on `{pause}`; stated in the user documentation |
| Round trip through other editors | An editor that rewrites the text could escape or reflow braces | The marks are plain characters; Caret never rewrites a file on open. Test: save, reopen, and compare in Caret; check a copy through the clipboard and through the Convert hub; note editors that escape `{` |
| Heading anchors and outline | `## Opening {budget 3m}` changes generated anchors and the outline label | The outline label hides marks; the user documentation says to use **Remove all speech marks** before publishing; `{budget}` may sit anywhere in its section, not only in the heading |
| Export reveals the marks | HTML or PDF export of a marked talk shows `{pause 2s}` to readers | Check in step 1a what export does with them; either strip speech marks in exports when Speech mode is on, or warn; decided in 1a with the owner |
| Editor redraws paragraphs | Page elements are lost between a click and a dialog (met in the review work) | Remember the paragraph id and text offsets, never elements; same code pattern as the review script |
| Caret behaviour near marks | Backspace/Delete across a hidden mark; selection that cuts a mark | Same hide/show-when-caret-inside behaviour as the review marks; the palette refuses unsafe selections; tested in the real app |
| Spell check, search, word count | `pause`, `emphasis` flagged in a Polish text; Find matching inside marks; the status-bar word count including marks | Mark text is excluded from spell check; Find finds inside marks (accepted, as for review marks); the status-bar count is checked in 1a and corrected if it counts mark words |
| Wrong time estimates | The speaker trusts an estimate that is 20 % off | The panel says "estimate", asks for the speaker's own baseline, and step 3 measures it |
| Accessibility | Colour only, small chips, mouse only | Section 5.2; keyboard-complete palette; text labels and tooltips; reduced motion honoured in step 2; high-contrast themes checked |
| Localisation | A string missing in one language; a unit or decimal formatted differently | All four `.resw` files in the same PR; `LocalizationTests`; the file format itself is language-neutral (section 3.5). Known: numbers in the UI follow Windows regional settings |
| Performance | Re-timing a long talk on every key | Debounced, one pass over the text, off when Speech mode is off |
| Privacy wording | A feature named "speech" read as recording | No microphone icon, no audio code; the privacy text gets one sentence saying so when step 1 ships |
| Own marks drift apart | The same name means different things in two documents, or the library and a document disagree | By design each document keeps its own definition and the library never edits documents; the hints offer "add or update this definition from my library" as an explicit click |
| A hand-edited definition is wrong | A typo in a `{define}` line turns marks into plain text | The definition is shown as text and listed in the hints with the reason ("speed must be between 10 % and 300 %") |
| Definitions clutter the file | A talk starts with a block of lines | One paragraph at the top, one line per mark; **Remove all speech marks** removes it with the marks; the legend for an AI is built from the same lines |
| The ring gets in the way | Right-click no longer gives the ordinary menu | Only in Speech mode; Shift+right-click, the centre button and the extra item on special targets keep the ordinary menu one gesture away; the Speech card stays as the plain alternative |
| The ring is hard to use | Small targets, precision, no mouse, screen reader | Large petals; keyboard model and announcements; press-and-hold for touch; every action also exists in the Speech card |
| Too many items in an arc | The ring crowds as the library grows | At most eight per arc and a "More..." petal into the Speech card; honeycomb as a fallback layout for groups that overflow (5.4) |
| Settings page grows large | A big page to build and translate | One page with four parts; the rules live in plain .NET with tests; all strings in the four languages in the same PR |
| Recipes make bad text | A template that writes broken or unbalanced marks | Checked when saved (known marks, balanced pairs, one `{text}`), with the test button |
| Scope growth | The idea has many attractive extras | The phases above; anything else goes to the owner first |

## 9. Decisions for the owner

1. **Syntax:** curly braces with a word; the small built-in set (3.3); tone and cue as free text; **the user's own marks as `{define ...}` lines inside the document** (3.7), so a file always explains itself.
2. **Speech mode:** off by default, manual switch, nothing automatic (5.1).
3. **The right-click ring** in Speech mode, with the exceptions, and the ordinary menu always one gesture away (5.4).
4. **Phase 1:** PRs 1a to 1f as in section 6 (marks, card, Settings page with My marks, ring, timing, strip and Copy for AI).
5. **Defaults:** 130 words per minute; headings not spoken; amber at 100 to 110 %.
6. **Export:** how HTML and PDF export treat the marks (decided with the owner in 1a, section 8).
