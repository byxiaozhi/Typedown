// The Speech ring (docs/speech-marks-design.md, section 5.4): in Speech mode a right-click opens a round menu around the
// pointer instead of a list. Six petals (Time, Pace, Volume, Tone, Cue, Mine) and, around the petal under the pointer, an
// arc of its items in an order that means something (slowest to fastest, shortest to longest, clockwise). It is drawn
// here, over the page and outside the text, so the selection and the focus are not disturbed; the host sends what to
// show (the same list as the Speech card) and the words in the language of the interface.
//
// Two ways to use it. Click: the ring stays open (Esc, a click outside or the middle button close it) and items can be
// added one after another. Flick: hold the right button, move toward a petal and then an item, let go to apply it; the
// ring closes. Items that are already on the selection are lit, and a click on a lit one takes the mark away. The
// geometry, the flick and the keyboard are in Muya/lib/parser/speechRing.js (tested alone); what is written is decided
// by the rules of speechEdit.js, through services/speechPage.ts, exactly as for the Speech card.
import 'services/speechRing.css'
import { appliedMark } from 'components/Muya/lib/parser/speechEdit'
import { paceStyle, SWATCHES } from 'components/Muya/lib/parser/speech'
import {
    PETALS, ITEM_HEIGHT, PETAL_RADIUS, PETAL_SIZE, CENTER_RADIUS,
    petalAngle, layoutPetals, limitItems, itemWidth, layoutArcIn, place, hit, keyStep
} from 'components/Muya/lib/parser/speechRing'
import { formatClock } from 'components/Muya/lib/parser/speechTiming'
import { insert, removeMark, reselectPlaced, sampleText, selectionInfo, speechModeOn, previewTiming, previewRemoval, Status } from './speechPage'

type Item = {
    label: string, spec: any, written: string, meaning: string, role: string, kind: string, style: string,
    speed: number, perWord: number, color: string, mine: boolean, match: any, more?: boolean
}
type Group = { id: string, label: string, items: Item[] }
type Catalog = { labels: Record<string, string>, groups: Group[] }

const ring: {
    catalog?: Catalog
    el?: HTMLElement
    // where the ring is, and what is open
    cx: number, cy: number, open: number, arc: any[], items: Item[], lit: boolean[]
    keys: { level: 'petals' | 'arc', petal: number, item: number }
    sample: string
    // the right button is down (`down`), and the ring is following the pointer, so a flick may be on its way (`holding`)
    down: boolean, holding: boolean
    // the click that opened the ring, or an ordinary menu that was asked for: what the page tells the host about it
    swallow: boolean, plain: boolean
} = { cx: 0, cy: 0, open: -1, arc: [], items: [], lit: [], keys: { level: 'petals', petal: 0, item: 0 }, sample: '', down: false, holding: false, swallow: false, plain: false }

export const setSpeechRing = (catalog: Catalog | undefined) => {
    ring.catalog = catalog && Array.isArray(catalog.groups) ? catalog : undefined
    if (ring.el && !ring.catalog) closeRing()
}

const label = (key: string) => ring.catalog?.labels?.[key] ?? ''
const view = () => ({ width: document.documentElement.clientWidth, height: document.documentElement.clientHeight })
const elementOf = (node: Node | null): Element | null => node && node.nodeType === 1 ? node as Element : node && node.parentElement
const mark = (text: string, attrs: Record<string, string> = {}, tag = 'div', parent?: HTMLElement) => {
    const e = document.createElement(tag)
    e.textContent = text
    for (const k of Object.keys(attrs)) e.setAttribute(k, attrs[k])
    if (parent) parent.appendChild(e)
    return e
}

// --- where the ring may open ---

