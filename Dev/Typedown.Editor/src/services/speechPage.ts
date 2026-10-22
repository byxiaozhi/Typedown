// Speech marks, the page's half of the Speech card and the shortcuts (docs/speech-marks-design.md, section 5.3).
// The host asks for a mark with window.__caretSpeech.insert({ name, value?, text?, after? }); this finds what is
// selected, asks the rules (Muya/lib/parser/speechEdit.js, tested alone) what to write, and types it so Undo works
// as for any typing. Works in the editor and in the Code/Split source pane.
//
// The selection helpers are the ones of the review script (MainWindow.Review.cs): the editor draws its paragraphs
// again at times, so a place is the paragraph's id and offsets in its text (the marks are text in the page too, only
// hidden), and a selection is widened over the hidden marks of **bold** and the like.
import { buildAny, planEdit, planDefinition, planRemove, withDefinitions, enclosing } from 'components/Muya/lib/parser/speechEdit'
import { collectDocumentDefinitions, proseLines, stripMarkdown, stripMarks, matchMark, isEscaped, maskCode, WPM_MIN, WPM_MAX } from 'components/Muya/lib/parser/speech'
import { estimateText, computeTiming, wpmAfter, DEFAULT_WPM } from 'components/Muya/lib/parser/speechTiming'
import { buildExplanation } from 'components/Muya/lib/parser/speechExplain'

export type Status = 'ok' | 'nofocus' | 'nothing' | 'unsafe' | 'several' | 'unknown'

// `placed` is where the words of the pair written last are (a paragraph's id and offsets in its text without marks): the
// Speech ring selects them again, so the next item it is asked for goes inside or around the same words.
// `wpm` is the words per minute of Settings, the pace a document has until a {wpm N} of its own says otherwise.
// `library` is the speaker's marks (for the explanation for an AI), `timing` the settings the time of the document is worked out with.
const state: {
    editor: any, on: boolean, wpm: number, placed: { id: string, start: number, end: number } | null,
    library: any[], timing: { wpm?: number, headingsSpoken: boolean }
} = { editor: undefined, on: false, wpm: DEFAULT_WPM, placed: null, library: [], timing: { headingsSpoken: false } }

export const setSpeechEditor = (editor: any) => { state.editor = editor }
export const setSpeechMode = (on: boolean) => { state.on = on }
export const speechModeOn = () => state.on
export const setSpeechBaseline = (wpm: number) => { state.wpm = Number.isFinite(Number(wpm)) && Number(wpm) > 0 ? Number(wpm) : DEFAULT_WPM }
export const setSpeechLibrary = (library: any[]) => { state.library = library }
export const setSpeechTimingOptions = (options: { wpm?: number, headingsSpoken: boolean }) => { state.timing = options }

const elementOf = (node: Node | null): Element | null => node && node.nodeType === 1 ? node as Element : node && node.parentElement
const editable = (node: Node | null) => elementOf(node)?.closest('[contenteditable="true"]') ?? null
const blockOf = (node: Node | null) => elementOf(node)?.closest('.ag-paragraph') as HTMLElement | null
const contentOf = (block: HTMLElement) => (block.querySelector('.ag-paragraph-content') as HTMLElement | null) ?? block
const sourcePane = () => (document.querySelector('.CodeMirror') as any)?.CodeMirror

const offsetOf = (content: Element, node: Node, offset: number) => {
    const r = document.createRange()
    r.selectNodeContents(content)
    r.setEnd(node, offset)
    return r.toString().length
}

const pointAt = (content: Element, offset: number) => {
    const walker = document.createTreeWalker(content, NodeFilter.SHOW_TEXT)
    let seen = 0
    let last: Text | null = null
    while (walker.nextNode()) {
        last = walker.currentNode as Text
        if (offset <= seen + last.length) return { node: last as Node, offset: offset - seen }
        seen += last.length
    }
    return last ? { node: last as Node, offset: last.length } : { node: content as Node, offset: 0 }
}

