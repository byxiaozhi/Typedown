import React, { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { remote } from "services/remote";
import transport from "services/transport";
import { activeButtons, IRect, placeToolbar, ToggleButton } from "./place";
import './index.scss'

// The mini toolbar (Settings > Editor > Toolbar over a selection): a small floating toolbar over the words that are selected in the
// Visual view, 8 px above them, so the common formats are under the pointer. It shows B I U S, a link and a heading menu; with review
// on, Comment; with Speech mode on, pause, slow and emphasis. It comes a moment (250 ms) after the selection is made and goes with a
// click elsewhere, Esc or the selection going; Alt+Down moves the keyboard into it, the arrow keys move along it, Esc returns to the text.
// The fixed toolbar of the window stays. The selection is told by the editor itself (`muya-format-picker`, with the formats the
// selection lies in); the buttons use the editor's own commands, as the menus do.
interface IProps {
    editor: any
    review: boolean
    speech: boolean
    onFormat: (type: string) => void
    onParagraph: (type: string) => void
}

const SHOW_AFTER = 250

const LABEL_KEYS = ['FormatMenu_Title', 'BoldCtrlB', 'ItalicCtrlI', 'Underline', 'Strikethrough', 'Link', 'Paragraph',
    'Heading1', 'Heading2', 'Heading3', 'Heading4', 'ReviewAddComment']

const HEADINGS: [string, string][] = [['paragraph', 'Paragraph'], ['heading 1', 'Heading1'], ['heading 2', 'Heading2'], ['heading 3', 'Heading3'], ['heading 4', 'Heading4']]

// Where the selected words are on the screen, or null when nothing is selected.
const selectionRect = (): IRect | null => {
    const selection = window.getSelection()
    if (!selection || selection.isCollapsed || selection.rangeCount === 0) return null
    const r = selection.getRangeAt(0).getBoundingClientRect()
    if (r.width === 0 && r.height === 0) return null
    return { left: r.left, top: r.top, right: r.right, bottom: r.bottom }
}

const MiniToolbar: React.FC<IProps> = ({ editor, review, speech, onFormat, onParagraph }) => {
    const [shown, setShown] = useState(false)
    const [rect, setRect] = useState<IRect | null>(null)
    const [on, setOn] = useState<Set<ToggleButton>>(new Set())
    const [pos, setPos] = useState<{ left: number, top: number } | null>(null)
    const [menuOpen, setMenuOpen] = useState(false)
    const [labels, setLabels] = useState<Record<string, string>>({})
    const barRef = useRef<HTMLDivElement>(null)
    const timer = useRef<number>()
    const saved = useRef<Range | null>(null)
    // The selection the toolbar was sent away from with Esc: the editor says it again on the next key-up, and it stays away until another is made.
    const dismissed = useRef<Range | null>(null)

    useEffect(() => {
        remote.getStringResources({ names: LABEL_KEYS }).then(setLabels).catch(() => { })
    }, [])

    const hide = useCallback(() => {
        window.clearTimeout(timer.current)
        setShown(false)
        setMenuOpen(false)
        setPos(null)
    }, [])

    // Where the keyboard was (the text), to give it back.
    const restoreText = useCallback(() => {
        const root = document.getElementById('ag-editor-id')
        root?.focus()
        if (saved.current) {
            const selection = window.getSelection()
            selection?.removeAllRanges()
            selection?.addRange(saved.current)
        }
    }, [])

    const inBar = () => !!barRef.current && barRef.current.contains(document.activeElement)

    // The editor says what is selected (and in which formats) after a selection is made, by the mouse or the keys.
    useEffect(() => {
        if (!editor) return
        const picked = ({ reference, formats }: any) => {
            window.clearTimeout(timer.current)
            if (!reference) { dismissed.current = null; hide(); return }
            timer.current = window.setTimeout(() => {
                const at = selectionRect()
                if (!at) { dismissed.current = null; hide(); return }
                const selection = window.getSelection()
                const now = selection && selection.rangeCount > 0 ? selection.getRangeAt(0) : null
                const old = dismissed.current
                if (old && now && now.compareBoundaryPoints(Range.START_TO_START, old) === 0 && now.compareBoundaryPoints(Range.END_TO_END, old) === 0) return
                dismissed.current = null
                setOn(activeButtons(formats))
                setRect(at)
                setShown(true)
            }, SHOW_AFTER)
        }
        editor.eventCenter.subscribe('muya-format-picker', picked)
        return () => {
            editor.eventCenter.unsubscribe('muya-format-picker', picked)
            window.clearTimeout(timer.current)
        }
    }, [editor, hide])

    // Gone with a click elsewhere or Esc; Alt+Down goes into the toolbar.
    useEffect(() => {
        if (!shown) return
        const down = (e: PointerEvent) => {
            if (barRef.current && !barRef.current.contains(e.target as Node)) hide()
        }
        const key = (e: KeyboardEvent) => {
            if (e.key === 'Escape') {
                const wasIn = inBar()
                const selection = window.getSelection()
                dismissed.current = selection && selection.rangeCount > 0 ? selection.getRangeAt(0).cloneRange() : null
                hide()
                if (wasIn) { e.preventDefault(); restoreText() }
            } else if (e.altKey && e.key === 'ArrowDown' && !inBar()) {
                const first = barRef.current?.querySelector('button')
                if (!first) return
                e.preventDefault()
                e.stopPropagation()
                const selection = window.getSelection()
                saved.current = selection && selection.rangeCount > 0 ? selection.getRangeAt(0).cloneRange() : null
                first.focus()
            }
        }
        // the toolbar follows its words when the page scrolls
        const scrolled = () => {
            const at = selectionRect()
            if (at) setRect(at)
            else hide()
        }
        document.addEventListener('pointerdown', down, true)
        document.addEventListener('keydown', key, true)
        addEventListener('scroll', scrolled, { passive: true })
        return () => {
            document.removeEventListener('pointerdown', down, true)
            document.removeEventListener('keydown', key, true)
            removeEventListener('scroll', scrolled)
        }
    }, [shown, hide, restoreText])

    // Placed once it has a size.
    useLayoutEffect(() => {
        const bar = barRef.current
        if (!shown || !rect || !bar) return
        const at = placeToolbar(rect, { width: bar.offsetWidth, height: bar.offsetHeight }, { width: window.innerWidth, height: window.innerHeight })
        setPos(old => old && old.left === at.left && old.top === at.top ? old : { left: at.left, top: at.top })
    }, [shown, rect, review, speech, labels, menuOpen])

    // After a command the selection is still there: the buttons that are on are looked at again.
    const refresh = useCallback(() => {
        window.setTimeout(() => {
            const at = selectionRect()
            if (!at) { hide(); return }
            setOn(activeButtons(editor?.contentState?.selectionFormats?.().formats))
            setRect(at)
        }, 60)
    }, [editor, hide])

    // A command: with the keyboard in the toolbar, the text first gets it back (the command works on the selection there).
    const run = (command: () => void, keep = true) => {
        if (inBar()) restoreText()
        command()
        if (keep) refresh()
        else hide()
    }

    const onBarKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
        const buttons = Array.from(barRef.current?.querySelectorAll('button:not([role="menuitem"])') ?? []) as HTMLElement[]
        const at = buttons.indexOf(document.activeElement as HTMLElement)
        if (e.key === 'ArrowRight' || e.key === 'ArrowLeft' || e.key === 'Home' || e.key === 'End') {
            e.preventDefault()
            const next = e.key === 'Home' ? 0 : e.key === 'End' ? buttons.length - 1
                : (at + (e.key === 'ArrowRight' ? 1 : -1) + buttons.length) % buttons.length
            buttons[next]?.focus()
        } else if (e.key === 'Tab') {
            e.preventDefault()
            hide()
            restoreText()
        }
    }

    // The mouse must not take the focus (or the selection) from the text.
    const keepSelection = (e: React.MouseEvent) => e.preventDefault()

    if (!shown) return null
    const label = (key: string) => labels[key] ?? ''
    const toggle = (name: ToggleButton, text: string, className: string, type: string, glyph: React.ReactNode) => (
        <button type="button" className={`mt-button ${className}`} aria-label={text || undefined} title={text || undefined}
            aria-pressed={on.has(name)} tabIndex={-1} onMouseDown={keepSelection} onClick={() => run(() => onFormat(type))}>
            {glyph}
        </button>
    )
    const speechButton = (name: string) => (
        <button key={name} type="button" className="mt-button mt-word" title={`{${name}}`} tabIndex={-1} onMouseDown={keepSelection}
            onClick={() => run(() => (window as any).__caretSpeech?.insert(JSON.stringify({ name })))}>
            {name}
        </button>
    )

    return (
        <div
            ref={barRef}
            className="mini-toolbar"
            role="toolbar"
            aria-label={label('FormatMenu_Title') || undefined}
            style={{ left: pos?.left ?? 0, top: pos?.top ?? 0, visibility: pos ? 'visible' : 'hidden' }}
            onKeyDown={onBarKeyDown}
        >
            {toggle('bold', label('BoldCtrlB'), 'mt-bold', 'strong', 'B')}
            {toggle('italic', label('ItalicCtrlI'), 'mt-italic', 'em', 'I')}
            {toggle('underline', label('Underline'), 'mt-underline', 'u', 'U')}
            {toggle('strike', label('Strikethrough'), 'mt-strike', 'del', 'S')}
            {toggle('link', label('Link'), 'mt-icon', 'link', '')}
            <button type="button" className="mt-button mt-heading" aria-haspopup="menu" aria-expanded={menuOpen} aria-label="H" tabIndex={-1}
                onMouseDown={keepSelection} onClick={() => setMenuOpen(open => !open)}>
                H<span className="mt-chevron">{''}</span>
            </button>
            {review && (
                <button type="button" className="mt-button mt-icon" title={label('ReviewAddComment')} aria-label={label('ReviewAddComment') || undefined}
                    tabIndex={-1} onMouseDown={keepSelection}
                    onClick={() => run(() => transport.postMessageNoDiff('SelectionToolbar', { command: 'comment' }), false)}>
                    {''}
                </button>
            )}
            {speech && ['pause', 'slow', 'emphasis'].map(speechButton)}
            {menuOpen && (
                <div className="mt-menu" role="menu">
                    {HEADINGS.map(([type, key]) => (
                        <button key={type} type="button" role="menuitem" className="mt-item" tabIndex={-1} onMouseDown={keepSelection}
                            onClick={() => run(() => onParagraph(type), false)}>
                            {label(key)}
                        </button>
                    ))}
                </div>
            )}
        </div>
    )
}

export default MiniToolbar