// Plain text and selected text: the ring. Everything the ordinary menu is needed for stays with it, and a link, an image, a
// table or code are not prose where a mark could be written.
const OTHER = 'a, img, table, pre, .ag-front-matter, [class*="ag-code"], .ag-math, .ag-image, .ag-html-block'
const eligible = (target: Element | null, x?: number, y?: number): boolean => {
    if (!speechModeOn() || !ring.catalog || !target) return false
    if (target.closest('.CodeMirror') || !target.closest('.ag-paragraph') || target.closest(OTHER)) return false
    const w = window as any
    if (x !== undefined && y !== undefined) {
        if (w.__caretSpell && w.__caretSpell.atPoint && w.__caretSpell.atPoint(x, y)) return false
        if (w.__caretReview && w.__caretReview.chainAt && w.__caretReview.chainAt(x, y)) return false
    }
    return true
}

const caretPoint = (): { x: number, y: number, target: Element | null } | null => {
    const sel = window.getSelection()
    if (!sel || sel.rangeCount === 0) return null
    const range = sel.getRangeAt(0)
    const target = elementOf(range.startContainer)
    let rect = range.getClientRects()[0] || range.getBoundingClientRect()
    if ((!rect || (!rect.width && !rect.height && !rect.top)) && target) rect = target.getBoundingClientRect()
    return rect ? { x: rect.left + Math.min(rect.width, 8), y: rect.bottom, target } : null
}

// --- what is lit ---

const refreshLit = () => {
    const info = selectionInfo()
    ring.lit = ring.items.map(item => !!info && !!item.match && !item.more && !!appliedMark(info.text, info.start, info.end, item.match, info.defs))
    ring.sample = sampleText()
    if (ring.el) ring.el.querySelectorAll<HTMLElement>('.caret-ring-item').forEach((button, i) => paintItem(button, i))
}

const paintItem = (button: HTMLElement, i: number) => {
    const item = ring.items[i]
    const lit = !!ring.lit[i]
    button.classList.toggle('lit', lit)
    button.textContent = `${lit ? '✓ ' : ''}${item.mine ? '★ ' : ''}${item.label}`
    const state = item.more || !item.match ? '' : `, ${label(lit ? 'applied' : 'notApplied')}`
    button.setAttribute('aria-label', `${item.label}${state}${item.meaning ? `, ${item.meaning}` : ''}`)
}

// --- drawing ---

const closeRing = () => {
    if (ring.el) ring.el.remove()
    ring.el = undefined
    ring.open = -1
    ring.arc = []
    ring.items = []
    ring.lit = []
    ring.holding = false
}

export const openRing = (x: number, y: number) => {
    if (!ring.catalog) return false
    closeRing()
    const size = view()
    const at = place(x, y, size)
    ring.cx = at.x
    ring.cy = at.y
    ring.keys = { level: 'petals', petal: 0, item: 0 }
    ring.sample = sampleText()

    const root = mark('', { class: 'caret-ring', role: 'group', 'aria-label': label('ring') })
    root.style.left = `${at.x}px`
    root.style.top = `${at.y}px`
    // pressing on the ring must not move the focus or the selection of the page
    root.addEventListener('mousedown', e => e.preventDefault())
    const put = (e: HTMLElement, dx: number, dy: number) => {
        e.style.left = `${dx}px`
        e.style.top = `${dy}px`
        root.appendChild(e)
    }

    const center = mark('☰', { class: 'caret-ring-center', type: 'button', 'aria-label': label('center'), title: label('center') }, 'button')
    center.style.width = center.style.height = `${CENTER_RADIUS * 2}px`
    center.addEventListener('click', () => ordinaryMenu())
    put(center, 0, 0)

    const spots = layoutPetals(PETALS.length)
    PETALS.forEach((id, i) => {
        const group = ring.catalog!.groups.find(g => g.id === id)
        const count = group ? group.items.length : 0
        const petal = mark(group ? group.label : id, {
            class: `caret-ring-petal${count ? '' : ' empty'}`,
            type: 'button',
            'aria-haspopup': 'true',
            'aria-expanded': 'false',
            // no tooltip on a petal with items: it would cover the items; an empty one says why
            title: count ? '' : label('empty')
        }, 'button')
        if (!count) petal.setAttribute('aria-disabled', 'true')
        petal.style.width = petal.style.height = `${PETAL_SIZE * 2}px`
        petal.addEventListener('click', () => { if (count) openArc(i, true) })
        put(petal, spots[i].x, spots[i].y)
    })

    const status = mark('', { class: 'caret-ring-live', role: 'status', 'aria-live': 'polite' })
    root.appendChild(status)
    const preview = mark('', { class: 'caret-ring-preview' })
    root.appendChild(preview)
    document.body.appendChild(root)
    ring.el = root
    showPreview(null)
    return true
}

