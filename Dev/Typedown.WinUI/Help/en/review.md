# Review: comments and changes

The review function works like the review tools of a word processor, but everything is written into the document as plain text, so a colleague, another editor and an AI assistant can read it too. The marks use a public convention called CriticMarkup:

| You see | Written in the file as |
|---|---|
| green added text | `{++added++}` |
| red deleted text | `{--deleted--}` |
| a replacement | `{~~old~>new~~}` |
| a yellow comment | `{==the text==}{>>@Name 2026-10-07: the note<<}` |

Nothing is stored anywhere else, and the review never starts by itself: typing and deleting do not create marks.

## Add a comment

Select text, right-click, **Add comment** (or **Review > Add comment**). Write the note and check your name (it starts as your Windows user name). The text becomes a yellow highlight with the note beside it. It is typed into the document, so **Undo** works.

## Compare with another file

**Review > Compare with another file...** asks for an earlier version of the document (for example the copy you sent) and who made the changes. Caret writes the differences between that version and the document on screen as a review *in a new tab*; your document is not changed. Use it to see what a colleague changed in the file they returned. If tabs are turned off (**Settings > Tabs and windows**), the review takes the place of your document in the window instead, so save your document first.

## Track changes as you edit

**Review > Track changes** (also a button in the Review panel) remembers the document as it is now. While you edit, a moment after you stop typing, Caret shows what you changed: the right-hand pane of the **Split** view draws the document with additions in green and deletions in red, each with your name and the day, and the Review panel lists the changes and counts them (also in the status bar). Nothing is written into the file. **Stop tracking** forgets the starting version and leaves the document as it is. The list is also a way to move through the document: *click a change* in the list and Caret jumps to it in the editor and highlights it in the right-hand pane, however long the document is. **Previous change** and **Next change** step from one to the next. The ✓ beside a change accepts it (it becomes part of the starting version and leaves the list); the ✗ rejects it (the old text comes back in the document, and **Undo** works). **Undo accept** takes back the last accepted changes. **Accept all changes** and **Reject all changes** do the same for every tracked change at once. Differences inside blocks of code can only be accepted or rejected all together.

In the Visual view the changes are drawn in the text itself: added text has a green background, and a small red mark stands where text was deleted. Point at a change or at a mark and a card shows the old text, who and when, with **Accept change** and **Reject change**. Clicking a change in the Review panel scrolls to it and outlines it for a moment. A change that Caret cannot find in the text of the page is not drawn there, but it stays in the list and in the Split view.

Tracking goes on when you close the file and open it again: Caret keeps the starting version, your name and the day each change was first seen in its own data folder (never in your file, and not for a document that has never been saved), and the changes are listed again when the file is opened. A change keeps the day it was first seen; it does not become today's every morning. If the file was edited in another program in the meantime, those edits show up as changes too, and the panel says so. **Stop tracking** removes what was kept, and so does moving the file to the Trash from Caret; what is not opened for 90 days is cleaned away.

**Write changes into the document** turns the tracked changes into review marks in the text itself: each change becomes `{++added++}`, `{--deleted--}` or `{~~old~>new~~}` followed by your name and the day it was first seen, the same marks **Compare with another file...** writes. It asks first, it is one step of **Undo**, and tracking stops. From then on they are ordinary marks: send the file to a colleague, and accept or reject them one by one (right-click a change) or all at once (**Accept all changes**, **Reject all changes**). Differences that could not be marked stay as plain text, and the window says so.

## Accept and reject

- *Right-click a change* (green, red or a replacement): **Accept change** keeps what it says (additions stay, deletions go); **Reject change** puts the old text back.
- *Right-click a comment:* **Delete comment**.
- **Review > Accept all changes**, **Reject all changes** and **Delete all comments** do it for the whole document, as one step of Undo.

## Hide the marks

**Settings > Editor > Show review marks** turns the colours off; the marks then show as the plain text they are.

## Good to know

- The marks are text, so a file with a review can be saved, sent and opened in any editor.
- A returned file with a colleague's marks is read the same way: their changes show in colour and you accept or reject them.