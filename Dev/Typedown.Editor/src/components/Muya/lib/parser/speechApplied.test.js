// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { marksOf, marksAt, appliedMark, planRemove, enclosing } from './speechEdit'
import { estimateText } from './speechTiming'
import { collectDefinitions } from './speech'

const defs = collectDefinitions(['{define very-slow pace 50%: half speed}', '{define wave note: wave}']).defs

const apply = (text, plan) => text.substring(0, plan.start) + plan.replacement + text.substring(plan.end)

describe('the marks of a paragraph', () => {
  test('pairs and single marks, in order, with where they are', () => {
    const text = 'a {pause 2s}b {slow}c{/slow} d'
    const marks = marksOf(text, defs)
    expect(marks.map(m => [m.role, m.name])).toEqual([['point', 'pause'], ['pair', 'slow']])
    expect(marks[1].open).toEqual([text.indexOf('{slow}'), text.indexOf('{slow}') + 6])
    expect(marks[1].close).toEqual([text.indexOf('{/slow}'), text.indexOf('{/slow}') + 7])
  })

  test('a pair that is not closed runs to the end of the paragraph', () => {
    const [pair] = marksOf('so {loud}this', defs)
    expect(pair.close).toEqual([13, 13])
  })

  test('words of the document count, and text that is not a mark does not', () => {
    expect(marksOf('{very-slow}x{/very-slow} {width=50%} {nothing}', defs).map(m => m.name)).toEqual(['very-slow'])
  })
})

describe('what is applied at a selection', () => {
  const text = 'Good evening. {slow}We lost every file.{/slow} {pause 3s}Then we left.'
  const at = s => text.indexOf(s)

  test('a pair that holds the caret or the selection', () => {
    expect(marksAt(text, at('lost'), at('lost'), defs).map(m => m.name)).toEqual(['slow'])
    expect(marksAt(text, at('We lost'), at('file.') + 5, defs).map(m => m.name)).toEqual(['slow'])
    expect(marksAt(text, at('Good'), at('Good') + 4, defs)).toEqual([])
  })

  test('a pair that the selection holds whole is applied too, as after wrapping words that had one', () => {
    const wrapped = '{loud}{slow}We lost it.{/slow}{/loud}'
    const inner = { start: '{loud}'.length, end: wrapped.length - '{/loud}'.length }
    expect(marksAt(wrapped, inner.start, inner.end, defs).map(m => m.name).sort()).toEqual(['loud', 'slow'])
    expect(marksAt('x {slow}a{/slow} y', 0, 2, defs)).toEqual([])
  })

  test('a selection that only reaches into a pair is not inside it', () => {
    expect(marksAt(text, at('evening'), at('lost') + 4, defs)).toEqual([])
  })

  test('a single mark right before or after the caret, with only spaces between', () => {
    expect(marksAt(text, at('Then'), at('Then'), defs).map(m => m.name)).toEqual(['pause'])
    expect(marksAt(text, at(' {pause') + 1, at(' {pause') + 1, defs).map(m => m.name).sort()).toEqual(['pause'])
    expect(marksAt('One {beat} two', 3, 3, defs).map(m => m.name)).toEqual(['beat'])
    expect(marksAt('One two {beat} three', 0, 0, defs)).toEqual([])
  })

  test('an item stands for a word, its pause and its note: a pause of 3 s is not a plain pause', () => {
    expect(appliedMark(text, at('Then'), at('Then'), { name: 'pause', value: '3s' }, defs)).not.toBeNull()
    expect(appliedMark(text, at('Then'), at('Then'), { name: 'pause' }, defs)).toBeNull()
    const cue = 'Hello {cue: look up} there'
    expect(appliedMark(cue, 6, 6, { name: 'cue', text: 'look up' }, defs)).not.toBeNull()
    expect(appliedMark(cue, 6, 6, { name: 'cue', text: 'next slide' }, defs)).toBeNull()
    expect(appliedMark(cue, 6, 6, { name: 'cue' }, defs)).not.toBeNull()
  })

  test('an item the host sends with null for what it does not say is the same as one that leaves it out', () => {
    const text = 'Say {slow}this{/slow} and {pause 3s}that'
    expect(appliedMark(text, 8, 8, { name: 'slow', value: null, text: null }, defs)).not.toBeNull()
    const that = text.indexOf('that')
    expect(appliedMark(text, that, that, { name: 'pause', value: '3s', text: null }, defs)).not.toBeNull()
    expect(appliedMark(text, that, that, { name: 'pause', value: null, text: null }, defs)).toBeNull()
  })

  test('the innermost pair counts when a word is applied twice', () => {
    const nested = '{loud}a {loud}b{/loud} c{/loud}'
    const mark = appliedMark(nested, 14, 14, { name: 'loud' }, defs)
    expect(mark.open[0]).toBe(8)
  })
})

