import { edit, noop } from './utils'

/* eslint-disable no-useless-escape */

/**
 * Block-Level Rules
 */

// Front matter at the top of a document: YAML between --- lines, TOML between +++, JSON between ;;; or { }. A closing
// fence is a line of its own (trailing spaces allowed) and may be followed directly by the document's first block, as
// Hugo, Jekyll and most templates write it; requiring a blank line after it made such a header a rule and a setext
// heading of its last line ("---" under "title: x"). The { } form still needs the blank line: a lone "}" line is too
// ordinary to end a header on.
export const FRONT_MATTER = /^(?:(?:---[ \t]*\n([\s\S]*?\n)---[ \t]*(?=\n|$))|(?:\+\+\+[ \t]*\n([\s\S]*?\n)\+\+\+[ \t]*(?=\n|$))|(?:;;;[ \t]*\n([\s\S]*?\n);;;[ \t]*(?=\n|$))|(?:\{\n([\s\S]+?)\}(?=\n{2,}|\n{1,2}$)))/

export const block = {
  newline: /^\n+/,
  code: /^( {4}[^\n]+\n*)*( {4}[^\n]+(?=\n+|$)*)/,
  fences: /^ {0,3}(`{3,}(?=[^`\n]*\n)|~{3,})([^\n]*)\n(?:|([\s\S]*?)\n)(?: {0,3}\1[~`]* *(?=\n+|$)|$)/,
  hr: /^ {0,3}((?:- *){3,}|(?:_ *){3,}|(?:\* *){3,})(?=\n+|$)/,
  heading: /^ {0,3}(#{1,6})(?=\s|$)(.*)(?=\n+|$)/,
  blockquote: /^( {0,3}> ?(paragraph|[^\n]*)(?:\n|$))*( {0,3}> ?(paragraph|[^\n]*)(?=\n|$))/,
  // The lookahead has to refuse a newline as well as a space: with three or more newlines the engine would
  // otherwise backtrack to two of them, see a newline where a space was forbidden, and end the list in the
  // middle of a fenced code block inside a list item — leaving its closing fence behind to open a new one.
  list: /^( {0,3})(bull) [\s\S]+?(?:hr|def|\n{2,}(?![ \n])(?!\1bull )\n*|\s*$)/,
  html: '^ {0,3}(?:' + // optional indentation
    '<(script|pre|style)[\\s>][\\s\\S]*?(?:</\\1>[^\\n]*\\n+|$)' + // (1)
    '|comment[^\\n]*(\\n+|$)' + // (2)
    '|<\\?[\\s\\S]*?(?:\\?>\\n*|$)' + // (3)
    '|<![A-Z][\\s\\S]*?(?:>\\n*|$)' + // (4)
    '|<!\\[CDATA\\[[\\s\\S]*?(?:\\]\\]>\\n*|$)' + // (5)
    '|</?(tag)(?: +|\\n|/?>)[\\s\\S]*?(?:\\n{2,}|$)' + // (6)
    '|<(?!script|pre|style)([a-z][\\w-]*)(?:attribute)*? */?>(?=\\h*\\n)[\\s\\S]*?(?:\\n{2,}|$)' + // (7) open tag
    '|</(?!script|pre|style)[a-z][\\w-]*\\s*>(?=\\h*\\n)[\\s\\S]*?(?:\\n{2,}|$)' + // (7) closing tag
    ')',
  def: /^ {0,3}\[(label)\]: *\n? *<?([^\s>]+)>?(?:(?: +\n? *| *\n *)(title))? *(?=\n+|$)/,
  nptable: noop,
  table: noop,
  lheading: /^([^\n]+)\n {0,3}(=+|-+) *(?=\n+|$)/,
  // regex template, placeholders will be replaced according to different paragraph
  // interruption rules of commonmark and the original markdown spec:
  _paragraph: /^([^\n]+(?:\n(?!hr|heading|lheading|blockquote|fences|list|html|table)[^\n]+)*)/,
  text: /^[^\n]+/,

  // extra
  frontmatter: FRONT_MATTER,
  multiplemath: /^\$\$\n([\s\S]+?)\n\$\$(?=\n+|$)/,
  // A whole line of the form `$$ ... $$` is display math too (Typora/Obsidian/KaTeX behaviour, upstream #26).
  multiplemathSingleLine: /^\$\$([^\n$]+?)\$\$ *(?=\n+|$)/,
  multiplemathGitlab: /^ {0,3}(`{3,})math\n(?:(|[\s\S]*?)\n)(?: {0,3}\1`* *(?=\n+|$)|$)/, // Math inside a code block (GitLab display math)
  // A definition ends at a blank line, at the next `[^id]:` definition (they are usually written back to
  // back without a blank line, which used to swallow every following definition into the first one), or at EOF.
  footnote: /^\[\^([^\^\[\]\s]+?)(?<!\\)\]:[\s\S]+?(?=\n *\n {0,3}[^ ]+|\n {0,3}\[\^[^\^\[\]\s]+?(?<!\\)\]:|$)/
}

block._label = /(?!\s*\])(?:\\[\[\]]|[^\[\]])+/
block._title = /(?:"(?:\\"?|[^"\\])*"|'[^'\n]*(?:\n[^'\n]+)*\n?'|\([^()]*\))/
block.def = edit(block.def)
  .replace('label', block._label)
  .replace('title', block._title)
  .getRegex()

block.checkbox = /^\[([ xX])\] +/
block.bullet = /(?:[*+-]|\d{1,9}(?:\.|\)))/ // patched: support "(" as ordered list delimiter too
// patched: fix https://github.com/marktext/marktext/issues/831#issuecomment-477719256
// block.item = /^( *)(bull) ?[^\n]*(?:\n(?!\1bull ?)[^\n]*)*/
block.item = /^(( {0,3})(bull) [^\n]*(?:\n(?!(\2bull |\2bull\n))[^\n]*)*|( {0,3})(bull)(?:\n(?!(\2bull |\2bull\n)))*)/ // eslint-disable-line no-useless-backreference
block.item = edit(block.item, 'gm')
  .replace(/bull/g, block.bullet)
  .getRegex()

block.list = edit(block.list)
  .replace(/bull/g, block.bullet)
  .replace('hr', '\\n+(?=\\1?(?:(?:- *){3,}|(?:_ *){3,}|(?:\\* *){3,})(?:\\n+|$))')
  .replace('def', '\\n+(?=' + block.def.source + ')')
  .getRegex()

block._tag = 'address|article|aside|base|basefont|blockquote|body|caption' +
  '|center|col|colgroup|dd|details|dialog|dir|div|dl|dt|fieldset|figcaption' +
  '|figure|footer|form|frame|frameset|h[1-6]|head|header|hr|html|iframe' +
  '|legend|li|link|main|menu|menuitem|meta|nav|noframes|ol|optgroup|option' +
  '|p|param|section|source|summary|table|tbody|td|tfoot|th|thead|title|tr' +
  '|track|ul'
block._comment = /<!--(?!-?>)[\s\S]*?(?:-->|$)/
block.html = edit(block.html, 'i')
  .replace('comment', block._comment)
  .replace('tag', block._tag)
  .replace('attribute', / +[a-zA-Z:_][\w.:-]*(?: *= *"[^"\n]*"| *= *'[^'\n]*'| *= *[^\s"'=<>`]+)?/)
  .getRegex()

