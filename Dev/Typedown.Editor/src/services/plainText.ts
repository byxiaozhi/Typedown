/**
 * Copy as plain text: what the selection reads as, without Markdown. The text comes from the page as drawn, not from
 * the source, so it is the same in visual and reading mode:
 *  - the syntax markers (`#`, `**`, `[`…`](url)`, `$`, raw HTML tags) are left out, whether they are shown or not;
 *  - a formula, a code block and a diagram give their source, once (not the drawn formula as well);
 *  - an image gives its alt text, a footnote reference `[1]`;
 *  - an ordered list keeps its numbers, a bullet list gets "• ", a task "☐ " or "☑ "; nested items are indented;
 *  - a table row is its cells separated by tabs (a spreadsheet pastes that as cells);
 *  - blocks are separated by a blank line, list items and table rows by a line break.
 *
 * Selection.toString() was used before: it gave a heading's `#` in visual mode, a formula three times over (the
 * source and two drawn copies), no image, and no list numbers.
 */

/** Elements that are syntax, tools or drawn copies of a source kept elsewhere. */
const SKIP = [
    '.ag-remove', '.ag-output-remove', '.ag-math-marker', '.ag-front-icon', '.ag-code-copy', '.ag-language-input',
    '.ag-math-render', '.ag-container-preview', '.ag-footnote-backlink', '.ag-image-container', '.ag-ruby-render',
    '.ag-html-preview', '.ag-copy-remove', '.ag-tool-bar', '[data-role="hr"]', 'input:not(.ag-task-list-item-checkbox)',
].join(',');

const BLOCK = new Set(['P', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'PRE', 'FIGURE', 'BLOCKQUOTE', 'TABLE', 'UL', 'OL', 'DIV', 'SECTION']);

/** The alt text of an image written as `![alt](src)` (or `<img alt=…>`). */
function imageAlt(el: Element): string {
    const raw = el.getAttribute('data-raw') ?? '';
    const md = /^!\[((?:\\.|[^\]\\])*)\]/.exec(raw);
    if (md) return md[1].replace(/\\(.)/g, '$1');
    return el.querySelector('img')?.getAttribute('alt') ?? '';
}

/** A list item's own number, from the list in the page (a partial selection's copy of the list starts at its first
 *  selected item). */
function itemNumber(li: Element, doc: Document): number {
    const original = (li.id && doc.getElementById(li.id)) || li;
    const list = original.parentElement;
    const start = parseInt(list?.getAttribute('start') ?? '1', 10) || 1;
    const items = list ? Array.from(list.children).filter(c => c.tagName === 'LI') : [];
    const at = items.indexOf(original);
    return start + Math.max(0, at);
}

class Writer {
    private blocks: string[] = [];
    private line = '';

    text(s: string) { this.line += s; }

    /** Ends the current block: `gap` lines apart from the next one. */
    end(gap: 1 | 2) {
        const text = this.line.replace(/[ \t]+$/gm, '');
        this.line = '';
        if (!text.trim()) {
            // Nothing new: a wider gap still applies after what came before (a list ends with a blank line).
            if (gap === 2 && this.blocks.length) this.blocks[this.blocks.length - 1] = '\n\n';
            return;
        }
        this.blocks.push(text, gap === 2 ? '\n\n' : '\n');
    }

    result() {
        this.end(1);
        return this.blocks.join('').replace(/\n{3,}/g, '\n\n').replace(/\s+$/, '');
    }
}

function inline(node: Node, out: Writer) {
    if (node.nodeType === Node.TEXT_NODE) {
        out.text((node.textContent ?? '').replace(/\u200b/g, ''));
        return;
    }
    if (!(node instanceof Element)) return;
    if (node.matches(SKIP)) return;
    if (node.classList.contains('ag-inline-image')) { out.text(imageAlt(node)); return; }
    if (node.classList.contains('ag-inline-footnote-identifier')) { out.text(`[${node.textContent?.replace(/[[\]^]/g, '') ?? ''}]`); return; }
    if (node.classList.contains('ag-soft-line-break') || node.classList.contains('ag-hard-line-break') || node.tagName === 'BR') {
        // The break's own text is the newline in the source; a soft break reads as one too.
        out.text('\n');
        return;
    }
    if (node.tagName === 'INPUT') {
        out.text((node as HTMLInputElement).checked || node.hasAttribute('checked') ? '☑ ' : '☐ ');
        return;
    }
    node.childNodes.forEach(c => inline(c, out));
}

