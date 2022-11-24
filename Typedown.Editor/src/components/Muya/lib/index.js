import ContentState from './contentState'
import EventCenter from './eventHandler/event'
import MouseEvent from './eventHandler/mouseEvent'
import Clipboard from './eventHandler/clipboard'
import Keyboard from './eventHandler/keyboard'
import DragDrop from './eventHandler/dragDrop'
import Resize from './eventHandler/resize'
import ClickEvent from './eventHandler/clickEvent'
import { CLASS_OR_ID, MUYA_DEFAULT_OPTION } from './config'
import { wordCount } from './utils'
import ExportMarkdown from './utils/exportMarkdown'
import ExportHtml from '../../../services/exportHtml'
import ToolTip from './ui/tooltip'
import '../../../assets/styles/index.css'

class Muya {
  static plugins = []

  static use(plugin, options = {}) {
    this.plugins.push({
      plugin,
      options
    })
  }

  constructor(container, options) {
    this.options = Object.assign({}, MUYA_DEFAULT_OPTION, options)
    const { markdown } = this.options
    this.markdown = markdown
    this.container = getContainer(container, this.options)
    this.eventCenter = new EventCenter()
    this.tooltip = new ToolTip(this)
    // UI plugins
    if (Muya.plugins.length) {
      for (const { plugin: Plugin, options: opts } of Muya.plugins) {
        this[Plugin.pluginName] = new Plugin(this, opts)
      }
    }

    this.contentState = new ContentState(this, this.options)
    this.clipboard = new Clipboard(this)
    this.clickEvent = new ClickEvent(this)
    this.keyboard = new Keyboard(this)
    this.dragdrop = new DragDrop(this)
    this.resize = new Resize(this)
    this.mouseEvent = new MouseEvent(this)
    this.init()
  }

  init() {
    const { container, contentState, eventCenter } = this
    contentState.stateRender.setContainer(container.children[0])
    eventCenter.subscribe('stateChange', this.dispatchChange)
    const { markdown } = this
    const { focusMode } = this.options
    this.setMarkdown(markdown)
    this.setFocusMode(focusMode, true)
    this.mutationObserver()
    eventCenter.attachDOMEvent(container, 'focus', () => {
      eventCenter.dispatch('focus')
    })
    eventCenter.attachDOMEvent(container, 'blur', () => {
      eventCenter.dispatch('blur')
    })
  }

  mutationObserver() {
    // Select the node that will be observed for mutations
    const { container, eventCenter } = this

    // Options for the observer (which mutations to observe)
    const config = { childList: true, subtree: true }

    // Callback function to execute when mutations are observed
    const callback = (mutationsList, observer) => {
      for (const mutation of mutationsList) {
        if (mutation.type === 'childList') {
          const { removedNodes, target } = mutation
          // If the code executes any of the following `if` statements, the editor has gone wrong.
          // need to report bugs.
          if (removedNodes && removedNodes.length) {
            const hasTable = Array.from(removedNodes).some(node => node.nodeType === 1 && node.closest('table.ag-paragraph'))
            if (hasTable) {
              eventCenter.dispatch('crashed')
              console.warn('There was a problem with the table deletion.')
            }
          }

          if (target.getAttribute('id') === 'ag-editor-id' && target.childElementCount === 0) {
            // TODO: the editor can not be input any more. report bugs and recovery...
            eventCenter.dispatch('crashed')
            console.warn('editor crashed, and can not be input any more.')
          }
        }
      }
    }

    // Create an observer instance linked to the callback function
    const observer = new MutationObserver(callback)

    // Start observing the target node for configured mutations
    observer.observe(container, config)
  }

  dispatchChange = () => {
    this.dispatchChangeContentChange()
    setTimeout(() => {
      try {
        this.dispatchSelectionChange()
        this.dispatchSelectionFormats()
      } catch (err) {
        console.log(err)
      }
    });
  }

  dispatchChangeContentChange = () => {
    const { markdown, cursor } = this.getMarkdownAndCursor()
    const wordCount = this.getWordCount(markdown)
    this.markdown = markdown
    const toc = this.getTOC()
    this.eventCenter.dispatch('contentChange', { markdown: this.markdown, wordCount, cursor, toc })
  }

  dispatchSelectionChange = () => {
    const selection = this.contentState.selectionChange()
    this.eventCenter.dispatch('selectionChange', selection)
  }