// The marks of **bold**, *italic*, ~~del~~ are in the page as text hidden by a class (ag-hide): a selection that looks
// like "very important" ends before the hidden closing **. Wrapping that would leave the ** half inside the mark, so
// the selection is widened over the hidden marks at its edges (the ones that show, with the caret inside, are left).
const isMark = (n: Node | null): n is HTMLElement => !!n && n.nodeType === 1 && (n as HTMLElement).classList.contains('ag-remove')
const hiddenMark = (n: Node | null) => isMark(n) && n.classList.contains('ag-hide')
const formatted = (n: Node | null) => !!n && n.nodeType === 1 && (n as HTMLElement).classList.contains('ag-inline-rule')
const after = (node: Node, at: number): Node | null => {
    if (node.nodeType === 3) { if (at < (node as Text).length) return null } else if (at < node.childNodes.length) return node.childNodes[at]
    for (let n: Node | null = node; n; n = n.parentNode) if (n.nextSibling) return n.nextSibling
    return null
}
const before = (node: Node, at: number): Node | null => {
    if (node.nodeType === 3) { if (at > 0) return null } else if (at > 0) return node.childNodes[at - 1]
    for (let n: Node | null = node; n; n = n.parentNode) if (n.previousSibling) return n.previousSibling
    return null
}
const widen = (range: Range) => {
    let next: Node | null
    let prev: Node | null
    while ((next = after(range.endContainer, range.endOffset)) && hiddenMark(next) && formatted(next.previousSibling)) range.setEndAfter(next)
    while ((prev = before(range.startContainer, range.startOffset)) && hiddenMark(prev) && formatted(prev.nextSibling)) range.setStartBefore(prev)
}
const covers = (range: Range, node: Node) => {
    const size = node.nodeType === 3 ? (node as Text).length : node.childNodes.length
    return range.comparePoint(node, 0) === 0 && range.comparePoint(node, size) === 0
}
// Bold, italic and strikethrough can be inside a mark or be taken whole with their marks; anything else the selection
// touches (a link, an image, code, math) cannot be cut. The speech marks and the review marks are text in a line.
const cutsFormatting = (range: Range, block: HTMLElement) => {
    const list = block.querySelectorAll('.ag-inline-rule')
    for (let i = 0; i < list.length; i++) {
        const e = list[i] as HTMLElement
        if (e.classList.contains('ag-critic') || e.classList.contains('ag-speech') || !range.intersectsNode(e)) continue
        if (e.tagName !== 'STRONG' && e.tagName !== 'EM' && e.tagName !== 'DEL') continue
        if (e.contains(range.startContainer) && e.contains(range.endContainer)) continue
        const open = e.previousSibling
        const close = e.nextSibling
        if (!(isMark(open) && isMark(close) && covers(range, open) && covers(range, close))) return true
    }
    return false
}

// Zero-width spaces are the editor's own markers, not text: the rules see the text without them.
const ZWSP = String.fromCharCode(0x200b)
const ZW = new RegExp(ZWSP, 'g')
const toClean = (raw: string, at: number) => at - (raw.substring(0, at).match(ZW)?.length ?? 0)
const toRaw = (raw: string, at: number) => {
    let seen = 0
    for (let i = 0; i < raw.length; i++) {
        if (seen === at) return i
        if (raw[i] !== ZWSP) seen++
    }
    return raw.length
}

const insertInEditor = (spec: any): Status => {
    const sel = window.getSelection()
    if (!sel || sel.rangeCount === 0) return 'nofocus'
    const range = sel.getRangeAt(0).cloneRange()
    if (!editable(range.startContainer) || !editable(range.endContainer)) return 'nofocus'
    const first = blockOf(range.startContainer)
    const last = blockOf(range.endContainer)
    if (!first || !last) return 'nofocus'
    // a code block and the front matter are not prose: a mark written there would be literal text
    const notProse = '.ag-front-matter, [class*="ag-code"], pre'
    if (elementOf(range.startContainer)?.closest(notProse) || elementOf(range.endContainer)?.closest(notProse)) return 'unsafe'
    // a word of the user's own library is built with its definition, which the document may not have yet
    const { defs, missing } = withDefinitions(state.editor?.options?.speechDefs, spec.definitions)
    const mark = buildAny(spec, defs)
    if (!mark) return 'unknown'
    let status: Status
    if (first !== last) {
        status = mark.role === 'point' ? insertPointAtEnd(range, last, mark, defs) : 'several'
    } else {
        if (!range.collapsed) widen(range)
        status = mark.role === 'pair' && !range.collapsed && cutsFormatting(range, first) ? 'unsafe' : typeInto(first, range, mark, defs)
    }
    if (status === 'ok') addDefinitionsInEditor(missing)
    return status
}

