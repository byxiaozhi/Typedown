import transport from "./transport"

const postScrollState = () => {
    transport.postMessage('OnScroll', {
        viewportWidth: window.innerWidth,
        viewportHeight: window.innerHeight,
        maximumX: document.body.scrollWidth - window.innerWidth,
        maximumY: document.body.scrollHeight - window.innerHeight,
        scrollX: window.scrollX,
        scrollY: window.scrollY
    })
}

const resizeObserver = new ResizeObserver(postScrollState)
resizeObserver.observe(document.body)
addEventListener('scroll', postScrollState)
addEventListener('resize', postScrollState)

transport.addListener<{ scrollX: number, scrollY: number }>('OnScroll', ({ scrollX, scrollY }) => {
    const equals = (a: number, b: number) => Math.abs(a - b) < 1
    if (!equals(scrollX, window.scrollX) || !equals(scrollY, window.scrollY))
        window.scrollTo(scrollX, scrollY)
})