const announce = (text: string) => {
    const live = ring.el?.querySelector('.caret-ring-live')
    if (live) live.textContent = text
}

const openArc = (index: number, keyboard = false) => {
    const root = ring.el
    const group = ring.catalog?.groups.find(g => g.id === PETALS[index])
    if (!root || !group) return
    root.querySelectorAll('.caret-ring-item').forEach(e => e.remove())
    root.querySelectorAll('.caret-ring-petal').forEach((e, i) => {
        e.classList.toggle('open', i === index)
        e.setAttribute('aria-expanded', i === index ? 'true' : 'false')
    })
    ring.open = index
    ring.items = limitItems(group.items, { label: label('more'), spec: null, written: '', meaning: '', role: 'point', kind: '', style: '', speed: 1, perWord: 0, color: '', mine: false, match: null }) as Item[]
    // room for the check mark and the star that may stand before the label
    const sizes = ring.items.map(item => ({ w: itemWidth(`xxxx${item.label}`), h: ITEM_HEIGHT }))
    // turned away from the border of the window when the ring is near it
    ring.arc = layoutArcIn(petalAngle(index), sizes, { x: ring.cx, y: ring.cy }, view()).items
    ring.arc.forEach((box, i) => {
        const button = mark('', { class: 'caret-ring-item', type: 'button' }, 'button')
        button.style.width = `${box.w}px`
        button.style.height = `${box.h}px`
        button.style.left = `${box.x}px`
        button.style.top = `${box.y}px`
        button.addEventListener('click', () => applyItem(i, false))
        button.addEventListener('mouseenter', () => showPreview(i))
        root.appendChild(button)
    })
    refreshLit()
    if (keyboard) ring.keys = { level: 'arc', petal: index, item: 0 }
    showPreview(null)
}

// The card beside the ring: what the item writes, its meaning, the words it is about drawn as they would look, and
// whether it is applied now. Where it is: on the side opposite the open arc, so it never covers it.
const sampleStyle = (item: Item): string => {
    const pace = (speed: number, perWord: number) => {
        const s = paceStyle({ speed, perWord }) as any
        return `letter-spacing:${s.letterSpacing};${s.wordSpacing ? `word-spacing:${s.wordSpacing};` : ''}text-decoration:underline ${speed < 1 ? 'dotted' : 'dashed'};text-underline-offset:.22em;`
    }
    switch (item.style) {
        case 'pace': return pace(item.speed, item.perWord)
        case 'loud': return 'font-size:1.12em;font-weight:600;'
        case 'soft': return 'font-size:.92em;color:var(--editorColor60);'
        case 'emphasis': return 'color:var(--speechEmphasis);font-weight:600;text-decoration:underline;text-underline-offset:.22em;'
        case 'tone': return 'background:var(--speechToneBg);padding:0 .25em;'
        case 'user': {
            let css = item.kind === 'pace' ? pace(item.speed, item.perWord) : ''
            if (SWATCHES.includes(item.color)) css += `background:var(--speech${item.color[0].toUpperCase()}${item.color.substring(1)}Bg);padding:0 .25em;`
            return css
        }
        default: return ''
    }
}

