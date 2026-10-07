// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import formatCtrl from './formatCtrl'
import selection from '../selection'

jest.mock('../selection', () => ({ __esModule: true, default: { getCursorRange: jest.fn() } }))

// Just enough of a ContentState for `format`: text blocks in a row, the cursor the browser would report, no drawing.
class Doc {
  constructor (lines) {
    this.changes = 0
    this.drawn = []
    this.muya = { options: {}, dispatchChange: () => { this.changes++ } }
    this.blocks = lines.map((line, i) => {
      const [text, functionType = 'paragraphContent'] = Array.isArray(line) ? line : [line]
      return { key: `b${i}`, type: 'span', functionType, text }
    })
    this.cursor = null
  }

  getBlock (key) { return this.blocks.find(b => b.key === key) }
  findNextBlockInLocation (block) { return this.blocks[this.blocks.indexOf(block) + 1] || null }
  partialRender () { this.drawn.push({ start: { ...this.cursor.start }, end: { ...this.cursor.end } }) }
  get texts () { return this.blocks.map(b => b.text) }

  // the selection from `[line, offset]` to `[line, offset]`, as getCursorRange would give it
  select ([i, from], [j, to]) {
    selection.getCursorRange.mockReturnValue({
      start: { key: `b${i}`, offset: from },
      end: { key: `b${j}`, offset: to === 'end' ? this.blocks[j].text.length : to }
    })
  }

  // the next command works on what the last one left selected
  reselect () {
    const { start, end } = this.cursor
    selection.getCursorRange.mockReturnValue({ start: { ...start }, end: { ...end } })
  }
}
formatCtrl(Doc)

const LINES = ['First line of the text.', 'Second line is longer.', 'Third line.', 'Fourth line.', 'Fifth line.']

describe('formatting a selection over several lines', () => {
  test('every line gets the format, not only the last ones', () => {
    const doc = new Doc(LINES)
    doc.select([0, 0], [4, 'end'])
    doc.format('strong')
    expect(doc.texts).toEqual(LINES.map(l => `**${l}**`))
    // the host is told, so it saves the new text
    expect(doc.changes).toBe(1)
  })

  test('a selection that starts and ends inside a line formats only what is selected there', () => {
    const doc = new Doc(LINES)
    doc.select([0, 6], [3, 4])
    doc.format('em')
    expect(doc.texts).toEqual([
      'First *line of the text.*',
      '*Second line is longer.*',
      '*Third line.*',
      '*Four*th line.',
      'Fifth line.'
    ])
  })

  test('the same command again takes the format off every line', () => {
    const doc = new Doc(LINES)
    doc.select([0, 0], [4, 'end'])
    doc.format('strong')
    doc.reselect()
    doc.format('strong')
    expect(doc.texts).toEqual(LINES)
  })

  test('a selection with a line that is not formatted yet formats all of them', () => {
    const doc = new Doc(['**Bold line**', 'Plain line'])
    doc.select([0, 0], [1, 'end'])
    doc.format('strong')
    expect(doc.texts).toEqual(['**Bold line**', '**Plain line**'])
  })

  test('the selection stays over the formatted text, so the next command can follow', () => {
    const doc = new Doc(LINES)
    doc.select([0, 0], [4, 'end'])
    doc.format('strong')
    doc.reselect()
    doc.format('em')
    expect(doc.texts).toEqual(LINES.map(l => `***${l}***`))
    doc.reselect()
    doc.format('em')
    expect(doc.texts).toEqual(LINES.map(l => `**${l}**`))
  })

  test('empty lines and lines of spaces stay as they are, spaces at the edges stay outside the markers', () => {
    const doc = new Doc(['First  ', '', '   ', '  Last line '])
    doc.select([0, 0], [3, 'end'])
    doc.format('strong')
    expect(doc.texts).toEqual(['**First**  ', '', '   ', '  **Last line** '])
  })

  test('a heading keeps its hashes outside the format', () => {
    const doc = new Doc([['#\u00A0Title', 'atxLine'], 'Text below'])
    doc.select([0, 0], [1, 'end'])
    doc.format('strong')
    expect(doc.texts).toEqual(['#\u00A0**Title**', '**Text below**'])
  })

  test('the closing hashes of a heading stay outside the format', () => {
    const doc = new Doc([['#\u00A0Title #', 'atxLine'], 'Text below'])
    doc.select([0, 0], [1, 'end'])
    doc.format('strong')
    expect(doc.texts).toEqual(['#\u00A0**Title** #', '**Text below**'])
  })

  test('clear takes every format off every line', () => {
    const doc = new Doc(['**a** and *b*', '~~c~~ and `d`'])
    doc.select([0, 0], [1, 'end'])
    doc.format('clear')
    expect(doc.texts).toEqual(['a and b', 'c and d'])
  })

  test('clear takes out formats inside each other, the inner one first', () => {
    const doc = new Doc(['***Hello big***', '**a *b* c**'])
    doc.select([0, 0], [1, 'end'])
    doc.format('clear')
    expect(doc.texts).toEqual(['Hello big', 'a b c'])
  })

  test('clear inside one line takes out formats inside each other too', () => {
    const doc = new Doc(['***Hello big***'])
    doc.select([0, 0], [0, 'end'])
    doc.format('clear')
    expect(doc.texts).toEqual(['Hello big'])
  })

  test('links over several lines: every line is drawn with the cursor over all of them, then the cursor is put between the last ()', () => {
    const doc = new Doc(['One', 'Two', 'Three'])
    doc.select([0, 0], [2, 'end'])
    doc.format('link')
    expect(doc.texts).toEqual(['[One]()', '[Two]()', '[Three]()'])
    expect(doc.drawn).toHaveLength(2)
    expect(doc.drawn[0].start.key).toBe('b0')
    expect(doc.drawn[0].end.key).toBe('b2')
    expect(doc.drawn[1].start).toEqual(doc.drawn[1].end)
    expect(doc.drawn[1].end).toMatchObject({ key: 'b2', offset: 8 })
  })

  test('a selection of empty lines changes nothing', () => {
    const doc = new Doc(['', ' '])
    doc.select([0, 0], [1, 'end'])
    doc.format('strong')
    expect(doc.texts).toEqual(['', ' '])
  })
})

describe('taking a format off a line that has more than one', () => {
  test('italic off inside bold italic leaves the bold', () => {
    const doc = new Doc(['***Hello***'])
    doc.select([0, 3], [0, 8])
    doc.format('em')
    expect(doc.texts).toEqual(['**Hello**'])
  })

  test('bold off inside bold italic leaves the italic', () => {
    const doc = new Doc(['***Hello***'])
    doc.select([0, 3], [0, 8])
    doc.format('strong')
    expect(doc.texts).toEqual(['*Hello*'])
  })

  test('italic off on whole lines of bold italic leaves the bold', () => {
    const doc = new Doc(['***Hello big world***', '***Second one***'])
    doc.select([0, 3], [1, 13])
    doc.format('em')
    expect(doc.texts).toEqual(['**Hello big world**', '**Second one**'])
  })
})