function block(node: Node, out: Writer, indent: string) {
    if (node.nodeType === Node.TEXT_NODE) { out.text(node.textContent ?? ''); return; }
    if (!(node instanceof Element)) return;
    if (node.matches(SKIP)) return;
    const tag = node.tagName;
    const doc = node.ownerDocument;

    // Code, a formula block, a diagram, front matter, an HTML block: the source as written.
    const code = node.matches('pre, figure.ag-container-block') ? node.querySelector('.ag-code-content') : null;
    if (code) {
        out.end(2);
        out.text(indent + (code.textContent ?? '').replace(/\n/g, '\n' + indent));
        out.end(2);
        return;
    }
    if (tag === 'TABLE') {
        out.end(2);
        for (const row of Array.from(node.querySelectorAll('tr'))) {
            const cells = Array.from(row.children).filter(c => c.tagName === 'TD' || c.tagName === 'TH');
            cells.forEach((cell, i) => {
                if (i) out.text('\t');
                const w = new Writer();
                cell.childNodes.forEach(c => inline(c, w));
                out.text(w.result().replace(/\s*\n\s*/g, ' '));
            });
            out.end(1);
        }
        out.end(2);
        return;
    }
    if (tag === 'UL' || tag === 'OL') {
        // A nested list follows its item on the next line; a list of its own is a block apart.
        const gap = indent ? 1 : 2;
        out.end(gap);
        for (const li of Array.from(node.children).filter(c => c.tagName === 'LI')) {
            const checkbox = li.querySelector(':scope > input.ag-task-list-item-checkbox') as HTMLInputElement | null;
            const marker = checkbox ? (checkbox.checked || checkbox.hasAttribute('checked') ? '☑ ' : '☐ ')
                : tag === 'OL' ? `${itemNumber(li, doc)}. ` : '• ';
            out.text(indent + marker);
            let first = true;
            for (const child of Array.from(li.childNodes)) {
                if (child instanceof Element && child.matches('input.ag-task-list-item-checkbox')) continue;
                if (child instanceof Element && (child.tagName === 'UL' || child.tagName === 'OL')) {
                    out.end(1);
                    block(child, out, indent + '    ');
                    first = true;
                    continue;
                }
                if (child instanceof Element && BLOCK.has(child.tagName) && child.tagName !== 'P') {
                    out.end(1);
                    block(child, out, indent + '    ');
                    continue;
                }
                if (!first) { out.end(1); out.text(indent + '    '); }
                inline(child, out);
                first = false;
            }
            out.end(1);
        }
        out.end(gap);
        return;
    }
    if (tag === 'FIGURE' && node.getAttribute('data-role') === 'FOOTNOTE') {
        out.end(2);
        const id = node.querySelector('.ag-footnote-input')?.textContent ?? '';
        out.text(`${indent}[${id}] `);
        node.childNodes.forEach(c => { if (!(c instanceof Element && c.classList.contains('ag-footnote-input'))) inline(c, out); });
        out.end(2);
        return;
    }
    if (BLOCK.has(tag) || tag === 'LI') {
        // A block holding blocks (a quote, a figure, the editor itself) goes through them; a paragraph or heading
        // is a line of inline text.
        const hasBlocks = Array.from(node.children).some(c => BLOCK.has(c.tagName) || c.tagName === 'LI');
        out.end(2);
        if (hasBlocks) node.childNodes.forEach(c => block(c, out, indent));
        else { out.text(indent); node.childNodes.forEach(c => inline(c, out)); }
        out.end(2);
        return;
    }
    inline(node, out);
}

/** The plain text of a fragment of the editor page (a selection's cloneContents, or a whole container). */
export function fragmentToPlainText(fragment: Node): string {
    const out = new Writer();
    fragment.childNodes.forEach(c => block(c, out, ''));
    return out.result();
}

/** The containers that give a selection across blocks its shape: from one list item to another is still numbered
 *  items, across table cells still a row. A selection within one paragraph is only its text (a word picked out of a
 *  list item is not "4. word"). */
const STRUCTURE = new Set(['LI', 'OL', 'UL', 'TABLE', 'THEAD', 'TBODY', 'TR', 'BLOCKQUOTE']);

/** One range's content, inside copies of the structure around it (cloneContents gives only what is between the
 *  range's ends, so a selection within one list item lost the list and with it the number). */
function rangeFragment(range: Range): Node {
    const start = range.commonAncestorContainer;
    const element = start instanceof Element ? start : start.parentElement;
    // Within a code block or a formula's source the selection is source text as it stands.
    if (element?.closest('.ag-code-content, .ag-math-text')) {
        const div = document.createElement('div');
        const pre = document.createElement('pre');
        const code = document.createElement('span');
        code.className = 'ag-code-content';
        code.textContent = range.toString();
        pre.appendChild(code);
        div.appendChild(pre);
        return div;
    }
    let fragment: Node = range.cloneContents();
    for (let node = element && STRUCTURE.has(element.tagName) ? element : null; node && node.id !== 'ag-editor-id' && node !== document.body; node = node.parentElement) {
        if (!STRUCTURE.has(node.tagName)) continue;
        const copy = node.cloneNode(false);
        copy.appendChild(fragment);
        fragment = copy;
    }
    const div = document.createElement('div');
    div.appendChild(fragment);
    return div;
}

/** The plain text of the page's current selection, or '' when nothing is selected. */
export function selectionToPlainText(selection: Selection | null = window.getSelection()): string {
    if (!selection || selection.rangeCount === 0 || selection.isCollapsed) return '';
    const wrapper = document.createElement('div');
    for (let i = 0; i < selection.rangeCount; i++) wrapper.appendChild(rangeFragment(selection.getRangeAt(i)));
    return fragmentToPlainText(wrapper);
}