// What the item does to the time of the words it is about: "0:05 -> 0:09", or "+0:03" for a pause. `visible` is drawn, `spoken`
// is for a screen reader (the arrow is not read well).
const timeOf = (index: number): { visible: string, spoken: string } | null => {
    const item = ring.items[index]
    if (!item || item.more || (!item.spec && !item.match)) return null
    const lit = !!ring.lit[index]
    const change = lit && item.match ? previewRemoval(item.match) : previewTiming(item.spec)
    if (!change) return null
    const fill = (text: string, a: string, b = '') => text.replace('{0}', a).replace('{1}', b)
    if (change.role === 'point') {
        const added = formatClock(change.after)
        return { visible: `${label('time')} +${added}`, spoken: fill(label('timeAdds'), added) }
    }
    const from = formatClock(change.before)
    const to = formatClock(change.after)
    return { visible: `${label('time')} ${from} → ${to}`, spoken: fill(label('timeFromTo'), from, to) }
}

const showPreview = (index: number | null, problem = '') => {
    const card = ring.el?.querySelector<HTMLElement>('.caret-ring-preview')
    if (!card) return
    card.textContent = ''
    const item = index === null ? null : ring.items[index]
    if (problem) mark(problem, { class: 'caret-ring-problem' }, 'div', card)
    if (item && !item.more) {
        const lit = !!ring.lit[index as number]
        const head = mark('', { class: 'caret-ring-title' }, 'div', card)
        head.textContent = `${item.mine ? '★ ' : ''}${item.label}${item.match ? ` · ${label(lit ? 'applied' : 'notApplied')}` : ''}`
        if (item.written) mark(item.written, { class: 'caret-ring-written' }, 'div', card)
        if (item.meaning) mark(item.meaning, { class: 'caret-ring-meaning' }, 'div', card)
        if (item.role === 'pair' && ring.sample) {
            const sample = mark(ring.sample, { class: 'caret-ring-sample' }, 'div', card)
            sample.setAttribute('style', sampleStyle(item))
        }
        const time = timeOf(index as number)
        if (time) mark(time.visible, { class: 'caret-ring-time', 'aria-label': time.spoken }, 'div', card)
    } else if (!problem) {
        mark(label('hint'), { class: 'caret-ring-meaning' }, 'div', card)
    }
    const spot = previewSpot(card.offsetWidth || 250, card.offsetHeight || 80)
    card.style.left = `${spot.x}px`
    card.style.top = `${spot.y}px`
}

// Where the card goes, relative to the centre of the ring: on the side opposite the open arc, or else the nearest side
// where it is inside the window and covers neither the petals nor an item of the arc.
const previewSpot = (width: number, height: number) => {
    const size = view()
    const open = ring.open >= 0
    const first = open ? petalAngle(ring.open) + 180 : 90
    const around = open ? [0, 45, -45, 90, -90, 135, -135, 180] : [0, 180, 90, -90, 45, -45, 135, -135]
    const reach = PETAL_RADIUS + PETAL_SIZE + 10
    const fits = (x: number, y: number) =>
        ring.cx + x - width / 2 >= 6 && ring.cx + x + width / 2 <= size.width - 6 && ring.cy + y - height / 2 >= 6 && ring.cy + y + height / 2 <= size.height - 6 &&
        !ring.arc.some(box => Math.abs(box.x - x) < (box.w + width) / 2 + 6 && Math.abs(box.y - y) < (box.h + height) / 2 + 6)
    let best = { x: 0, y: reach + height / 2 }
    for (const turn of around) {
        const rad = ((first + turn) * Math.PI) / 180
        const c = Math.cos(rad)
        const s = Math.sin(rad)
        const distance = reach + (width / 2) * Math.abs(c) + (height / 2) * Math.abs(s)
        const x = c * distance
        const y = s * distance
        if (fits(x, y)) return { x, y }
        if (turn === 0) best = { x, y }
    }
    // nowhere it fits whole: on the preferred side, moved into the window
    return {
        x: Math.min(Math.max(ring.cx + best.x, 6 + width / 2), size.width - 6 - width / 2) - ring.cx,
        y: Math.min(Math.max(ring.cy + best.y, 6 + height / 2), size.height - 6 - height / 2) - ring.cy
    }
}

