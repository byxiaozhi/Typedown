// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { buildExplanation, marksAsWords, secondsInWords } from './speechExplain'
import { computeTiming } from './speechTiming'

const library = [
  { name: 'very-slow', kind: 'pace', meaning: 'about half your normal speed, every word clear', definition: '{define very-slow pace 50%: about half your normal speed, every word clear}' },
  { name: 'whisper', kind: 'span', meaning: 'barely audible, as if telling a secret', definition: '{define whisper span: barely audible, as if telling a secret}' },
  { name: 'long-pause', kind: 'pause', meaning: 'the pause before the key line', definition: '{define long-pause pause 5s: the pause before the key line}' },
  { name: 'wave', kind: 'note', meaning: 'wave to the room', definition: '{define wave note: wave to the room}' }
]
const words = n => Array.from({ length: n }, () => 'word').join(' ')

describe('the explanation, with everything', () => {
  const text = buildExplanation({ library })

  test('says what the text is and that the marks are not to be spoken', () => {
    expect(text).toMatch(/speech script/)
    expect(text).toMatch(/not words to be spoken/)
    expect(text).toMatch(/keep them exactly as they are unless I ask/)
  })

  test('says how a mark is written, with pairs, definitions, times and speeds', () => {
    expect(text).toContain('{name: note}')
    expect(text).toContain('{name}some words{/name}')
    expect(text).toContain('{define name kind value: meaning}')
    expect(text).toMatch(/1m30s/)
    expect(text).toMatch(/Anything in braces that is not one of the marks below is ordinary text/)
  })

  test('lists every mark of Caret', () => {
    for (const form of ['{beat}', '{pause 2s}', '{wait 5s: laugh}', '{cue: look at the back row}', '{slow}words{/slow}', '{fast}words{/fast}',
      '{loud}words{/loud}', '{soft}words{/soft}', '{emphasis}words{/emphasis}', '{tone: dry irony}words{/tone}', '{wpm 140}', '{budget 3m}']) {
      expect(text).toContain(form)
    }
    expect(text).toMatch(/slow.*75 % of the normal speed/)
    expect(text).toMatch(/fast.*125 % of the normal speed/)
  })

  test('lists every mark of the library with the speaker\'s own words and how to add its definition', () => {
    expect(text).toMatch(/\{very-slow\}words\{\/very-slow\}: about half your normal speed, every word clear \(pace: speak the words between the marks at 50 % of the normal speed\)/)
    expect(text).toMatch(/\{whisper\}words\{\/whisper\}: barely audible, as if telling a secret/)
    expect(text).toMatch(/\{long-pause\} or \{long-pause 8s\}: the pause before the key line \(pause of 5 seconds\)/)
    expect(text).toMatch(/\{wave\}: wave to the room \(a cue to the speaker/)
    expect(text).toContain('add the line {define whisper span: barely audible, as if telling a secret}')
  })

  test('no speech when there is no document, and it ends with a new line', () => {
    expect(text).not.toMatch(/The speech follows/)
    expect(text.endsWith('\n')).toBe(true)
  })
})

describe('with the speech', () => {
  const markdown = `{define wave note: wave to the room}\n\n# Opening {budget 1m}\n\nGood evening. {pause 2s}{slow}I want to say something.{/slow}{wait 3s: laugh} {wave}\n\n${words(130)}`
  const timing = computeTiming(markdown)
  const text = buildExplanation({ library, markdown, timing })

  test('the document follows the explanation as it is', () => {
    expect(text).toContain('The speech follows.')
    expect(text.endsWith(`${markdown}\n`)).toBe(true)
    expect(text.indexOf('The speech follows.')).toBeGreaterThan(text.indexOf('Marks built into the writing tool'))
  })

  test('the planned times are added: the speed, the total, the sections with their budgets', () => {
    expect(text).toMatch(/speaking speed is 130 words per minute/)
    expect(text).toMatch(/planned total is 1:\d\d, against a budget of 1:00/)
    expect(text).toMatch(/- Opening: 1:\d\d \(budget 1:00\)/)
  })

  test('a word the document defines is not asked to be added again', () => {
    expect(text).not.toContain('add the line {define wave')
    expect(text).toMatch(/\{wave\}: wave to the room/)
  })

  test('the explanation only, with a document given, leaves the speech out', () => {
    const only = buildExplanation({ library, markdown, timing, withText: false })
    expect(only).not.toContain('The speech follows')
    expect(only).toMatch(/planned total/)
  })
})

describe('the last rehearsal', () => {
  const run = ['## Rehearsal 2026-10-08 09:05', '', 'Planned 1:11, read in 1:06 (-0:05).', ''].join('\n')
  const markdown = 'Say {pause 2s} this.'

  test('goes between the times and the speech, with a sentence that says what it is', () => {
    const text = buildExplanation({ library, markdown, timing: computeTiming(markdown), rehearsal: run })
    expect(text).toContain('The writer rehearsed this talk aloud')
    expect(text).toContain('## Rehearsal 2026-10-08 09:05')
    expect(text.indexOf('speaking speed is')).toBeLessThan(text.indexOf('The writer rehearsed'))
    expect(text.indexOf('## Rehearsal')).toBeLessThan(text.indexOf('The speech follows.'))
  })

  test('is left out when there is none', () => {
    expect(buildExplanation({ library, markdown, timing: computeTiming(markdown) })).not.toContain('rehearsed')
    expect(buildExplanation({ library, markdown, timing: computeTiming(markdown), rehearsal: '   ' })).not.toContain('rehearsed')
  })

  test('comes with the explanation alone as well', () => {
    expect(buildExplanation({ library, markdown, timing: computeTiming(markdown), withText: false, rehearsal: run })).toContain('Planned 1:11, read in 1:06')
  })
})

describe('only the marks the document uses', () => {
  const markdown = '{define whisper span: barely audible}\n\nSay {pause 2s} this {slow}slowly{/slow}.'
  const text = buildExplanation({ library, markdown, timing: computeTiming(markdown), usedOnly: true })

  test('the others are left out, the document\'s own words stay', () => {
    expect(text).toContain('{pause 2s}')
    expect(text).toContain('{slow}words{/slow}')
    expect(text).not.toContain('{beat}')
    expect(text).not.toContain('{tone:')
    expect(text).not.toContain('very-slow')
  })
})

describe('a document that changes the numbers of a built-in word', () => {
  test('the explanation says so', () => {
    const markdown = '{define slow pace 60%}\n\n{slow}text{/slow}'
    const text = buildExplanation({ library: [], markdown, timing: computeTiming(markdown) })
    expect(text).toMatch(/slow.*at 60 % of the normal speed/)
    expect(text).toMatch(/changes the numbers of this mark/)
  })
})

describe('a document that overrides a built-in pace with extra seconds, or a library mark with its own meaning', () => {
  test('the seconds added after every word are told too', () => {
    const markdown = '{define slow pace 60% +1s}\n\n{slow}text{/slow}'
    const text = buildExplanation({ library: [], markdown, timing: computeTiming(markdown) })
    expect(text).toMatch(/slow.*at 60 % of the normal speed, and 1 second are added after every word/)
    expect(text).not.toMatch(/fast.*added after every word/)
  })

  test('the meaning the document gives a library mark is the one told, not the one of the library', () => {
    const markdown = '{define whisper span: only for the last line}\n\n{whisper}bye{/whisper}'
    const text = buildExplanation({ library, markdown, timing: computeTiming(markdown) })
    expect(text).toContain('{whisper}words{/whisper}: only for the last line')
    expect(text).not.toContain('barely audible, as if telling a secret')
  })
})

describe('marks as words', () => {
  test('every kind of mark', () => {
    const markdown = '{define wave note: wave}\n\nSay {pause 2s} this {slow}slowly{/slow}{wait 3s: laugh} {cue: look up} {beat} {tone: dry}x{/tone} {wpm 140} {budget 3m}'
    expect(marksAsWords(markdown)).toBe('Say (pause, 2 seconds) this (start slow)slowly(end slow)(wait, 3 seconds: laugh) (cue: look up) (beat) (start tone: dry)x(end tone) (speaking speed: 140 words per minute) (time budget: 3 minutes)')
  })

  test('only the definition lines go: the rest of the text, blank lines in code included, is as it was', () => {
    const markdown = '{define wave note: wave}\n\nIntro {pause}\n\n```\na\n\n\n\nb\n```\n\n\n\nEnd'
    expect(marksAsWords(markdown)).toBe('Intro (pause)\n\n```\na\n\n\n\nb\n```\n\n\n\nEnd')
  })

  test('code and text that is not a mark are left alone, definition lines go', () => {
    const markdown = '{define wave note: wave}\n\n```\n{pause 2s}\n```\n\n{width=50%} and {nothing} {wave}'
    expect(marksAsWords(markdown)).toBe('```\n{pause 2s}\n```\n\n{width=50%} and {nothing} (wave)')
  })

  test('the explanation says the marks are words and lists them as they appear', () => {
    const markdown = 'Say {pause 2s} {slow}this{/slow}.'
    const text = buildExplanation({ library, markdown, timing: computeTiming(markdown), asWords: true })
    expect(text).toMatch(/written as words in round brackets/)
    expect(text).toContain('(pause) or (pause, 2 seconds)')
    expect(text).toContain('(start slow) ... (end slow)')
    expect(text).toContain('- (beat): a very short pause')
    expect(text).not.toContain('{slow}words{/slow}')
    expect(text.endsWith('Say (pause, 2 seconds) (start slow)this(end slow).\n')).toBe(true)
  })
})

describe('times in words', () => {
  test.each([[1, '1 second'], [2, '2 seconds'], [2.5, '2.5 seconds'], [60, '1 minute'], [90, '1 minute 30 seconds'], [180, '3 minutes']])('%s', (s, text) => {
    expect(secondsInWords(s)).toBe(text)
  })
})
