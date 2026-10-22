// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { computeHints, LONG_PAUSE, FAST_RUN } from './speechHints'

const words = n => Array.from({ length: n }, () => 'word').join(' ')
const hints = (text, options) => computeHints(text, options)
const kinds = text => hints(text).map(h => h.kind)

describe('sections over their budget', () => {
  test('a section, with how far', () => {
    const found = hints(`{budget 1m}

# A {budget 30s}

${words(130)}`)
    expect(found.map(h => [h.kind, h.whole, h.title, Math.round(h.seconds)])).toEqual([['over', false, 'A', 30]])
  })

  test('the whole talk too, before the sections', () => {
    const found = hints(`{budget 30s}

# A {budget 20s}

${words(130)}`)
    expect(found.map(h => [h.kind, h.whole, Math.round(h.seconds)])).toEqual([['over', true, 30], ['over', false, 40]])
  })

  test('on time, no hint', () => {
    expect(kinds(`# A {budget 2m}\n\n${words(130)}`)).toEqual([])
  })
})

describe('a joke needs room for the laugh', () => {
  test('a joke with no pause after it', () => {
    expect(kinds('{tone: joke}Why did it fail?{/tone} Because.')).toEqual(['joke'])
    expect(kinds('{tone: Joke}Why did it fail?{/tone}')).toEqual(['joke'])
  })

  test('a wait, a pause or a beat after it is room enough, also at the start of the next paragraph', () => {
    expect(kinds('{tone: joke}Why?{/tone}{wait 3s: laugh} Because.')).toEqual([])
    expect(kinds('{tone: joke}Why?{/tone} {pause} Because.')).toEqual([])
    expect(kinds('{tone: joke}Why?{/tone}\n\n{beat} Because.')).toEqual([])
    expect(kinds('{tone: joke}Why?{/tone}\n\nBecause.')).toEqual(['joke'])
  })

  test('other tones and the word for a joke in other languages', () => {
    expect(kinds('{tone: dry irony}Well done.{/tone}')).toEqual([])
    expect(kinds('{tone: żart}Dlaczego?{/tone}')).toEqual(['joke'])
    expect(kinds('{tone: blague}Pourquoi ?{/tone}')).toEqual(['joke'])
  })
})

describe('a quick pace that goes on', () => {
  test('more than a minute of fast in a row, once', () => {
    const found = hints(`{fast}${words(130 * 2)}{/fast}`).filter(h => h.kind === 'fast')
    expect(found).toHaveLength(1)
    expect(found[0].seconds).toBeGreaterThan(FAST_RUN)
  })

  test('across paragraphs, until a word at the normal speed', () => {
    const text = `{fast}${words(100)}{/fast}\n\n{fast}${words(100)}{/fast}\n\n${words(10)}`
    expect(kinds(text)).toEqual(['fast'])
    expect(kinds(`{fast}${words(100)}{/fast}\n\n${words(5)}\n\n{fast}${words(100)}{/fast}`)).toEqual([])
  })

  test('a short quick stretch is nothing', () => {
    expect(kinds(`{fast}${words(40)}{/fast}`)).toEqual([])
  })
})

describe('a quick pace in a talk with a pace of its own', () => {
  test('the pace the document says ({wpm N}) is the one the minute is counted at', () => {
    expect(kinds(`{wpm 65}

{fast}${words(130)}{/fast}`)).toEqual(['fast'])
    expect(kinds(`{fast}${words(130)}{/fast}`)).toEqual([])
  })
})

describe('pauses', () => {
  test('a pause longer than ten seconds is probably a typo', () => {
    const found = hints('word {pause 100s} word')
    expect(found.map(h => [h.kind, h.seconds, h.word])).toEqual([['long', 100, 'pause']])
    expect(LONG_PAUSE).toBe(10)
    expect(kinds('word {pause 10s} word {wait 5s} {pause 600s}')).toEqual(['long'])
  })

  test('a pause of the user over ten seconds too', () => {
    expect(kinds('{define long-pause pause 30s: a long one}\n\nword {long-pause}')).toEqual(['long'])
  })
})

describe('words that look like marks', () => {
  test('a typo is told once, with its closer flag', () => {
    const found = hints('say {pauze 2s} and {pauze} then {/slwo}')
    expect(found.map(h => [h.kind, h.word, h.closer])).toEqual([['unknown', 'pauze', false], ['unknown', 'slwo', true]])
  })

  test('what is not a mark and not a hint: attributes, math, code, templates with an equals sign, short names, review marks', () => {
    expect(kinds('![img](a.png){width=50%} and {#id} and {++added++} and {--gone--}')).toEqual([])
    expect(kinds('the value $x_{i} + \frac{a}{b}$ is small and `{typo}` too')).toEqual([])
    expect(kinds('```\n{typo}\n```')).toEqual([])
    expect(kinds('a {x} and {ab}')).toEqual([])
  })

  test('a word the document defines is a mark, a word the library has but the document does not is a hint', () => {
    expect(kinds('{define whisper span: barely audible}\n\n{whisper}hush{/whisper}')).toEqual([])
    expect(kinds('{whisper}hush{/whisper}')).toEqual(['unknown'])
  })
})

describe('definitions', () => {
  test('a word defined twice, and a definition that does not hold', () => {
    const found = hints('{define wave note: a}\n{define wave note: b}\n{define x}\n\ntext')
    expect(found.map(h => h.kind).sort()).toEqual(['baddef', 'twice'])
    expect(found.find(h => h.kind === 'twice').raw).toContain('wave')
    expect(found.find(h => h.kind === 'baddef').error).toBeTruthy()
  })
})

describe('where a hint points', () => {
  test('to the paragraph it is in, as the editor shows it', () => {
    const text = '# Title\n\nfirst paragraph here\n\nsecond one {pause 99s} here'
    const found = hints(text)
    expect(found).toHaveLength(1)
    expect(found[0].prefix).toBe('second one {pause 99s} here')
    expect(found[0].nth).toBe(0)
    expect(found[0].line).toBe(4)
  })

  test('hints come in the order of the text, those about the whole talk first', () => {
    const text = `{budget 10s}\n\n# A\n\nsay {pause 50s}\n\n${words(130)} {pauze}`
    const found = hints(text)
    expect(found[0].kind).toBe('over')
    expect(found[0].whole).toBe(true)
    expect(found.slice(1).map(h => h.kind)).toEqual(['long', 'unknown'])
  })
})
