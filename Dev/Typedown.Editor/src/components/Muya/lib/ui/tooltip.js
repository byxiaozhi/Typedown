import transport from 'services/transport'

class Tooltip {
  constructor(muya) {
    this.muya = muya
    this.cache = new WeakMap()
    const { eventCenter } = this.muya
    eventCenter.attachDOMEvent(document, 'mouseover', this.mouseOver.bind(this))
  }

  mouseOver(event) {
    const { target } = event
    const toolTipTarget = target.closest('[data-tooltip]')
    if (toolTipTarget && !this.cache.has(toolTipTarget)) {
      const { eventCenter } = this.muya
      const eventId = eventCenter.attachDOMEvent(toolTipTarget, 'mouseleave', this.mouseLeave.bind(this))
      this.cache.set(toolTipTarget, eventId)
      const tooltip = toolTipTarget.getAttribute('data-tooltip')
      const boundingClientRect = toolTipTarget.getBoundingClientRect()
      transport.postMessage('OpenToolTip', { open: true, tooltip, boundingClientRect })
      const timer = setInterval(() => {
        if (!document.body.contains(toolTipTarget)) {
          this.mouseLeave({ target: toolTipTarget })
          clearInterval(timer)
        }
      }, 300)
    }
  }

  mouseLeave({ target }) {
    if (this.cache.has(target)) {
      const { eventCenter } = this.muya
      const eventId = this.cache.get(target);
      eventCenter.detachDOMEvent(eventId)
      transport.postMessage('OpenToolTip', { open: false });
      this.cache.delete(target)
    }
  }
}

export default Tooltip
