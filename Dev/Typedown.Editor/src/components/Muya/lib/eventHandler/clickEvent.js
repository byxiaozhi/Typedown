import { operateClassName } from '../utils/domManipulate'
import { getImageInfo } from '../utils/getImageInfo'
import { CLASS_OR_ID } from '../config'
import selection from '../selection'
import remote from 'services/remote/common'
import Slugger from '../parser/marked/slugger'
import { getHeadingPlainText } from '../utils'

class ClickEvent {
  constructor(muya) {
    this.muya = muya
    this.clickBinding()
    this.contextClickBingding()
  }

  /**
   * A link to a place in this document: `#fragment`. Headings in the editor carry their block key as id, not
   * a slug of their text, so `document.querySelector('#some-heading')` found nothing and the link did nothing
   * (five Store reviews). The fragment is matched the way the export names headings — the same slugger, in
   * document order, so duplicates number the same way — and the heading lands at the top, as from the outline.
   */
  scrollToAnchor(href) {
    let fragment = href.slice(1)
    try { fragment = decodeURIComponent(fragment) } catch (e) { }
    let target = fragment ? document.getElementById(fragment) : null
    if (!target) {
      // Headings carry a block key as id, not a slug, so match the fragment against each heading. Tools and
      // people write the fragment differently — with the dots kept (#1.1-x), dropped (#11-x), or spaces left
      // in — so besides the exact slug this also compares a loose form with every non-letter/digit/CJK
      // character removed, which brings "1.1-研究" and "11 研究" to the same thing.
      const slugger = new Slugger()
      const wanted = fragment.toLowerCase()
      const loose = t => t.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, '')
      const wantedLoose = loose(fragment)
      let looseHit = null
      for (const block of this.muya.contentState.blocks) {
        if (!/^h\d$/.test(block.type)) continue
        const { headingStyle, key } = block
        const text = block.children[0].text
        const plain = getHeadingPlainText(headingStyle === 'setext' ? text.trim() : text.replace(/^\s*#{1,6}\s{1,}/, '').trim())
        const slug = slugger.slug(plain)
        if (slug === wanted || plain.toLowerCase() === wanted) {
          target = document.querySelector(`#${key}`)
          break
        }
        if (!looseHit && wantedLoose && (loose(slug) === wantedLoose || loose(plain) === wantedLoose)) {
          looseHit = key
        }
      }
      if (!target && looseHit) target = document.querySelector(`#${looseHit}`)
    }
    if (!target) return
    const top = target.getBoundingClientRect().top
    window.scrollTo(window.scrollX, window.scrollY + top - 16)
  }

  contextClickBingding() {
    const { container, eventCenter, contentState } = this.muya
    const handler = event => {
      event.preventDefault()
      event.stopPropagation()

      // Hide all float box
      const { keyboard } = this.muya
      if (keyboard) {
        keyboard.hideAllFloatTools()
      }

      const { start, end } = selection.getCursorRange()

      // Cursor out of editor
      if (!start || !end) {
        return
      }

      const startBlock = contentState.getBlock(start.key)
      const nextTextBlock = contentState.findNextBlockInLocation(startBlock)
      if (
        nextTextBlock && nextTextBlock.key === end.key &&
        end.offset === 0 &&
        start.offset === startBlock.text.length
      ) {
        // Set cursor at the end of start block and reset cursor
        // Because if you right click at the end of one text block, the cursor.start will at the end of
        // start block and the cursor.end will at the next text block beginning. So we reset the cursor
        // at the end of start block.
        contentState.cursor = {
          start,
          end: start
        }
        selection.setCursorRange(contentState.cursor)
      } else {
        // Commit native cursor position because right-clicking doesn't update the cursor postion.
        contentState.cursor = {
          start,
          end
        }
      }

      const sectionChanges = contentState.selectionChange(contentState.cursor)
      eventCenter.dispatch('contextmenu', event, sectionChanges)
    }
    eventCenter.attachDOMEvent(container, 'contextmenu', handler)
  }

  clickBinding() {
    const { container, eventCenter, contentState } = this.muya
    const clickHandler = event => {
      const { target } = event
      // Reading mode is display only: everything that would start an edit (table tools, image tools, switching a
      // math/HTML block back to its source, ticking a task box) is skipped. Links, the code copy button and the
      // footnote back link still work because they only navigate or copy.
      const readOnly = !!this.muya.options.readOnly
      // handler table click
      const toolItem = getToolItem(target)
      contentState.selectedImage = null
      contentState.selectedTableCells = null
      if (toolItem && !readOnly) {
        event.preventDefault()
        event.stopPropagation()
        const type = toolItem.getAttribute('data-label')
        const grandPa = toolItem.parentNode.parentNode
        if (grandPa.classList.contains('ag-tool-table')) {
          contentState.tableToolBarClick(type)
        }
      }
      // Handle table drag bar click
      if (target.classList.contains('ag-drag-handler') && !readOnly) {
        event.preventDefault()
        event.stopPropagation()
        const rect = target.getBoundingClientRect()
        const reference = {
          getBoundingClientRect() {
            return rect
          },
          width: rect.offsetWidth,
          height: rect.offsetHeight
        }
        eventCenter.dispatch('muya-table-bar', {
          reference,
          tableInfo: {
            barType: target.classList.contains('left') ? 'left' : 'bottom'
          }
        })
      }
      // Handle image and inline math preview click
      const markedImageText = target.previousElementSibling
      const mathRender = target.closest(`.${CLASS_OR_ID.AG_MATH_RENDER}`)
      const rubyRender = target.closest(`.${CLASS_OR_ID.AG_RUBY_RENDER}`)
      const imageWrapper = target.closest(`.${CLASS_OR_ID.AG_INLINE_IMAGE}`)
      const codeCopy = target.closest('.ag-code-copy')
      const footnoteBackLink = target.closest('.ag-footnote-backlink')
      const imageDelete = target.closest('.ag-image-icon-delete') || target.closest('.ag-image-icon-close')
      const mathText = mathRender && mathRender.previousElementSibling
      const rubyText = rubyRender && rubyRender.previousElementSibling
      if (readOnly) {
        // nothing below selects or reveals source text in reading mode
      } else if (markedImageText && markedImageText.classList.contains(CLASS_OR_ID.AG_IMAGE_MARKED_TEXT)) {
        eventCenter.dispatch('format-click', {
          event,
          formatType: 'image',
          data: event.target.getAttribute('src')
        })
        selectionText(markedImageText)
      } else if (mathText) {
        selectionText(mathText)
      } else if (rubyText) {
        selectionText(rubyText)
      }
      if (codeCopy) {
        event.stopPropagation()
        event.preventDefault()
        return this.muya.contentState.copyCodeBlock(event)
      }
      // Handle delete inline iamge by click delete icon.
      if (imageDelete && imageWrapper && !readOnly) {
        const imageInfo = getImageInfo(imageWrapper)
        event.preventDefault()
        event.stopPropagation()
        // hide image selector if needed.
        eventCenter.dispatch('muya-image-selector', { reference: null })
        return contentState.deleteImage(imageInfo)
      }

      if (footnoteBackLink) {
        event.preventDefault()
        event.stopPropagation()
        const figure = event.target.closest('figure')
        const identifier = figure.querySelector('span.ag-footnote-input').textContent
        if (identifier) {
          const footnoteIdentifier = document.querySelector(`#noteref-${identifier}`)
          if (footnoteIdentifier) {
            footnoteIdentifier.scrollIntoView({ behavior: 'smooth' })
          }
        }
        return
      }

      // Handle image click, to select the current image
      if (target.tagName === 'IMG' && imageWrapper && !readOnly) {
        // Handle select image
        const imageInfo = getImageInfo(imageWrapper)
        event.preventDefault()
        eventCenter.dispatch('select-image', imageInfo)
        // Handle show image toolbar
        const rect = imageWrapper.querySelector('.ag-image-container').getBoundingClientRect()
        const reference = {
          getBoundingClientRect() {
            return rect
          },
          width: imageWrapper.offsetWidth,
          height: imageWrapper.offsetHeight
        }
        if (event.type == 'click') {
          eventCenter.dispatch('muya-image-toolbar', {
            reference,
            imageInfo
          })
        }
        contentState.selectImage(imageInfo)
        return
      }

      // Handle click imagewrapper when it's empty or image load failed.
      if (
        !readOnly && imageWrapper &&
        (
          imageWrapper.classList.contains('ag-empty-image') ||
          imageWrapper.classList.contains('ag-image-fail')
        )
      ) {
        const rect = imageWrapper.getBoundingClientRect()
        const reference = {
          getBoundingClientRect() {
            return rect
          }
        }
        const imageInfo = getImageInfo(imageWrapper)
        eventCenter.dispatch('muya-image-selector', {
          reference,
          imageInfo,
          cb: () => { }
        })
        event.preventDefault()
        return event.stopPropagation()
      }

      if (target.closest('div.ag-container-preview') || target.closest('div.ag-html-preview')) {
        event.stopPropagation()
        if (target.closest('div.ag-container-preview') && !readOnly) {
          event.preventDefault()
          const figureEle = target.closest('figure')
          contentState.handleContainerBlockClick(figureEle)
        }
        return
      }
      // handler container preview click
      const editIcon = target.closest('.ag-container-icon')
      if (editIcon && !readOnly) {
        event.preventDefault()
        event.stopPropagation()
        if (editIcon.parentNode.classList.contains('ag-container-block')) {
          contentState.handleContainerBlockClick(editIcon.parentNode)
        }
      }

      // handler to-do checkbox click
      if (target.tagName === 'INPUT' && target.classList.contains(CLASS_OR_ID.AG_TASK_LIST_ITEM_CHECKBOX) && !readOnly) {
        contentState.listItemCheckBoxClick(target)
      }

      // Links are followed on mousedown (below). The click that follows must not place the caret either:
      // the page has just been scrolled to the anchor, and a caret put back into the link's paragraph
      // would be above the window, which the caret-follow rule answers by scrolling right back up.
      const link = event.target.closest('a')
      if (link) {
        event.preventDefault();
        if (event.metaKey || event.ctrlKey || readOnly) return
      }

      contentState.clickHandler(event)
    }

    const mouseupHandler = event => {
      setTimeout(() => contentState.mouseupHandler(event))
    }

    // Following a link has to happen on mousedown, not click: while editing, the mousedown puts the caret
    // into the link's block, the block re-renders as raw Markdown, and by the time the click arrives the
    // <a> is gone — "Ctrl+click says it will open the link and does nothing" (Store reviews). Ctrl+click
    // follows a link while editing; in reading mode a plain click does, there being nothing else a click
    // on a link could mean there.
    const linkMousedownHandler = event => {
      if (event.button !== 0) return
      const readOnly = !!this.muya.options.readOnly
      if (!(event.metaKey || event.ctrlKey || readOnly)) return
      const link = event.target.closest('a')
      const href = link && link.getAttribute('href')
      if (!href) return
      event.preventDefault()
      event.stopPropagation()
      if (href.startsWith('#')) {
        this.scrollToAnchor(href)
      } else {
        remote.openNewWindow(href)
      }
    }
    eventCenter.attachDOMEvent(container, 'mousedown', linkMousedownHandler)
    eventCenter.attachDOMEvent(window, 'mouseup', mouseupHandler)
    eventCenter.attachDOMEvent(container, 'click', clickHandler)
    eventCenter.attachDOMEvent(container, 'contextmenu', clickHandler)
  }
}

function getToolItem(target) {
  return target.closest('[data-label]')
}

function selectionText(node) {
  const textLen = node.textContent.length
  operateClassName(node, 'remove', CLASS_OR_ID.AG_HIDE)
  operateClassName(node, 'add', CLASS_OR_ID.AG_GRAY)
  selection.importSelection({
    start: textLen,
    end: textLen
  }, node)
}

export default ClickEvent
