import React, { useCallback, useEffect, useRef, useState } from "react";
import { remote } from "services/remote";
import transport from "services/transport";
import { MAX_TICKS, scanSource, TickKind } from "./scan";
import './index.scss'

// The document map (View > Document map): a thin strip along the right edge of the page. The page's own scroll bar is hidden
// (App.scss), so the strip is also the one sign of where in a long note the window is (the lighter rectangle) and a way to move: a
// click or a drag scrolls, like a scroll bar. On it, in the place they have in the note, are ticks for the headings (grey), the
// matches of a search (the colour of the app; the current match brighter), the review changes (green added, red deleted, amber
// highlighted), the comments (yellow) and the speech pauses (blue). A click on a tick goes to it; the pointer over one says its
// first words; Tab reaches the strip and the arrow keys step from tick to tick. It is drawn for the Visual view and the Code view
// (the Split view has its own scroll bar for the source pane).
interface ITick {
    kind: TickKind
    top: number      // in pixels from the top of the page
    text: string
}

interface IProps {
    // the Code view (the text is read) or the Visual view (the page is asked)
    source: boolean
    markdown: string
    // the review marks are drawn; Speech mode is on
    review: boolean
    speech: boolean
}

const scroller = () => document.scrollingElement || document.documentElement
// the zero-width characters the editor puts between the words are not words
const ZERO_WIDTH = new RegExp([0x200b, 0x200c, 0x200d, 0xfeff].map(code => String.fromCharCode(code)).join('|'), 'g')
const clean = (text: string) => text.replace(ZERO_WIDTH, '').replace(/\s+/g, ' ').trim()

// How close (in pixels of the strip) the pointer must be to a tick to mean it.
const NEAR = 6
// A change in the page is looked at again after this many milliseconds without another.
const SETTLE = 250

const LABEL_KEY = 'DocumentMapMenuItem_Text'

// The strip is a stop of its own in F6 (the areas of the window): the editor swallows Tab, so the host asks the page to focus the strip
// (window.__caretMap.focus) and, going back, to give the focus to the text again (release), where the caret was.
let savedRange: Range | null = null
const focusStrip = (strip: HTMLElement | null): boolean => {
    if (!strip) return false
    const selection = window.getSelection()
    savedRange = selection && selection.rangeCount > 0 ? selection.getRangeAt(0).cloneRange() : null
    strip.focus()
    return true
}
const releaseToText = () => {
    const cm = (document.querySelector('.CodeMirror') as any)?.CodeMirror
    if (cm) { cm.focus(); return }
    const root = document.getElementById('ag-editor-id')
    if (!root) return
    root.focus()
    if (savedRange) {
        const selection = window.getSelection()
        selection?.removeAllRanges()
        selection?.addRange(savedRange)
    }
}

// The Visual view: the elements the editor has drawn.
const collectVisual = (review: boolean, speech: boolean): ITick[] => {
    const root = document.getElementById('ag-editor-id')
    if (!root) return []
    const base = window.scrollY
    const ticks: ITick[] = []
    const add = (kind: TickKind, selector: string) => {
        root.querySelectorAll(selector).forEach(element => {
            ticks.push({ kind, top: element.getBoundingClientRect().top + base, text: clean(element.textContent ?? '') })
        })
    }
    // In the order of what the cap (MAX_TICKS) keeps first: the current match, the marks of the review and the speech (they are few), the
    // headings, then the other matches (there can be thousands).
    add('current', 'span.ag-highlight')
    if (review) {
        add('add', '.ag-critic-add')
        add('del', '.ag-critic-del')
        add('mark', '.ag-critic-mark')
        add('comment', '.ag-critic-comment:not(.ag-critic-stamp)')
    }
    if (speech) add('pause', '.ag-speech-chip.ag-speech-pause')
    add('heading', 'h1, h2, h3, h4, h5, h6')
    add('match', 'span.ag-selection')
    return ticks.slice(0, MAX_TICKS)
}

// The Code view: the text is read (what is out of sight is not drawn, so the page cannot be asked), and CodeMirror says how high
// each line is and where the matches of a search are marked.
const collectSource = (review: boolean, speech: boolean): ITick[] => {
    const cm = (document.querySelector('.CodeMirror') as any)?.CodeMirror
    if (!cm) return []
    // the same order as collectVisual, for the cap: the current match first, the other matches last
    const matches: ITick[] = []
    const ticks: ITick[] = []
    for (const marker of cm.getAllMarks()) {
        const kind = marker.className === 'ag-highlight' ? 'current' : marker.className === 'ag-selection' ? 'match' : null
        const at = kind ? marker.find() : null
        if (kind && at?.from) (kind === 'current' ? ticks : matches).push({ kind, top: cm.heightAtLine(at.from.line, 'page'), text: clean(cm.getRange(at.from, at.to)) })
    }
    const items = scanSource(cm.getValue(), review, speech).map(item => ({
        kind: item.kind,
        top: cm.heightAtLine(item.line, 'page'),
        text: item.text
    }))
    ticks.push(...items.filter(item => item.kind !== 'heading'), ...items.filter(item => item.kind === 'heading'), ...matches)
    return ticks.slice(0, MAX_TICKS)
}