block.paragraph = edit(block._paragraph)
  .replace('hr', block.hr)
  .replace('heading', ' {0,3}#{1,6} ')
  .replace('|lheading', '') // setex headings don't interrupt commonmark paragraphs
  .replace('|table', '') // tables are a GFM extension
  .replace('blockquote', ' {0,3}>')
  .replace('fences', ' {0,3}(?:`{3,}(?=[^`\\n]*\\n)|~{3,})[^\\n]*\\n')
  .replace('list', ' {0,3}(?:[*+-]|1[.)]) ') // only lists starting from 1 can interrupt
  .replace('html', '</?(?:tag)(?: +|\\n|/?>)|<(?:script|pre|style|!--)')
  .replace('tag', block._tag) // pars can be interrupted by type (6) html blocks
  .getRegex()

block.blockquote = edit(block.blockquote)
  .replace('paragraph', block.paragraph)
  .getRegex()

/**
 * Normal Block Grammar
 */

export const normal = Object.assign({}, block)

/**
 * GFM Block Grammar
 */

export const gfm = Object.assign({}, normal, {
  nptable: '^ *([^|\\n ].*\\|.*)\\n' + // Header
    ' {0,3}([-:]+ *\\|[-| :]*)' + // Align
    '(?:\\n((?:(?!\\n|hr|heading|blockquote|code|fences|list|html).*(?:\\n|$))*)\\n*|$)', // Cells
  table: '^ *\\|(.+)\\n' + // Header
    ' {0,3}\\|?( *[-:]+[-| :]*)' + // Align
    '(?:\\n *((?:(?!\\n|hr|heading|blockquote|code|fences|list|html).*(?:\\n|$))*)\\n*|$)' // Cells
})

