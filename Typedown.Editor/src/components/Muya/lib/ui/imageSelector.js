import transport from 'services/transport'
import { URL_REG, isWin } from '../config'
import { getUniqueId, getImageInfo as getImageSrc } from '../utils'
import { getImageInfo } from '../utils/getImageInfo'

class ImageSelector {
  static pluginName = 'imageSelector'

  constructor(muya) {
    this.muya = muya
    muya.eventCenter.subscribe('muya-image-selector', ({ reference, imageInfo }) => {
      if (reference) {
        const boundingClientRect = reference.getBoundingClientRect()
        this.imageInfo = imageInfo;
        let { alt, src, title } = imageInfo.token;
        if (src && /^file:\/\//.test(src)) {
          let protocolLen = 7
          if (isWin && /^file:\/\/\//.test(src)) {
            protocolLen = 8
          }
          src = src.substring(protocolLen)
        }
        transport.postMessage('OpenImageSelector', { imageInfo: { alt, src, title }, boundingClientRect })
      }
    })
    transport.addListener('ReplaceImage', this.replaceImageAsync);
  }

  replaceImageAsync = async ({ alt, src, title }) => {
    if (!this.muya.options.imageAction || URL_REG.test(src)) {
      const { alt: oldAlt, src: oldSrc, title: oldTitle } = this.imageInfo.token.attrs
      if (alt !== oldAlt || src !== oldSrc || title !== oldTitle) {
        this.muya.contentState.replaceImage(this.imageInfo, { alt, src, title })
      }
    } else {
      if (src) {
        const id = `loading-${getUniqueId()}`
        this.muya.contentState.replaceImage(this.imageInfo, {
          alt: id,
          src,
          title
        })

        try {
          const newSrc = await this.muya.options.imageAction(src, id, alt)
          const { src: localPath } = getImageSrc(src)
          if (localPath) {
            this.muya.contentState.stateRender.urlMap.set(newSrc, localPath)
          }
          const imageWrapper = this.muya.container.querySelector(`span[data-id=${id}]`)

          if (imageWrapper) {
            const imageInfo = getImageInfo(imageWrapper)
            this.muya.contentState.replaceImage(imageInfo, {
              alt,
              src: newSrc,
              title
            })
          }
        } catch (error) {
          // TODO: Notify user about an error.
          console.error('Unexpected error on image action:', error)
        }
      }
    }
    this.muya.eventCenter.dispatch('stateChange')
  }

  destroy() {
    transport.removeListener('ReplaceImage', this.replaceImageAsync);
  }

}

export default ImageSelector
