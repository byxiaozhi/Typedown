// The changes of a tracked document drawn in place in the Visual view (docs/live-review-design.md, step L3).
//
// Added text gets a green background, drawn as a CSS custom highlight (like the spell checker's underline and the punctuation hints:
// the editor's own DOM and content model are not touched, so nothing here can get out of step with the text). Text that was deleted
// is not in the page, so a small red mark stands at the place it was. Pointing at a change, or at a mark, opens a card with the old
// text, who and when, and Accept and Reject (the host does it: MainWindow.LiveReview.cs).
//
// Where each change is comes from its words (trackText.js); one that cannot be found is not drawn here and is still in the list.
// Only the Visual view is drawn on: the Split view has its own preview (criticPreview.js) and the source pane has none.
import transport from './transport'
import { locateChanges } from './trackText'

interface IChange { index: number, kind: string, old: string, new: string, before: string, after: string, day: string }
interface IConfig { author: string, accept: string, reject: string }
interface IPlaced { index: number, kind: string, start: number, end: number, at: number, change: IChange, range: Range | null, point: { node: Node, offset: number } | null, badge: HTMLElement | null, stack?: number }

const w = window as any
const SKIP_TAGS: Record<string, number> = { SVG: 1, SCRIPT: 1, STYLE: 1, BUTTON: 1, INPUT: 1, TEXTAREA: 1 }
// The syntax the editor shows around the text of the block being edited (the marks of bold, the address of a link...) is not text of the document.
const SKIP_CLASS = /(^|\s)(ag-gray|ag-hide|ag-html-tag|ag-math-render|ag-link-in-bracket|ag-image-src|ag-image-marked-text|ag-emoji-marked-text|ag-reference-label|ag-reference-title)(\s|$)/
const BLOCKS = 'p, h1, h2, h3, h4, h5, h6, li, td, th, dt, dd, figcaption, blockquote, pre'
const CARD_TEXT = 320

let changes: IChange[] = []
let config: IConfig = { author: '', accept: 'Accept', reject: 'Reject' }
let placed: IPlaced[] = []
let blocks: any[] = []
let timer: any = 0
let hideTimer: any = 0
let frame = 0
let layer: HTMLElement | null = null
let card: HTMLElement | null = null
let shown: IPlaced | null = null
let styleEl: HTMLStyleElement | null = null

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

// The text of each block of the editor: { el, base (where it starts in the whole text), parts: [{ node, start, length }], text }.
const collect = () => {
    const out: any[] = []
    let budget = 400000
    document.querySelectorAll('[contenteditable="true"]').forEach(root => {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT)
        let current: any = null
        for (let node = walker.nextNode(); node && budget > 0; node = walker.nextNode()) {
            const text = node.nodeValue
            if (!text || skipped(node, root)) continue
            const el = (node.parentElement && node.parentElement.closest(BLOCKS)) || root
            if (!current || current.el !== el) {
                current = { el, base: 0, parts: [], text: '' }
                out.push(current)
            }
            current.parts.push({ node, start: current.text.length, length: text.length })
            current.text += text
            budget -= text.length
        }
    })
    let base = 0
    out.forEach(b => { b.base = base; base += b.text.length + 1 })
    return out
}

// The text node and offset of a place in the whole text (blocks joined by a line break); the end of a range belongs to the part it ends in.
const pointAt = (index: number, end = false) => {
    for (const block of blocks) {
        if (index < block.base || index > block.base + block.text.length) continue
        const local = index - block.base
        for (const part of block.parts) {
            if (end ? local > part.start && local <= part.start + part.length : local >= part.start && local < part.start + part.length)
                return { node: part.node as Node, offset: local - part.start }
        }
        const last = block.parts[block.parts.length - 1]
        if (last && local >= last.start + last.length) return { node: last.node as Node, offset: last.length }
    }
    return null
}

