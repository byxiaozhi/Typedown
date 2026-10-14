import { CLASS_OR_ID, BLOCK_TYPE6 } from '../../../config'
import { snakeToCamel } from '../../../utils'
import sanitize, { isValidAttribute } from '../../../utils/dompurify'

export default function htmlTag (h, cursor, block, token, outerClass) {
  const { tag, openTag, closeTag, children, attrs } = token
  // Even an empty inline element (an anchor target like <a id="x"></a>) hides its source in reading mode,
  // like every other marker. It used to be forced always-visible when it had no children, so the raw HTML
  // of an anchor showed in the reader (a Store review).
  const className = this.getClassName(outerClass, block, token, cursor)
  const tagClassName = className === CLASS_OR_ID.AG_HIDE ? className : CLASS_OR_ID.AG_HTML_TAG
  const { start, end } = token.range
  const openContent = this.highlight(h, block, start, start + openTag.length, token)
  const closeContent = closeTag
    ? this.highlight(h, block, end - closeTag.length, end, token)
    : ''

  const anchor = Array.isArray(children) && tag !== 'ruby' // important
    ? children.reduce((acc, to) => {
      const chunk = this[snakeToCamel(to.type)](h, cursor, block, to, className)
      return Array.isArray(chunk) ? [...acc, ...chunk] : [...acc, chunk]
    }, [])
    : ''

  switch (tag.toLowerCase()) {
    // Handle html img.
    case 'img': {
      return this.image(h, cursor, block, token, outerClass)
    }
    case 'br': {
      // The <br> renders as an actual line break either way. Its source text goes in a marker span that is
      // shown, greyed, only while the caret is in it, and hidden otherwise — so a reader sees the break,
      // not the literal "<br>". Same rule as every other inline marker (getClassName).
      const brMarker = this.getClassName(outerClass, block, token, cursor)
      return [h(`span.${CLASS_OR_ID.AG_HTML_TAG}.${brMarker}`, openContent), h(tag)]
    }
    default:
      // handle void html tag
      if (!closeTag) {
        return [h(`span.${CLASS_OR_ID.AG_HTML_TAG}`, openContent)]
      } else if (tag === 'ruby') {
        return this.htmlRuby(h, cursor, block, token, outerClass)
      } else {
        // if  tag is a block level element, use a inline element `span` to instead.
        // Because we can not nest a block level element in span element(line is span element)
        // we also recommand user not use block level element in paragraph. use block element in html block.
        // Use code !sanitize(`<${tag}>`) to filter some malicious tags. for example: <embed>.
        let selector = BLOCK_TYPE6.includes(tag) || !sanitize(`<${tag}>`) ? 'span' : tag
        selector += `.${CLASS_OR_ID.AG_INLINE_RULE}.${CLASS_OR_ID.AG_RAW_HTML}`
        const data = {
          attrs: {},
          dataset: {
            start,
            end,
            raw: token.raw
          }
        }

        // Disable spell checking for these tags
        if (tag === 'code' || tag === 'kbd') {
          Object.assign(data.attrs, { spellcheck: 'false' })
        }

        if (attrs.id) {
          selector += `#${attrs.id}`
        }
        if (attrs.class && /\S/.test(attrs.class)) {
          const classNames = attrs.class.split(/\s+/)
          for (const className of classNames) {
            selector += `.${className}`
          }
        }

        for (const attr of Object.keys(attrs)) {
          if (attr !== 'id' && attr !== 'class') {
            const attrData = attrs[attr]
            if (isValidAttribute(tag, attr, attrData)) {
              data.attrs[attr] = attrData
            }
          }
        }

        return [
          h(`span.${tagClassName}.${CLASS_OR_ID.AG_OUTPUT_REMOVE}`, {
            attrs: {
              spellcheck: 'false'
            }
          }, openContent),
          h(`${selector}`, data, anchor),
          h(`span.${tagClassName}.${CLASS_OR_ID.AG_OUTPUT_REMOVE}`, {
            attrs: {
              spellcheck: 'false'
            }
          }, closeContent)
        ]
      }
  }
}
