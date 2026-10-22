// The teleprompter and the speaking clock (docs/speech-marks-design.md, step 2). A window of its own (a second screen is the real
// use) that shows the talk in big text, scrolls it at the planned pace (each paragraph passes in its planned time), draws the
// marks as large cues, counts down to the next pause, and keeps a clock: time spoken, time left, ahead or behind the plan, the
// section and its light. Start and stop on Space, next and back on the arrows and Page Up/Down (a presentation clicker sends
// those), speed on Up/Down; a step mode that does not scroll by itself (also the default when Windows asks for less motion).
// The plan and the clockwork are plain functions (plan.js, player.js) with their own tests; this file draws them and keeps the
// time. Still no network, no audio: the text comes from the main window, the time from the clock of the PC.
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import './teleprompter.css'
import { buildPlan } from './plan'
import { nextBlockStart, prevBlockStart, upcoming, clockState, blockAt, sectionAt } from './player'
import { formatClock } from '../components/Muya/lib/parser/speechTiming'
import { paceStyle, SWATCHES } from '../components/Muya/lib/parser/speech'
import { summarizeRun, formatRun } from './rehearsal'
import { groupLines, buildPath, yAt, follow, smooth, tAtY, nearestLine, steadyPath, pageY } from './scrollPath'

type Script = {
    markdown: string, wpm: number, headingsSpoken: boolean, styles?: Record<string, { color?: string, icon?: string }>,
    labels?: Record<string, string>, title?: string
}

const DEFAULT_LABELS: Record<string, string> = {
    start: 'Start', stop: 'Stop', elapsed: 'Spoken', left: 'Left', over: 'over', planned: 'Planned', ahead: 'Ahead', behind: 'Behind', onPlan: 'On plan',
    pause: 'Pause', audience: 'Audience', pauseIn: 'Pause in', audienceIn: 'Audience in', now: 'Now', auto: 'Automatic', step: 'Step by step',
    next: 'Next', back: 'Back', mirror: 'Mirror', flip: 'Flip', dark: 'Dark', light: 'Light', fullscreen: 'Full screen', clock: 'Clock', close: 'Close',
    speed: 'Speed', wpmShort: 'wpm', size: 'Text size', end: 'End of the talk', empty: 'Nothing to read yet.', waiting: 'Waiting for the text…',
    help: 'Space start/stop · E rehearse · ← → or Page Up/Down back/next · ↑ ↓ speed · wheel or click move · J sections · O options · S step mode · M mirror · V flip · D dark/light · [ ] size · C clock · F full screen · R restart · Esc stop',
    section: 'Section', options: 'Options', focus: 'Focus band', countdown: 'Countdown before start', off: 'Off', width: 'Text width', spacing: 'Line spacing', colors: 'Colors', paletteStandard: 'Standard', paletteYellow: 'Yellow on black', paletteGreen: 'Green on black',
    paletteWhite: 'White on black', paletteBlack: 'Black on white', resetDisplay: 'Reset display', paceMode: 'Text movement', paceFollow: 'Follows the plan', paceSteady: 'Constant speed', finishBy: 'Finish by', ends: 'Ends',
    early: '{0} early', late: '{0} late', sections: 'Sections', sectionsEmpty: 'There are no headings in this talk.',
    rehearse: 'Rehearse', rehearseArmed: 'Rehearsal: read the first paragraph aloud. Press Enter to start timing, Esc to cancel.',
    rehearsing: 'Rehearsing', rehearsePaused: 'Pause taken', finish: 'Finish', cancel: 'Cancel',
    rehearseHelp: 'Rehearsing: → next paragraph · ← back · Space pause start/end · ↑ slower here · ↓ faster here · L laugh · X stumbled · Enter finish · Esc cancel',
    doneTitle: 'Rehearsal done', doneNothing: 'Nothing was timed: no paragraph was read.', paceIs: 'Your pace', wpm: 'words per minute',
    paceNone: 'The pace could not be measured (too little read, or pauses not tapped).', adopt: 'Use {0} words per minute as my pace',
    again: 'Rehearse again', read: 'Read in', paragraph: 'Paragraph'
}

type Palette = 'standard' | 'yellow' | 'green' | 'white' | 'black'
const PALETTES: Palette[] = ['standard', 'yellow', 'green', 'white', 'black']
type Prefs = { size: number, mirror: boolean, dark: boolean, mode: 'auto' | 'step', clock: boolean, speed: number, focus: boolean, countdown: number, steady: boolean, width: number, spacing: number, palette: Palette, flip: boolean }
const loadPrefs = (): Prefs => {
    const reduced = !!window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches
    const base: Prefs = { size: 48, mirror: false, dark: true, mode: reduced ? 'step' : 'auto', clock: true, speed: 1, focus: true, countdown: 3, steady: false, width: 100, spacing: 1.38, palette: 'standard', flip: false }
    try {
        const saved = JSON.parse(window.localStorage.getItem('caret.teleprompter') || '{}')
        return { ...base, ...saved }
    } catch (e) {
        return base
    }
}
const savePrefs = (p: Prefs) => { try { window.localStorage.setItem('caret.teleprompter', JSON.stringify(p)) } catch (e) { /* storage may be off */ } }

const host = () => (window as any).chrome && (window as any).chrome.webview
const send = (name: string, args?: any) => { const h = host(); if (h) h.postMessage(JSON.stringify({ name, args })) }

const READING_LINE = 0.35 // the line being read is this far down the window
const SPEED_MIN = 0.25 // the speed is a share of the planned pace: a quarter of it to three times as fast
const SPEED_MAX = 3
const SPEED_STEP = 0.05
const WIDTH_MIN = 40 // the width of the text, in percent of the usual one
const WIDTH_MAX = 150
const SPACING_MIN = 1.1 // the line spacing, as a multiple of the size of the text
const SPACING_MAX = 2
const DEFAULTS_DISPLAY = { width: 100, spacing: 1.38, palette: 'standard' as Palette }