const rangeOf = (start: number, end: number) => {
    const a = pointAt(start)
    const b = pointAt(end, true)
    if (!a || !b) return null
    const r = document.createRange()
    try { r.setStart(a.node, a.offset); r.setEnd(b.node, b.offset) } catch (e) { return null }
    return r
}

const isDark = () => w.actualTheme === 'dark'

const ensureStyle = () => {
    if (!styleEl) {
        styleEl = document.createElement('style')
        document.head.appendChild(styleEl)
    }
    const dark = isDark()
    styleEl.textContent = [
        `::highlight(caret-add){background-color:${dark ? 'rgba(46,160,67,.32)' : 'rgba(46,160,67,.24)'};}`,
        `::highlight(caret-flash){background-color:${dark ? 'rgba(255,170,0,.5)' : 'rgba(255,170,0,.55)'};}`,
        '#caret-track-layer{position:absolute;top:0;left:0;width:0;height:0;overflow:visible;z-index:30;pointer-events:none;}',
        `.caret-del-mark{position:absolute;width:5px;margin-left:-2px;border-radius:2px;background:${dark ? '#ff7b72' : '#d13438'};pointer-events:auto;cursor:pointer;opacity:.85;transition:opacity .1s,width .1s;}`,
        '.caret-del-mark:hover,.caret-del-mark.caret-flash{opacity:1;width:7px;}',
        '.caret-del-mark.caret-flash{outline:2px solid rgba(255,170,0,.8);}',
        `.caret-track-card{position:absolute;width:max-content;max-width:380px;padding:8px 10px;border-radius:8px;font:13px/1.4 "Segoe UI",system-ui,sans-serif;pointer-events:auto;z-index:31;box-shadow:0 4px 18px rgba(0,0,0,.25);background:${dark ? '#24262d' : '#ffffff'};color:${dark ? '#e6e6e8' : '#1f1f23'};border:1px solid ${dark ? 'rgba(255,255,255,.18)' : 'rgba(0,0,0,.16)'};}`,
        '.caret-track-card .who{font-size:11.5px;opacity:.7;margin-bottom:4px;}',
        `.caret-track-card .old{white-space:pre-wrap;word-break:break-word;text-decoration:line-through;color:${dark ? '#ffa198' : '#a4262c'};margin-bottom:6px;}`,
        '.caret-track-card .row{display:flex;gap:6px;}',
        `.caret-track-card button{white-space:nowrap;font:12px "Segoe UI",system-ui,sans-serif;padding:3px 10px;border-radius:5px;cursor:pointer;border:1px solid ${dark ? 'rgba(255,255,255,.25)' : 'rgba(0,0,0,.25)'};background:${dark ? '#30323a' : '#f3f3f5'};color:inherit;}`,
        `.caret-track-card button:hover{background:${dark ? '#3b3e48' : '#e4e4e8'};}`,
    ].join('\n')
}

const ensureLayer = () => {
    if (layer && layer.isConnected) return layer
    layer = document.createElement('div')
    layer.id = 'caret-track-layer'
    document.body.appendChild(layer)
    return layer
}

const clearMarks = () => {
    const css = w.CSS
    if (css && css.highlights) { css.highlights.delete('caret-add'); css.highlights.delete('caret-flash') }
    placed.forEach(p => p.badge && p.badge.remove())
    placed = []
    hideCard()
}

const short = (text: string) => {
    const t = String(text || '').replace(/\s+/g, ' ').trim()
    return t.length > CARD_TEXT ? t.slice(0, CARD_TEXT - 1) + '\u2026' : t
}

const hideCard = () => {
    clearTimeout(hideTimer)
    if (card) card.remove()
    card = null
    shown = null
}

const act = (p: IPlaced, accept: boolean) => {
    hideCard()
    // the card says which change it showed: its text, the text around it, and whether it was the only one with those words
    const unique = changes.filter(c => c.old === p.change.old && c.new === p.change.new).length === 1
    transport.postMessage('TrackAction', { index: p.index, accept, old: p.change.old, new: p.change.new, before: p.change.before, after: p.change.after, unique })
}