  dispatchSelectionFormats = () => {
    const { formats } = this.contentState.selectionFormats()
    this.eventCenter.dispatch('selectionFormats', formats)
  }

  getMarkdown() {
    if (!this.contentState) {
      return ''
    }
    const blocks = this.contentState.getBlocks()
    const { isGitlabCompatibilityEnabled, listIndentation } = this.contentState
    const markdown = new ExportMarkdown(blocks, listIndentation, isGitlabCompatibilityEnabled).generate();
    return markdown ?? this.markdown
  }

  getTOC() {
    return this.contentState.getTOC()
  }

  exportStyledHTML(options) {
    const { markdown } = this
    return new ExportHtml(markdown, this.options).generate(options)
  }

  exportHtml() {
    const { markdown } = this
    return new ExportHtml(markdown, this.options).renderHtml()
  }

  getWordCount(markdown) {
    return wordCount(markdown)
  }

  getMarkdownAndCursor() {
    return this.contentState.getMarkdownAndCodeMirrorCursor()
  }

  setMarkdown(markdown, cursor, isRenderCursor = true) {
    let newMarkdown = markdown
    let isValid = false
    if (cursor && cursor.anchor && cursor.focus) {
      const cursorInfo = this.contentState.addCursorToMarkdown(markdown, cursor)
      newMarkdown = cursorInfo.markdown
      isValid = cursorInfo.isValid
    }
    this.contentState.importMarkdown(newMarkdown)
    this.contentState.importCursor(cursor && isValid)
    this.contentState.render(isRenderCursor)
    setTimeout(() => {
      this.dispatchChange()
    }, 0)
  }

  setCursor(cursor) {
    const markdown = this.getMarkdown()
    const isRenderCursor = true

    return this.setMarkdown(markdown, cursor, isRenderCursor)
  }

  createTable(tableChecker) {
    return this.contentState.createTable(tableChecker)
  }

  getSelection() {
    return this.contentState.selectionChange()
  }

  setFocusMode(bool, first = false) {
    const { container } = this
    const { focusMode } = this.options
    if (bool && (!focusMode || first)) {
      container.classList.add(CLASS_OR_ID.AG_FOCUS_MODE)
    } else {
      container.classList.remove(CLASS_OR_ID.AG_FOCUS_MODE)
    }
    this.options.focusMode = bool
  }

  setFont({ fontSize, lineHeight }) {
    if (fontSize) {
      this.options.fontSize = parseInt(fontSize, 10)
    }
    if (lineHeight) {
      this.options.lineHeight = lineHeight
    }
    this.contentState.render(false)
  }

  setTabSize(tabSize) {
    if (!tabSize || typeof tabSize !== 'number') {
      tabSize = 4
    } else if (tabSize < 1) {
      tabSize = 1
    } else if (tabSize > 4) {
      tabSize = 4
    }
    this.contentState.tabSize = tabSize
  }

  setListIndentation(listIndentation) {
    if (typeof listIndentation === 'number') {
      if (listIndentation < 1 || listIndentation > 4) {
        listIndentation = 1
      }
    } else if (listIndentation !== 'dfm') {
      listIndentation = 1
    }
    this.contentState.listIndentation = listIndentation
  }

  updateParagraph(type) {
    this.contentState.updateParagraph(type)
  }

  duplicate() {
    this.contentState.duplicate()
  }

  deleteParagraph() {
    this.contentState.deleteParagraph()
  }

  insertParagraph(location/* before or after */, text = '', outMost = false) {
    this.contentState.insertParagraph(location, text, outMost)
  }

  editTable(data) {
    this.contentState.editTable(data)
  }

  hasFocus() {
    return document.activeElement === this.container
  }

  focus() {
    this.contentState.setCursor()
    this.container.focus()
  }

  blur(isRemoveAllRange = false, unSelect = false) {
    if (isRemoveAllRange) {
      const selection = document.getSelection()
      selection.removeAllRanges()
    }

    if (unSelect) {
      this.contentState.selectedImage = null
      this.contentState.selectedTableCells = null
    }

    this.hideAllFloatTools()
    this.container.blur()
  }

  format(type) {
    this.contentState.format(type)
  }

  insertImage(imageInfo) {
    this.contentState.insertImage(imageInfo)
  }

  search(value, opt) {
    const { selectHighlight } = opt
    this.contentState.search(value, opt)
    this.contentState.render(!!selectHighlight)
    return this.contentState.searchMatches
  }