gfm.nptable = edit(gfm.nptable)
  .replace('hr', block.hr)
  .replace('heading', ' {0,3}#{1,6} ')
  .replace('blockquote', ' {0,3}>')
  .replace('code', ' {4}[^\\n]')
  .replace('fences', ' {0,3}(?:`{3,}(?=[^`\\n]*\\n)|~{3,})[^\\n]*\\n')
  .replace('list', ' {0,3}(?:[*+-]|1[.)]) ') // only lists starting from 1 can interrupt
  .replace('html', '</?(?:tag)(?: +|\\n|/?>)|<(?:script|pre|style|!--)')
  .replace('tag', block._tag) // tables can be interrupted by type (6) html blocks
  .getRegex()

gfm.table = edit(gfm.table)
  .replace('hr', block.hr)
  .replace('heading', ' {0,3}#{1,6} ')
  .replace('blockquote', ' {0,3}>')
  .replace('code', ' {4}[^\\n]')
  .replace('fences', ' {0,3}(?:`{3,}(?=[^`\\n]*\\n)|~{3,})[^\\n]*\\n')
  .replace('list', ' {0,3}(?:[*+-]|1[.)]) ') // only lists starting from 1 can interrupt
  .replace('html', '</?(?:tag)(?: +|\\n|/?>)|<(?:script|pre|style|!--)')
  .replace('tag', block._tag) // tables can be interrupted by type (6) html blocks
  .getRegex()

// GFM tables can interrupt a paragraph when the following two lines form a
// table header and delimiter row. Keep this out of the normal and pedantic
// grammars, and use the complete table rules so an isolated pipe is not enough.
const gfmTableInterrupt = new RegExp('(?:' + gfm.nptable.source + '|' + gfm.table.source + ')')
gfm.paragraph = edit(block._paragraph)
  .replace('hr', block.hr)
  .replace('heading', ' {0,3}#{1,6} ')
  .replace('|lheading', '')
  .replace('table', gfmTableInterrupt)
  .replace('blockquote', ' {0,3}>')
  .replace('fences', ' {0,3}(?:`{3,}(?=[^`\\n]*\\n)|~{3,})[^\\n]*\\n')
  .replace('list', ' {0,3}(?:[*+-]|1[.)]) ')
  .replace('html', '</?(?:tag)(?: +|\\n|/?>)|<(?:script|pre|style|!--)')
  .replace('tag', block._tag)
  .getRegex()

/**
 * Pedantic grammar (original John Gruber's loose markdown specification)
 */

export const pedantic = Object.assign({}, normal, {
  html: edit(
    '^ *(?:comment *(?:\\n|\\s*$)' +
    '|<(tag)[\\s\\S]+?</\\1> *(?:\\n{2,}|\\s*$)' + // closed tag
    '|<tag(?:"[^"]*"|\'[^\']*\'|\\s[^\'"/>\\s]*)*?/?> *(?:\\n{2,}|\\s*$))')
    .replace('comment', block._comment)
    .replace(/tag/g, '(?!(?:' +
      'a|em|strong|small|s|cite|q|dfn|abbr|data|time|code|var|samp|kbd|sub' +
      '|sup|i|b|u|mark|ruby|rt|rp|bdi|bdo|span|br|wbr|ins|del|img)' +
      '\\b)\\w+(?!:|[^\\w\\s@]*@)\\b')
    .getRegex(),
  def: /^ *\[([^\]]+)\]: *<?([^\s>]+)>?(?: +(["(][^\n]+[")]))? *(?=\n+|$)/,
  heading: /^(#{1,6})(.*)(?=\n+|$)/,
  fences: noop, // fences not supported
  paragraph: edit(normal._paragraph)
    .replace('hr', block.hr)
    .replace('heading', ' *#{1,6} *[^\n]')
    .replace('lheading', block.lheading)
    .replace('|table', '')
    .replace('blockquote', ' {0,3}>')
    .replace('|fences', '')
    .replace('|list', '')
    .replace('|html', '')
    .getRegex()
})

/* eslint-ensable no-useless-escape */