const DocumentMap: React.FC<IProps> = ({ source, markdown, review, speech }) => {
    const [ticks, setTicks] = useState<ITick[]>([])
    const [metrics, setMetrics] = useState({ total: 0, height: 0, scrollY: 0 })
    const [hover, setHover] = useState<{ tick: ITick, y: number } | null>(null)
    const [label, setLabel] = useState('')
    const [announced, setAnnounced] = useState('')
    const [focused, setFocused] = useState(false)
    const stripRef = useRef<HTMLDivElement>(null)
    const press = useRef<{ y: number, moved: boolean } | null>(null)
    // the tick the arrow keys went to last (-1: none, or the user has moved the window since): the next step starts from it
    const current = useRef(-1)
    // the ticks as last drawn (to find the one the keys went to again after a refresh), and when the keys last made the window jump
    const ticksRef = useRef<ITick[]>([])
    const lastJump = useRef(0)

    useEffect(() => {
        remote.getStringResources({ names: [LABEL_KEY] }).then(dic => setLabel(dic[LABEL_KEY] ?? '')).catch(() => { })
    }, [])

    const measure = useCallback(() => {
        const total = scroller().scrollHeight
        const height = window.innerHeight
        setMetrics(old => old.total === total && old.height === height && Math.abs(old.scrollY - window.scrollY) < 0.5 ? old : { total, height, scrollY: window.scrollY })
    }, [])

    const refresh = useCallback(() => {
        const found = (source ? collectSource(review, speech) : collectVisual(review, speech)).sort((a, b) => a.top - b.top)
        // the tick the keys went to stays the current one when it is still there (a refresh must not send the next step back)
        const previous = ticksRef.current[current.current]
        ticksRef.current = found
        current.current = previous
            ? found.findIndex(t => t.kind === previous.kind && t.text === previous.text && Math.abs(t.top - previous.top) < 2)
            : -1
        setTicks(found)
        measure()
    }, [source, review, speech, measure])

    // The text changed: look again once it has settled. The page changing without the text changing (a search, the marks drawn or
    // hidden, a picture loading) is seen by the observers below.
    useEffect(() => {
        const timer = window.setTimeout(refresh, SETTLE)
        return () => window.clearTimeout(timer)
    }, [markdown, refresh])

    useEffect(() => {
        let timer: number | undefined
        const later = () => {
            window.clearTimeout(timer)
            timer = window.setTimeout(refresh, SETTLE)
        }
        let mutations: MutationObserver | undefined
        let target: Element | null = null
        // the editor's element may not be there yet when the strip is first drawn
        const attach = () => {
            const root = document.getElementById('ag-editor-id') ?? document.querySelector('.CodeMirror')
            if (!root || root === target) return
            mutations?.disconnect()
            target = root
            mutations = new MutationObserver(later)
            mutations.observe(root, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ['class'] })
        }
        attach()
        const watch = window.setInterval(attach, 1000)
        const resizes = new ResizeObserver(later)
        resizes.observe(document.body)
        addEventListener('resize', later)
        addEventListener('scroll', measure, { passive: true })
        // the user moving the window with the wheel starts the arrow keys over from where the window is
        const forget = () => { current.current = -1 }
        addEventListener('wheel', forget, { passive: true })
        later()
        return () => {
            window.clearTimeout(timer)
            window.clearInterval(watch)
            mutations?.disconnect()
            resizes.disconnect()
            removeEventListener('resize', later)
            removeEventListener('scroll', measure)
            removeEventListener('wheel', forget)
        }
    }, [refresh, measure])

    const { total, height, scrollY } = metrics
    // nothing to map in a note that fits the window
    const shown = total > height + 1 && height > 0

    // The host is told whether the strip is there and whether it has the focus (F6 passes through it), and can ask for the focus.
    useEffect(() => {
        transport.postMessageNoDiff('DocumentMapState', { shown, focused: shown && focused })
    }, [shown, focused])
    useEffect(() => {
        (window as any).__caretMap = { focus: () => focusStrip(stripRef.current), release: releaseToText }
        return () => {
            delete (window as any).__caretMap
            transport.postMessageNoDiff('DocumentMapState', { shown: false, focused: false })
        }
    }, [])

    if (!shown) return null

    const toY = (top: number) => Math.min(height - 1, Math.max(0, top / total * height))
    const scrollTo = (top: number, smooth = false) =>
        window.scrollTo({ left: window.scrollX, top: Math.max(0, top - window.innerHeight / 2), behavior: smooth ? 'smooth' : 'auto' })
    const toScroll = (y: number) => y / height * total

    const nearest = (y: number): ITick | null => {
        let best: ITick | null = null
        let bestDistance = NEAR + 1
        for (const tick of ticks) {
            // the headings are on every note and close together: the other kinds win when they are as near
            const distance = Math.abs(toY(tick.top) - y) + (tick.kind === 'heading' ? 0.5 : 0)
            if (distance < bestDistance) { best = tick; bestDistance = distance }
        }
        return best
    }

    const yOf = (e: React.PointerEvent) => e.clientY - (stripRef.current?.getBoundingClientRect().top ?? 0)

    const onPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
        e.preventDefault()   // the caret and the focus stay where they were
        e.currentTarget.setPointerCapture(e.pointerId)
        press.current = { y: e.clientY, moved: false }
        current.current = -1
        scrollTo(toScroll(yOf(e)))
    }

    const onPointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
        const y = yOf(e)
        if (press.current) {
            if (Math.abs(e.clientY - press.current.y) > 3) press.current.moved = true
            scrollTo(toScroll(y))
            setHover(null)
            return
        }
        const tick = nearest(y)
        setHover(tick ? { tick, y } : null)
    }

    const onPointerUp = (e: React.PointerEvent<HTMLDivElement>) => {
        // a click (not a drag) on a tick goes to the tick itself
        const tick = press.current && !press.current.moved ? nearest(yOf(e)) : null
        press.current = null
        if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId)
        if (tick) scrollTo(tick.top, true)
    }

    // Arrow keys: the next and the previous tick from the middle of the window; Home and End the first and the last.
    const onKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
        if (e.key === 'Escape') {
            e.preventDefault()
            releaseToText()
            return
        }
        if (ticks.length === 0) return
        // From the tick the keys went to last; with none (or after the window was moved by hand) from the middle of the window. The
        // window cannot centre a tick in the first or last half screen, so its middle is no guide once the keys are in use.
        const last = ticks.length - 1
        // The remembered tick counts only while it is in the window, or while the jump to it is still going on (the window may have been
        // moved some other way since: the scroll bar, PageDown, a search): then the step starts from where the window is.
        const remembered = current.current >= 0 && current.current <= last ? ticks[current.current] : undefined
        const onScreen = !!remembered && remembered.top >= window.scrollY && remembered.top <= window.scrollY + window.innerHeight
        const used = !!remembered && (onScreen || Date.now() - lastJump.current < 800)
        const middle = window.scrollY + window.innerHeight / 2
        let index: number | undefined
        if (e.key === 'ArrowDown' || e.key === 'ArrowRight') {
            const next = ticks.findIndex(t => t.top > middle + 4)
            index = used ? Math.min(last, current.current + 1) : next < 0 ? last : next
        } else if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') {
            let before = -1
            ticks.forEach((t, i) => { if (t.top < middle - 4) before = i })
            index = used ? Math.max(0, current.current - 1) : Math.max(0, before)
        } else if (e.key === 'Home') index = 0
        else if (e.key === 'End') index = last
        if (index === undefined) return
        current.current = index
        lastJump.current = Date.now()
        const to = ticks[index]
        e.preventDefault()
        scrollTo(to.top, true)
        setAnnounced(`${to.kind}: ${to.text} (${index + 1}/${ticks.length})`)
    }

    const thumbHeight = Math.max(16, height * height / total)
    const thumbTop = (height - thumbHeight) * Math.min(1, scrollY / Math.max(1, total - height))

    return (
        <div
            ref={stripRef}
            className="document-map"
            role="group"
            aria-label={label || undefined}
            tabIndex={0}
            onPointerDown={onPointerDown}
            onPointerMove={onPointerMove}
            onPointerUp={onPointerUp}
            onPointerCancel={onPointerUp}
            onPointerLeave={() => setHover(null)}
            onKeyDown={onKeyDown}
            onFocus={() => setFocused(true)}
            onBlur={() => setFocused(false)}
        >
            <div className="dm-view" style={{ top: thumbTop, height: thumbHeight }} />
            {ticks.map((tick, i) => <div key={i} className={`dm-tick dm-${tick.kind}`} style={{ top: toY(tick.top) }} />)}
            {hover && <div className="dm-tip" style={{ top: Math.max(0, hover.y - 12) }}>{hover.tick.text || '…'}</div>}
            <div className="dm-live" role="status" aria-live="polite">{announced}</div>
        </div>
    )
}

export default DocumentMap