  replace(value, opt) {
    this.contentState.replace(value, opt)
    this.contentState.render(false)
    this.dispatchChange()
    return this.contentState.searchMatches
  }

  find(action/* pre or next */) {
    this.contentState.find(action)
    this.contentState.render(false)
    return this.contentState.searchMatches
  }

  on(event, listener) {
    this.eventCenter.subscribe(event, listener)
    return () => this.off(event, listener)
  }

  off(event, listener) {
    this.eventCenter.unsubscribe(event, listener)
  }

  once(event, listener) {
    this.eventCenter.subscribeOnce(event, listener)
  }

  invalidateImageCache() {
    this.contentState.stateRender.invalidateImageCache()
    this.contentState.render(true)
  }

  selectAll() {
    if (!this.hasFocus() && !this.contentState.selectedTableCells) {
      return
    }
    this.contentState.selectAll()
    this.dispatchSelectionChange()
    this.dispatchSelectionFormats()
  }

  delete() {
    this.contentState.cutHandler()
  }

  /**
   * Get all images' src from the given markdown.
   * @param {string} markdown you want to extract images from this markdown.
   */
  extractImages(markdown = this.markdown) {
    return this.contentState.extractImages(markdown)
  }

  setOptions(options, needRender = false) {
    // FIXME: Just to be sure, disabled due to #1648.
    if (options.codeBlockLineNumbers) {
      options.codeBlockLineNumbers = false
    }

    Object.assign(this.options, options)
    if (needRender) {
      this.contentState.render(false, true)
    }

    // Set quick insert hint visibility
    const hideQuickInsertHint = options.hideQuickInsertHint
    if (typeof hideQuickInsertHint !== 'undefined') {
      const hasClass = this.container.classList.contains('ag-show-quick-insert-hint')
      if (hideQuickInsertHint && hasClass) {
        this.container.classList.remove('ag-show-quick-insert-hint')
      } else if (!hideQuickInsertHint && !hasClass) {
        this.container.classList.add('ag-show-quick-insert-hint')
      }
    }

    // Set spellcheck container attribute
    const spellcheckEnabled = options.spellcheckEnabled
    if (typeof spellcheckEnabled !== 'undefined') {
      this.container.setAttribute('spellcheck', !!spellcheckEnabled)
    }

    if (options.bulletListMarker) {
      this.contentState.turndownConfig.bulletListMarker = options.bulletListMarker
    }
  }

  hideAllFloatTools() {
    return this.keyboard.hideAllFloatTools()
  }

  /**
   * Replace the word range with the given replacement.
   *
   * @param {*} line A line block reference of the line that contains the word to
   *                 replace - must be a valid reference!
   * @param {*} wordCursor The range of the word to replace (line: "abc >foo< abc"
   *                       whereas `>`/`<` is start and end of `wordCursor`). This
   *                       range is replaced by `replacement`.
   * @param {string} replacement The replacement.
   * @param {boolean} setCursor Shoud we update the editor cursor?
   */
  replaceWordInline(line, wordCursor, replacement, setCursor = false) {
    this.contentState.replaceWordInline(line, wordCursor, replacement, setCursor)
  }

  destroy() {
    // this.quickInsert.destroy()
    // this.tablePicker.destroy()
    this.contentState.clear()
    this.codePicker.destroy()
    this.emojiPicker.destroy()
    this.eventCenter.detachAllDomEvents()
    this.frontMenu.destroy()
    this.imageSelector.destroy()
    this.imageToolbar.destroy()
    this.tableBarTools.destroy()
    this.isDestroyed = true
  }
}

/**
  * [ensureContainerDiv ensure container element is div]
  */
function getContainer(originContainer, options) {
  const { hideQuickInsertHint, spellcheckEnabled } = options
  const container = document.createElement('div')
  const rootDom = document.createElement('div')
  const attrs = originContainer.attributes
  // copy attrs from origin container to new div element
  Array.from(attrs).forEach(attr => {
    container.setAttribute(attr.name, attr.value)
  })

  if (!hideQuickInsertHint) {
    container.classList.add('ag-show-quick-insert-hint')
  }

  container.setAttribute('contenteditable', true)
  container.setAttribute('autocorrect', false)
  container.setAttribute('autocomplete', 'off')
  // NOTE: The browser is not able to correct misspelled words words without
  // a custom implementation like in MarkText.
  container.setAttribute('spellcheck', !!spellcheckEnabled)
  container.appendChild(rootDom)
  originContainer.replaceWith(container)
  return container
}

export default Muya
