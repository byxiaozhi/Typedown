import { CLASS_OR_ID } from '../../../config'
import { snakeToCamel } from '../../../utils'

// CriticMarkup, drawn as a review: an addition in green, a deletion in red and struck through, a comment in
// yellow, a highlight in yellow. The marks (`{++`, `++}`...) are hidden like the marks of **bold**, and show in
// gray while the caret is inside, so the text can still be edited as it is in the file.
export default function critic (h, cursor, block, token, outerClass) {
  const className = this.getClassName(outerClass, block, token, cursor)
  const MARKER = `span.${className}.${CLASS_OR_ID.AG_REMOVE}`
  const { kind, open, close } = token
  const { start, end } = token.range
  const mark = (from, to) => h(MARKER, this.highlight(h, block, from, to, token))
  const render = children => children.reduce((acc, to) => {
    const chunk = this[snakeToCamel(to.type)](h, cursor, block, to, className)
    return Array.isArray(chunk) ? [...acc, ...chunk] : [...acc, chunk]
  }, [])
  const part = (selector, content) => h(`span.ag-critic.${selector}.${CLASS_OR_ID.AG_INLINE_RULE}`, content)

  if (kind === 'comment') {
    // `@Name 2026-10-07` alone (who and when, written after a change) is drawn small and quiet; a note is a yellow comment
    const stamp = /^@[^:\r\n]+ \d{4}-\d{2}-\d{2}$/.test(token.content)
    return [
      mark(start, start + open.length),
      part(stamp ? 'ag-critic-comment.ag-critic-stamp' : 'ag-critic-comment', this.highlight(h, block, start + open.length, end - close.length, token)),
      mark(end - close.length, end)
    ]
  }

  if (kind === 'sub') {
    const oldEnd = start + open.length + token.oldChildren.reduce((n, to) => n + to.raw.length, 0)
    return [
      mark(start, start + open.length),
      part('ag-critic-del', render(token.oldChildren)),
      mark(oldEnd, oldEnd + token.middle.length),
      part('ag-critic-add', render(token.newChildren)),
      mark(end - close.length, end)
    ]
  }

  const style = { add: 'ag-critic-add', del: 'ag-critic-del', mark: 'ag-critic-mark' }[kind]
  return [
    mark(start, start + open.length),
    part(style, render(token.children)),
    mark(end - close.length, end)
  ]
}
