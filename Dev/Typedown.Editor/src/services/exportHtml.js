import marked from '../components/Muya/lib/parser/marked'
import Prism from 'prismjs'
import katex from 'katex'
// mhchem's \ce, added to the KaTeX imported above: the package's ES module build of the extension extends the
// ES module KaTeX (its UMD file in dist/ extends the CommonJS copy, which nothing here renders with).
import 'katex/contrib/mhchem'
import loadRenderer from '../components/Muya/lib/renderers'
import githubMarkdownCss from '!!raw-loader!github-markdown-css/github-markdown.css'
import exportStyle from '!!raw-loader!../assets/styles/exportStyle.css'
import highlightCss from '!!raw-loader!prismjs/themes/prism.css'
import katexCss from '!!raw-loader!katex/dist/katex.css'
import footerHeaderCss from '!!raw-loader!../assets/styles/headerFooterStyle.css'
import { EXPORT_DOMPURIFY_CONFIG } from '../components/Muya/lib/config'
import { sanitize, unescapeHTML } from '../components/Muya/lib/utils'
import { validEmoji } from '../components/Muya/lib/ui/emojis'
import { alias } from '../components/Muya/lib/prism/loadLanguage'

const DIAGRAM_TYPE = [
  'mermaid',
  'flowchart',
  'sequence',
  'plantuml',
  'vega-lite'
]

