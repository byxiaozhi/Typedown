// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { buildMark, atomicSpans, planEdit } from './speechEdit'
import { collectDefinitions, stripMarkdown, stripMarks } from './speech'

const defs = (...lines) => collectDefinitions(lines).defs
// the text after an edit
const apply = (text, plan) => text.substring(0, plan.start) + plan.replacement + text.substring(plan.end)
const edit = (text, start, end, spec, d) => {
  const mark = buildMark(spec, d)
  expect(mark).not.toBeNull()
  const plan = planEdit(text, start, end, mark, d)
  return plan.ok ? apply(text, plan) : plan.reason
}
// a caret is written as "|" in the text, a selection as "[" and "]"
const at = (marked, spec, d) => {
  const open = marked.indexOf('[')
  const close = marked.indexOf(']')
  if (open >= 0) {
    const text = marked.replace('[', '').replace(']', '')
    return edit(text, open, close - 1, spec, d)
  }
  const caret = marked.indexOf('|')
  return edit(marked.replace('|', ''), caret, caret, spec, d)
}

describe('what to write for a mark', () => {
  test('a single mark, a pair, a tone and a cue', () => {
    expect(buildMark({ name: 'pause' })).toMatchObject({ role: 'point', open: '{pause}', close: '' })
    expect(buildMark({ name: 'pause', value: '3s' }).open).toBe('{pause 3s}')
    expect(buildMark({ name: 'wait', value: '3s', text: 'laugh' }).open).toBe('{wait 3s: laugh}')
    expect(buildMark({ name: 'slow' })).toMatchObject({ role: 'pair', open: '{slow}', close: '{/slow}', scope: 'sentence' })
    expect(buildMark({ name: 'emphasis' }).scope).toBe('word')
    expect(buildMark({ name: 'tone', text: 'dry irony' })).toMatchObject({ open: '{tone: dry irony}', close: '{/tone}' })
    expect(buildMark({ name: 'cue', text: 'look up' }).open).toBe('{cue: look up}')
  })

  test('a recipe writes a mark after the pair', () => {
    expect(buildMark({ name: 'tone', text: 'joke', after: { name: 'wait', value: '3s', text: 'laugh' } }).after).toBe('{wait 3s: laugh}')
  })

  test('what is not a mark, or not valid, is refused', () => {
    expect(buildMark({ name: 'pauze' })).toBeNull()
    expect(buildMark({ name: 'pause', value: 'fast' })).toBeNull()
    expect(buildMark({ name: 'cue' })).toBeNull()
    expect(buildMark({ name: 'tone', text: 'x', after: { name: 'slow' } })).toBeNull()
  })

  test('a word the document defines is a mark, one it does not define is not', () => {
    const d = defs('{define very-slow pace 50%: half speed}')
    expect(buildMark({ name: 'very-slow' }, d)).toMatchObject({ role: 'pair', close: '{/very-slow}' })
    expect(buildMark({ name: 'very-slow' })).toBeNull()
  })
})

describe('a single mark goes at the caret', () => {
  test.each([
    ['Good evening| everybody', 'Good evening {pause} everybody'],
    ['Good evening |everybody', 'Good evening {pause} everybody'],
    ['|Good evening', '{pause} Good evening'],
    ['Good evening.|', 'Good evening. {pause}'],
    ['Good evening. |', 'Good evening. {pause}'],
    ['Good|evening', 'Good{pause}evening'],
    ['one {beat}| two', 'one {beat}{pause} two']
  ])('%s', (marked, expected) => expect(at(marked, { name: 'pause' })).toBe(expected))

  test('after a selection, not over it', () => {
    expect(at('Good [evening] all', { name: 'pause', value: '2s' })).toBe('Good evening {pause 2s} all')
  })

  test('a caret inside a mark moves out of it, and marks follow each other without a space', () => {
    expect(edit('a {pause 2s} b', 5, 5, { name: 'beat' })).toBe('a {pause 2s}{beat} b')
  })

  test('a caret inside a link, code or a tag is refused', () => {
    expect(edit('see [the link](http://x.com) now', 6, 6, { name: 'beat' })).toBe('unsafe')
    expect(edit('use `code here` now', 8, 8, { name: 'beat' })).toBe('unsafe')
  })
})

