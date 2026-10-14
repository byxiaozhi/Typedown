# Typedown.XamlUI 1.0.3

`nupkgs/Typedown.XamlUI.1.0.3.nupkg` is the vendored 1.0.1 package (upstream https://github.com/byxiaozhi/Typedown.XamlUI,
whose source has the same code) with two changes.

## 1.0.2: `Dispatcher.PostTask`


    var taskId = curTaskId++;                                        // 1.0.1
    var taskId = (uint)(Interlocked.Increment(ref curTaskId) - 1);   // 1.0.2

Callbacks are posted to a window's UI thread from any thread (await continuations, timers, the automation pipe).
Two threads posting at once could take the same number; the second `TryAdd` under it failed and its callback was
dropped - an await whose continuation never ran, the UI thread idle (E2E C01 hung a minute, about one full run in
eight; E2E D01 lost 1-2 of 8000 posts from 8 threads before, none after).

Made with this tool, which checks the instructions it replaces and changes nothing else (decompiling the original and
the patched assembly differs in that line only):

    dotnet run --project Tools/XamlUIPatch -- show  <Typedown.XamlUI.dll>
    dotnet run --project Tools/XamlUIPatch -- patch <1.0.1 Typedown.XamlUI.dll> <patched dll>

Both copies in the package (`lib/netstandard2.0`, `lib/uap10.0.18362`) were the same file and are replaced by the
patched one; the nuspec says 1.0.2. Everything else in the package is unchanged.

## 1.0.3: `Window.RemoveWindowMessageHook`

The list of a window's message hooks is created by the first `AddWindowMessageHook`. The caption buttons
(`CaptionControlGroup`) add theirs when they load and remove it when they unload; a window closed as it opens unloads
them before they loaded, and `RemoveWindowMessageHook` ran `Where` over the null list: an ArgumentNullException in a XAML
event handler, which ended the process (`XamlUnhandledException`, seen 2026-10-02 on hp with a window opened by a second
launch and closed at once). With the lock held, a null list now leaves the method as it would after removing:

    dotnet run --project Tools/XamlUIPatch -- show-remove  <1.0.2 Typedown.XamlUI.dll>
    dotnet run --project Tools/XamlUIPatch -- patch-remove <1.0.2 Typedown.XamlUI.dll> <patched dll>

Decompiled, the patched assembly differs from 1.0.2 in that method only (`if (_windowMessagehooks != null)` around the
removal). Both DLLs in the package are replaced; the nuspec says 1.0.3.

## Repacking: give the patched DLLs the time they were made

A patched DLL has the size of the one it replaces, and the package entry kept the original's time (2023-04-18). An
incremental build copies a reference only when its size or time differs, so after the switch to 1.0.3 a build on hp
kept the 1.0.2 DLL in `bin` and packed it into the installer (clean builds, as on CI, were not affected). The DLL
entries carry the time of the repack; when a later version is made, restamp them the same way, then delete the old
copy of that version from the NuGet cache (`%USERPROFILE%\.nuget\packages\typedown.xamlui\<version>`) on machines
that restored it before.
