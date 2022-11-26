import transport from 'services/transport'

class FrontMenu {
  static pluginName = 'frontMenu'

  constructor(muya) {
    muya.eventCenter.subscribe('muya-front-menu', ({ reference, ...arg }) => {
      if (reference) {
        const boundingClientRect = reference.getBoundingClientRect()
        transport.postMessage('OpenFrontMenu', { ...arg, boundingClientRect })
      }
    })

    this.onFrontMenuClosed = () => {
      const { contentState } = muya
      contentState.selectedBlock = null
      contentState.partialRender()
    };

    transport.addListener('FrontMenuClosed', this.onFrontMenuClosed);
  }

  destroy() {
    transport.removeListener('FrontMenuClosed', this.onFrontMenuClosed);
  }

}

export default FrontMenu
