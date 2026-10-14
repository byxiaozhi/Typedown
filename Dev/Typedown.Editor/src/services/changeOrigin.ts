import transport from "services/transport";

// Where a change to the document text came from, as the page reports it with MarkdownChange:
//
//   user   - the reader did something that edits: a trusted key, input, paste, cut, drop or pointer event in the
//            page, or an editing command the host forwarded from its menus or shortcuts.
//   editor - the text changed with no such action before it: a re-render, a late asynchronous render, a
//            normalization. The host must not take it as the reader's edit (docs/automation-api-spec.md).
//
// The page cannot follow one action to the change it causes (Muya reports changes throttled, CodeMirror at once),
// so an action arms a flag and the next change report consumes it. A load clears it: the load's text is the
// host's, and so is whatever the editor reports for it.

export type ChangeOrigin = 'user' | 'editor';

/** Host messages that edit the document on the reader's behalf. */
const editingCommands = ['Cut', 'Paste', 'DeleteSelection', 'DeleteParagraph', 'Duplicate', 'Format', 'InsertImage',
    'InsertParagraph', 'InsertTable', 'Replace', 'UpdateParagraph', 'ImportFile'];
const inputEvents = ['keydown', 'beforeinput', 'paste', 'cut', 'drop', 'compositionend', 'pointerdown'];

let armed = false;

/** Records that the reader did something that may edit the document. */
export function noteUserIntent() {
    armed = true;
}

/** Forgets any pending action: the host is about to replace the content. */
export function resetUserIntent() {
    armed = false;
}

/** The origin of the change being reported now; consumes the pending action. */
export function takeChangeOrigin(): ChangeOrigin {
    const origin = armed ? 'user' : 'editor';
    armed = false;
    return origin;
}

let installed = false;

export function installChangeOriginTracking() {
    if (installed) return;
    installed = true;
    for (const type of inputEvents)
        window.addEventListener(type, e => { if (e.isTrusted) noteUserIntent() }, true);
    // Registered at start-up, before the components' own listeners, so the command counts before it edits.
    for (const name of editingCommands) transport.addListener(name, noteUserIntent);
    for (const name of ['LoadFile', 'SetMarkdown']) transport.addListener(name, resetUserIntent);
}