// --- doing something ---

const problemOf = (status: Status): string => {
    switch (status) {
        case 'nothing': return label('nothing')
        case 'unsafe': return label('unsafeText')
        case 'several': return label('several')
        case 'unknown': return label('unknown')
        default: return label('noFocus')
    }
}

const applyItem = (index: number, flick: boolean) => {
    const item = ring.items[index]
    if (!item) return
    if (item.more) {
        const id = PETALS[ring.open]
        closeRing()
        post('SpeechMore', { group: id })
        return
    }
    const lit = !!ring.lit[index]
    const status: Status = lit && item.match ? removeMark(item.match) : insert(item.spec)
    if (status !== 'ok') {
        const problem = problemOf(status)
        showPreview(index, problem)
        announce(problem)
        return
    }
    if (flick) {
        closeRing()
        return
    }
    // the editor draws its paragraph again; then the new words are selected, so the next item goes on them
    setTimeout(() => {
        if (!ring.el) return
        reselectPlaced()
        refreshLit()
        showPreview(index)
        announce(`${item.label}, ${label(lit ? 'notApplied' : 'applied')}`)
    }, 80)
}

const post = (name: string, args: any) => {
    const host = (window as any).chrome && (window as any).chrome.webview
    if (host) host.postMessage(JSON.stringify({ type: 'message', name, args }))
}

// The round button in the middle: the ordinary menu (Cut, Copy, Paste ...), where the ring was.
const ordinaryMenu = () => {
    const x = ring.cx
    const y = ring.cy
    const sel = window.getSelection()
    closeRing()
    post('ContextMenu', { x, y, hasSelection: !!sel && !sel.isCollapsed, code: false, spell: '', review: '', fromRing: true })
}

// --- the mouse ---

const hitAt = (e: MouseEvent) => hit(e.clientX - ring.cx, e.clientY - ring.cy, ring.open, ring.arc, PETALS.length)

// The pointer moves over the ring (a flick, or just looking): the petal under it opens its arc; the item under it is
// previewed and marked.
const follow = (e: MouseEvent) => {
    if (!ring.el) return
    const found = hitAt(e)
    ring.el.querySelectorAll('.hot').forEach(h => h.classList.remove('hot'))
    if (found.kind === 'petal') {
        const group = ring.catalog?.groups.find(g => g.id === PETALS[found.petal as number])
        if (found.petal !== ring.open && group && group.items.length) openArc(found.petal as number)
        ring.el.querySelectorAll('.caret-ring-petal')[found.petal as number]?.classList.add('hot')
    } else if (found.kind === 'item') {
        const buttons = ring.el.querySelectorAll('.caret-ring-item')
        buttons[found.item as number]?.classList.add('hot')
        showPreview(found.item as number)
    } else if (found.kind === 'center') {
        ring.el.querySelector('.caret-ring-center')?.classList.add('hot')
    }
}

window.addEventListener('mousedown', e => {
    const target = elementOf(e.target as Node)
    const inside = !!ring.el && !!target && ring.el.contains(target)
    if (e.button === 1) {
        if (ring.el) { closeRing(); e.preventDefault() }
        return
    }
    if (inside) return
    if (e.button !== 2) { closeRing(); return }
    // the right button: on plain text the ring opens (at once, so a flick can begin); on anything else, or with Shift, the
    // host's ordinary menu follows as before
    closeRing()
    ring.swallow = false
    ring.plain = false
    if (e.shiftKey || !eligible(target, e.clientX, e.clientY)) {
        ring.plain = true
        return
    }
    ring.swallow = true
    ring.down = true
    const { clientX, clientY } = e
    setTimeout(() => { if (ring.swallow && openRing(clientX, clientY)) ring.holding = ring.down }, 0)
}, true)

window.addEventListener('mousemove', e => {
    if (!ring.el) return
    if (ring.holding && (e.buttons & 2) === 0) { ring.down = false; release(e); return }
    follow(e)
}, true)

