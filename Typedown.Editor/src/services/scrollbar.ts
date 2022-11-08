import transport from "./transport"

const postScrollState = () => {
    transport.postMessage('OnScroll', {
        viewportWidth: window.innerWidth,
        viewportHeight: window.innerHeight,
        maximumX: document.body.clientWidth - window.innerWidth,
        maximumY: document.body.clientHeight - window.innerHeight,
        scrollX: window.scrollX,
        scrollY: window.scrollY
    })
}

const resizeObserver = new ResizeObserver(postScrollState)
resizeObserver.observe(document.body)
addEventListener('scroll', postScrollState)

transport.addListener<{ scrollX: number, scrollY: number }>('OnScroll', ({ scrollX, scrollY }) => {
    if (Math.abs(scrollX - window.scrollX) > 1e-6 || Math.abs(scrollY - window.scrollY) > 1e-6)
        window.scrollTo(scrollX, scrollY)
})