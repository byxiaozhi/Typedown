import transport from "./transport"

// The load the page is showing. A report without one comes from a page that has not been given a document
// yet — a freshly navigated page reports its empty body's first layout as "scrolled to 0", and the host
// took that for the reader's position and handed it back on the very next load: leaving the settings, which
// navigates the page again, always came back at the top.
let loadId: number | undefined
export const setScrollLoadId = (id: number | undefined) => { loadId = id }

const postScrollState = () => {
    transport.postMessage('OnScroll', {
        loadId,
        viewportWidth: window.innerWidth,
        viewportHeight: window.innerHeight,
        maximumX: document.body.scrollWidth - window.innerWidth,
        maximumY: document.body.scrollHeight - window.innerHeight,
        scrollX: window.scrollX,
        scrollY: window.scrollY
    })
}

let pendingFrame: number | undefined
const scheduleScrollState = () => {
    if (pendingFrame !== undefined) return
    pendingFrame = requestAnimationFrame(() => {
        pendingFrame = undefined
        postScrollState()
    })
}

const resizeObserver = new ResizeObserver(scheduleScrollState)
resizeObserver.observe(document.body)
addEventListener('scroll', scheduleScrollState, { passive: true })
addEventListener('resize', scheduleScrollState)

transport.addListener<{ scrollX: number, scrollY: number }>('OnScroll', ({ scrollX, scrollY }) => {
    const equals = (a: number, b: number) => Math.abs(a - b) < 1
    if (!equals(scrollX, window.scrollX) || !equals(scrollY, window.scrollY))
        window.scrollTo(scrollX, scrollY)
})
