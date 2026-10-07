// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { tokenizer, generator } from './index'

const critics = tokens => tokens.filter(t => t.type === 'critic')
const tokenize = src => tokenizer(src, { hasBeginRules: false })

describe('CriticMarkup', () => {
  test.each([
    ['add', 'a {++new++} b'],
    ['del', 'a {--old--} b'],
    ['sub', 'a {~~old~>new~~} b'],
    ['mark', 'a {==this==} b'],
    ['comment', 'a {>>why?<<} b']
  ])('%s is one token and the text is unchanged', (kind, src) => {
    const tokens = tokenize(src)
    const found = critics(tokens)
    expect(found).toHaveLength(1)
    expect(found[0].kind).toBe(kind)
    // what is drawn is made from the same text, nothing is lost or added
    expect(generator(tokens)).toBe(src)
  })

  test('ranges point at the marks and the text between them', () => {
    const src = 'x {~~old~>new~~} y'
    const [token] = critics(tokenize(src))
    expect(src.substring(token.range.start, token.range.end)).toBe('{~~old~>new~~}')
    expect(token.oldChildren.map(t => t.raw).join('')).toBe('old')
    expect(token.newChildren.map(t => t.raw).join('')).toBe('new')
    expect(token.newChildren[0].range.start).toBe(src.indexOf('new'))
  })

  test('a search match inside either side of a replacement is passed on to the tokens drawn from it', () => {
    const src = 'x {~~old word~>new word~~} y'
    const oldAt = src.indexOf('word')
    const newAt = src.lastIndexOf('word')
    const [token] = critics(tokenizer(src, {
      hasBeginRules: false,
      highlights: [{ start: oldAt, end: oldAt + 4, active: false }, { start: newAt, end: newAt + 4, active: true }]
    }))
    expect(token.oldChildren[0].highlights).toEqual([{ start: oldAt, end: oldAt + 4, active: false }])
    expect(token.newChildren[0].highlights).toEqual([{ start: newAt, end: newAt + 4, active: true }])
  })

  test('Markdown inside a change is still Markdown', () => {
    const [token] = critics(tokenize('{++a **bold** word++}'))
    expect(token.children.map(t => t.type)).toEqual(['text', 'strong', 'text'])
  })

  test('a change with its author note, as the app will write them', () => {
    const src = '{--gone--}{>>@Ferenc 2026-10-07<<}'
    const tokens = critics(tokenize(src))
    expect(tokens.map(t => t.kind)).toEqual(['del', 'comment'])
    expect(tokens[1].content).toBe('@Ferenc 2026-10-07')
  })

  test('an unfinished mark and ordinary braces are plain text', () => {
    for (const src of ['{++never closed', 'a {b} c', '{ ++x++ }', 'json: {"a": 1}']) {
      expect(critics(tokenize(src))).toHaveLength(0)
      expect(generator(tokenize(src))).toBe(src)
    }
  })

  test('code that contains the marks is left alone', () => {
    const tokens = tokenize('`{++x++}`')
    expect(critics(tokens)).toHaveLength(0)
    expect(tokens[0].type).toBe('inline_code')
  })

  test('~~strikethrough~~ still works next to it', () => {
    const types = tokenize('~~out~~ {++in++}').map(t => t.type)
    expect(types).toEqual(['del', 'text', 'critic'])
  })
})
