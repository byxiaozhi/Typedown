# Speech marks

Speech marks are small notes in the text of a talk that say how to deliver it: where to pause, what to say slowly, what to stress, how long each part may take. They are plain text, so you can type them or add them from the Speech panel, and you can show the marked text to an AI assistant yourself: Caret never sends anything.

## Turn it on

**View > Speech mode** shows the marks as small chips and opens the Speech panel (also under **Speech marks** in the sidebar). The panel lists every mark, the time of each section and the cue list.

## The marks

| Mark | Meaning |
|---|---|
| `{pause 3s}` | a pause of three seconds (`{beat}` is half a second) |
| `{wait 5s: applause}` | time left for the audience |
| `{slow}...{/slow}`, `{fast}...{/fast}` | slower or faster |
| `{loud}...{/loud}`, `{soft}...{/soft}` | louder or quieter |
| `{emphasis}...{/emphasis}` | stress this |
| `{tone: dry humour}...{/tone}` | any tone, in your own words |
| `{cue: look at the back row}` | something to do or remember |
| `{wpm 130}` | speaking speed from here on, in words per minute |
| `{budget 3m}` | time allowed for this section, put after its heading |

The words are always English, whatever language the talk is in, so a file means the same to everyone. A mark written wrongly stays plain text; the panel counts these.

## Add marks

- *Right-click* in Speech mode opens the *Speech ring* with the marks to choose from. Selected text is wrapped in a pair.
- `Ctrl+Shift+.` adds a pause, `Ctrl+Shift+,` a beat and `Ctrl+Shift+E` emphasis.
- **Settings > Speech marks** lets you make your own marks and recipes (several marks in one click).

## Time, teleprompter, rehearsal

The panel adds up the time (words at your speed, plus pauses) against each section's budget. **View > Teleprompter** opens a window that scrolls the text at your pace, and a rehearsal records how long each part really took. **Copy for AI** copies the talk with a short explanation of the marks. The teleprompter has a help topic of its own in the Help menu.

## Starting points

**Library > Templates > Add starter templates** includes a speech (a toast) and a presentation (a talk or pitch) already marked, in every language of Caret.