// The `{define ...}` lines the document is missing for a mark just written: one paragraph of definitions at its top. The
// text is set again with the caret where it was (moved down by the lines that were added above it).
const addDefinitionsInEditor = (lines: string[]) => {
    const editor = state.editor
    if (!editor) return
    for (const line of lines) {
        const { markdown, cursor } = editor.getMarkdownAndCursor()
        const plan = planDefinition(markdown, line)
        const offset = markdown.split('\n').slice(0, plan.line).reduce((n: number, l: string) => n + l.length + 1, 0) + plan.ch
        const added = (plan.insert.match(/\n/g) ?? []).length
        // a caret on the line the text is written into, or below it, moves down with the text that is pushed down
        const moved = (p: { line: number, ch: number }) =>
            p.line > plan.line || (p.line === plan.line && p.ch >= plan.ch && plan.insert.endsWith('\n')) ? { ...p, line: p.line + added } : p
        const next = markdown.substring(0, offset) + plan.insert + markdown.substring(offset)
        editor.setMarkdown(next, cursor && cursor.anchor && cursor.focus ? { anchor: moved(cursor.anchor), focus: moved(cursor.focus) } : undefined)
    }
}

// A point mark after a selection over several paragraphs: at its end.
const insertPointAtEnd = (range: Range, last: HTMLElement, mark: any, defs: any): Status => {
    const tail = range.cloneRange()
    tail.collapse(false)
    return typeInto(last, tail, mark, defs)
}

const typeInto = (block: HTMLElement, range: Range, mark: any, defs: any): Status => {
    const content = contentOf(block)
    const raw = content.textContent ?? ''
    const start = offsetOf(content, range.startContainer, range.startOffset)
    const end = offsetOf(content, range.endContainer, range.endOffset)
    const text = raw.replace(ZW, '')
    const plan = planEdit(text, toClean(raw, start), toClean(raw, end), mark, defs)
    if (!plan.ok) return plan.reason as Status
    const from = pointAt(content, toRaw(raw, plan.start))
    const to = pointAt(content, toRaw(raw, plan.end))
    const target = document.createRange()
    target.setStart(from.node, from.offset)
    target.setEnd(to.node, to.offset)
    const root = editable(target.startContainer) as HTMLElement | null
    if (!root) return 'nofocus'
    root.focus()
    const sel = window.getSelection()!
    sel.removeAllRanges()
    sel.addRange(target)
    const typed = plan.replacement === '' ? document.execCommand('delete') : document.execCommand('insertText', false, plan.replacement)
    state.placed = typed && mark.role === 'pair'
        ? { id: block.id, start: (plan.start ?? 0) + mark.open.length, end: (plan.start ?? 0) + (plan.replacement ?? '').length - mark.close.length - mark.after.length }
        : null
    return typed ? 'ok' : 'nofocus'
}