const showCard = (p: IPlaced, pageX: number, pageY: number) => {
    clearTimeout(hideTimer)
    if (shown === p && card && card.isConnected) return
    hideCard()
    const el = document.createElement('div')
    el.className = 'caret-track-card'
    const who = document.createElement('div')
    who.className = 'who'
    who.textContent = [config.author, p.change.day].filter(Boolean).join(' \u00b7 ')
    el.appendChild(who)
    if (p.change.old && p.change.old.trim()) {
        const old = document.createElement('div')
        old.className = 'old'
        old.textContent = short(p.change.old)
        el.appendChild(old)
    }
    const row = document.createElement('div')
    row.className = 'row'
    const yes = document.createElement('button')
    yes.textContent = '\u2713 ' + config.accept
    yes.onclick = () => act(p, true)
    const no = document.createElement('button')
    no.textContent = '\u2717 ' + config.reject
    no.onclick = () => act(p, false)
    row.appendChild(yes)
    row.appendChild(no)
    el.appendChild(row)
    el.onmouseenter = () => clearTimeout(hideTimer)
    el.onmouseleave = () => scheduleHide()
    ensureLayer().appendChild(el)
    card = el
    shown = p
    const width = el.offsetWidth
    const height = el.offsetHeight
    const left = Math.max(window.scrollX + 8, Math.min(pageX - 10, window.scrollX + window.innerWidth - width - 14))
    let top = pageY + 20
    if (top + height > window.scrollY + window.innerHeight - 8) top = Math.max(window.scrollY + 8, pageY - height - 12)
    el.style.left = left + 'px'
    el.style.top = top + 'px'
}

const scheduleHide = () => {
    clearTimeout(hideTimer)
    hideTimer = setTimeout(hideCard, 350)
}

// The red marks stand at the place a text was, at the end of the character before it (the start of the first one at the start of a block).
const placeBadges = () => {
    frame = 0
    placed.forEach(p => {
        if (!p.badge) return
        const point = p.point
        if (!point || !point.node.isConnected) { p.badge.style.display = 'none'; return }
        const length = (point.node.nodeValue || '').length
        const r = document.createRange()
        try {
            if (point.offset > 0) { r.setStart(point.node, point.offset - 1); r.setEnd(point.node, point.offset) }
            else { r.setStart(point.node, 0); r.setEnd(point.node, Math.min(1, length)) }
        } catch (e) { p.badge.style.display = 'none'; return }
        const rect = r.getBoundingClientRect()
        if (!rect.height) { p.badge.style.display = 'none'; return }
        p.badge.style.display = ''
        p.badge.style.left = (window.scrollX + (point.offset > 0 ? rect.right : rect.left) + (p.stack || 0) * 8) + 'px'
        p.badge.style.top = (window.scrollY + rect.top) + 'px'
        p.badge.style.height = rect.height + 'px'
    })
}

const replace = () => { if (!frame) frame = requestAnimationFrame(placeBadges) }

