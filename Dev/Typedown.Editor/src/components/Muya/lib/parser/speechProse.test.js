// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { proseLines, stripMarkdown } from './speech'

const prose = text => proseLines(text.split('\n'))

describe('which lines are prose', () => {
  test('all of them in a plain document', () => {
    expect(prose('# T\n\ntext\n')).toEqual([true, true, true, true])
  })

  test('not the lines of a fenced code block, nor its fences', () => {
    expect(prose('a\n```js\n{pause}\n```\nb')).toEqual([true, false, false, false, true])
    expect(prose('a\n~~~\n{pause}\n~~~\nb')).toEqual([true, false, false, false, true])
  })

  test('a fence that is never closed runs to the end', () => {
    expect(prose('a\n```\n{pause}\nb')).toEqual([true, false, false, false])
  })

  test('a longer fence is closed only by one as long', () => {
    expect(prose('````\n```\n{pause}\n````\nb')).toEqual([false, false, false, false, true])
  })

  test('not a closed front matter block', () => {
    expect(prose('---\ntitle: x\n---\ntext')).toEqual([false, false, false, true])
    expect(prose('---\ntitle: x\n...\ntext')).toEqual([false, false, false, true])
  })

  test('a lone --- at the start is a rule: the text after it is prose', () => {
    expect(prose('---\ntext {pause}\nmore')).toEqual([true, true, true])
  })

  test('a front matter block is only at the start', () => {
    expect(prose('text\n---\nmore\n---\nend')).toEqual([true, true, true, true, true])
  })
})

describe('Remove all speech marks on a document that starts with a rule', () => {
  test('the marks after a lone --- are removed', () => {
    expect(stripMarkdown('---\nGood {pause 2s} evening.\n')).toBe('---\nGood evening.\n')
  })

  test('a closed front matter block is still left alone', () => {
    expect(stripMarkdown('---\ntitle: {pause}\n---\nGood {pause} evening.')).toBe('---\ntitle: {pause}\n---\nGood evening.')
  })

  test('a fence that is never closed is left alone to the end', () => {
    expect(stripMarkdown('a {beat} b\n```\n{beat} c')).toBe('a b\n```\n{beat} c')
  })
})