describe('a pair wraps a selection', () => {
  test('the words selected', () => {
    expect(at('So [very slowly] now', { name: 'slow' })).toBe('So {slow}very slowly{/slow} now')
  })

  test('the spaces at the edges of the selection stay outside', () => {
    expect(at('So[ very slowly ]now', { name: 'slow' })).toBe('So {slow}very slowly{/slow} now')
  })

  test('Markdown inside is kept', () => {
    expect(at('So [**very** slowly] now', { name: 'loud' })).toBe('So {loud}**very** slowly{/loud} now')
  })

  test('a tone and a recipe', () => {
    expect(at('Of [course] not', { name: 'tone', text: 'dry irony' })).toBe('Of {tone: dry irony}course{/tone} not')
    expect(at('A [joke here.]', { name: 'tone', text: 'joke', after: { name: 'wait', value: '3s', text: 'laugh' } }))
      .toBe('A {tone: joke}joke here.{/tone}{wait 3s: laugh}')
  })

  test('nothing but spaces selected is nothing', () => {
    expect(at('a[ ]b', { name: 'slow' })).toBe('nothing')
  })

  test('a mark only partly selected is taken whole', () => {
    expect(edit('say {pause 2s} now', 6, 18, { name: 'soft' })).toBe('say {soft}{pause 2s} now{/soft}')
    expect(edit('say {pause 2s} now', 0, 7, { name: 'soft' })).toBe('{soft}say {pause 2s}{/soft} now')
  })

  test('a review change is taken whole too', () => {
    expect(edit('a {++new++} b', 5, 9, { name: 'loud' })).toBe('a {loud}{++new++}{/loud} b')
  })

  test('a selection that cuts a link, code or math is refused', () => {
    expect(edit('see [the link](http://x.com) now', 8, 30, { name: 'slow' })).toBe('unsafe')
    expect(edit('use `code here` now', 6, 15, { name: 'slow' })).toBe('unsafe')
    expect(edit('a $x+y$ b', 3, 8, { name: 'slow' })).toBe('unsafe')
  })

  test('a selection that holds a link whole is wrapped', () => {
    expect(edit('see [the link](http://x.com) now', 4, 28, { name: 'slow' })).toBe('see {slow}[the link](http://x.com){/slow} now')
  })

  test('pairs inside are kept when they are whole, refused when they are cut in two', () => {
    expect(at('a [{loud}b{/loud} c] d', { name: 'slow' })).toBe('a {slow}{loud}b{/loud} c{/slow} d')
    expect(edit('a {loud}b c{/loud} d', 9, 16, { name: 'slow' })).toBe('unsafe')
    expect(edit('a {loud}b c{/loud} d', 0, 11, { name: 'slow' })).toBe('unsafe')
  })
})

describe('a pair at a caret wraps a word or a sentence', () => {
  test('emphasis: the word', () => {
    expect(at('This is imp|ortant now', { name: 'emphasis' })).toBe('This is {emphasis}important{/emphasis} now')
    expect(at('This is important| now', { name: 'emphasis' })).toBe('This is {emphasis}important{/emphasis} now')
    expect(at('This is |important now', { name: 'emphasis' })).toBe('This is {emphasis}important{/emphasis} now')
    expect(at('Say don\'t|', { name: 'emphasis' })).toBe('Say {emphasis}don\'t{/emphasis}')
  })

  test('between two words, the one before', () => {
    expect(at('one |two', { name: 'emphasis' })).toBe('one {emphasis}two{/emphasis}')
  })

  test('no word, nothing', () => {
    expect(at('|  ', { name: 'emphasis' })).toBe('nothing')
    expect(edit('', 0, 0, { name: 'emphasis' })).toBe('nothing')
  })

  test('the others: the sentence', () => {
    expect(at('First one. Second one is| here. Third.', { name: 'slow' })).toBe('First one. {slow}Second one is here.{/slow} Third.')
    expect(at('First one. Seco|nd? Third.', { name: 'soft' })).toBe('First one. {soft}Second?{/soft} Third.')
  })

  test('the last sentence, with the caret after its full stop', () => {
    expect(at('First one. Second one.|', { name: 'slow' })).toBe('First one. {slow}Second one.{/slow}')
  })

  test('in the white space between sentences, the one before', () => {
    expect(at('First one. | Second one.', { name: 'slow' })).toBe('{slow}First one.{/slow}  Second one.')
  })

  test('a sentence of one paragraph with no full stop', () => {
    expect(at('No full stop he|re', { name: 'fast' })).toBe('{fast}No full stop here{/fast}')
  })

  test('a full stop inside a mark does not end the sentence', () => {
    expect(at('Say it {cue: next slide. then wait} now| and go.', { name: 'slow' }))
      .toBe('{slow}Say it {cue: next slide. then wait} now and go.{/slow}')
  })

  test('marks in the sentence are kept; a pair cut by the sentence is refused', () => {
    expect(at('One {beat} two| three.', { name: 'slow' })).toBe('{slow}One {beat} two three.{/slow}')
    expect(at('One {loud}two. Thr|ee.{/loud}', { name: 'slow' })).toBe('unsafe')
  })

  test('the word does not swallow a mark next to it', () => {
    expect(at('word|{pause} next', { name: 'emphasis' })).toBe('{emphasis}word{/emphasis}{pause} next')
  })
})