const insertInCode = (cm: any, spec: any): Status => {
    const { defs, missing } = withDefinitions(collectDocumentDefinitions(cm.getValue()).defs, spec.definitions)
    const mark = buildAny(spec, defs)
    if (!mark) return 'unknown'
    const from = cm.getCursor('from')
    const to = cm.getCursor('to')
    if (from.line !== to.line && mark.role !== 'point') return 'several'
    // a mark after a selection over several lines goes at its end; otherwise over the selection on its line
    const line = from.line !== to.line ? to.line : from.line
    const start = from.line !== to.line ? to.ch : from.ch
    // marks live in prose: not in a fenced code block, not in front matter (where they would be literal text)
    if (!proseLines(cm.getValue().split('\n'))[line]) return 'unsafe'
    const plan = planEdit(cm.getLine(line), start, to.ch, mark, defs)
    if (!plan.ok) return plan.reason as Status
    cm.replaceRange(plan.replacement, { line, ch: plan.start }, { line, ch: plan.end })
    // the definitions the text is missing go to the top; the caret follows the text it was in
    for (const definition of missing) {
        const at = planDefinition(cm.getValue(), definition)
        cm.replaceRange(at.insert, { line: at.line, ch: at.ch })
    }
    cm.focus()
    return 'ok'
}

export const insert = (spec: any): Status => {
    try {
        const cm = sourcePane()
        return cm ? insertInCode(cm, spec) : insertInEditor(spec)
    } catch (err) {
        console.log(err)
        return 'nofocus'
    }
}

// The selection as the rules see it, for one paragraph of the editor: its text without the editor's own markers and the
// offsets in it. null in the source pane, outside the editor and over several paragraphs.
export const selectionInfo = () => {
    if (sourcePane()) return null
    const sel = window.getSelection()
    if (!sel || sel.rangeCount === 0) return null
    const range = sel.getRangeAt(0)
    if (!editable(range.startContainer) || !editable(range.endContainer)) return null
    const block = blockOf(range.startContainer)
    if (!block || block !== blockOf(range.endContainer)) return null
    const content = contentOf(block)
    const raw = content.textContent ?? ''
    return {
        block,
        text: raw.replace(ZW, ''),
        start: toClean(raw, offsetOf(content, range.startContainer, range.startOffset)),
        end: toClean(raw, offsetOf(content, range.endContainer, range.endOffset)),
        defs: state.editor?.options?.speechDefs
    }
}

// Takes away the mark an item of the ring stands for (`match`: { name, value?, text? }) at the selection or the caret:
// a pair loses its two marks and keeps its words. Typed like the rest, so Undo takes it back.
export const removeMark = (match: any): Status => {
    try {
        const info = selectionInfo()
        if (!info) return 'nofocus'
        const plan = planRemove(info.text, info.start, info.end, match, info.defs)
        if (!plan.ok) return 'nothing'
        const { start, end, replacement } = plan as { start: number, end: number, replacement: string }
        const content = contentOf(info.block)
        const raw = content.textContent ?? ''
        const from = pointAt(content, toRaw(raw, start))
        const to = pointAt(content, toRaw(raw, end))
        const target = document.createRange()
        target.setStart(from.node, from.offset)
        target.setEnd(to.node, to.offset)
        const root = editable(target.startContainer) as HTMLElement | null
        if (!root) return 'nofocus'
        root.focus()
        const sel = window.getSelection()!
        sel.removeAllRanges()
        sel.addRange(target)
        state.placed = null
        const typed = replacement === '' ? document.execCommand('delete') : document.execCommand('insertText', false, replacement)
        return typed ? 'ok' : 'nofocus'
    } catch (err) {
        console.log(err)
        return 'nofocus'
    }
}

// The words of the pair written last are selected again (after the editor has drawn its paragraph again).
export const reselectPlaced = () => {
    const placed = state.placed
    const block = placed && (document.getElementById(placed.id) as HTMLElement | null)
    if (!placed || !block) return
    const content = contentOf(block)
    const raw = content.textContent ?? ''
    const from = pointAt(content, toRaw(raw, placed.start))
    const to = pointAt(content, toRaw(raw, placed.end))
    const range = document.createRange()
    range.setStart(from.node, from.offset)
    range.setEnd(to.node, to.offset)
    const sel = window.getSelection()
    if (!sel || !editable(range.startContainer)) return
    sel.removeAllRanges()
    sel.addRange(range)
}

