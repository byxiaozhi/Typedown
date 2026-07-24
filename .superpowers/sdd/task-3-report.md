# Task 3 Report

## Scope

Implemented Task 3 in `E:\self\playground\typedown-original\.worktrees\multi-tab` on branch `feature/multi-tab`:

- added a single `TabCoordinator` that owns tab state and keeps one editor instance;
- wired editor markdown/cursor callbacks back into React tab state;
- switched `FileViewModel` New/Open/Save/SaveAs command producers to tab events;
- kept the existing remote invoke path (`ActivateTab`, `SaveTab`, `CloseWindow`) and the existing `LoadFile` editor message.

## RED

1. Added `Dev/Typedown.Editor/src/components/Tabs/TabCoordinator.test.tsx`.
2. Verified an initial RED when `TabCoordinator` did not exist yet.
3. Tightened the test harness so the suite failed for real coordinator behavior instead of Jest mock factory issues.

## GREEN

Implemented:

- `Dev/Typedown.Editor/src/components/Tabs/TabCoordinator.tsx`
- `Dev/Typedown.Editor/src/components/Editor/index.tsx`
- `Dev/Typedown.Editor/src/App.tsx`
- `Dev/Typedown.Core/ViewModels/FileViewModel.cs`

Key outcomes:

- startup settings create exactly one tab and one editor instance;
- `DocumentLoaded` opens a canonical-path tab or activates an existing one;
- editor changes update only the active tab snapshot;
- tab switching calls `activateTab` before posting `LoadFile`;
- failed `saveTab` keeps the active tab dirty;
- `NewTabRequested` creates a new untitled tab without replacing existing tabs;
- clean last-tab close calls `CloseWindow`;
- `OpenFileCommand` emits `DocumentLoaded` with `{ path, text, basePath, dirty }`;
- backup-recovered open state stays dirty via the emitted `dirty` flag.

## Verification

### Focused frontend tests

Passed:

```powershell
node node_modules/react-app-rewired/bin/index.js test --watchAll=false --testMatch "**/tabModel.test.ts" --testMatch "**/TabCoordinator.test.tsx"
```

Result: `2` suites passed, `22` tests passed.

### Frontend build

Passed:

```powershell
node node_modules/react-app-rewired/bin/index.js build
```

Result: production build completed successfully.

Note: build still reports pre-existing lint warnings in Muya/vendor files and `sequence-diagram-snap.js`; no new task-specific build warning remains.

### C# build

Not passing in this environment; blocker recorded, not claimed green:

```powershell
dotnet build Typedown.sln
```

Observed environment blockers:

- access denied reading `C:\Users\temp\AppData\Roaming\NuGet\NuGet.Config`
- `Microsoft.VisualStudio.JavaScript.Sdk` could not be resolved from this environment

## Files changed

- `Dev/Typedown.Editor/src/components/Tabs/TabCoordinator.tsx`
- `Dev/Typedown.Editor/src/components/Tabs/TabCoordinator.test.tsx`
- `Dev/Typedown.Editor/src/components/Editor/index.tsx`
- `Dev/Typedown.Editor/src/App.tsx`
- `Dev/Typedown.Core/ViewModels/FileViewModel.cs`
- `.superpowers/sdd/task-3-report.md`

## Self-review

- kept a single editor instance; no per-tab Muya/CodeMirror mount was introduced;
- stayed on the existing IPC surfaces (`transport` + `RemoteInvoke`), with no second channel or new dependency;
- preserved existing editor behaviors for search, export/import, settings listeners, Muya, and CodeMirror;
- kept C# build status honest: attempted, blocked by environment, not reported as passing.

## Review Fix Follow-up

### Issues addressed

1. `Editor/index.tsx` no longer feeds externally synchronized markdown/cursor back into parent tab state as if they were user edits.
2. `TabCoordinator.tsx` no longer posts `LoadFile` from React during tab switches; the active document now changes through the existing controlled `Editor` props path, while `ActivateTab` remains the host-side state sync.
3. Added a focused `Editor` test that renders the real `Editor` component with thin Muya/CodeMirror mocks and verifies external prop changes do not call parent edit callbacks. The failed-save coordinator test now explicitly asserts the tab remains dirty.

### RED

After adding the new focused tests and tightening the coordinator expectation, this command failed as expected:

```powershell
node node_modules/react-app-rewired/bin/index.js test --watchAll=false --testMatch "**/tabModel.test.ts" --testMatch "**/TabCoordinator.test.tsx" --testMatch "**/Editor/index.test.tsx"
```

Meaningful failures observed:

- `external markdown and cursor sync does not call parent edit callbacks` failed because `onMarkdownChange` was called during external prop sync.
- `switching calls activateTab before loading the target document into the editor` failed because the coordinator still emitted `post:LoadFile`.

### GREEN

Minimal fixes applied:

- added callback-suppression refs in `Dev/Typedown.Editor/src/components/Editor/index.tsx` so external prop sync does not trigger parent edit callbacks;
- kept user-edit callbacks intact after the external sync completes;
- removed the invalid coordinator-side `LoadFile` post and kept the tab switch path as `TabCoordinator -> Editor props` plus `remote.activateTab`.

### Verification evidence

Focused tests passed:

```powershell
node node_modules/react-app-rewired/bin/index.js test --watchAll=false --testMatch "**/tabModel.test.ts" --testMatch "**/TabCoordinator.test.tsx" --testMatch "**/Editor/index.test.tsx"
```

Result: `3` suites passed, `24` tests passed.

Frontend build passed:

```powershell
node node_modules/react-app-rewired/bin/index.js build
```

Result: production build completed successfully with only pre-existing vendor/library warnings.

### Files updated in review fix

- `Dev/Typedown.Editor/src/components/Editor/index.tsx`
- `Dev/Typedown.Editor/src/components/Editor/index.test.tsx`
- `Dev/Typedown.Editor/src/components/Tabs/TabCoordinator.tsx`
- `Dev/Typedown.Editor/src/components/Tabs/TabCoordinator.test.tsx`
