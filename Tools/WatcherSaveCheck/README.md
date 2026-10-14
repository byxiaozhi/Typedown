# WatcherSaveCheck

Proves the file watcher never follows a save's own rename onto a temporary or backup file, while still
following a genuine user rename. A save replaces the file: the content goes to a temp file and the original
is renamed aside (on Windows, to a name like `Guide.md~RF….TMP`) before the new file lands. The folder
watcher sees that rename; following it put the editor on the backup temp — the reported bug. Linux's atomic
write does not emit that Windows rename, so the check exercises the rename handler's decision directly
against the exact events the app receives, and shows the old logic would have followed it. It also confirms
a real atomic save updates the content and leaves no stray files. Runs anywhere with the .NET SDK; it
compiles `SafeFile.cs` alone.

    cd Tools/WatcherSaveCheck && dotnet run
