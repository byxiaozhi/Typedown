import React, { useCallback, useEffect, useRef, useState } from "react";

// The scroll bar of the left (source) pane in the Split view. The page's own scroll bar is hidden (App.scss) and the right half of the
// window is the preview, so the source pane had no bar at all: this one sits at the right edge of the left half, follows the page's
// scroll, and can be dragged or clicked like any other. The page itself still does the scrolling (CodeMirror's search and scroll logic
// depend on it).
const MIN_THUMB = 28

// The element the page scrolls with: the document element, or the body when the page is in quirks mode.
const scroller = () => document.scrollingElement || document.documentElement

interface IMetrics {
    show: boolean
    thumbTop: number
    thumbHeight: number
}

const SplitScrollbar: React.FC = () => {
    const [metrics, setMetrics] = useState<IMetrics>({ show: false, thumbTop: 0, thumbHeight: 0 })
    const drag = useRef<{ startY: number, startScroll: number } | null>(null)

    const measure = useCallback(() => {
        const view = window.innerHeight
        const total = scroller().scrollHeight
        if (total <= view + 1) {
            setMetrics(old => old.show ? { show: false, thumbTop: 0, thumbHeight: 0 } : old)
            return
        }
        const thumbHeight = Math.max(MIN_THUMB, view * view / total)
        const room = view - thumbHeight
        const thumbTop = room * Math.min(1, window.scrollY / (total - view))
        setMetrics(old => old.show && Math.abs(old.thumbTop - thumbTop) < 0.5 && Math.abs(old.thumbHeight - thumbHeight) < 0.5 ? old : { show: true, thumbTop, thumbHeight })
    }, [])

    useEffect(() => {
        measure()
        addEventListener('scroll', measure, { passive: true })
        addEventListener('resize', measure)
        const observer = new ResizeObserver(measure)
        observer.observe(document.body)
        observer.observe(document.documentElement)
        // the text can grow without the body changing size: look again now and then
        const timer = window.setInterval(measure, 500)
        return () => {
            window.clearInterval(timer)
            removeEventListener('scroll', measure)
            removeEventListener('resize', measure)
            observer.disconnect()
        }
    }, [measure])

    const onThumbDown = (e: React.PointerEvent<HTMLDivElement>) => {
        e.preventDefault()
        e.stopPropagation()
        e.currentTarget.setPointerCapture(e.pointerId)
        drag.current = { startY: e.clientY, startScroll: window.scrollY }
    }

    const onThumbMove = (e: React.PointerEvent<HTMLDivElement>) => {
        if (!drag.current) return
        const view = window.innerHeight
        const total = scroller().scrollHeight
        const room = view - metrics.thumbHeight
        if (room <= 0) return
        window.scrollTo(window.scrollX, drag.current.startScroll + (e.clientY - drag.current.startY) * (total - view) / room)
    }

    const onThumbUp = (e: React.PointerEvent<HTMLDivElement>) => {
        drag.current = null
        if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId)
    }

    // A click on the track outside the thumb moves one page towards it.
    const onTrackDown = (e: React.PointerEvent<HTMLDivElement>) => {
        if (e.target !== e.currentTarget) return
        e.preventDefault()
        const above = e.clientY < metrics.thumbTop + metrics.thumbHeight / 2
        window.scrollBy({ top: (above ? -1 : 1) * window.innerHeight * 0.9 })
    }

    if (!metrics.show) return null
    return (
        <div className="split-scrollbar" onPointerDown={onTrackDown}>
            <div
                className="split-scrollbar-thumb"
                style={{ top: metrics.thumbTop, height: metrics.thumbHeight }}
                onPointerDown={onThumbDown}
                onPointerMove={onThumbMove}
                onPointerUp={onThumbUp}
                onPointerCancel={onThumbUp}
            />
        </div>
    )
}

export default SplitScrollbar