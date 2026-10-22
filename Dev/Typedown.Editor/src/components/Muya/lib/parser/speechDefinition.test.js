// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { buildMark, withDefinitions, planDefinition } from './speechEdit'
import { collectDefinitions } from './speech'

const defs = (...lines) => collectDefinitions(lines).defs
const line = '{define very-slow pace 50%: about half speed}'

// the text after writing what planDefinition says
const place = (markdown, plan) => {
  const lines = markdown.split('\n')
  const offset = lines.slice(0, plan.line).reduce((n, l) => n + l.length + 1, 0) + plan.ch
  return markdown.substring(0, offset) + plan.insert + markdown.substring(offset)
}

describe("a mark of a document's own word needs its definition", () => {
  test('the missing lines are found, and the mark can be built before they are written', () => {
    const { defs: merged, missing } = withDefinitions(undefined, [line])
    expect(missing).toEqual([line])
    expect(buildMark({ name: 'very-slow' }, merged)).toMatchObject({ role: 'pair', close: '{/very-slow}' })
    expect(buildMark({ name: 'very-slow' }, undefined)).toBeNull()
  })

  test('a word the document already defines keeps its own definition', () => {
    const own = defs("{define very-slow pace 30%: the document's own}")
    const merged = withDefinitions(own, [line])
    expect(merged.missing).toEqual([])
    expect(merged.defs.get('very-slow').speed).toBe(0.3)
    expect(own.get('very-slow').speed).toBe(0.3)
  })

  test('lines that are not definitions are ignored', () => {
    expect(withDefinitions(undefined, ['{pause 2s}', 'text', '{define broken pace}']).missing).toEqual([])
  })

  test('the same word twice in the lines is written once', () => {
    expect(withDefinitions(undefined, [line, line]).missing).toEqual([line])
  })
})

describe('where a definition is written', () => {
  test('a paragraph of its own before the first text', () => {
    const doc = '# Talk\n\nHello.'
    expect(place(doc, planDefinition(doc, line))).toBe(`${line}\n\n# Talk\n\nHello.`)
  })

  test('after the paragraph of definitions that starts the document', () => {
    const doc = '{define a-one span: x}\n{define b-two note: y}\n\n# Talk'
    expect(place(doc, planDefinition(doc, line))).toBe(`{define a-one span: x}\n{define b-two note: y}\n${line}\n\n# Talk`)
  })

  test('not into a paragraph that holds other text too', () => {
    const doc = '{define a-one span: x}\nand some text\n\n# Talk'
    expect(place(doc, planDefinition(doc, line))).toBe(`${line}\n\n${doc}`)
  })

  test('after a front matter block, and its blank line stays where it was', () => {
    const doc = '---\ntitle: T\n---\n\n# Talk'
    expect(place(doc, planDefinition(doc, line))).toBe(`---\ntitle: T\n---\n\n${line}\n\n# Talk`)
  })

  test('in an empty document, and in one that has only a line', () => {
    expect(place('', planDefinition('', line))).toBe(`${line}\n`)
    expect(place('Hello', planDefinition('Hello', line))).toBe(`${line}\n\nHello`)
  })

  test('the document then reads the definition', () => {
    const doc = place('# Talk', planDefinition('# Talk', line))
    expect(collectDefinitions(doc.split('\n')).defs.get('very-slow').speed).toBe(0.5)
  })

  test('a second definition goes after the first, in the same paragraph', () => {
    const second = '{define whisper span: quiet}'
    const once = place('# Talk', planDefinition('# Talk', line))
    const twice = place(once, planDefinition(once, second))
    expect(twice).toBe(`${line}\n${second}\n\n# Talk`)
  })
})