describe('taking a mark away', () => {
  test('a pair loses its two marks and keeps the words', () => {
    const text = 'Good. {slow}We lost every file.{/slow} Then we left.'
    const plan = planRemove(text, text.indexOf('lost'), text.indexOf('lost'), { name: 'slow' }, defs)
    expect(apply(text, plan)).toBe('Good. We lost every file. Then we left.')
  })

  test('a tone loses its note with its opener', () => {
    const text = '{tone: dry irony}Well done.{/tone}{wait 3s: laugh}'
    const plan = planRemove(text, 5, 5, { name: 'tone', text: 'dry irony' }, defs)
    expect(apply(text, plan)).toBe('Well done.{wait 3s: laugh}')
  })

  test('a single mark goes with the space it leaves, and punctuation stays', () => {
    expect(apply('One {beat} two', planRemove('One {beat} two', 3, 3, { name: 'beat' }, defs))).toBe('One two')
    const end = 'End {beat}.'
    expect(apply(end, planRemove(end, 4, 4, { name: 'beat' }, defs))).toBe('End.')
    const start = '{pause} Hello'
    expect(apply(start, planRemove(start, 0, 0, { name: 'pause' }, defs))).toBe('Hello')
  })

  test('nothing applied, nothing removed', () => {
    expect(planRemove('Plain text', 2, 2, { name: 'slow' }, defs)).toEqual({ ok: false, reason: 'nothing' })
  })

  test('a word of the document is taken away like any other', () => {
    const text = '{very-slow}Word by word{/very-slow}'
    expect(apply(text, planRemove(text, 5, 5, { name: 'very-slow' }, defs))).toBe('Word by word')
  })
})

describe('the surroundings of a stretch, for its time', () => {
  const words = n => Array.from({ length: n }, () => 'word').join(' ')

  test('the pairs that hold it, outermost first, with their closers the other way round', () => {
    const text = '{fast}a {tone: dry}b c d{/tone} e{/fast}'
    const at = text.indexOf('c')
    expect(enclosing(text, at, at + 1, defs)).toEqual({ open: '{fast}{tone: dry}', close: '{/tone}{/fast}' })
    expect(enclosing(text, 0, 0, defs)).toEqual({ open: '', close: '' })
    // a stretch that is the pair itself is not inside it
    const whole = text.indexOf('{tone')
    expect(enclosing(text, whole, text.indexOf('{/tone}') + 7, defs)).toEqual({ open: '{fast}', close: '{/fast}' })
  })

  test('a pair that is not closed runs to the end, so it holds what comes after it', () => {
    expect(enclosing('{slow}one two three', 8, 11, defs)).toEqual({ open: '{slow}', close: '{/slow}' })
  })

  test('a stretch measured with its surroundings takes the time of the pace it is spoken at', () => {
    const text = `{slow}${words(130)}{/slow}`
    const around = enclosing(text, 6, 6 + words(130).length, defs)
    expect(estimateText(`${around.open}${words(130)}${around.close}`, defs)).toBeCloseTo(80, 5)
    expect(estimateText(words(130), defs)).toBeCloseTo(60, 5)
  })
})
