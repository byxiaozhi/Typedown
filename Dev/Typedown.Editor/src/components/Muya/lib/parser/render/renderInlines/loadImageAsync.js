import { getUniqueId, loadImage } from '../../../utils'
import { insertAfter, operateClassName } from '../../../utils/domManipulate'
import { CLASS_OR_ID } from '../../../config'

export default function loadImageAsync (imageInfo, attrs, className, imageClass) {
  const { src, isUnknownType } = imageInfo
  let id
  let isSuccess
  let w
  let h
  let domsrc

  let reload = false
  if (this.loadImageMap.has(src)) {
    const imageInfo = this.loadImageMap.get(src)
    if (imageInfo.dispMsec !== imageInfo.touchMsec) {
      // We have a cached image, but force it to load.
      reload = true
    }
  } else {
    reload = true
  }
  if (reload) {
    id = getUniqueId()
    loadImage(src, isUnknownType)
      .then(({ url, width, height }) => {
        const imageText = document.querySelector(`#${id}`)
        const img = document.createElement('img')
        let dispMsec = Date.now()
        let touchMsec = dispMsec
        if (/^file:\/\//.test(src)) {
          domsrc = url + '?msec=' + dispMsec
        } else {
          domsrc = url
        }
        img.src = domsrc
        if (attrs.alt) img.alt = attrs.alt.replace(/[`*{}[\]()#+\-.!_>~:|<>$]/g, '')
        if (attrs.title) img.setAttribute('title', attrs.title)
        if (attrs.style) img.setAttribute('style', attrs.style)
        if (attrs.width && typeof attrs.width === 'number') {
          img.setAttribute('width', attrs.width)
        }
        if (attrs.height && typeof attrs.height === 'number') {
          img.setAttribute('height', attrs.height)
        }
        if (imageClass) {
          img.classList.add(imageClass)
        }

        // The picture loaded: that is recorded first. Putting it on the page comes after and cannot undo it - an
        // exception there (a picture inside a link had no image container) used to land in the catch below, which
        // cached the file as failed, and every other picture of that file was then drawn failed when re-rendered (the
        // showcase's checkerboard, clicked after source mode and back, IM01).
        if (this.urlMap.has(src)) {
          this.urlMap.delete(src)
        }
        this.loadImageMap.set(src, {
          id,
          isSuccess: true,
          width,
          height,
          dispMsec,
          touchMsec,
          domsrc
        })
        try {
          if (imageText) {
            if (imageText.classList.contains('ag-inline-image')) {
              // A picture inside a link has no container of its own here: its state changes, nothing is added.
              const imageContainer = imageText.querySelector('.ag-image-container')
              if (imageContainer) {
                const oldImage = imageContainer.querySelector('img')
                if (oldImage) {
                  oldImage.remove()
                }
                imageContainer.appendChild(img)
              }
              imageText.classList.remove('ag-image-loading')
              imageText.classList.add('ag-image-success')
            } else {
              insertAfter(img, imageText)
              operateClassName(imageText, 'remove', CLASS_OR_ID.AG_IMAGE_FAIL)
              operateClassName(imageText, 'add', className)
            }
          }
        } catch (err) {
          console.log('image: the picture loaded but could not be put on the page', err)
        }
      })
      .catch(() => {
        const imageText = document.querySelector(`#${id}`)
        if (imageText) {
          operateClassName(imageText, 'remove', CLASS_OR_ID.AG_IMAGE_LOADING)
          operateClassName(imageText, 'add', CLASS_OR_ID.AG_IMAGE_FAIL)
          const image = imageText.querySelector('img')
          if (image) {
            image.remove()
          }
        }
        if (this.urlMap.has(src)) {
          this.urlMap.delete(src)
        }
        this.loadImageMap.set(src, {
          id,
          isSuccess: false
        })
      })
  } else {
    const imageInfo = this.loadImageMap.get(src)

    id = imageInfo.id
    isSuccess = imageInfo.isSuccess
    w = imageInfo.width
    h = imageInfo.height
    domsrc = imageInfo.domsrc
  }

  return { id, isSuccess, domsrc, width: w, height: h }
}