// The KaTeX stylesheet, taken raw, points its fonts at `fonts/…` next to a stylesheet the export does not
// have: an exported page, and a PDF made from one, fell back to whatever font the browser had — "the formula
// font is different in the PDF" (a Store review). This page has the same fonts bundled under hashed names;
// their absolute addresses are read off the page's own @font-face rules and put into the export.
const withAbsoluteKatexFonts = (css) => {
  const found = {}
  try {
    for (const sheet of document.styleSheets) {
      let rules
      try { rules = sheet.cssRules } catch (e) { continue }
      for (const rule of rules) {
        if (!(rule instanceof CSSFontFaceRule)) continue
        const family = (rule.style.getPropertyValue('font-family') || '').replace(/["']/g, '')
        if (!family.startsWith('KaTeX_')) continue
        const src = rule.style.getPropertyValue('src') || ''
        for (const m of src.matchAll(/url\(["']?([^"')]+)["']?\)/g)) {
          const url = new URL(m[1], sheet.href || document.baseURI).href
          // Bundled as KaTeX_Main-BoldItalic.<hash>.woff2: the base name is the one the raw stylesheet uses.
          const named = url.match(/(KaTeX_[A-Za-z0-9-]+)\.[0-9a-f]+\.(woff2|woff|ttf)(\?|$)/)
          if (named) found[`${named[1]}|${named[2]}`] = url
        }
      }
    }
  } catch (e) {
    console.log('could not read the page\'s KaTeX font faces', e)
  }
  return css.replace(/url\(fonts\/(KaTeX_[A-Za-z0-9-]+)\.(woff2|woff|ttf)\)/g, (whole, name, ext) => {
    const hit = found[`${name}|${ext}`]
    return hit ? `url(${hit})` : whole
  })
}

class ExportHtml {
  constructor(markdown, options) {
    this.markdown = markdown
    this.options = options
    this.exportContainer = null
    this.mathRendererCalled = false
  }

  async renderMermaid() {
    const codes = this.exportContainer.querySelectorAll('code.language-mermaid')
    // Mermaid is loaded only for a document that has a diagram: one that failed to load stopped every export.
    if (codes.length === 0) return
    // Mermaid that cannot be loaded leaves its diagrams as their source, and the rest of the export goes on.
    let mermaid
    try {
      mermaid = await loadRenderer('mermaid')
    } catch (err) {
      console.log('export: Mermaid could not be loaded; its diagrams stay code', err)
      return
    }
    for (const code of codes) {
      const preEle = code.parentNode
      const mermaidContainer = document.createElement('div')
      mermaidContainer.innerHTML = sanitize(unescapeHTML(code.innerHTML), EXPORT_DOMPURIFY_CONFIG, true)
      mermaidContainer.classList.add('mermaid')
      if (this.options?.diagramsAsPictures) mermaidContainer.dataset.diagramSource = 'Mermaid: ' + unescapeHTML(code.innerHTML)
      preEle.replaceWith(mermaidContainer)
    }
    // The live preview follows the app theme, but HTML export is always light:
    // render with the `default` theme. (Could be made configurable in the future.)
    // Labels as SVG text rather than HTML inside the SVG when the diagram is to become a picture: a canvas a
    // drawing with HTML in it was drawn on cannot be read back (the browser's rule), so it could not be saved.
    const htmlLabels = !this.options?.diagramsAsPictures
    mermaid.initialize({
      startOnLoad: false,
      securityLevel: 'strict',
      theme: 'default',
      htmlLabels,
      flowchart: { htmlLabels }
    })
    // One diagram at a time: a diagram whose text Mermaid cannot read threw (not an Error: an object), the whole render
    // stopped there - an export failed - and the container it was rendered in stayed at the bottom of the page. One that
    // fails is kept as its source, in a code block; Mermaid's own leftovers of a failed render go.
    for (const node of [...this.exportContainer.querySelectorAll('div.mermaid')]) {
      const source = node.textContent
      try {
        await mermaid.run({ nodes: [node] })
      } catch (err) {
        console.log('mermaid: a diagram could not be drawn for the export', err && (err.message || err.str || err))
        const pre = document.createElement('pre')
        const code = document.createElement('code')
        code.className = 'language-mermaid'
        code.textContent = source
        pre.appendChild(code)
        node.replaceWith(pre)
      }
    }
    for (const leftover of document.querySelectorAll('body > [id^="dmermaid"], body > svg[id^="mermaid"]')) leftover.remove()
  }

  async renderDiagram() {
    const DIAGRAM_NAMES = { sequence: 'Sequence diagram', plantuml: 'PlantUML', flowchart: 'Flowchart', 'vega-lite': 'Vega-Lite' }
    const selector = 'code.language-vega-lite, code.language-flowchart, code.language-sequence, code.language-plantuml'
    const codes = this.exportContainer.querySelectorAll(selector)
    // Each renderer loaded when a diagram of its kind is there, and one that cannot be loaded leaves only its diagrams
    // as code: loading all four first, one failing stopped every export, diagrams or not.
    const renderers = {}
    const RENDER_MAP = new Proxy({}, { get: (_, type) => renderers[type] })
    const ensure = async type => {
      if (!(type in renderers)) {
        try { renderers[type] = await loadRenderer(type) } catch (err) {
          console.log(`export: the ${type} renderer could not be loaded`, err)
          renderers[type] = null
        }
      }
      return renderers[type]
    }
    for (const code of codes) {
      const rawCode = unescapeHTML(code.innerHTML)
      const functionType = (() => {
        if (/sequence/.test(code.className)) {
          return 'sequence'
        } else if (/plantuml/.test(code.className)) {
          return 'plantuml'
        } else if (/flowchart/.test(code.className)) {
          return 'flowchart'
        } else {
          return 'vega-lite'
        }
      })()
      // Drawing PlantUML off (the default): the export keeps the block as code rather than an image fetched from
      // plantuml.com with the diagram's source - by the export, and by whoever opens the file.
      if (functionType === 'plantuml' && !this.options?.renderPlantUml) continue
      if (!(await ensure(functionType))) continue
      const render = RENDER_MAP[functionType]
      const preParent = code.parentNode
      const diagramContainer = document.createElement('div')
      diagramContainer.classList.add(functionType)
      // The picture made of it (diagramsAsPictures) says what it was: its kind and the start of its source.
      if (this.options?.diagramsAsPictures) diagramContainer.dataset.diagramSource = DIAGRAM_NAMES[functionType] + ': ' + rawCode
      preParent.replaceWith(diagramContainer)
      const options = {}
      if (functionType === 'sequence') {
        Object.assign(options, { theme: this.options.sequenceTheme })
      } else if (functionType === 'vega-lite') {
        Object.assign(options, {
          actions: false,
          tooltip: false,
          renderer: 'svg',
          theme: 'latimes' // only render light theme
        })
      }
      try {
        if (functionType === 'flowchart' || functionType === 'sequence') {
          const diagram = render.parse(rawCode)
          diagramContainer.innerHTML = ''
          diagram.drawSVG(diagramContainer, options)
        }
        if (functionType === 'plantuml') {
          const diagram = render.parse(rawCode)
          diagramContainer.innerHTML = ''
          diagram.insertImgElement(diagramContainer)
        }
        if (functionType === 'vega-lite') {
          await render(diagramContainer, JSON.parse(rawCode), options)
        }
      } catch (err) {
        diagramContainer.innerHTML = '< Invalid Diagram >'
      }
    }
  }

  mathRenderer = (math, displayMode) => {
    this.mathRendererCalled = true

    try {
      return katex.renderToString(math, {
        displayMode
      })
    } catch (err) {
      return displayMode
        ? `<pre class="multiple-math invalid">\n${math}</pre>\n`
        : `<span class="inline-math invalid" title="invalid math">${math}</span>`
    }
  }

  // render pure html by marked
  async renderHtml(toc) {
    this.mathRendererCalled = false
    let html = marked(this.markdown, {
      superSubScript: this.options?.superSubScript ?? false,
      footnote: this.options?.footnote ?? false,
      isGitlabCompatibilityEnabled: this.options?.isGitlabCompatibilityEnabled ?? false,
      baseUrl: this.options?.baseUrl,
      highlight(code, lang) {
        // Language may be undefined (GH#591)
        if (!lang) {
          return code
        }

        if (DIAGRAM_TYPE.includes(lang)) {
          return code
        }

        const grammar = Prism.languages[alias[lang] ?? lang]
        if (!grammar) {
          console.warn(`Unable to find grammar for "${lang}".`)
          return code
        }
        return Prism.highlight(code, grammar, lang)
      },
      emojiRenderer(emoji) {
        const validate = validEmoji(emoji)
        if (validate) {
          return validate.emoji
        } else {
          return `:${emoji}:`
        }
      },
      mathRenderer: this.mathRenderer,
      tocRenderer() {
        if (!toc) {
          return ''
        }
        return toc
      }
    })

    html = sanitize(html, EXPORT_DOMPURIFY_CONFIG, false)

    const exportContainer = this.exportContainer = document.createElement('div')
    exportContainer.classList.add('ag-render-container')
    exportContainer.innerHTML = html
    document.body.appendChild(exportContainer)

    // render only render the light theme of mermaid and diragram...
    // The container goes whatever happens: one left behind showed the rendered document under the editor, in every mode.
    let result
    try {
      await this.renderMermaid()
      await this.renderDiagram()
      result = exportContainer.innerHTML
    } finally {
      exportContainer.remove()
      this.exportContainer = null
    }

    // hack to add arrow marker to output html
    const pathes = document.querySelectorAll('path[id^=raphael-marker-]')
    const def = '<defs style="-webkit-tap-highlight-color: rgba(0, 0, 0, 0);">'
    result = result.replace(def, () => {
      let str = ''
      for (const path of pathes) {
        str += path.outerHTML
      }
      return `${def}${str}`
    })

    this.exportContainer = null
    return result
  }

  /**
   * Get HTML with style
   *
   * @param {*} options Document options
   */
  async generate(options) {
    const { printOptimization } = options

    // WORKAROUND: Hide Prism.js style when exporting or printing. Otherwise the background color is white in the dark theme.
    const highlightCssStyle = printOptimization ? `@media print { ${highlightCss} }` : highlightCss
    const html = this._prepareHtml(await this.renderHtml(options.toc), options)
    const katexCssStyle = this.mathRendererCalled ? withAbsoluteKatexFonts(katexCss) : ''
    this.mathRendererCalled = false

    // `extraCss` may changed in the mean time.
    const { title, extraCss, extraHead, extraBody } = options
    return `<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${sanitize(title, EXPORT_DOMPURIFY_CONFIG, true)}</title>
  <style>
  ${githubMarkdownCss}
  </style>
  <style>
  ${highlightCssStyle}
  </style>
  <style>
  ${katexCssStyle}
  </style>
  <style>
    .markdown-body {
      font-family: -apple-system,Segoe UI,Helvetica,Arial,sans-serif,Apple Color Emoji,Segoe UI Emoji;
      box-sizing: border-box;
      min-width: 200px;
      max-width: 980px;
      margin: 0 auto;
      padding: 45px;
    }

    @media not print {
      .markdown-body {
        padding: 45px;
      }

      @media (max-width: 767px) {
        .markdown-body {
          padding: 15px;
        }
      }
    }

    .hf-container {
      color: #24292e;
      line-height: 1.3;
    }

    .markdown-body .highlight pre,
    .markdown-body pre {
      white-space: pre-wrap;
    }
    /* Paper has no scrollbar. The GitHub sheet and Prism both put white-space: pre on the code inside
       the block and overflow: auto on the block, which on paper prints a scrollbar and cuts the line;
       both are more specific than the rule above, so they are named here and told to wrap. */
    @media print {
      .markdown-body pre,
      .markdown-body pre > code,
      .markdown-body pre[class*="language-"],
      .markdown-body pre[class*="language-"] > code {
        white-space: pre-wrap;
        word-break: break-all;
        overflow-wrap: anywhere;
        overflow: visible;
      }
    }
    .markdown-body table {
      display: table;
    }
    .markdown-body img[data-align="center"] {
      display: block;
      margin: 0 auto;
    }
    .markdown-body img[data-align="right"] {
      display: block;
      margin: 0 0 0 auto;
    }
    .markdown-body li.task-list-item {
      list-style-type: none;
    }
    .markdown-body li > [type=checkbox] {
      margin: 0 0 0 -1.3em;
    }
    .markdown-body input[type="checkbox"] ~ p {
      margin-top: 0;
      display: inline-block;
    }
    .markdown-body ol ol,
    .markdown-body ul ol {
      list-style-type: decimal;
    }
    .markdown-body ol ol ol,
    .markdown-body ol ul ol,
    .markdown-body ul ol ol,
    .markdown-body ul ul ol {
      list-style-type: decimal;
    }
  </style>
  <style>${exportStyle}</style>
  ${extraCss ? `<style>${extraCss}</style>` : ''}
  ${this.options?.customCss ? `<style>${this.options.customCss}</style>` : ''}
  ${extraHead ?? ''}
</head>
<body>
  ${html}
  ${extraBody ?? ''}
</body>
</html>`
  }

  /**
   * @private
   *
   * @param {string} html The converted HTML text.
   * @param {*} options The export options.
   */
  _prepareHtml(html, options) {
    const { header, footer } = options
    const appendHeaderFooter = !!header || !!footer
    if (!appendHeaderFooter) {
      return createMarkdownArticle(html)
    }

    if (!options.extraCss) {
      options.extraCss = footerHeaderCss
    } else {
      options.extraCss = footerHeaderCss + options.extraCss
    }

    let output = HF_TABLE_START
    if (header) {
      output += createTableHeader(options)
    }

    if (footer) {
      output += HF_TABLE_FOOTER
      output = createRealFooter(options) + output
    }

    output = output + createTableBody(html) + HF_TABLE_END
    return sanitize(output, EXPORT_DOMPURIFY_CONFIG, false)
  }
}

// Variables and function to generate the header and footer.
const HF_TABLE_START = '<table class="page-container">'
const createTableBody = html => {
  return `<tbody><tr><td>
  <div class="main-container">
    ${createMarkdownArticle(html)}
  </div>
</td></tr></tbody>`
}
const HF_TABLE_END = '</table>'

/// The header at is shown at the top.
const createTableHeader = options => {
  const { header, headerFooterStyled } = options
  const { type, left, center, right } = header
  let headerClass = type === 1 ? 'single' : ''
  headerClass += getHeaderFooterStyledClass(headerFooterStyled)
  return `<thead class="page-header ${headerClass}"><tr><th>
  <div class="hf-container">
    <div class="header-content-left">${left}</div>
    <div class="header-content">${center}</div>
    <div class="header-content-right">${right}</div>
  </div>
</th></tr></thead>`
}

/// Fake footer to reserve space.
const HF_TABLE_FOOTER = `<tfoot class="page-footer-fake"><tr><td>
  <div class="hf-container">
    &nbsp;
  </div>
</td></tr></tfoot>`

/// The real footer at is shown at the bottom.
const createRealFooter = options => {
  const { footer, headerFooterStyled } = options
  const { type, left, center, right } = footer
  let footerClass = type === 1 ? 'single' : ''
  footerClass += getHeaderFooterStyledClass(headerFooterStyled)
  return `<div class="page-footer ${footerClass}">
  <div class="hf-container">
    <div class="footer-content-left">${left}</div>
    <div class="footer-content">${center}</div>
    <div class="footer-content-right">${right}</div>
  </div>
</div>`
}

/// Generate the mardown article HTML.
const createMarkdownArticle = html => {
  return `<article class="markdown-body">${html}</article>`
}

/// Return the class whether a header/footer should be styled.
const getHeaderFooterStyledClass = value => {
  if (value === undefined) {
    // Prefer theme settings.
    return ''
  }
  return !value ? ' simple' : ' styled'
}

export default ExportHtml
