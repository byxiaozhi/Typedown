# Automation client examples

Direct clients of the local automation API (`docs/automation-api-spec.md`), without typedownctl (or its MCP server).
Turn the API on in Typedown first: Settings > General > Allow local automation.

- `typedown_client.py`: Python 3.8+, standard library only. `list`, `read`, `replace-text`.
- `typedown_demo.py`: a guided tour on top of `typedown_client.py` - writes a demo file section by section, edits,
  shows a rejected stale write, undoes and redoes, switches the views and a few settings, then saves and closes it and
  puts the view and settings back. `--pause SECONDS`, `--step` (Enter between steps), `--keep`.
- `Typedown-Client.ps1`: Windows PowerShell 5.1+. `Connect-Typedown`, `Initialize-Typedown`, `Invoke-Typedown`,
  `Set-TypedownText`.

Both show the one rule every writer must follow: read, write against the revision you read, and when the write fails
with `revision_conflict` or `match_count_mismatch`, read again and decide again from the new text. Never resend an edit
computed from old text.

Tested: `ClientExampleTests` runs the Python example against a pipe server (including a reader typing between its read
and its write); E2E EX01 runs both against the Windows test host. The demo was run against Typedown.Uno 1.2.0 on Linux.
