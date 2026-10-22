// Hints for the opening marks of Spanish questions and exclamations, and a way to type the marks that are not on every
// keyboard. The rules are in punctuationCore.js (tested); this file reads the text of the editor, underlines what the
// rules report and answers the host's menus (MainWindow.Punctuation.cs).
//
// Nothing is ever changed by itself: the underline is a hint, and the mark goes in only when the user chooses it in
// the right-click menu. Like the spell checker's underline it is a CSS custom highlight, so the editor's own DOM and
// content model are not touched. Only the Visual view is read (the source pane has its own line structure).
import { findMissingOpeners, PLACE } from './punctuationCore'

const w = window as any
const P: any = w.__caretPunct = { enabled: false }

let entries: any[] = []     // the reports of the last scan: { kind, at, from, start, comma, block }
let hit: any = null         // the report a right-click is on
let timer: any = 0
let last: any = null        // where the user last typed: { root } or { cm }, with the selection there
const BLOCKS = 'p, h1, h2, h3, h4, h5, h6, li, td, th, dt, dd, figcaption, blockquote'
const SKIP_TAGS: Record<string, number> = { CODE: 1, PRE: 1, KBD: 1, SVG: 1, SCRIPT: 1, STYLE: 1, BUTTON: 1, INPUT: 1, TEXTAREA: 1 }
const SKIP_CLASS = /(^|\s)(ag-gray|ag-hide|ag-html-tag|ag-math|ag-math-text|ag-math-render|ag-link-in-bracket|ag-image-src|ag-image-marked-text|ag-emoji-marked-text|ag-reference-label|ag-reference-title|ag-front-matter)(\s|$)/
const SKIP_TEXT = /(?:https?:\/\/|www\.)\S+|[^\s@]+@[^\s@]+\.[^\s@]+|[A-Za-z]:\\\S+/g

const skipped = (node: Node, root: Element) => {
    for (let el = node.parentNode as Element | null; el && el !== root; el = el.parentNode as Element | null) {
        if (el.nodeType !== 1) continue
        if (SKIP_TAGS[el.tagName]) return true
        if (el.getAttribute('contenteditable') === 'false') return true
        const cls = el.getAttribute('class')
        if (cls && SKIP_CLASS.test(cls)) return true
    }
    return false
}

// The text of each block of the editor, as one string; what is not prose becomes PLACE, character for character.
const collect = () => {
    const blocks: any[] = []
    let budget = 200000
    document.querySelectorAll('[contenteditable="true"]').forEach(root => {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT)
        let current: any = null
        for (let node = walker.nextNode(); node && budget > 0; node = walker.nextNode()) {
            const text = node.nodeValue
            if (!text) continue
            const block = (node.parentElement && node.parentElement.closest(BLOCKS)) || root
            if (!current || current.el !== block) {
                current = { el: block, parts: [], text: '' }
                blocks.push(current)
            }
            const kept = skipped(node, root) ? PLACE.repeat(text.length) : text.replace(SKIP_TEXT, m => PLACE.repeat(m.length))
            current.parts.push({ node, start: current.text.length, length: text.length })
            current.text += kept
            budget -= text.length
        }
    })
    return blocks
}

// The text node and offset of a place in a block's string; the end of a range belongs to the part it ends in.
const pointAt = (block: any, index: number, end = false) => {
    for (const part of block.parts) {
        if (end ? index > part.start && index <= part.start + part.length : index >= part.start && index < part.start + part.length)
            return { node: part.node, offset: index - part.start }
    }
    const lastPart = block.parts[block.parts.length - 1]
    return lastPart && index >= lastPart.start + lastPart.length ? { node: lastPart.node, offset: lastPart.length } : null
}

const rangeOf = (entry: any) => {
    const a = pointAt(entry.block, entry.from)
    const b = pointAt(entry.block, entry.at + 1, true)
    if (!a || !b) return null
    const r = document.createRange()
    try { r.setStart(a.node, a.offset); r.setEnd(b.node, b.offset) } catch (x) { return null }
    return r
}

