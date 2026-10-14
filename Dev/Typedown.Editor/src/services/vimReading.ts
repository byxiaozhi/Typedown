import { vimCommand } from "services/vim";

/**
 * Reading mode with Vim keys: moving around a document that cannot be edited. j/k a few lines, d/u (Ctrl+D/U) half a
 * page, f/b/Space (Ctrl+F/B) a page, Ctrl+E/Y a line, gg/G the ends, ]] / [[ the next / previous heading, / find,
 * n/N the next / previous match, m{a-z} / '{a-z} marks. A count goes before any of them (5j, 3]]).
 *
 * Returns the keydown listener; the caller adds it while the page is in reading mode with Vim keys on.
 */
export function createReadingKeys(scroller: () => { top: () => number, by: (dy: number) => void, to: (y: number) => void, height: () => number },
    headings: () => HTMLElement[]) {
    let count = '';
    let pending = '';
    const marks = new Map<string, number>();
    const LINE = 20;

    const next = (direction: 1 | -1, times: number) => {
        const s = scroller();
        const all = headings();
        for (let i = 0; i < times; i++) {
            const tops = all.map(h => h.getBoundingClientRect().top);
            const index = direction > 0 ? tops.findIndex(t => t > 12) : tops.map((t, j) => [t, j]).filter(([t]) => t < -2).map(([, j]) => j).pop() ?? -1;
            if (index < 0) return;
            s.to(s.top() + tops[index] - 8);
        }
    };

    return (e: KeyboardEvent) => {
        const target = e.target as HTMLElement | null;
        if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable && !target.closest('#editor'))) return;
        if (e.altKey || e.metaKey) return;
        const s = scroller();
        const half = () => Math.round(s.height() / 2);
        const page = () => Math.max(s.height() - 40, LINE);
        const key = e.key;
        const times = Math.max(1, parseInt(count || '1', 10));
        let handled = true;

        if (e.ctrlKey) {
            if (e.shiftKey) return;
            switch (key.toLowerCase()) {
                case 'd': s.by(half() * times); break;
                case 'u': s.by(-half() * times); break;
                case 'f': s.by(page() * times); break;
                case 'b': s.by(-page() * times); break;
                case 'e': s.by(LINE * times); break;
                case 'y': s.by(-LINE * times); break;
                default: handled = false;
            }
        } else if (pending) {
            const what = pending;
            pending = '';
            if (what === 'g' && key === 'g') s.to(0);
            else if (what === ']' && key === ']') next(1, times);
            else if (what === '[' && key === '[') next(-1, times);
            else if (what === 'm' && /^[a-zA-Z]$/.test(key)) marks.set(key, s.top());
            else if ((what === "'" || what === '`') && marks.has(key)) s.to(marks.get(key)!);
            else handled = key !== 'Escape' ? false : true;
        } else if (/^[0-9]$/.test(key) && (key !== '0' || count)) {
            count += key;
            e.preventDefault();
            return;
        } else {
            switch (key) {
                case 'j': s.by(LINE * 3 * times); break;
                case 'k': s.by(-LINE * 3 * times); break;
                case 'd': s.by(half() * times); break;
                case 'u': s.by(-half() * times); break;
                case 'f': case ' ': s.by((e.shiftKey ? -1 : 1) * page() * times); break;
                case 'b': s.by(-page() * times); break;
                case 'G': s.to(Number.MAX_SAFE_INTEGER); break;
                case 'g': case ']': case '[': case 'm': case "'": case '`': pending = key; e.preventDefault(); return;
                case '/': vimCommand('find'); break;
                case 'n': vimCommand('findNext'); break;
                case 'N': vimCommand('findPrevious'); break;
                case 'Escape': break;
                default: handled = false;
            }
        }
        count = '';
        if (handled) {
            e.preventDefault();
            e.stopPropagation();
        }
    };
}
