import transport from "services/transport";

/**
 * Vim keys (Settings > Editor > Vim keys): full Vim in source mode (CodeMirror's own keymap), Vim-style moving
 * around in reading mode (services/vimReading). Not in the visual editor.
 *
 * Both hosts take some Ctrl shortcuts before the page sees them (Windows: a keyboard hook; Linux/macOS: the bridge's
 * capture listener). While Vim needs one of those, the page says so: the state goes to the host as "VimState", and
 * the bridge asks window.__typedownVimWants. Keep VimKeys.cs (Windows) in step with the lists here.
 */
export type VimState = 'off' | 'normal' | 'insert' | 'visual' | 'replace' | 'reading';

/** Ctrl+key Vim uses in normal and visual mode: block visual, redo, half pages, jumps, increment, scrolling. */
export const NORMAL_CTRL_KEYS = ['v', 'r', 'd', 'u', 'o', 'a', 'x', 'e', 'y', 'b', 'f', '['];
/** Ctrl+key the reading mode moves with. */
export const READING_CTRL_KEYS = ['d', 'u', 'e', 'y', 'f', 'b'];

let state: VimState = 'off';

export const vimState = () => state;

export function setVimState(next: VimState) {
    if (next === state) return;
    state = next;
    transport.postMessageNoDiff('VimState', { state: next });
}

/** Whether Vim needs this key press now (the host must not take it). Insert mode keeps the application's shortcuts. */
export function vimWantsKey(e: { key: string, ctrlKey: boolean, shiftKey: boolean, altKey: boolean, metaKey?: boolean }) {
    if (!e.ctrlKey || e.altKey || e.metaKey || e.shiftKey) return false;
    const key = e.key.toLowerCase();
    if (key === '[') return state !== 'off' && state !== 'reading';
    if (state === 'normal' || state === 'visual') return NORMAL_CTRL_KEYS.includes(key);
    if (state === 'reading') return READING_CTRL_KEYS.includes(key);
    return false;
}

(window as any).__typedownVimWants = vimWantsKey;

/**
 * An application command from Vim (:w, :q, :wq, / and n/N in reading mode). The Linux/macOS bridge draws the find
 * bar itself and answers what it can (window.__typedownBridgeVim); the rest goes to the host as "VimCommand".
 */
export function vimCommand(command: 'write' | 'quit' | 'writeQuit' | 'find' | 'findNext' | 'findPrevious') {
    const bridge = (window as any).__typedownBridgeVim;
    if (typeof bridge === 'function' && bridge(command)) return;
    transport.postMessageNoDiff('VimCommand', { command });
}