// The words a mark would be about, for the preview of the ring: the selection, or the sentence at the caret, without its
// marks, and cut short.
export const sampleText = (): string => {
    const info = selectionInfo()
    if (!info) return ''
    let from = info.start
    let to = info.end
    if (from === to) {
        const sentence = planEdit(info.text, from, to, { role: 'pair', open: '', close: '', after: '', scope: 'sentence' }, info.defs)
        if (!sentence.ok) return ''
        from = sentence.start
        to = sentence.end
    }
    const text = stripMarks(info.text.substring(from, to), info.defs, true).replace(/\s+/g, ' ').trim()
    return text.length > 90 ? `${text.substring(0, 89)}…` : text
}

// The words per minute in force where the selection is: the pace of Settings, changed by every {wpm N} in the paragraphs
// before it and in its own paragraph up to the selection (a later {wpm} does not change what comes before it).
const wpmAt = (info: { block: HTMLElement, start: number, defs: any }): number => {
    let wpm = state.wpm
    const target = contentOf(info.block)
    for (const block of Array.from(document.querySelectorAll('.ag-paragraph-content')) as HTMLElement[]) {
        const raw = (block.textContent ?? '').replace(ZW, '')
        if (block === target) return wpmAfter(raw.substring(0, info.start), info.defs, wpm)
        wpm = wpmAfter(raw, info.defs, wpm)
    }
    return wpm
}

// How long the words an item is about take before and after it: for a pair the selection (or the sentence at the caret) as it
// is and as it would be written, for a single mark only the seconds it adds. The same arithmetic as the timing of the whole
// document (speechTiming.js). null when nothing can be written there.
export const previewTiming = (spec: any): { before: number, after: number, role: string } | null => {
    try {
        const info = selectionInfo()
        if (!info) return null
        const { defs } = withDefinitions(info.defs, spec.definitions)
        const mark = buildAny(spec, defs)
        if (!mark) return null
        const plan = planEdit(info.text, info.start, info.end, mark, defs)
        if (!plan.ok) return null
        const wpm = wpmAt(info)
        if (mark.role === 'point') return { before: 0, after: estimateText(plan.replacement ?? '', defs, wpm), role: 'point' }
        // the stretch is measured with the pairs around it (a stretch inside {slow} is spoken slowly), before and after
        const around = enclosing(info.text, plan.start ?? 0, plan.end ?? 0, defs)
        const timed = (text: string) => estimateText(`${around.open}${text}${around.close}`, defs, wpm)
        return { before: timed(info.text.substring(plan.start ?? 0, plan.end ?? 0)), after: timed(plan.replacement ?? ''), role: mark.role }
    } catch (err) {
        console.log(err)
        return null
    }
}

// The same for taking an applied item away.
export const previewRemoval = (match: any): { before: number, after: number, role: string } | null => {
    try {
        const info = selectionInfo()
        if (!info) return null
        const plan = planRemove(info.text, info.start, info.end, match, info.defs)
        if (!plan.ok) return null
        const around = enclosing(info.text, plan.start ?? 0, plan.end ?? 0, info.defs)
        const wpm = wpmAt(info)
        const timed = (text: string) => estimateText(`${around.open}${text}${around.close}`, info.defs, wpm)
        return {
            before: timed(info.text.substring(plan.start ?? 0, plan.end ?? 0)),
            after: timed(plan.replacement ?? ''),
            role: 'remove'
        }
    } catch (err) {
        console.log(err)
        return null
    }
}