export default function Teleprompter ({ clockOnly }: { clockOnly: boolean }) {
    const [script, setScript] = useState<Script | null>(null)
    const [prefs, setPrefs] = useState<Prefs>(loadPrefs)
    const [, setTick] = useState(0)
    const scroller = useRef<HTMLDivElement>(null)
    const pageRef = useRef<HTMLDivElement>(null)
    const rootRef = useRef<HTMLDivElement>(null)
    const linesRef = useRef<{ from: number, to: number, y: number }[]>([])
    // the path of the plan before it is shaped, and the pauses on the page: the shape depends on the speed and on the way the text moves
    const rawRef = useRef<{ raw: { t: number, y: number }[], pauses: { from: number, to: number, y: number }[], total: number } | null>(null)
    // the panels over the text: the options, or the list of sections (with the one chosen in it)
    const [drawer, setDrawer] = useState<'' | 'options' | 'sections'>('')
    const drawerRef = useRef(drawer)
    drawerRef.current = drawer
    const [chosen, setChosen] = useState(0)
    const chosenRef = useRef(0)
    chosenRef.current = chosen
    const [finishBy, setFinishBy] = useState('') // the time of day the talk must be over by, HH:MM (not kept: it is for the talk of the day)
    // the scroll path (scrollPath.js): measured when the text or its size changes, then only read; `pos` is where the page is drawn
    const pathRef = useRef<{ t: number, y: number }[] | null>(null)
    const view = useRef({ pos: 0, drawn: '', reduced: false })
    const [toast, setToast] = useState('')
    const toastTimer = useRef(0)
    const refs = useRef<Record<string, HTMLElement | null>>({})
    // the time: `elapsed` is what has really been spoken, `place` the place in the planned speech
    const clock = useRef({ running: false, elapsed: 0, place: 0, last: 0, speed: 1, mode: 'auto' as 'auto' | 'step', countdown: 0 })
    const prefsRef = useRef(prefs)
    prefsRef.current = prefs
    // the rehearsal (rehearsal.js): what was pressed and when; `ui` is what the page shows about it
    const reh = useRef({ phase: 'off' as 'off' | 'armed' | 'running' | 'done', t0: 0, events: [] as any[], pausing: false, taps: 0, prevMode: 'auto' as 'auto' | 'step' })
    const [ui, setUi] = useState<{ phase: 'off' | 'armed' | 'running' | 'done', run?: any, saved?: string, adopted?: string }>({ phase: 'off' })
    const labels = useMemo(() => ({ ...DEFAULT_LABELS, ...(script?.labels ?? {}) }), [script])
    const labelsRef = useRef(labels)
    labelsRef.current = labels
    const plan = useMemo(() => script ? buildPlan(script.markdown, { wpm: script.wpm, headingsSpoken: script.headingsSpoken }) : null, [script])
    const planRef = useRef<any>(null)
    planRef.current = plan
    const scriptRef = useRef<Script | null>(null)
    scriptRef.current = script
    // a text that arrives while a rehearsal runs waits: a plan that changes under the run would change the blocks the events point at
    const pendingScript = useRef<Script | null>(null)

    useEffect(() => { savePrefs(prefs); clock.current.speed = prefs.speed; clock.current.mode = prefs.mode }, [prefs])

    // the text comes from the main window; it is asked for when this page is ready, and sent again when the text changes
    useEffect(() => {
        const h = host()
        const onMessage = ({ data }: { data: string }) => {
            try {
                const { name, args } = JSON.parse(data)
                if (name === 'Script') {
                    if (reh.current.phase === 'armed' || reh.current.phase === 'running') pendingScript.current = args
                    else setScript(args)
                }
                else if (name === 'RehearsalSaved') setUi(u => ({ ...u, saved: args && args.message ? String(args.message) : '' }))
                else if (name === 'PaceAdopted') setUi(u => ({ ...u, adopted: args && args.message ? String(args.message) : '' }))
            } catch (e) { /* not ours */ }
        }
        if (h) h.addEventListener('message', onMessage)
        send('TeleprompterReady')
        return () => { if (h) h.removeEventListener('message', onMessage) }
    }, [])

    useEffect(() => { document.title = `${script?.title ?? ''} ${clockOnly ? labels.clock : 'Teleprompter'}`.trim() }, [script, clockOnly, labels])
    useEffect(() => { document.documentElement.classList.add('tp-page') }, [])
    // the colors: the dark or light of the button, or one of the sets of colors that is chosen in the options
    const palette = PALETTES.includes(prefs.palette) ? prefs.palette : 'standard'
    useEffect(() => { document.body.className = palette !== 'standard' ? `tp-pal-${palette}` : prefs.dark ? 'tp-dark' : 'tp-light' }, [prefs.dark, palette])

    // --- scrolling: the reading line glides through the text with the planned time ---
    // The page is not scrolled but moved (a transform, to a fraction of a pixel: a scroll position is whole pixels, and at a slow
    // speed that is a step every few frames). Where it must be comes from the path measured once (scrollPath.js): a line through
    // the middle of every line of the text, drawn in time, so it never jumps between lines or paragraphs. Nothing is measured
    // while it moves.
    // The path as it is drawn: the plan, or the plan kept only at the pauses and the ends (the constant speed), with the corners rubbed
    // off over a few seconds of REAL time, so a faster text is smoothed over more seconds of the plan (the same feel at any speed). The
    // constant speed is for the automatic mode only: the step mode and the rehearsal count on the blocks of the plan.
    const shapePath = useCallback(() => {
        const r = rawRef.current
        if (!r || !r.raw.length) return []
        const { speed, steady, mode } = prefsRef.current
        const constant = steady && mode === 'auto'
        const base = constant ? steadyPath(r.raw, r.pauses, r.total) : r.raw
        let window = Math.min(constant ? 4.5 : 18, Math.max(constant ? 0.5 : 1.5, (constant ? 1.5 : 6) * speed))
        if (constant) {
            // a pause keeps at least half of its seconds still: the window is never longer than half of the shortest one
            const holds = r.pauses.map(h => h.to - h.from).filter(n => n > 0.05)
            if (holds.length) window = Math.min(window, Math.min(...holds) * 0.5)
            if (window < 0.3) return base
        }
        return smooth(base, window)
    }, [])

    const measure = useCallback(() => {
        const page = pageRef.current
        const p = planRef.current
        if (!page || !p) return null
        const pageBox = page.getBoundingClientRect()
        const flipped = prefsRef.current.flip
        const pieces: { from: number, to: number, y: number }[] = []
        const pauses: { from: number, to: number, y: number }[] = []
        p.blocks.forEach((block: any) => block.segments.forEach((segment: any, j: number) => {
            const el = refs.current[`${block.index}:${j}`]
            if (!el || !el.isConnected) return
            const from = block.start + segment.at
            if (segment.type === 'words') {
                const rects = Array.from(el.getClientRects()).filter(r => r.width > 0)
                const total = rects.reduce((n, r) => n + r.width, 0)
                if (!rects.length || !total) return
                let seen = 0
                for (const r of rects) {
                    pieces.push({ from: from + segment.seconds * (seen / total), to: from + segment.seconds * ((seen + r.width) / total), y: pageY(r, pageBox, flipped) })
                    seen += r.width
                }
            } else if (segment.type === 'pause' || segment.type === 'title') {
                const r = el.getBoundingClientRect()
                pieces.push({ from, to: from + segment.seconds, y: pageY(r, pageBox, flipped) })
                if (segment.type === 'pause') pauses.push({ from, to: from + segment.seconds, y: pageY(r, pageBox, flipped) })
            }
        }))
        const lines = groupLines(pieces, prefsRef.current.size * 0.4)
        linesRef.current = lines
        rawRef.current = { raw: buildPath(lines, p.total), pauses, total: p.total }
        return shapePath()
    }, [shapePath])

    // Draws the page for the place of the clock: `snap` puts it there at once, else it glides (and is still in a moment).
    const draw = useCallback((dt: number, snap: boolean) => {
        const box = scroller.current
        const page = pageRef.current
        if (!box || !page || !planRef.current || clockOnly) return
        if (!pathRef.current) pathRef.current = measure()
        const path = pathRef.current
        if (!path || !path.length) return
        const v = view.current
        const target = Math.max(0, yAt(path, clock.current.place) - box.clientHeight * READING_LINE)
        v.pos = snap || v.reduced ? target : follow(v.pos, target, dt)
        // moving: a fraction of a pixel; at rest: a whole device pixel, so the text is sharp
        const dpr = window.devicePixelRatio || 1
        const shown = v.pos === target ? Math.round(v.pos * dpr) / dpr : v.pos
        const css = `translate3d(0, ${-shown}px, 0)`
        if (css !== v.drawn) { v.drawn = css; page.style.transform = css }
    }, [clockOnly, measure])

    const scrollTo = useCallback((place: number, smooth: boolean) => draw(0, !smooth), [draw])

    // the speed or the way the text moves changed: the path is shaped again (not at every step of the slider, a moment after the last)
    useEffect(() => {
        const timer = window.setTimeout(() => { if (rawRef.current) pathRef.current = shapePath() }, 120)
        return () => window.clearTimeout(timer)
    }, [prefs.speed, prefs.steady, prefs.mode, shapePath])

    // the text was laid out again (a new text, the size, the window, the fonts): measure again and stay on the same place
    const relayout = useCallback(() => { pathRef.current = null; window.requestAnimationFrame(() => draw(0, true)) }, [draw])
    useEffect(() => {
        const media = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null
        const update = () => { view.current.reduced = !!(media && media.matches) }
        update()
        if (media && media.addEventListener) media.addEventListener('change', update)
        const box = scroller.current
        const observer = typeof ResizeObserver !== 'undefined' && box ? new ResizeObserver(() => relayout()) : null
        if (observer && box) observer.observe(box)
        // the page itself too: its text can lay out again while the window stays the same size (a pause label that gets a digit wider)
        if (observer && pageRef.current) observer.observe(pageRef.current)
        const fonts = (document as any).fonts
        if (fonts && fonts.ready) fonts.ready.then(() => relayout())
        return () => { if (media && media.removeEventListener) media.removeEventListener('change', update); if (observer) observer.disconnect() }
    }, [relayout, plan])

    // --- the clock ---
    useEffect(() => {
        let raf = 0
        let shown = 0
        const frame = (ts: number) => {
            const c = clock.current
            const p = planRef.current
            const dt = c.last ? Math.min(0.25, (ts - c.last) / 1000) : 0
            c.last = ts
            if (c.countdown > 0) {
                c.countdown -= dt
                if (c.countdown <= 0) { c.countdown = 0; c.running = true }
            }
            if (c.running && p) {
                c.elapsed += dt
                if (c.mode === 'auto') {
                    c.place = Math.min(p.total, c.place + dt * c.speed)
                    if (c.place >= p.total) c.running = false
                }
            }
            draw(dt, false)
            if (ts - shown > 100) { shown = ts; setTick(n => n + 1) }
            raf = window.requestAnimationFrame(frame)
        }
        raf = window.requestAnimationFrame(frame)
        return () => window.cancelAnimationFrame(raf)
    }, [draw])

    // a new text: the place is kept, as far as the new talk goes
    useEffect(() => {
        if (!plan) return
        clock.current.place = Math.min(clock.current.place, plan.total)
        relayout()
    }, [plan, relayout, prefs.size, prefs.width, prefs.spacing])

    const jump = useCallback((place: number) => {
        const p = planRef.current
        if (!p) return
        clock.current.place = Math.min(p.total, Math.max(0, place))
        scrollTo(clock.current.place, true)
    }, [scrollTo])

    const toggle = useCallback(() => {
        const c = clock.current
        const p = planRef.current
        if (!p || reh.current.phase === 'armed' || reh.current.phase === 'running') return
        if (c.countdown > 0) { c.countdown = 0; return }
        if (c.running) { c.running = false; return }
        if (c.mode === 'auto' && c.place >= p.total) { c.place = 0; c.elapsed = 0; scrollTo(0, false) }
        // the countdown before the text starts to move: a moment to get ready (not in the step mode: there the speaker starts the clock)
        if (!clockOnly && c.mode === 'auto' && prefsRef.current.countdown > 0) c.countdown = prefsRef.current.countdown
        else c.running = true
    }, [scrollTo, clockOnly])

    const restart = useCallback(() => {
        const c = clock.current
        if (reh.current.phase === 'armed' || reh.current.phase === 'running') return
        c.running = false
        c.countdown = 0
        c.elapsed = 0
        c.place = 0
        scrollTo(0, false)
    }, [scrollTo])

    const change = useCallback((patch: Partial<Prefs>) => setPrefs(p => ({ ...p, ...patch })), [])

    // the speed is a share of the planned pace, so it is told in words per minute too: that is what a speaker knows of their own pace
    const speedText = (speed: number) => {
        const wpm = planRef.current ? Math.round(planRef.current.wpm * speed) : 0
        return `${labelsRef.current.speed} ${Math.round(speed * 100)} %${wpm ? ` · ${wpm} ${labelsRef.current.wpmShort}` : ''}`
    }
    const setSpeed = useCallback((value: number) => {
        const speed = Math.min(SPEED_MAX, Math.max(SPEED_MIN, Math.round(value * 100) / 100))
        change({ speed })
        setToast(speedText(speed))
        window.clearTimeout(toastTimer.current)
        toastTimer.current = window.setTimeout(() => setToast(''), 1400)
    }, [change])

    // --- rehearsal: the speaker is the sensor; only what was pressed, and when, is kept ---
    const rehLog = useCallback((type: string) => {
        const r = reh.current
        const p = planRef.current
        const at = p ? blockAt(p, clock.current.place) : null
        r.events.push({ t: (performance.now() - r.t0) / 1000, type, block: at ? at.index : 0 })
    }, [])

    // the rehearsal is over (finished or cancelled): the text that came meanwhile is taken now
    const rehLeave = useCallback(() => {
        const r = reh.current
        clock.current.running = false
        clock.current.mode = r.prevMode
        change({ mode: r.prevMode })
        if (pendingScript.current) { setScript(pendingScript.current); pendingScript.current = null }
    }, [change])

    const rehArm = useCallback(() => {
        const r = reh.current
        const p = planRef.current
        if (!p || clockOnly || !p.blocks.length || r.phase === 'armed' || r.phase === 'running') return
        r.prevMode = prefsRef.current.mode
        clock.current.mode = 'step'
        change({ mode: 'step' })
        clock.current.running = false
        clock.current.elapsed = 0
        clock.current.place = 0
        scrollTo(0, false)
        Object.assign(r, { phase: 'armed', events: [], pausing: false, taps: 0 })
        setUi({ phase: 'armed' })
    }, [change, clockOnly, scrollTo])

    const rehBegin = useCallback(() => {
        const r = reh.current
        r.t0 = performance.now()
        r.events = []
        r.phase = 'running'
        clock.current.elapsed = 0
        clock.current.running = true
        rehLog('start')
        setUi({ phase: 'running' })
    }, [rehLog])

    const rehFinish = useCallback(() => {
        const r = reh.current
        const p = planRef.current
        if (r.phase !== 'running' || !p) return
        rehLog('end')
        r.phase = 'done'
        rehLeave()
        const run = summarizeRun(p, r.events)
        const baseline = scriptRef.current ? scriptRef.current.wpm : 130
        send('RehearsalDone', { markdown: run.rows.length ? formatRun(run, { date: new Date(), baseline }) : '', pace: run.pace, baseline })
        setUi({ phase: 'done', run })
    }, [rehLog, rehLeave])

    const rehCancel = useCallback(() => {
        const r = reh.current
        if (r.phase === 'running' || r.phase === 'armed') rehLeave()
        r.phase = 'off'
        setUi({ phase: 'off' })
    }, [rehLeave])

    // a key while rehearsing; returns whether it was one of the rehearsal's
    const rehKey = useCallback((key: string): boolean => {
        const r = reh.current
        const p = planRef.current
        const c = clock.current
        if (!p) return false
        if (r.phase === 'armed') {
            if (key === 'Enter') rehBegin()
            else if (key === 'Escape') rehCancel()
            else return false
            return true
        }
        if (r.phase !== 'running') return false
        const tap = (type: string) => { rehLog(type); r.taps++ }
        // a pause still open when the speaker moves to another paragraph ends there, in the paragraph it began in
        const endPause = () => { if (r.pausing) { r.pausing = false; rehLog('pause-end') } }
        if (key === 'ArrowRight' || key === 'PageDown') {
            const target = nextBlockStart(p, c.place)
            endPause()
            if (target >= p.total - 1e-6) { rehFinish(); return true }
            jump(target)
            rehLog('next')
        } else if (key === 'ArrowLeft' || key === 'PageUp') {
            endPause()
            jump(prevBlockStart(p, c.place))
            rehLog('back')
        } else if (key === ' ' || key === 'Spacebar') {
            r.pausing = !r.pausing
            rehLog(r.pausing ? 'pause-start' : 'pause-end')
        } else if (key === 'ArrowUp') tap('slower')
        else if (key === 'ArrowDown') tap('faster')
        else if (key === 'l' || key === 'L') tap('laugh')
        else if (key === 'x' || key === 'X') tap('stumble')
        else if (key === 'Enter') rehFinish()
        else if (key === 'Escape') rehCancel()
        else if (['s', 'S', 'r', 'R', 'Home'].includes(key)) { /* not while timing */ } else return false
        return true
    }, [jump, rehBegin, rehCancel, rehFinish, rehLog])

    // a section: the reading line goes to where the heading is on the page (in the constant speed the time of the plan is not the place)
    const jumpToSection = useCallback((start: number) => {
        const path = pathRef.current
        const r = rawRef.current
        if (prefsRef.current.steady && prefsRef.current.mode === 'auto' && path && path.length && r && r.raw.length) jump(tAtY(path, yAt(r.raw, start)))
        else jump(start)
    }, [jump])

    // the list of sections opens on the one the talk is in
    const openSections = useCallback(() => {
        const p = planRef.current
        if (!p || !p.sections.length) return
        const at = sectionAt(p, clock.current.place)
        setChosen(at ? (at as any).index : 0)
        setDrawer('sections')
    }, [])

    // --- the wheel and the mouse: they correct the place without leaving the keyboard's modes ---
    // The wheel moves the reading line over the text (the page glides after it); with Ctrl it changes the speed. A click on a line
    // of the text moves the reading line there. Neither while a rehearsal is timed: its events point at paragraphs.
    useEffect(() => {
        const root = rootRef.current
        if (!root || clockOnly) return
        const onWheel = (e: WheelEvent) => {
            const target = e.target as HTMLElement
            if (target && target.closest && target.closest('.tp-toolbar, .tp-clock, .tp-panel, .tp-overlay')) return
            e.preventDefault()
            if (e.ctrlKey) { setSpeed(prefsRef.current.speed + (e.deltaY < 0 ? SPEED_STEP : -SPEED_STEP)); return }
            if (reh.current.phase === 'armed' || reh.current.phase === 'running') return
            const path = pathRef.current
            const box = scroller.current
            if (!path || !path.length || !box) return
            const unit = e.deltaMode === 1 ? 40 : e.deltaMode === 2 ? box.clientHeight : 1
            clock.current.place = tAtY(path, yAt(path, clock.current.place) + e.deltaY * unit)
        }
        // not passive: the wheel with Ctrl would zoom the page
        root.addEventListener('wheel', onWheel, { passive: false })
        return () => root.removeEventListener('wheel', onWheel)
    }, [clockOnly, setSpeed, plan])

    const onTextClick = useCallback((e: React.MouseEvent) => {
        if (reh.current.phase === 'armed' || reh.current.phase === 'running') return
        const picked = window.getSelection ? window.getSelection() : null
        if (picked && picked.toString()) return // text was selected: not a click to move
        const page = pageRef.current
        if (!page) return
        const box = page.getBoundingClientRect()
        const line = nearestLine(linesRef.current, prefsRef.current.flip ? box.bottom - e.clientY : e.clientY - box.top, prefsRef.current.size * Math.max(0.8, prefsRef.current.spacing * 0.6))
        const path = pathRef.current
        if (line && path && path.length) jump(tAtY(path, line.y))
        else if (line) jump(line.from)
    }, [jump])

    useEffect(() => {
        const onKey = (e: KeyboardEvent) => {
            if (e.ctrlKey || e.altKey || e.metaKey) return
            const c = clock.current
            const p = planRef.current
            const key = e.key
            let handled = true
            const target = e.target as HTMLElement
            const tag = target && target.tagName
            // a field of the options is typed in: its keys are its own (Escape still closes the panel)
            const typing = tag === 'SELECT' || tag === 'TEXTAREA' || (tag === 'INPUT' && ((target as HTMLInputElement).type !== 'range' || !!(target.closest && target.closest('.tp-panel'))))
            if (drawerRef.current && key === 'Escape') { setDrawer(''); if (target && target.blur) target.blur(); e.preventDefault(); return }
            if (typing) return
            const rehearsing = reh.current.phase === 'armed' || reh.current.phase === 'running'
            if (!clockOnly && rehearsing && rehKey(key)) { e.preventDefault(); return }
            if (!clockOnly && !rehearsing && drawerRef.current === 'sections' && p) {
                const list = p.sections
                if (key === 'ArrowUp' || key === 'ArrowDown') { setChosen(i => Math.min(list.length - 1, Math.max(0, i + (key === 'ArrowUp' ? -1 : 1)))); e.preventDefault(); return }
                if (key === 'Enter') { if (list[chosenRef.current]) jumpToSection(list[chosenRef.current].start); setDrawer(''); e.preventDefault(); return }
            }
            if (!clockOnly && !rehearsing && (key === 'o' || key === 'O')) { setDrawer(v => v === 'options' ? '' : 'options'); e.preventDefault(); return }
            if (!clockOnly && !rehearsing && (key === 'j' || key === 'J') && p && p.sections.length) { openSections(); e.preventDefault(); return }

            if (reh.current.phase === 'done' && key === 'Escape') { reh.current.phase = 'off'; setUi({ phase: 'off' }); e.preventDefault(); return }
            if (key === 'e' || key === 'E') { if (!clockOnly) rehArm() }
            else if (key === ' ' || key === 'Spacebar') toggle()
            else if (key === 'ArrowRight' || key === 'PageDown') p && jump(nextBlockStart(p, c.place))
            else if (key === 'ArrowLeft' || key === 'PageUp') p && jump(prevBlockStart(p, c.place))
            else if (key === 'ArrowUp' || key === '+') setSpeed(prefsRef.current.speed + SPEED_STEP)
            else if (key === 'ArrowDown' || key === '-') setSpeed(prefsRef.current.speed - SPEED_STEP)
            else if (key === 'Home' || key === 'r' || key === 'R') restart()
            else if (key === 's' || key === 'S') change({ mode: prefsRef.current.mode === 'auto' ? 'step' : 'auto' })
            else if (key === 'm' || key === 'M') change({ mirror: !prefsRef.current.mirror })
            else if (key === 'v' || key === 'V') change({ flip: !prefsRef.current.flip })
            else if (key === 'd' || key === 'D') { if (prefsRef.current.palette === 'standard') change({ dark: !prefsRef.current.dark }) }
            else if (key === 'c' || key === 'C') change({ clock: !prefsRef.current.clock })
            else if (key === '[') change({ size: Math.max(20, prefsRef.current.size - 4) })
            else if (key === ']') change({ size: Math.min(160, prefsRef.current.size + 4) })
            else if (key === 'f' || key === 'F' || key === 'F11') send('Fullscreen')
            else if (key === 'Escape') { if (c.countdown > 0) c.countdown = 0; else if (c.running) c.running = false; else send('Escape') }
            else handled = false
            if (handled) e.preventDefault()
        }
        window.addEventListener('keydown', onKey)
        return () => window.removeEventListener('keydown', onKey)
    }, [toggle, jump, jumpToSection, restart, change, setSpeed, clockOnly, rehKey, rehArm, openSections])

    // --- drawing ---
    const c = clock.current
    const state = plan ? clockState(plan, c.elapsed, c.place) : null
    const next = plan ? upcoming(plan, c.place) : null
    const here = plan ? blockAt(plan, c.place) : null
    const styleOf = (name: string) => script?.styles?.[name]

    const renderSegment = (block: any, segment: any, j: number) => {
        const key = `${block.index}:${j}`
        const ref = (el: HTMLElement | null) => { refs.current[key] = el }
        switch (segment.type) {
            case 'title':
                return <span key={j} ref={ref}>{segment.text}</span>
            case 'words': {
                const classes = ['tp-words']
                const style: React.CSSProperties = {}
                for (const s of segment.styles) {
                    if (s.kind === 'pace') {
                        Object.assign(style, paceStyle({ speed: s.speed, perWord: s.perWord }))
                        classes.push(s.speed < 1 ? 'tp-slower' : 'tp-faster')
                    }
                    if (s.user) {
                        const look = styleOf(s.name)
                        classes.push('tp-user')
                        if (look && look.color && SWATCHES.includes(look.color)) classes.push(`tp-c-${look.color}`)
                    } else if (['loud', 'soft', 'emphasis', 'tone'].includes(s.name)) {
                        classes.push(`tp-${s.name}`)
                    }
                }
                return <span key={j} ref={ref} className={classes.join(' ')} style={style}>{segment.text}</span>
            }
            case 'pause': {
                const active = next && next.active && next.remaining !== undefined && c.place >= block.start + segment.at && c.place < block.start + segment.at + segment.seconds
                const secs = active ? (next as any).remaining : segment.seconds
                const word = segment.audience ? labels.audience : labels.pause
                return (
                    <span key={j} ref={ref} className={`tp-pause${segment.audience ? ' tp-audience' : ''}${active ? ' tp-active' : ''}`}>
                        {word} {formatClock(Math.ceil(secs - 1e-6))}{segment.note ? ` · ${segment.note}` : ''}
                    </span>
                )
            }
            case 'cue':
                return <span key={j} ref={ref} className="tp-cue">▶ {segment.text}</span>
            case 'open': {
                if (segment.name === 'emphasis' || segment.name === 'loud' || segment.name === 'soft') return null
                const look = segment.user ? styleOf(segment.name) : undefined
                const text = segment.name === 'tone' ? segment.text : `${look && look.icon ? look.icon + ' ' : ''}${segment.name}`
                return <span key={j} ref={ref} className="tp-label">{text}</span>
            }
            default:
                return null
        }
    }

    const nextText = next
        ? (next.active
            ? `${next.audience ? labels.audience : labels.pause} ${formatClock(Math.ceil((next.remaining as number) - 1e-6))}`
            : `${next.audience ? labels.audienceIn : labels.pauseIn} ${formatClock(Math.ceil((next.in as number) - 1e-6))}`)
        : ''

    const aheadText = state
        ? (Math.abs(state.ahead) < 1 ? labels.onPlan : `${state.ahead > 0 ? labels.ahead : labels.behind} ${formatClock(Math.abs(state.ahead))}`)
        : ''
    // what is left: in the automatic mode the text still to go at the speed it goes (a slower text takes longer), else the spoken time against the plan
    const leftSecs = state && plan ? (prefs.mode === 'auto' ? (plan.total - c.place) / Math.max(prefs.speed, 0.01) : state.remaining) : 0
    // when the talk will be over if it goes on at this speed, and against the time it must be over by (what is left, from the clock of the PC)
    const clockText = (d: Date) => d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    const endAt = new Date(Date.now() + Math.max(0, leftSecs) * 1000)
    const endClock = clockText(endAt)
    let endSmall = labels.ends
    let endLate = false
    if (finishBy && /^\d{1,2}:\d{2}$/.test(finishBy)) {
        const [hours, minutes] = finishBy.split(':').map(Number)
        const target = new Date()
        target.setHours(hours, minutes, 0, 0)
        if (target.getTime() < Date.now() - 12 * 3600 * 1000) target.setDate(target.getDate() + 1)
        const slack = Math.round((target.getTime() - endAt.getTime()) / 1000)
        endLate = slack < 0
        endSmall = `${labels.finishBy} ${clockText(target)} · ${(slack >= 0 ? labels.early : labels.late).replace('{0}', formatClock(Math.abs(slack)))}`
    }

    const sectionsPanel = plan && (
        <div className="tp-panel tp-sections" role="dialog" aria-label={labels.sections}>
            {plan.sections.length === 0
                ? <p>{labels.sectionsEmpty}</p>
                : plan.sections.map((s: any, i: number) => (
                    <button type="button" key={i} className={`tp-section${i === chosen ? ' tp-chosen' : ''}`} style={{ paddingLeft: 12 + Math.max(0, (s.level || 1) - 1) * 16 }}
                        aria-current={i === chosen} ref={el => { if (el && i === chosen) el.scrollIntoView({ block: 'nearest' }) }}
                        onClick={() => { jumpToSection(s.start); setDrawer('') }}>
                        <span className="tp-section-title">{s.title || labels.section}</span><span className="tp-section-time">{formatClock(s.start)}</span>
                    </button>
                ))}
        </div>
    )

    const optionsPanel = (
        <div className="tp-panel tp-options" role="dialog" aria-label={labels.options}>
            <label className="tp-field"><input type="checkbox" checked={prefs.focus} onChange={e => change({ focus: e.target.checked })} /> {labels.focus}</label>
            <label className="tp-field">{labels.paceMode}
                <select value={prefs.steady ? 'steady' : 'plan'} onChange={e => change({ steady: e.target.value === 'steady' })}>
                    <option value="plan">{labels.paceFollow}</option>
                    <option value="steady">{labels.paceSteady}</option>
                </select>
            </label>
            <label className="tp-field">{labels.countdown}
                <select value={prefs.countdown} onChange={e => change({ countdown: Number(e.target.value) })}>
                    {[0, 3, 5, 10].map(n => <option key={n} value={n}>{n ? `${n} s` : labels.off}</option>)}
                </select>
            </label>
            <label className="tp-field">{labels.finishBy}
                <input type="time" value={finishBy} onChange={e => setFinishBy(e.target.value)} />
                {finishBy && <button type="button" className="tp-button" onClick={() => setFinishBy('')} aria-label={labels.off}>×</button>}
            </label>
            <div className="tp-divider" role="separator" />
            <label className="tp-field">{labels.width}
                <span className="tp-slide">
                    <input type="range" min={WIDTH_MIN} max={WIDTH_MAX} step={5} value={prefs.width} onChange={e => change({ width: Number(e.target.value) })}
                        aria-label={labels.width} aria-valuetext={`${prefs.width} %`} />
                    <output>{prefs.width} %</output>
                </span>
            </label>
            <label className="tp-field">{labels.spacing}
                <span className="tp-slide">
                    <input type="range" min={SPACING_MIN} max={SPACING_MAX} step={0.05} value={prefs.spacing} onChange={e => change({ spacing: Math.round(Number(e.target.value) * 100) / 100 })}
                        aria-label={labels.spacing} aria-valuetext={prefs.spacing.toFixed(2)} />
                    <output>{prefs.spacing.toFixed(2)}</output>
                </span>
            </label>
            <label className="tp-field">{labels.colors}
                <select value={palette} onChange={e => change({ palette: e.target.value as Palette })}>
                    {PALETTES.map(name => <option key={name} value={name}>{labels[`palette${name.charAt(0).toUpperCase()}${name.slice(1)}`]}</option>)}
                </select>
            </label>
            <button type="button" className="tp-button" onClick={() => change({ ...DEFAULTS_DISPLAY })}>{labels.resetDisplay}</button>
        </div>
    )

    const light = state ? state.light : ''
    const lightDot = light === 'green' ? '#2e9d57' : light === 'amber' ? '#d98e04' : light === 'red' ? '#e5484d' : 'transparent'

    const panel = state && plan && (
        <div className={`tp-clock${clockOnly ? ' tp-clock-only' : ''}`} role="timer" aria-label={labels.clock}>
            <button type="button" className="tp-button tp-primary" onClick={toggle} aria-label={c.running ? labels.stop : labels.start}>{c.running ? '❚❚' : '▶'}</button>
            <div className="tp-time">
                <span className="tp-big">{formatClock(state.elapsed)}</span>
                <span className="tp-small">{labels.elapsed} · {labels.planned} {formatClock(state.total)}</span>
            </div>
            <div className="tp-time">
                <span className={`tp-big${leftSecs < 0 ? ' tp-over' : ''}`}>{leftSecs < 0 ? '+' : ''}{formatClock(Math.abs(leftSecs))}</span>
                <span className="tp-small">{leftSecs < 0 ? labels.over : labels.left}</span>
            </div>
            <div className="tp-time">
                <span className={`tp-big${endLate ? ' tp-over' : ''}`}>{endClock}</span>
                <span className="tp-small">{endSmall}</span>
            </div>
            <div className="tp-time tp-grow">
                <span className="tp-big tp-light"><i style={{ background: lightDot }} aria-hidden="true" />{aheadText}</span>
                <span className="tp-small">{state.section ? `${state.section.title || labels.section} · ${formatClock(state.section.left)}${state.section.budget > 0 ? ` / ${formatClock(state.section.budget)}` : ''}` : ''}</span>
            </div>
            <div className="tp-time">
                <span className="tp-big">{nextText}</span>
                <span className="tp-small">{prefs.mode === 'auto' ? `${labels.speed} ${Math.round(prefs.speed * 100)} %` : labels.step}</span>
            </div>
            {clockOnly && (
                <div className="tp-keys">
                    <button type="button" className="tp-button" onClick={() => plan && jump(prevBlockStart(plan, c.place))} aria-label={labels.back}>‹</button>
                    <button type="button" className="tp-button" onClick={() => plan && jump(nextBlockStart(plan, c.place))} aria-label={labels.next}>›</button>
                    <button type="button" className="tp-button" onClick={restart} aria-label="R">↺</button>
                </div>
            )}
        </div>
    )

    const r = reh.current
    const rehearsing = ui.phase === 'running'
    const sayWpm = (text: string, n: number) => text.replace('{0}', String(n))
    const shortDiff = (n: number) => `${n >= 0 ? '+' : '−'}${formatClock(Math.abs(Math.round(n)))}`
    const banner = !clockOnly && (ui.phase === 'armed' || rehearsing) && (
        <div className="tp-rehearsal" role="status">
            {ui.phase === 'armed'
                ? labels.rehearseArmed
                : <>● {labels.rehearsing} {formatClock(c.elapsed)}{r.pausing ? ` · ${labels.rehearsePaused}` : ''}{r.taps ? ` · ${r.taps} ✓` : ''}</>}
        </div>
    )
    const done = !clockOnly && ui.phase === 'done' && ui.run && (
        <div className="tp-overlay" role="dialog" aria-label={labels.doneTitle}>
            <div className="tp-dialog">
                <h2>{labels.doneTitle}</h2>
                {!ui.run.rows.length
                    ? <p>{labels.doneNothing}</p>
                    : <>
                        <p>{labels.planned} {formatClock(ui.run.plannedTotal)} · {labels.read} {formatClock(ui.run.actualTotal)} ({shortDiff(ui.run.actualTotal - ui.run.plannedTotal)})</p>
                        <p>{ui.run.pace ? `${labels.paceIs}: ${ui.run.pace} ${labels.wpm}` : labels.paceNone}</p>
                        <div className="tp-rows">
                            {ui.run.rows.map((row: any, i: number) => (
                                <div key={row.block} className="tp-row">
                                    <span>{i + 1}</span><span className="tp-rowtext">{row.text.substring(0, 60)}</span>
                                    <span>{formatClock(row.planned)}</span><span>{formatClock(row.actual)}</span><span>{shortDiff(row.actual - row.planned)}</span>
                                </div>
                            ))}
                        </div>
                    </>}
                {ui.saved && <p className="tp-status" role="status">{ui.saved}</p>}
                {ui.adopted && <p className="tp-status" role="status">{ui.adopted}</p>}
                <div className="tp-dialog-buttons">
                    {ui.run.pace && !ui.adopted && <button type="button" className="tp-button tp-primary-text" onClick={() => send('AdoptWpm', { wpm: ui.run.pace })}>{sayWpm(labels.adopt, ui.run.pace)}</button>}
                    <button type="button" className="tp-button" onClick={() => { reh.current.phase = 'off'; setUi({ phase: 'off' }); rehArm() }}>{labels.again}</button>
                    <button type="button" className="tp-button" onClick={() => { reh.current.phase = 'off'; setUi({ phase: 'off' }) }}>{labels.close}</button>
                </div>
            </div>
        </div>
    )

    if (!plan) return <div className="tp-message">{labels.waiting}</div>
    if (!plan.blocks.length) return <div className="tp-message">{labels.empty}</div>

    return (
        <div ref={rootRef} className={`tp-root${prefs.focus && !clockOnly ? ' tp-focus' : ''}${prefs.mirror ? ' tp-mx' : ''}${prefs.flip ? ' tp-my' : ''}`} style={{ ['--tp-size' as any]: `${prefs.size}px`, ['--tp-line-at' as any]: `${READING_LINE * 100}%`, ['--tp-width' as any]: prefs.width / 100, ['--tp-lh' as any]: prefs.spacing }}>
            {!clockOnly && (
                <div ref={scroller} className={`tp-scroll${prefs.mirror ? ' tp-mirror' : ''}${prefs.flip ? ' tp-flip' : ''}`} onClick={onTextClick}>
                    <div ref={pageRef} className="tp-page">
                        {plan.blocks.map((block: any) => {
                            const past = block.end <= c.place && block.end > block.start
                            const classes = `tp-block ${block.kind === 'heading' ? `tp-h tp-h${Math.min(block.level, 3)}` : 'tp-p'}${past ? ' tp-past' : ''}${here && here.index === block.index ? ' tp-here' : ''}`
                            const parts = block.segments.map((s: any, j: number) => renderSegment(block, s, j))
                            return block.kind === 'heading'
                                ? <h2 key={block.index} className={classes}>{parts}</h2>
                                : <p key={block.index} className={classes}>{parts}</p>
                        })}
                        <p className="tp-block tp-p tp-endmark">{labels.end}</p>
                    </div>
                </div>
            )}
            {!clockOnly && <div className="tp-line" aria-hidden="true" style={{ top: `${(prefs.flip ? 1 - READING_LINE : READING_LINE) * 100}%` }} />}
            {!clockOnly && nextText && <div className={`tp-next${next && next.active ? ' tp-active' : ''}`} aria-live="off">{nextText}</div>}
            {!clockOnly && toast && <div className="tp-toast" role="status">{toast}</div>}
            {!clockOnly && c.countdown > 0 && <div className="tp-countdown" role="status" aria-live="assertive">{Math.ceil(c.countdown)}</div>}
            {!clockOnly && drawer === 'sections' && sectionsPanel}
            {banner}
            {done}
            {clockOnly && panel}
            {!clockOnly && (
                <div className="tp-bottom">
                {drawer === 'options' && optionsPanel}
                {prefs.clock && panel}
                <div className="tp-toolbar">
                    <button type="button" className="tp-button" onClick={() => change({ mode: prefs.mode === 'auto' ? 'step' : 'auto' })} aria-pressed={prefs.mode === 'step'}>{prefs.mode === 'auto' ? labels.auto : labels.step}</button>
                    <div className="tp-speed" role="group" aria-label={labels.speed}>
                        <button type="button" className="tp-button" onClick={() => setSpeed(prefs.speed - SPEED_STEP)} aria-label={`${labels.speed} −`} disabled={prefs.mode === 'step'}>−</button>
                        <input type="range" className="tp-slider" min={Math.round(SPEED_MIN * 100)} max={Math.round(SPEED_MAX * 100)} step={Math.round(SPEED_STEP * 100)}
                            value={Math.round(prefs.speed * 100)} onChange={e => setSpeed(Number(e.target.value) / 100)} disabled={prefs.mode === 'step'}
                            aria-label={labels.speed} aria-valuetext={speedText(prefs.speed)} />
                        <button type="button" className="tp-button" onClick={() => setSpeed(prefs.speed + SPEED_STEP)} aria-label={`${labels.speed} +`} disabled={prefs.mode === 'step'}>+</button>
                        <span className="tp-speedout">{Math.round(prefs.speed * 100)} % · {Math.round(plan.wpm * prefs.speed)} {labels.wpmShort}</span>
                    </div>
                    <button type="button" className="tp-button" onClick={() => change({ size: Math.max(20, prefs.size - 4) })} aria-label={`${labels.size} −`}>A−</button>
                    <button type="button" className="tp-button" onClick={() => change({ size: Math.min(160, prefs.size + 4) })} aria-label={`${labels.size} +`}>A+</button>
                    <button type="button" className="tp-button" onClick={() => change({ mirror: !prefs.mirror })} aria-pressed={prefs.mirror}>{labels.mirror}</button>
                    <button type="button" className="tp-button" onClick={() => change({ flip: !prefs.flip })} aria-pressed={prefs.flip}>{labels.flip}</button>
                    <button type="button" className="tp-button" onClick={() => change({ dark: !prefs.dark })} disabled={palette !== 'standard'}>{prefs.dark ? labels.light : labels.dark}</button>
                    <button type="button" className="tp-button" onClick={() => change({ clock: !prefs.clock })} aria-pressed={prefs.clock}>{labels.clock}</button>
                    <button type="button" className="tp-button" onClick={() => send('Fullscreen')}>{labels.fullscreen}</button>
                    {plan.sections.length > 0 && <button type="button" className="tp-button" onClick={() => drawer === 'sections' ? setDrawer('') : openSections()} aria-pressed={drawer === 'sections'}>{labels.sections}</button>}
                    <button type="button" className="tp-button" onClick={() => setDrawer(v => v === 'options' ? '' : 'options')} aria-pressed={drawer === 'options'}>{labels.options}</button>
                    {ui.phase === 'running'
                        ? <button type="button" className="tp-button" onClick={rehFinish}>{labels.finish}</button>
                        : ui.phase === 'armed'
                            ? <button type="button" className="tp-button" onClick={rehBegin}>{labels.start}</button>
                            : <button type="button" className="tp-button" onClick={rehArm}>{labels.rehearse}</button>}
                    <span className="tp-help">{rehearsing ? labels.rehearseHelp : labels.help}</span>
                </div>
                </div>
            )}
        </div>
    )
}
