# README screenshots

Three languages (zh-Hans, en, ja) × four scenes (visual, source, outline, dark), taken in **one running app** through the
automation API only: the interface language (`settings set ui.language`), the document (`open` this language's sample,
`close` the others), the view (`view --mode … --side-pane …`), the theme and the window size. Nothing is restarted.

- `shots-linux.sh` — the Uno (Linux) edition under Xvfb + xfwm4, in an isolated profile.
  `APPDIR=<published Typedown.Uno folder> OUT=<folder> Tools/Screenshots/shots-linux.sh`
- `shots-windows.ps1` — the installed Windows app, run in the logged-on session (a scheduled task), with the user's
  Typedown data copied aside first and put back at the end, file for file. It stops if Typedown is running. Its work
  folders (`E:\src\…`) are the build box's; `SHOTS_OUT` sets the output folder.
- `samples/` — the sample document in each language.