// The words per minute of the talk, written into the document ({wpm N}): the first one that is there is changed, or one is
// added as a paragraph of its own before the first text. One step in Undo. Works in the editor and in the source pane.
const setWpm = (value: number): Status => {
    try {
        const wpm = Math.round(Math.min(WPM_MAX, Math.max(WPM_MIN, Number(value))))
        if (!Number.isFinite(wpm)) return 'unknown'
        const mark = `{wpm ${wpm}}`
        const cm = sourcePane()
        const editor = state.editor
        const markdown: string | undefined = cm ? cm.getValue() : editor?.getMarkdownAndCursor?.().markdown
        if (markdown === undefined) return 'nofocus'
        const lines = markdown.split('\n')
        const prose = proseLines(lines)
        let found: { line: number, from: number, to: number } | null = null
        for (let n = 0; n < lines.length && !found; n++) {
            if (!prose[n]) continue
            // not inside a code span: that is an example, not the pace of the talk
            const masked = maskCode(lines[n])
            const re = /\{wpm[ \t][^{}\n]*\}/g
            let m: RegExpExecArray | null
            while ((m = re.exec(masked))) {
                const read = matchMark(lines[n].substring(m.index, m.index + m[0].length), undefined)
                // an escaped brace is plain text
                if (read && read.name === 'wpm' && !isEscaped(lines[n], m.index)) { found = { line: n, from: m.index, to: m.index + m[0].length }; break }
            }
        }
        if (cm) {
            if (found) cm.replaceRange(mark, { line: found.line, ch: found.from }, { line: found.line, ch: found.to })
            else {
                const at = planDefinition(markdown, mark)
                cm.replaceRange(at.insert, { line: at.line, ch: at.ch })
            }
            return 'ok'
        }
        if (!editor) return 'nofocus'
        const { cursor } = editor.getMarkdownAndCursor()
        let next: string
        let moved = (p: { line: number, ch: number }) => p
        if (found) {
            const f = found
            lines[f.line] = lines[f.line].substring(0, f.from) + mark + lines[f.line].substring(f.to)
            next = lines.join('\n')
            const grow = mark.length - (f.to - f.from)
            moved = p => (p.line === f.line && p.ch >= f.to ? { ...p, ch: p.ch + grow } : p)
        } else {
            const plan = planDefinition(markdown, mark)
            const offset = lines.slice(0, plan.line).reduce((n: number, l: string) => n + l.length + 1, 0) + plan.ch
            const added = (plan.insert.match(/\n/g) ?? []).length
            next = markdown.substring(0, offset) + plan.insert + markdown.substring(offset)
            moved = p => (p.line > plan.line || (p.line === plan.line && p.ch >= plan.ch && plan.insert.endsWith('\n')) ? { ...p, line: p.line + added } : p)
        }
        editor.setMarkdown(next, cursor && cursor.anchor && cursor.focus ? { anchor: moved(cursor.anchor), focus: moved(cursor.focus) } : undefined)
        return 'ok'
    } catch (err) {
        console.log(err)
        return 'nofocus'
    }
}

// Scrolls to a paragraph or heading the timing panel points at (`prefix`: the start of its text as the editor shows it, `nth`:
// which of the paragraphs that start the same way), and puts the caret at its start. In the source pane, the line.
const goTo = (prefix: string, nth: number): boolean => {
    try {
        const cm = sourcePane()
        if (cm) {
            let seen = 0
            for (let n = 0; n < cm.lineCount(); n++) {
                const line = cm.getLine(n).trim()
                const plain = line.replace(/^(?:>\s*)*(?:(?:[-*+]|\d{1,9}[.)])\s+(?:\[[ xX]\]\s+)?)?/, '')
                if (!line.startsWith(prefix) && !plain.startsWith(prefix)) continue
                if (seen++ < nth) continue
                cm.setCursor({ line: n, ch: 0 })
                cm.scrollIntoView({ line: n, ch: 0 }, 120)
                return true
            }
            return false
        }
        // the blocks that hold text (a list or a quote holds blocks of its own); a heading has no content span, its marker is hidden
        const blocks = (Array.from(document.querySelectorAll('.ag-paragraph')) as HTMLElement[]).filter(b => !b.querySelector('.ag-paragraph'))
        // a heading's prefix is the line of the file ("## Opening"); the editor shows the heading without its marker
        const bare = (text: string) => text.replace(/^#{1,6}\s+/, '')
        // a mark in the prefix is drawn as a chip (its words are not the mark's), so the prefix is taken up to the first mark
        const upToMark = (text: string) => { const at = text.indexOf('{'); return at > 0 ? text.slice(0, at).trimEnd() : text }
        const wanted = upToMark(bare(prefix))
        let seen = 0
        for (const block of blocks) {
            const content = contentOf(block)
            const text = bare((content.textContent ?? '').replace(ZW, '').trim())
            if (!text.startsWith(wanted)) continue
            if (seen++ < nth) continue
            content.scrollIntoView({ block: 'center', behavior: 'smooth' })
            const point = pointAt(content, 0)
            const range = document.createRange()
            range.setStart(point.node, point.offset)
            range.collapse(true)
            const sel = window.getSelection()
            sel?.removeAllRanges()
            sel?.addRange(range)
            return true
        }
    } catch (err) {
        console.log(err)
    }
    return false
}