const paint = () => {
    const css = (window as any).CSS
    if (!css || !css.highlights || typeof (window as any).Highlight === 'undefined') return
    if (!P.enabled) { css.highlights.delete('caret-punct'); return }
    const ranges: Range[] = []
    entries.forEach(e => {
        if (!e.block.parts[0].node.isConnected) return
        const r = rangeOf(e)
        if (r) ranges.push(r)
    })
    css.highlights.set('caret-punct', new (window as any).Highlight(...ranges))
}

const scan = () => {
    timer = 0
    entries = []
    if (P.enabled) {
        collect().forEach(block => findMissingOpeners(block.text).forEach(f => entries.push({ ...f, block })))
    }
    paint()
}

const schedule = () => {
    if (!P.enabled) return
    clearTimeout(timer)
    timer = setTimeout(scan, 500)
}

P.enable = (on: boolean) => {
    P.enabled = !!on
    scan()
}

// The report under a right-click, as "q" or "e" (a question or an exclamation), with ",c" when the sentence has a comma
// where the mark could also go; an empty string where there is none. What the menu picks is applied with P.apply.
P.atPoint = (x: number, y: number): string => {
    hit = null
    if (!P.enabled || !document.caretRangeFromPoint) return ''
    const at = document.caretRangeFromPoint(x, y)
    if (!at) return ''
    scan()
    for (const e of entries) {
        const r = rangeOf(e)
        if (r && r.isPointInRange(at.startContainer, at.startOffset)) { hit = e; return e.kind + (e.comma >= 0 ? ',c' : '') }
    }
    return ''
}

// Types the mark where the user chose: at the start of the sentence, or after its last comma. As the editor sees typing,
// so undo works.
P.apply = (where: string) => {
    if (!hit) return
    const e = hit
    hit = null
    const index = where === 'comma' && e.comma >= 0 ? e.comma : e.start
    const pos = pointAt(e.block, index)
    if (!pos || !pos.node.isConnected) return
    const root = pos.node.parentElement && pos.node.parentElement.closest('[contenteditable="true"]') as HTMLElement | null
    if (root) root.focus()
    const range = document.createRange()
    range.setStart(pos.node, pos.offset)
    range.collapse(true)
    const sel = window.getSelection()
    if (!sel) return
    sel.removeAllRanges()
    sel.addRange(range)
    document.execCommand('insertText', false, e.kind === 'q' ? '¿' : '¡')
    schedule()
}

// Types a mark at the caret (Edit > Insert punctuation): in the editor, or in the source pane where the user last was.
P.insert = (text: string) => {
    if (last && last.cm && last.cm.getWrapperElement().isConnected) {
        last.cm.focus()
        last.cm.replaceSelection(text)
        return
    }
    if (!last || !last.root || !last.root.isConnected) return
    last.root.focus()
    const sel = window.getSelection()
    if (sel && last.range) { sel.removeAllRanges(); sel.addRange(last.range) }
    document.execCommand('insertText', false, text)
}

// The place of the caret, for Edit > Insert punctuation. A source pane (CodeMirror) keeps its caret in a hidden text box, so the
// page's own selection can still be a range in the Visual view that was left: the pane is known by the focus going into it.
const remember = () => {
    const sel = window.getSelection()
    if (!sel || sel.rangeCount === 0) return
    const node = sel.anchorNode
    const el = node && (node.nodeType === 1 ? node as Element : node.parentElement)
    const root = el && el.closest('[contenteditable="true"]') as HTMLElement | null
    // only a selection in the part the user is typing in counts
    if (root && document.activeElement && root.contains(document.activeElement)) last = { root, range: sel.getRangeAt(0).cloneRange() }
}

document.addEventListener('focusin', e => {
    const target = e.target as Element | null
    if (!target || !target.closest) return
    const box = target.closest('.CodeMirror') as any
    if (box && box.CodeMirror) { last = { cm: box.CodeMirror }; return }
    const root = target.closest('[contenteditable="true"]') as HTMLElement | null
    if (root) { last = { root, range: null }; remember() }
}, true)

const style = document.createElement('style')
style.textContent = '::highlight(caret-punct){text-decoration:underline dotted #2b88d8;text-decoration-thickness:1.5px;text-underline-offset:3px;}'
document.head.appendChild(style)
new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true, characterData: true })
document.addEventListener('selectionchange', () => { remember(); schedule() })
P.enable(!!w.__caretPunctInitial)

export {}