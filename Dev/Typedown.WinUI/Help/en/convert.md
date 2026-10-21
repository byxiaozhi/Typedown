# Convert documents and emails

Caret turns documents into Markdown: a clean text that keeps headings, lists, tables, links and footnotes, and leaves out fonts and layout. The result is usually a small part of the original size, easy for people to read and for AI assistants to take in with far fewer tokens. Everything is converted on your PC: nothing is uploaded.

## Documents

Open **Convert to Markdown** in the sidebar and drop files or a whole folder, or use **Choose files...**. Word, Excel, PowerPoint, PDF and CSV are supported. Each result shows its size before and after and an estimate of its AI tokens. **Copy all for AI** puts everything on the clipboard as one text.

You can also right-click a file, several files or a folder in *File Explorer* and choose **Convert to Markdown** (not available when your organization turns it off).

The Markdown files are saved next to the originals, or in a folder you choose. A file that is already there is never overwritten.

## Outlook emails

Open **Outlook emails** and choose emails saved from Outlook (`.msg`) or other mail programs (`.eml`). Caret turns the whole conversation into one file: each reply as its own message, oldest first, without signatures, legal disclaimers and "external sender" banners, with the attachments converted in place.

**Mask personal data** (on by default) replaces names, email addresses, phone numbers, bank account numbers and ID numbers with markers such as `[PERSON-1]`, the same marker for the same person, so the conversation stays readable when you paste it into an AI assistant. It works with fixed rules and check digits, not with AI. People are found from the senders and recipients of the email and from greetings and sign-offs, so a name that only appears in the middle of a sentence is not found. Check the result before you share it.

Emails in English, French, Spanish, Polish and Portuguese are understood, and the rules are plain files a company can extend.

## Drop files anywhere

You do not have to open the Convert page first: drag Word, PDF, Excel, PowerPoint or email files (or a whole folder) over the Caret window, anywhere, and release them when **Convert to Markdown** appears. A small tray at the bottom right lists them, each with a progress bar and, when the pointer is over it or the keys are in it, **Open**, **Reveal in File Explorer** (the folder button) and **Compare**. A file that fails says why, with **Retry**; **Clear list** empties the tray and Esc closes it.

**Compare** shows the original as plain text on the left and the Markdown on the right, and scrolls them together. Above them are the choices that were used: whether images were taken out and, for an email, whether personal data was masked and how many names, addresses, numbers and IDs were found (they are bold and underlined in the Markdown). Esc closes it. Use it to see what the conversion kept, dropped or masked before you share the result.

## When something does not convert

A file protected by a password, or damaged, is listed with the reason. A scanned PDF with no text cannot be read: Caret does not do text recognition.