// A definition line the document does not have yet, added at the top (the hint "defined in your library, not in this document").
const addDefinition = (line: string): Status => {
    try {
        const cm = sourcePane()
        if (cm) {
            const at = planDefinition(cm.getValue(), line)
            cm.replaceRange(at.insert, { line: at.line, ch: at.ch })
        } else {
            addDefinitionsInEditor([line])
        }
        return 'ok'
    } catch (err) {
        console.log(err)
        return 'nofocus'
    }
}

// The text for the clipboard that explains the document to an AI (Muya/lib/parser/speechExplain.js): what the text is, how
// the marks are written, every mark of Caret and of the speaker's library, the planned times, and the speech itself.
// `options`: { withText, usedOnly, asWords }. null when the text of the document cannot be had.
const explain = (options: { withText?: boolean, usedOnly?: boolean, asWords?: boolean, rehearsal?: string }): string | null => {
    try {
        const cm = sourcePane()
        const markdown: string | undefined = cm ? cm.getValue() : state.editor?.getMarkdownAndCursor?.().markdown
        if (markdown === undefined) return null
        const timing = computeTiming(markdown, state.timing)
        return buildExplanation({ library: state.library, markdown, timing, withText: options.withText !== false, usedOnly: !!options.usedOnly, asWords: !!options.asWords, rehearsal: options.rehearsal || '' })
    } catch (err) {
        console.log(err)
        return null
    }
}

// Direct keys, by physical key so they work on any layout (never Ctrl+Alt: that is AltGr on many keyboards). Only in
// Speech mode; the page tells the host nothing: it is all done here.
const SHORTCUTS: Record<string, any> = {
    Period: { name: 'pause' },
    Comma: { name: 'beat' },
    KeyE: { name: 'emphasis' }
}

const shortcut = (code: string): boolean => {
    if (!state.on || !SHORTCUTS[code]) return false
    insert(SHORTCUTS[code])
    return true
}

;(window as any).__caretSpeech = {
    insert: (json: string) => insert(JSON.parse(json)),
    shortcut,
    // the document without its speech marks (Edit > Remove all speech marks)
    strip: (markdown: string) => stripMarkdown(markdown),
    // the words per minute of the talk, written into the document (the timing panel of the Speech card)
    setWpm: (value: number) => setWpm(value),
    // scrolls to a paragraph (the timing panel, the hints and the shape of the talk)
    goTo: (prefix: string, nth: number) => goTo(prefix, nth),
    // a definition line added at the top of the document
    addDefinition: (line: string) => addDefinition(line),
    // the explanation for an AI, as text
    explain: (json: string) => explain(JSON.parse(json))
}

// The rendered editor hides the marks of a span (** * ~~) in markers beside it, so a selection of only the words inside **bold**
// comes back as plain text. The marks of the spans the selected text lies in are put back around it (spaces stay
// outside, because a mark must touch the text it marks). They are taken from the selected text itself, not from the range's
// common ancestor: a double-click starts the range at the end of the hidden marker before the word. A selection that starts in a
// span and ends outside it (or the reverse) gets the missing mark at that end. When the selection holds a span's own markers
// the text has its marks already, and a selection over several lines is left as it is.
const BLOCK_TAGS = ['P', 'LI', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'TD', 'TH', 'BLOCKQUOTE', 'PRE', 'FIGURE', 'BODY']
const STRIKE_TAG = ['D', 'EL'].join('')
const MARKS: { [tag: string]: string } = { STRONG: '**', EM: '*', [STRIKE_TAG]: '~~' }
const isMarkerSpan = (el: Element): boolean => el.classList.contains('ag-remove') || el.classList.contains('ag-hide') || el.classList.contains('ag-gray')

