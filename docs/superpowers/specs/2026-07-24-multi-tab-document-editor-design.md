# Multi-Tab Document Editor Design

**Date:** 2026-07-24

**Project baseline:** Official `byxiaozhi/Typedown` `main` at `a1baeae`.

## Goal

Add a first-version document-tab experience to Typedown while preserving its existing Windows file operations and editor behavior.

## Confirmed user behavior

- Each tab represents one document.
- A tab displays the file name.
- Hovering a tab displays the full file path.
- An untitled document is labeled `未命名` and has no file path.
- The tab `+` button, the existing New File menu command, and its shortcut all create a new untitled document.
- Opening a file that is already open activates its existing tab instead of creating a duplicate.
- Tabs can be switched and reordered by dragging.
- Closing a dirty tab presents `Save`, `Don't Save`, and `Cancel`.
- Closing the last tab closes the Typedown window.
- Restart recovery is out of scope for this version.

## Scope

### In scope

- Tab bar UI and styles consistent with the existing editor.
- Tab state for file path, display name, Markdown text, dirty state, and the active tab.
- Opening files from existing C# commands into tabs.
- New untitled documents from all confirmed entry points.
- Switching and reordering tabs without losing document text.
- Save, Save As, discard, and cancel behavior for the active tab.
- Updating the tab name and path after Save As.
- Closing the host window after the last tab is closed.

### Out of scope

- Restoring tabs after application restart.
- Multiple native WebView/editor instances.
- Cross-window tab synchronization.
- New persistence, synchronization, or third-party dependencies.

## Architecture

Use one existing editor instance and add a React-side tab coordinator.

React owns the in-window tab collection and the active document snapshot. Each tab contains:

- a stable tab ID;
- a nullable canonical file path;
- a display file name;
- Markdown text;
- dirty state;
- cursor state needed to restore the editing position.

The existing C# layer remains responsible for file dialogs, filesystem reads and writes, recent-file history, save errors, and window lifetime. It sends file-loading and save-result messages through the existing WebView transport. React sends the active tab's content and path when a save operation needs to be performed.

The editor component remains mounted once. Switching tabs first snapshots the current editor state into the active tab, then loads the target tab's text, image base path, and cursor. This avoids multiple editor instances and keeps the implementation compatible with the current React 17 and WebView communication model.

On Windows, file identity comparison is case-insensitive and uses canonical full paths. Two untitled documents are allowed because they have no path identity.

## Message/data flow

1. Startup or an existing Open command causes C# to provide the loaded document's canonical path, text, and image base path.
2. React creates or activates the corresponding tab and loads the text into the single editor.
3. Editor changes update the active tab snapshot and mark it dirty.
4. Switching tabs snapshots the current tab, then loads the selected tab snapshot into the editor.
5. Save or Save As sends the active tab identity and content to C#. C# returns the actual saved path and success/error information.
6. On success, React marks the tab clean and updates its display name and tooltip path.
7. Closing a dirty tab waits for the user's Save, Don't Save, or Cancel choice. Closing the last tab sends a close-window message to C#.

All new messages must be narrowly scoped, documented at their call sites, and use the existing transport rather than introducing a second IPC mechanism.

## Error handling

- A failed save keeps the tab open and dirty and surfaces the existing host error dialog.
- Cancel leaves the tab collection, active tab, and editor content unchanged.
- A missing or unreadable file does not silently replace the active tab; the host reports the error and the current tab remains available.
- A stale file path after Save As is replaced only after C# confirms the successful save path.

## Verification strategy

Write focused tests before implementation for the tab-state behavior:

- creating a new untitled tab;
- opening and deduplicating a canonical file path;
- switching tabs while preserving each document's text;
- reordering tabs;
- dirty close decisions for Save, Don't Save, and Cancel;
- closing the last tab and emitting the window-close message;
- updating the display name and tooltip path after Save As.

Run the focused frontend tests first, then the frontend build, then the relevant C# tests that are available in the environment. Manual verification must cover the real Windows host because WebView messages and file dialogs are not fully represented by frontend unit tests.

## Collaboration and GitHub workflow

The implementation follows Superpowers' sequence: design approval, isolated branch, written plan, small implementation tasks, test-first changes, task review after each task, and final review before integration.

The GitHub teaching workflow uses the official Typedown repository as `upstream` and the user's fork as `origin`. The local branch will be created from the clean upstream `main`; no changes from the previous local `feature/multi-tab` branch are used. The work will be pushed to the fork and opened as a Draft Pull Request against the upstream repository.

Each implementation task should have a focused commit, a test result, and a review checkpoint. The modifying agent writes code and tests; the review agent checks the requirements and diff, then sends actionable findings back to the modifying agent for correction and re-review.

## Constraints

- Preserve existing project style and file organization.
- Do not add dependencies unless the current code cannot support the behavior without one.
- Keep the first version limited to the confirmed behavior above.
- Do not modify or reset the unrelated repository at `E:\self\repo`.
