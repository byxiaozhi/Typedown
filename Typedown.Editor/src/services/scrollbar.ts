import transport from "./transport"

let prevViewportWidth = 0, prevViewportHeight = 0
let prevMaximumX = 0, prevMaximumY = 0
let prevScrollX = 0, prevScrollY = 0

const equals = (a: number, b: number) => Math.abs(a - b) < 1

const postScrollState = () => {
    const viewportWidth = window.innerWidth
    const viewportHeight = window.innerHeight
    const maximumX = document.body.clientWidth - window.innerWidth
    const maximumY = document.body.clientHeight - window.innerHeight
    const scrollX = window.scrollX
    const scrollY = window.scrollY
    if (!equals(viewportWidth, prevViewportWidth) ||
        !equals(viewportHeight, prevViewportHeight) ||
        !equals(maximumX, prevMaximumX) ||
        !equals(maximumY, prevMaximumY) ||
        !equals(scrollX, prevScrollX) ||
        !equals(scrollY, prevScrollY)) {
        transport.postMessage('OnScroll', { viewportWidth, viewportHeight, maximumX, maximumY, scrollX, scrollY })
        prevViewportWidth = window.innerWidth
        prevViewportHeight = window.innerHeight
        prevMaximumX = document.body.clientWidth - window.innerWidth
        prevMaximumY = document.body.clientHeight - window.innerHeight
        prevScrollX = scrollX
        prevScrollY = scrollY
    }
}

const resizeObserver = new ResizeObserver(postScrollState)
resizeObserver.observe(document.body)
addEventListener('scroll', postScrollState)
addEventListener('resize', postScrollState)

transport.addListener<{ scrollX: number, scrollY: number }>('OnScroll', ({ scrollX, scrollY }) => {
    if (!equals(scrollX, window.scrollX) || !equals(scrollY, window.scrollY)) {
        prevScrollX = scrollX
        prevScrollY = scrollY
        window.scrollTo(scrollX, scrollY)
    }
})