// How many characters of one text node the range selects.
const charsOf = (range: Range, node: Node): number => {
    if (!range.intersectsNode(node)) return 0
    const length = (node.nodeValue || '').length
    const from = node === range.startContainer ? range.startOffset : 0
    const to = node === range.endContainer ? range.endOffset : length
    return to > from ? to - from : 0
}

// How many characters of the text nodes under root the range selects.
const selectedChars = (range: Range, root: Node): number => {
    let total = 0
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT)
    for (let node = walker.nextNode(); node; node = walker.nextNode()) total += charsOf(range, node)
    return total
}

const withInlineMarks = (text: string): string => {
    try {
        if (text.includes('\n')) return text
        const selection = window.getSelection()
        const range = selection && selection.rangeCount > 0 ? selection.getRangeAt(0) : null
        if (!range) return text
        const container = range.commonAncestorContainer
        const root: Node | null = container.nodeType === 1 ? container : container.parentNode
        if (!root) return text
        // The spans (innermost first) of the first and of the last selected piece of text, markers left out
        let first: Element[] | null = null
        let last: Element[] = []
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT)
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (charsOf(range, node) === 0) continue
            const spans: Element[] = []
            let inMarker = false
            for (let el: Node | null = node.parentNode; el && el.nodeType === 1; el = el.parentNode) {
                const element = el as Element
                if (BLOCK_TAGS.includes(element.tagName)) break
                if (isMarkerSpan(element)) inMarker = true
                if (MARKS[element.tagName]) spans.push(element)
            }
            if (inMarker) continue // the text of a marker is not what is marked
            if (first === null) first = spans
            last = spans
        }
        if (!first) return text
        const start = first
        // In both ends: the whole selection is inside them, so the marks go round it. Only at the start or only at the end: the
        // selection runs out of the span, and the text holds its marker on one side only, so the other mark is put back.
        // (A span whose own markers are in the selection is in the text already.)
        const hasOwnMarkers = (span: Element): boolean =>
            [span.previousElementSibling, span.nextElementSibling].some(side => !!side && isMarkerSpan(side) && selectedChars(range, side) > 0)
        const whole = start.filter(span => last.includes(span) && !hasOwnMarkers(span))
        const opening = start.filter(span => !last.includes(span)).reverse().map(span => MARKS[span.tagName]).join('')
        const closing = last.filter(span => !start.includes(span)).map(span => MARKS[span.tagName]).join('')
        if (whole.length === 0 && !opening && !closing) return text
        const lead = text.slice(0, text.length - text.trimStart().length)
        const trail = text.slice(text.trimEnd().length)
        let core = text.trim()
        if (!core) return text
        core = opening + core + closing
        for (const span of whole) core = MARKS[span.tagName] + core + MARKS[span.tagName]
        return lead + core + trail
    } catch {
        return text
    }
}
// Edit > Copy as WhatsApp text (MainWindow.WhatsApp.cs): the Markdown of the selection, or of the whole document when nothing is
// selected, with the speech marks taken out. Reads only; the host turns it into WhatsApp's markup.
;(window as any).__caretCopy = {
    markdown: (): { text: string, selected: boolean } | null => {
        try {
            const cm = sourcePane()
            let text = ''
            let selected = false
            if (cm) {
                text = cm.getSelection() || ''
                selected = text.trim().length > 0
                if (!selected) text = cm.getValue()
            } else if (state.editor) {
                try { text = state.editor.contentState.getClipBoardData().text || '' } catch { text = '' }
                selected = text.trim().length > 0
                if (selected) text = withInlineMarks(text)
                if (!selected) text = state.editor.getMarkdownAndCursor().markdown
            }
            return { text: stripMarkdown(text), selected }
        } catch {
            return null
        }
    }
}