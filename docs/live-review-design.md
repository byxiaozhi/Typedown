# Live review: see your changes as you edit (design)

Status: **proposal for the owner to confirm** (2026-10-09). Nothing here is built yet. Asked for by the owner: "normally people are adding or deleting text manually, and now it doesn't change anything; we could copy how Microsoft Word does it, more or less." Chosen from three options on the same day: **option B**.

## 1. What it is for

Today the review function writes marks only when asked: **Add comment**, and **Compare with another file** (the differences between an earlier version and the document on screen, as a review in a new tab). Typing and deleting never create a mark; that was a deliberate decision ("live tracking of every keystroke like Word is deliberately not planned").

People who edit a text expect to switch tracking on, write, and see what they changed, with a way to accept or reject each change before sending. Live review gives that without tracking keystrokes:

1. **Start tracking** remembers the document as it is now (the *baseline*).
2. While you edit, Caret compares the baseline with the document, a moment after you stop typing, and shows the differences: additions in green, deletions in red, replacements as old and new, each with the author and the day.
3. Each change can be **accepted** (it becomes part of the baseline) or **rejected** (the old text comes back into the document), one by one or all at once.
4. **Write review into the file** turns what is shown into ordinary CriticMarkup text (the marks `Compare with another file` already writes and the editor already draws), so the file can be sent to a colleague, who sees the same colours in Caret or any tool that reads CriticMarkup, and an AI assistant can read it too.

## 2. Rules that do not change

- Plain text. The file never holds anything but the document and, only when the user asks, CriticMarkup marks.
- Offline, no AI, nothing leaves the PC (a rule of the project: no AI inside Caret).
- Nothing is written into the document by tracking itself. The baseline is kept **outside** the file (section 5). Switching tracking off, or closing without writing the review, leaves the file as it is.
- Undo and Redo work as for any typing, with one exception that is said plainly: **Reject** changes the document, so it is one step of Undo; **Accept** changes only the baseline (the document text does not change), so the editor's Undo cannot take it back. Accept has its own way back instead (section 4, L2: "Undo accept").
- No interception of keystrokes: the editor, the IME, paste and the spell checker behave exactly as without tracking. Everything is derived by comparing two texts.

## 3. How the differences are found (nothing new to invent)

`Services/ReviewDiff.cs` already compares two texts: the lines first (Myers), then word by word inside a changed line that has a similar partner; a link, an image, a code span, an HTML tag or a formula counts as one word; a list marker, heading mark or quote mark stays outside the mark. It returns CriticMarkup text. Live review needs it to also return **the changes as a list** (kind, old text, new text, and where each sits in the baseline and in the document), because accept and reject work on one change at a time:

```
Hunk { Kind (added | deleted | replaced), OldText, NewText, OldStart/OldEnd (baseline), NewStart/NewEnd (document) }
```

The text version is made from the list, so the comparison and the colours of `Compare with another file` do not change.

When it runs: the host already receives the whole text with every `MarkdownChange` from the editor. A change in the text starts a timer (about 400 ms after the last one); when it fires, the comparison runs on a background thread, and a newer change cancels an older run. A document over a limit (proposed 300 KB) is compared by lines only, and a message says so. No work is done while tracking is off.

## 4. What the user sees, in steps (each one a separate pull request)

**L1: switch on, see the changes.**
- **Review > Track changes** (a toggle; also in the Review panel). It asks once for the name used in the marks (the Windows user name, editable, as for comments).
- The **Review panel** shows how many changes there are and lists them (first words of each, kind and colour); a click moves the document to it.
- In **Split view** the right-hand pane shows the document *with the changes drawn* (the baseline compared with the document, rendered like a review), while the left pane is the ordinary source. In the Visual view the list is there and the document is plain until L3.
- The status bar says "Tracking: 5 changes".
- Checked: the list and the pane follow typing, deleting, pasting and Undo; switching off clears it and changes nothing in the file.

**L2: accept and reject.**
- **Accept** on a change: the baseline takes the new text of that change, so it stops being a difference.
- **Reject**: the document gets the old text back (written through the editor, so it is one step of Undo and Redo).
- **Undo accept**: Accept does not touch the document, so the editor's Undo does not know about it. Caret keeps the last 20 baselines (in memory and in the saved copy of section 5); **Review > Undo accept** (and the same button in the panel) goes back one. Going back with the editor's Undo past a point that was accepted simply shows those changes again, because the baseline already holds the accepted text.
- **Accept all / Reject all** in the Review panel and menu.
- Checked: the document and the baseline agree after each action; Undo of a reject brings the change back; Undo accept brings an accepted change back into the list.

