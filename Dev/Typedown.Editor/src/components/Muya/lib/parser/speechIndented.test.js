// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { proseLines, collectDocumentDefinitions, stripMarkdown } from './speech'

const prose = text => proseLines(text.split('\n'))

describe('indented code is not prose', () => {
  test('four spaces after a blank line open a code block that goes on while lines are indented', () => {
    expect(prose('text\n\n    {pause} code\n    more\n\nnext')).toEqual([true, true, false, false, true, true])
  })

  test('a tab does the same, and a code block may hold blank lines', () => {
    expect(prose('text\n\n\tcode\n\n\tmore code\n\nnext')).toEqual([true, true, false, true, false, true, true])
  })

  test('at the very start of a document too', () => {
    expect(prose('    code first\ntext')).toEqual([false, true])
  })

  test('an indented line that goes on with a paragraph is part of it', () => {
    expect(prose('a paragraph\n    carried on here\nmore')).toEqual([true, true, true])
  })

  test('under a list item an indented line is its own text, not code', () => {
    expect(prose('- item\n\n    continued {pause} here\n\nnext paragraph')).toEqual([true, true, true, true, true])
    expect(prose('1. item\n\n    more\n\n    and more')).toEqual([true, true, true, true, true])
  })

  test('after the list, a new paragraph and then code is code again', () => {
    expect(prose('- item\n\nplain paragraph\n\n    real code')).toEqual([true, true, true, true, false])
  })

  test('an indented line after a fenced block is code, with or without a blank line between', () => {
    expect(prose('```\nx\n```\n\n    code')).toEqual([false, false, false, true, false])
    expect(prose('```\nx\n```\n    code')).toEqual([false, false, false, false])
  })

  test('code may follow a heading or a rule right away, but not a line of a paragraph', () => {
    expect(prose('# Heading\n    {pause} code\ntext')).toEqual([true, false, true])
    expect(prose('text\n***\n    {pause} code')).toEqual([true, true, false])
    expect(prose('Title\n=====\n    code')).toEqual([true, true, false])
    expect(prose('text\n    {pause} still the paragraph')).toEqual([true, true])
  })

  test('code inside a list item needs four spaces beyond the content of the item', () => {
    expect(prose('- item\n\n      {pause} code\n      more\n\n  back in the item')).toEqual([true, true, false, false, true, true])
    expect(prose('1. item\n\n        {pause} code')).toEqual([true, true, false])
    expect(prose('1. item\n\n      {pause} not yet code')).toEqual([true, true, true])
    expect(prose('1. item\n\n       {pause} code (3 + 4)')).toEqual([true, true, false])
    expect(prose('- item\n      {pause} the item goes on')).toEqual([true, true])
  })

  test('nested lists: the innermost item counts, and a line that goes back out counts for the outer one', () => {
    expect(prose('- a\n  - b\n\n        code in b\n\n  back in a\n\n      code in a')).toEqual([true, true, true, false, true, true, true, false])
    expect(prose('- a\n  - b\n- c\n\n      code in c')).toEqual([true, true, true, true, false])
    expect(prose('- a\n\ntext\n\n    top-level code')).toEqual([true, true, true, true, false])
  })

  test('in a list, code from the page level is not meant: a code block holds list-like lines as code', () => {
    expect(prose('text\n\n    - not an item\n    - still code\n\n- real item')).toEqual([true, true, false, false, true, true])
  })

  test('a line with three spaces is still prose', () => {
    expect(prose('text\n\n   not code')).toEqual([true, true, true])
  })
})

describe('definitions only from prose', () => {
  test('an example in a fenced block or in indented code is not a definition of the document', () => {
    const doc = ['```', '{define whisper span: example}', '```', '', '    {define sotto span: also an example}', '', '{define wave note: real}'].join('\n')
    expect([...collectDocumentDefinitions(doc).defs.keys()]).toEqual(['wave'])
  })

  test('with Windows line endings too', () => {
    expect([...collectDocumentDefinitions('{define wave note: real}\r\n\r\ntext').defs.keys()]).toEqual(['wave'])
  })
})

describe('Remove all speech marks leaves indented code alone', () => {
  test('marks in prose go, marks in indented code stay', () => {
    expect(stripMarkdown('Good {pause} evening.\n\n    {pause} stays\n\nEnd {beat}.')).toBe('Good evening.\n\n    {pause} stays\n\nEnd.')
  })
})

describe('the space before a mark that is followed by punctuation goes with it', () => {
  test.each([
    ['End {beat}.', 'End.'],
    ['Wait {pause 2s}, then go', 'Wait, then go'],
    ['Really {slow}so{/slow}? Yes', 'Really so? Yes'],
    ['one {pause} two', 'one two']
  ])('%s', (text, expected) => {
    expect(stripMarkdown(text)).toBe(expected)
  })
})
