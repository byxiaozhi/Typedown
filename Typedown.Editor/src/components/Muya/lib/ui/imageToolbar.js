import transport from 'services/transport'

class ImageToolbar {
  static pluginName = 'imageToolbar'

  constructor(muya) {
    this.muya = muya;
    const { eventCenter } = muya
    this.imageInfo = null
    eventCenter.subscribe('muya-image-toolbar', ({ reference, imageInfo }) => {
      this.reference = reference
      if (reference) {
        this.imageInfo = imageInfo
        const boundingClientRect = reference.getBoundingClientRect()
        transport.postMessage('OpenImageToolbar', { boundingClientRect })
      }
    })
    this.onImageEditToolbarClick = ({ type }) => this.selectItem(type)
    transport.addListener('ImageEditToolbarClick', this.onImageEditToolbarClick)
  }

  selectItem(type) {
    const { imageInfo } = this
    switch (type) {
      // Delete image.
      case 'delete':
        this.muya.contentState.deleteImage(imageInfo)
        break;
      // Edit image, for example: editor alt and title, replace image.
      case 'edit': {
        const rect = this.reference.getBoundingClientRect()
        const reference = {
          getBoundingClientRect() {
            rect.height = 0
            return rect
          }
        }
        this.muya.eventCenter.dispatch('muya-image-selector', {
          reference,
          imageInfo,
          cb: () => { }
        })
        break;
      }
      case 'inline':
      case 'left':
      case 'center':
      case 'right': {
        this.muya.contentState.updateImage(this.imageInfo, 'data-align', type)
        break;
      }
    }
  }

  destroy() {
    transport.removeListener('ImageEditToolbarClick', this.onImageEditToolbarClick)
  }
}

export default ImageToolbar