**L3: in the Visual view.**
- Added text is drawn in place with a CSS highlight (the way the spell checker draws its underline: nothing in the editor's own DOM changes, so nothing can get out of step with the content).
- A deletion cannot be text in the editor (it is not in the document), so it is a small red mark at the place, with the deleted text in a card on hover or focus, and **Accept** and **Reject** in the same card and in the right-click menu.
- Checked in the real app, dark and light.

**L4: write it down.**
- **Write review into the file** replaces the document text with the CriticMarkup version of the changes (one step of Undo) and stops tracking.
- **Stop tracking** keeps the document as it is and forgets the baseline (a dialog asks when there are changes).
- A returned file with someone else's marks is not touched: the marks in the text are ordinary text for the comparison, and the existing accept and reject commands keep working on them.

## 5. Where the baseline lives

In memory for the open tab, and as a small copy in Caret's own data folder (`Review\<hash of the full path>.md` for the text, and `Review\<hash of the full path>.json` next to it with the author, the start day, the last 20 baselines of Undo accept, and the first-seen day of each change as described in section 6), so that closing Caret, a crash and the recovery of unsaved work do not lose it. Never next to the user's file, never in the file. Removed when tracking stops, when the file is deleted or moved to the Trash by Caret, and after 90 days without being opened. A file that was renamed or edited elsewhere while tracking was on is compared with the baseline as it is: the differences then include that outside edit, and the panel says so.

An untitled document has no path: its baseline stays with the tab (and with the recovery copy) until it is saved, then moves to the path.

## 6. Dates and names

One name for the whole review (the one asked for when tracking starts, shown in Settings > Review). The date of a change is **the day the comparison first saw it**. It is kept with the change while the change stays the same, so it does not move to "today" every morning, and it survives closing and reopening Caret: the `.json` of section 5 holds, for each change, a short fingerprint and its first day. The fingerprint is made of the old text, the new text, up to 40 characters of the unchanged text before and after the change, and its **occurrence number** (the 1st, 2nd... change with that same old and new text, in document order), so two identical changes in different places (the same word deleted twice) keep their own days. After a restart the changes found are matched to the fingerprints in three rounds, each only for what the one before left unmatched: first by the **whole fingerprint**; then by old text, new text and **the text around the change** on either side (the other side was edited a little); and only then by old text, new text and occurrence number, and **only when that is unambiguous**: the number of saved fingerprints with that old and new text equals the number of changes found with it, so each pairs with exactly one. If two identical changes were saved and one has vanished, the survivor does not take the day of the one that went (the counts differ, so it is treated as new and gets the day it is first seen). A change that matches in none of the rounds (it was edited, or it is new) gets the day it is first seen. Fingerprints of changes that no longer exist are dropped. Time of day is not written, as in comments.

## 7. Why not follow every keystroke (option C)

Writing `{++text++}` into the document as you type needs the editor to intercept input, in Muya's own model, for every block type (tables, headings, lists, code), pasting, IME and Undo, and the deleted text would have to stay in the document. It is the biggest and riskiest of the three options and it would change what Caret writes into every file. We start with B; C is only worth it if people ask for it after L4.

## 8. Risks and what we do about them

| Risk | What we do |
|---|---|
| Large documents make the comparison slow | Background thread, a pause after typing, newer edits cancel older runs, a limit for the word-level comparison, a message when the limit is hit |
| A change cannot be mapped back to a place in the editor (L3) | The list in the panel always works; the in-place drawing only draws what it can map and the panel says how many it could not |
| Reject writes into the wrong place after the user kept editing | The reject checks that the document still holds the text the change was made from, and says so (and does nothing) when it does not |
| Several windows or tabs of the same file | The baseline belongs to the path; a second tab of the same file shows the same changes |
| Marks written by a colleague mixed with live changes | CriticMarkup in the text is plain text for the comparison; the existing commands handle those marks |
| The user forgets tracking is on | The status bar chip, the menu check mark, and a reminder when closing a document with changes |

## 9. Decisions for the owner

1. Is the **Split view** a good place for L1 (so the first version needs no change to the Visual view), with the Visual view following in L3?
2. The baseline in Caret's data folder (section 5) rather than inside the file as a hidden block: confirmed?
3. One name for the whole review, and the day each change was first seen: confirmed?
4. Order: L1, L2, L3, L4, with a Help topic for each step (the in-app Help menu is being added separately and records how each step works).

## 10. Tests planned

- .NET: hunks for added, deleted and replaced lines and words, for lists, tables, headings and code; the text made from the hunks equals what `Compare with another file` writes today (the existing tests); accept and reject operations on the baseline and the document; the 300 KB limit; the sidecar file (written, read, removed).
- jest: the changes view in the preview pane; the list.
- Real app after each step, in the usual guarded way: type, delete, paste, Undo, accept, reject, close and reopen with tracking on.