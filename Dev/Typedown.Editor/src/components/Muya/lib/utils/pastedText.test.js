import { dedentPastedText } from './pastedText'
import Lexer from '../parser/marked/lexer'

const types = markdown => new Lexer({ gfm: true, disableInline: true }).lex(markdown).map(token => token.type)

describe('plain text pasted with every line indented is text, not an indented code block', () => {
  test.each([
    ['tab-indented paragraphs', '\t第一段文字。\n\n\t第二段文字。', '第一段文字。\n\n第二段文字。'],
    ['four-space indented lines', '    第一行\n    第二行', '第一行\n第二行'],
    ['English prose', '    The first paragraph.\n\n    The second one.\n', 'The first paragraph.\n\nThe second one.\n'],
    ['relative indentation is kept', '    - a\n        - b\n    - c', '- a\n    - b\n- c'],
    ['tabs and spaces measured in columns', '\tone\n    two\n\t  three', 'one\ntwo\n  three'],
    ['blank lines of spaces only', '    one\n  \n    two', 'one\n\ntwo'],
    ['CRLF line ends', '    one\r\n    two\r\n', 'one\r\ntwo\r\n']
  ])('%s', (_name, pasted, expected) => {
    expect(dedentPastedText(pasted)).toBe(expected)
    expect(types(dedentPastedText(pasted))).not.toContain('code')
  })

  test.each([
    ['a line without indentation', 'intro\n\n    code()\n'],
    ['no indentation at all', 'one\ntwo'],
    ['an empty paste', ''],
    ['only blank lines', '\n  \n'],
    ['a fenced block (its body indented, its fence not)', '```js\n    x()\n```']
  ])('%s is left as it is', (_name, pasted) => {
    expect(dedentPastedText(pasted)).toBe(pasted)
  })
})
