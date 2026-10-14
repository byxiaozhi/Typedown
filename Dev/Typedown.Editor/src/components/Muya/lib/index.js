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

// A top-level block's content without its identity (keys and links to its neighbours, and the parent a blank-line
// count was read in), for comparing two parses.
const blockSignature = block => JSON.stringify(block, (name, value) =>
  name === 'key' || name === 'parent' || name === 'preSibling' || name === 'nextSibling' || name === 'blankLinesParent' ? undefined : value)

// How many top-level blocks at the start (head) and at the end (tail) two block lists share.
function unchangedEnds(oldBlocks, newBlocks) {
  const oldSignatures = oldBlocks.map(blockSignature)
  const newSignatures = newBlocks.map(blockSignature)
  let head = 0
  while (head < oldBlocks.length && head < newBlocks.length && oldSignatures[head] === newSignatures[head]) head++
  let tail = 0
  while (tail < oldBlocks.length - head && tail < newBlocks.length - head &&
    oldSignatures[oldBlocks.length - 1 - tail] === newSignatures[newBlocks.length - 1 - tail]) tail++
  return { head, tail }
}

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
    this.dispatchChangeContentChangeThrottled()
    setTimeout(() => {
      try {
        this.dispatchSelectionChange()
        this.dispatchSelectionFormats()
      } catch (err) {
        console.log(err)
      }
    });
  }

  // Turning the whole document back into Markdown, working out where the caret lands in it and rebuilding the
  // outline costs tens of milliseconds on a large document, and none of it is needed between two keystrokes.
  // While typing it runs at most every 250 ms; whoever needs the text right now (a save, an export, leaving
  // the editor) calls flushContentChange first.
  dispatchChangeContentChangeThrottled = () => {
    const THROTTLE_ABOVE = 50000
    const INTERVAL = 250
    const length = this.markdown ? this.markdown.length : 0
    if (length < THROTTLE_ABOVE) {
      this.dispatchChangeContentChange()
      return
    }
    const now = Date.now()
    const since = now - (this.lastContentChangeAt || 0)
    clearTimeout(this.contentChangeTimer)
    if (since >= INTERVAL) {
      this.lastContentChangeAt = now
      this.dispatchChangeContentChange()
      return
    }
    this.contentChangeTimer = setTimeout(() => {
      this.lastContentChangeAt = Date.now()
      this.dispatchChangeContentChange()
    }, INTERVAL - since)
  }

  /// Runs the pending report now, so the text the host holds is the text on screen.
  flushContentChange = () => {
    clearTimeout(this.contentChangeTimer)
    this.lastContentChangeAt = Date.now()
    this.dispatchChangeContentChange()
  }

  dispatchChangeContentChange = () => {
    try {
      const { markdown, cursor } = this.getMarkdownAndCursor()
      const wordCount = this.getWordCountThrottled(markdown)
      this.markdown = markdown
      const toc = this.getTOC()
      this.eventCenter.dispatch('contentChange', { markdown, wordCount, cursor, toc })
    } catch (err) {
      console.log(err)
    }
  }

  // Counting words is O(document) with several regex passes; on large documents it is a noticeable share of
  // every keystroke. Recompute at most every 300 ms while typing and dispatch one final update when idle.
  getWordCountThrottled (markdown) {
    const THROTTLE_ABOVE = 50000
    const INTERVAL = 300
    if (markdown.length < THROTTLE_ABOVE) {
      return this.getWordCount(markdown)
    }
    const now = Date.now()
    clearTimeout(this.wordCountTimer)
    if (!this.wordCountCache || now - this.wordCountCache.time >= INTERVAL) {
      this.wordCountCache = { time: now, value: this.getWordCount(markdown) }
      return this.wordCountCache.value
    }
    this.wordCountTimer = setTimeout(() => {
      this.wordCountCache = null
      const { anchor, focus } = this.contentState.cursor || {}
      if (anchor && focus) this.dispatchChangeContentChange()
    }, INTERVAL)
    return this.wordCountCache.value
  }

  // The report a load makes of itself. It is the same report as after an edit, except that the selection
  // change carries a mark saying so: the caret has not moved, a document has arrived, and whoever keeps
  // the caret in view must not treat it as a caret movement — see the selectionChange handler.
  dispatchLoadChange = () => {
    this.dispatchChangeContentChangeThrottled()
    setTimeout(() => {
      try {
        this.dispatchSelectionChange(true)
        this.dispatchSelectionFormats()
      } catch (err) {
        console.log(err)
      }
    })
  }

  dispatchSelectionChange = (fromLoad = false) => {
    const selection = this.contentState.selectionChange()
    if (fromLoad) selection.fromLoad = true
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
    const { tableAlignColumns } = this.options
    return new ExportMarkdown(blocks, listIndentation, isGitlabCompatibilityEnabled, { alignTableColumns: tableAlignColumns !== false }).generate();
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
      try {
        const cursorInfo = this.contentState.addCursorToMarkdown(markdown, cursor)
        newMarkdown = cursorInfo.markdown
        isValid = cursorInfo.isValid
      } catch (err) {
        console.log(err)
      }
    }
    this.contentState.importMarkdown(newMarkdown)
    this.contentState.importCursor(cursor && isValid)
    // A whole new document: the one on screen has nothing in common with it, so throw it away rather than
    // diff against it (see StateRender.render).
    this.contentState.render(isRenderCursor, false, true)
    setTimeout(() => {
      this.dispatchLoadChange()
    }, 0)
  }

  /** The keys of the top-level blocks shown now that differ from oldBlocks (what an automation edit changed). */
  changedBlockKeys(oldBlocks) {
    const blocks = this.contentState.blocks
    const { head, tail } = unchangedEnds(oldBlocks, blocks)
    return blocks.slice(head, blocks.length - tail).map(block => block.key)
  }

  /**
   * Where an automation edit happened, for reveal: "change": the key of the first changed top-level block, or after a
   * pure deletion the block now in its place (the last block when the deletion was at the end); null when empty.
   */
  changeAnchorKey(oldBlocks) {
    const blocks = this.contentState.blocks
    if (!blocks.length) return null
    const { head } = unchangedEnds(oldBlocks, blocks)
    return blocks[Math.min(head, blocks.length - 1)].key
  }

  /**
   * An automation edit of the document shown (docs/automation-api-spec.md, section 2.2). The new text is parsed as a
   * whole, but the top-level blocks that did not change keep their objects and their DOM: the reader's cursor, the
   * scroll position and whatever is drawn there stay, and only the changed blocks are rendered. Returns false and
   * changes nothing when that cannot be shown to give the state setMarkdown would: the cursor is in a changed
   * block, reference definitions changed (they change how other blocks render), or the assembled blocks do not
   * export exactly what the whole new document exports. The caller then uses setMarkdown.
   */
  replaceMarkdownLocally(markdown) {
    const { contentState } = this
    const { stateRender } = contentState
    const oldBlocks = contentState.blocks
    const newBlocks = contentState.markdownToState(markdown)
    const { head, tail } = unchangedEnds(oldBlocks, newBlocks)
    if (head === 0 && tail === 0) return false

    const removed = oldBlocks.slice(head, oldBlocks.length - tail)
    const inserted = newBlocks.slice(head, newBlocks.length - tail)
    const kept = block => !removed.includes(block)
    const { start, end } = contentState.cursor || {}
    const outMost = key => {
      const block = key && contentState.getBlock(key)
      return block ? contentState.findOutMostBlock(block) : null
    }
    const startBlock = outMost(start && start.key)
    const endBlock = outMost(end && end.key)
    if (!startBlock || !endBlock || !kept(startBlock) || !kept(endBlock)) return false

    const labelsOf = blocks => {
      stateRender.collectLabels(blocks)
      return JSON.stringify([...stateRender.labels.entries()])
    }
    const oldLabels = labelsOf(oldBlocks)
    const newLabels = labelsOf(newBlocks)
    if (oldLabels !== newLabels) {
      stateRender.collectLabels(oldBlocks)
      return false
    }

    const assembled = [...oldBlocks.slice(0, head), ...inserted, ...oldBlocks.slice(oldBlocks.length - tail)]
    const link = blocks => blocks.forEach((block, i) => {
      block.preSibling = i > 0 ? blocks[i - 1].key : null
      block.nextSibling = i < blocks.length - 1 ? blocks[i + 1].key : null
    })
    const exportOf = blocks => {
      const { isGitlabCompatibilityEnabled, listIndentation } = contentState
      return new ExportMarkdown(blocks, listIndentation, isGitlabCompatibilityEnabled, { alignTableColumns: this.options.tableAlignColumns !== false }).generate()
    }
    link(assembled)
    if (exportOf(assembled) !== exportOf(newBlocks)) {
      link(oldBlocks)
      stateRender.collectLabels(oldBlocks)
      return false
    }

    contentState.blocks = assembled
    contentState.blockCache = null
    stateRender.collectLabels(assembled)
    const { matches } = contentState.searchMatches
    stateRender.replaceBlocks(removed.map(b => b.key), inserted, tail > 0 ? assembled[assembled.length - tail].key : null,
      contentState.getActiveBlocks(), matches)
    contentState.postRender()
    setTimeout(() => {
      this.dispatchLoadChange()
    }, 0)
    return true
  }

  setCursor(cursor) {
    try {
      const markdown = this.getMarkdown()
      const isRenderCursor = true
      this.setMarkdown(markdown, cursor, isRenderCursor)
      return true
    } catch (err) {
      console.log(err)
      return false
    }
  }

  createTable(tableChecker) {
    return this.contentState.createTable(tableChecker)
  }

  getSelection() {
    return this.contentState.selectionChange()
  }

  setFocusMode(bool) {
    const { container } = this
    if (bool) {
      if (!container.classList.contains(CLASS_OR_ID.AG_FOCUS_MODE))
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
    this.renderStyle()
  }

  setTextDirection(textDirection) {
    this.options.textDirection = textDirection === 'ltr' || textDirection === 'rtl' ? textDirection : 'auto'
    this.renderStyle()
  }

  /**
   * The redraw after a style setting (font, line height, direction). render(false) blurs the editor, which is right
   * when nobody is typing into it and wrong when the reader is: the caret stayed on screen and the keys went nowhere
   * until a click (a setting changed from another window or through the automation API). A focused editor is redrawn
   * with its cursor put back instead.
   */
  renderStyle() {
    this.contentState.render(document.activeElement === this.container)
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

  insertImages(images, afterKey) {
    this.contentState.insertImages(images, afterKey)
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
    clearTimeout(this.wordCountTimer)
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