const scan = () => {
    timer = 0
    clearMarks()
    if (!changes.length) return
    ensureStyle()
    blocks = collect()
    const flat = blocks.map(b => b.text).join('\n')
    const byIndex = new Map(changes.map(c => [c.index, c]))
    const found = locateChanges(flat, changes)
    const adds: Range[] = []
    placed = found.map((f: any) => {
        const range = f.end > f.start ? rangeOf(f.start, f.end) : null
        if (range) adds.push(range)
        const p: IPlaced = { ...f, change: byIndex.get(f.index) as IChange, range, point: pointAt(f.at, f.at > 0 && f.end === f.start), badge: null }
        if (!range) {
            const badge = document.createElement('div')
            badge.className = 'caret-del-mark'
            badge.onmouseenter = () => { const r = badge.getBoundingClientRect(); showCard(p, window.scrollX + r.left, window.scrollY + r.bottom - 14) }
            badge.onmouseleave = () => scheduleHide()
            ensureLayer().appendChild(badge)
            p.badge = badge
        }
        return p
    })
    // marks at the same place stand side by side, so that each can be pointed at
    const stacks: { node: Node, offset: number, n: number }[] = []
    placed.forEach(p => {
        if (!p.badge || !p.point) return
        let s = stacks.find(x => x.node === p.point!.node && x.offset === p.point!.offset)
        if (!s) { s = { node: p.point.node, offset: p.point.offset, n: 0 }; stacks.push(s) }
        p.stack = s.n++
    })
    const css = w.CSS
    if (adds.length && css && css.highlights && typeof w.Highlight !== 'undefined') css.highlights.set('caret-add', new w.Highlight(...adds))
    placeBadges()
}

const schedule = () => {
    if (!changes.length) return
    clearTimeout(timer)
    timer = setTimeout(scan, 250)
}

// The page's own text under the mouse: a change that was added or replaced has its text highlighted, and pointing at it opens the card.
let moveFrame = 0
const onMove = (e: MouseEvent) => {
    if (!placed.length || moveFrame) return
    const x = e.clientX
    const y = e.clientY
    const target = e.target as Element | null
    if (target && target.closest && target.closest('.caret-track-card, .caret-del-mark')) return
    moveFrame = requestAnimationFrame(() => {
        moveFrame = 0
        const at = document.caretRangeFromPoint ? document.caretRangeFromPoint(x, y) : null
        const hit = at ? placed.find(p => {
            if (!p.range || !p.range.startContainer.isConnected || !p.range.endContainer.isConnected) return false
            try { return p.range.isPointInRange(at.startContainer, at.startOffset) } catch (err) { return false }
        }) : null
        if (hit) showCard(hit, window.scrollX + x, window.scrollY + y)
        else if (shown && shown.range) scheduleHide()
    })
}

// Shows change `index` (a click in the Review panel): scrolls to it and flashes it. False when it is not drawn here.
const jump = (index: number) => {
    const p = placed.find(x => x.index === index)
    if (!p) return false
    const css = w.CSS
    if (p.range && css && css.highlights && typeof w.Highlight !== 'undefined') {
        const el = p.range.startContainer.parentElement
        if (el) el.scrollIntoView({ block: 'center', behavior: 'smooth' })
        const flash = new w.Highlight(p.range)
        flash.priority = 10
        css.highlights.set('caret-flash', flash)
        setTimeout(() => css.highlights.delete('caret-flash'), 1600)
        return true
    }
    if (p.badge) {
        const rect = p.badge.getBoundingClientRect()
        window.scrollTo({ top: Math.max(0, window.scrollY + rect.top - window.innerHeight / 3), behavior: 'smooth' })
        p.badge.classList.add('caret-flash')
        const badge = p.badge
        setTimeout(() => badge.classList.remove('caret-flash'), 1600)
        return true
    }
    return false
}

// The host's list of changes (null or empty: nothing is drawn).
const set = (list: IChange[] | null, options?: Partial<IConfig>) => {
    changes = Array.isArray(list) ? list : []
    if (options) config = { ...config, ...options }
    if (!changes.length) { clearTimeout(timer); timer = 0; clearMarks(); return }
    schedule()
}

w.__caretTrackVisual = { set, jump }

new MutationObserver(records => {
    if (!changes.length) return
    if (records.every(r => layer && layer.contains(r.target))) return
    schedule()
}).observe(document.body, { childList: true, subtree: true, characterData: true })
window.addEventListener('scroll', replace, { passive: true })
window.addEventListener('resize', () => { replace(); schedule() })
document.addEventListener('mousemove', onMove, { passive: true })

export { set as setTrackVisual }