describe('the parts that must not be cut', () => {
  test('are found', () => {
    const d = defs('{define wbw pace 60%: x}')
    const text = 'a {pause 2s} `c` [l](u) ![i](s) $m$ <b> {++c++} {slow}x{/slow} {wbw}y{/wbw} {pauze}'
    const kinds = atomicSpans(text, d).map(s => s.kind)
    expect(kinds).toEqual(['speech', 'code', 'link', 'link', 'math', 'html', 'critic', 'speech', 'speech', 'speech', 'speech'])
  })

  test('an escaped backtick is text, not the start of code', () => {
    expect(atomicSpans('a \\`b\\` c').map(s => s.kind)).toEqual([])
    expect(edit('a \\`b c\\` d', 0, 11, { name: 'slow' })).toBe('{slow}a \\`b c\\` d{/slow}')
    expect(edit('a \\`b\\` c', 5, 5, { name: 'beat' })).toBe('a \\`b {beat}\\` c')
  })

  test('the text is rebuilt from them without loss', () => {
    const text = 'a {pause 2s} b'
    const [span] = atomicSpans(text)
    expect(text.substring(span.start, span.end)).toBe('{pause 2s}')
  })
})

describe('removing all speech marks', () => {
  test('marks go, words stay, spaces are tidied', () => {
    expect(stripMarks('one {pause} two', undefined, true)).toBe('one two')
    expect(stripMarks('{pause} one', undefined, true)).toBe('one')
    expect(stripMarks('end. {beat}', undefined, true)).toBe('end.')
    expect(stripMarks('a {slow}b{/slow} c', undefined, true)).toBe('a b c')
    expect(stripMarks('a{pause}b', undefined, true)).toBe('ab')
    expect(stripMarks('- {cue: look} item', undefined, true)).toBe('- item')
    expect(stripMarks('one {pause} two', undefined, false)).toBe('one  two')
  })

  test('a document: definitions, marks, code and front matter', () => {
    const doc = [
      '---',
      'title: {pause}',
      '---',
      '{define very-slow pace 50%: half speed}',
      '{define wave note: wave}',
      '',
      '# Opening {budget 1m}',
      '',
      'Good evening {pause 2s} all. {very-slow}Slowly{/very-slow}.',
      '',
      '{cue: look up}',
      '',
      'Use `{pause}` to pause.',
      '',
      '```',
      '{pause} stays {slow}here{/slow}',
      '```',
      '',
      'End. {wave}'
    ].join('\n')
    expect(stripMarkdown(doc)).toBe([
      '---',
      'title: {pause}',
      '---',
      '',
      '# Opening',
      '',
      'Good evening all. Slowly.',
      '',
      'Use `{pause}` to pause.',
      '',
      '```',
      '{pause} stays {slow}here{/slow}',
      '```',
      '',
      'End.'
    ].join('\n'))
  })

  test('a line of marks only goes with one of its blank lines', () => {
    expect(stripMarkdown('one\n\n{pause 3s}\n\ntwo')).toBe('one\n\ntwo')
    expect(stripMarkdown('{beat}\n\nfirst')).toBe('first')
  })

  test('the line endings are kept', () => {
    expect(stripMarkdown('a {beat} b\r\n\r\nc')).toBe('a b\r\n\r\nc')
  })

  test('a document without marks is returned as it is', () => {
    const doc = '# Title\n\nSome {braces} and {width=50%}.\n'
    expect(stripMarkdown(doc)).toBe(doc)
  })

  test('words that are not defined here are not removed', () => {
    expect(stripMarkdown('a {very-slow}b{/very-slow} c')).toBe('a {very-slow}b{/very-slow} c')
  })
})