// The right button is let go: over an item that is a flick, and the item is applied; anywhere else the ring stays open.
const release = (e: MouseEvent) => {
    if (!ring.holding) return
    ring.holding = false
    if (!ring.el) return
    const found = hitAt(e)
    if (found.kind === 'item') applyItem(found.item as number, true)
}

window.addEventListener('mouseup', e => {
    if (e.button !== 2) return
    ring.down = false
    release(e)
    // the ordinary menu event follows at once; a flag that is not used must not outlive it
    setTimeout(() => { ring.swallow = false; ring.plain = false }, 400)
}, true)

// The page asks, from the host's script for the context menu, whether the ring has this click: the click that opened it
// (or whose ordinary menu was asked for) and a click from the keyboard (the menu key, Shift+F10), which opens the ring at
// the caret.
const context = (e: MouseEvent): boolean => {
    if (!speechModeOn() || !ring.catalog) return false
    // a right-click on the ring itself is not for the ordinary menu
    if (ring.el && ring.el.contains(e.target as Node)) return true
    if (ring.swallow) { ring.swallow = false; return true }
    if (ring.plain) { ring.plain = false; return false }
    if (e.button === 2) return false
    const at = caretPoint()
    if (!at || !eligible(at.target)) return false
    openRing(at.x, at.y)
    return true
}

// --- the keyboard ---

window.addEventListener('keydown', e => {
    if (!ring.el || e.ctrlKey || e.altKey || e.metaKey) return
    const counts = PETALS.map(id => ring.catalog?.groups.find(g => g.id === id)?.items.length ?? 0)
    const step = keyStep(ring.keys as any, e.key, counts.map((n, i) => (i === ring.keys.petal && ring.keys.level === 'arc' ? ring.items.length : n)))
    const handled = ['Escape', 'Enter', ' ', 'Backspace', 'Home', 'End', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.key) || /^[1-9]$/.test(e.key)
    if (!handled) return
    e.preventDefault()
    e.stopPropagation()
    ring.keys = step.state as any
    const action: any = step.action
    if (action === 'close') { closeRing(); return }
    if (action === 'back') { ring.el.querySelectorAll('.caret-ring-item').forEach(i => i.remove()); ring.open = -1; ring.items = []; showPreview(null) }
    if (action && action.open !== undefined) openArc(action.open, true)
    if (action && action.apply) { applyItem(action.apply.item, false); return }
    // what is under the keyboard: marked, previewed and read out
    ring.el.querySelectorAll('.hot').forEach(h => h.classList.remove('hot'))
    if (ring.keys.level === 'petals') {
        const group = ring.catalog?.groups.find(g => g.id === PETALS[ring.keys.petal])
        ring.el.querySelectorAll('.caret-ring-petal')[ring.keys.petal]?.classList.add('hot')
        announce(group ? `${group.label}, ${group.items.length || label('empty')}` : '')
    } else {
        ring.el.querySelectorAll('.caret-ring-item')[ring.keys.item]?.classList.add('hot')
        const item = ring.items[ring.keys.item]
        showPreview(ring.keys.item)
        const time = timeOf(ring.keys.item)
        if (item) announce(`${item.label}${item.more || !item.match ? '' : `, ${label(ring.lit[ring.keys.item] ? 'applied' : 'notApplied')}`}${item.meaning ? `, ${item.meaning}` : ''}${time ? `, ${time.spoken}` : ''}`)
    }
}, true)

// Anything that moves the page under the ring closes it.
window.addEventListener('resize', closeRing)
window.addEventListener('blur', closeRing)
window.addEventListener('scroll', () => { if (ring.el) closeRing() }, true)
window.addEventListener('wheel', e => { if (ring.el && !ring.el.contains(e.target as Node)) closeRing() }, { capture: true, passive: true })

const caret = (window as any).__caretSpeech
if (caret) {
    caret.openRing = (x: number, y: number) => openRing(x, y)
    caret.context = context
}
