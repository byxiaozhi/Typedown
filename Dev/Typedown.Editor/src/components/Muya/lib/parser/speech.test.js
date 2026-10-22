// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { tokenizer, generator } from './index'
import {
  parseDuration, parsePace, parseDefine, collectDefinitions, lookupMark, matchMark,
  describe as describeMark, paceStyle, formatSeconds, definitionsKey, hueOf, stripMarks, findCloser, isEscaped, maskCode
} from './speech'

const speechTokens = tokens => tokens.filter(t => t.type === 'speech')
const tokenize = (src, defs, mode = true) => tokenizer(src, {
  hasBeginRules: false,
  options: { speechMode: mode, speechDefs: defs }
})
const definitions = (...lines) => collectDefinitions(lines).defs

describe('code spans blanked out', () => {
  test('a span is closed by as many backticks as opened it', () => {
    expect(maskCode('a `x` b')).toBe(`a ${' '.repeat(3)} b`)
    // two backticks open it, so a single one inside does not close it and the mark inside is blanked too
    const span = '``x ` {wpm 120}``'
    expect(maskCode(`a ${span} b {wpm 90}`)).toBe(`a ${' '.repeat(span.length)} b {wpm 90}`)
    expect(maskCode('say `{wpm 120}` and {wpm 80}')).toBe(`say ${' '.repeat('`{wpm 120}`'.length)} and {wpm 80}`)
  })

  test('an escaped backtick or one with no closer is text, the length never changes', () => {
    expect(maskCode('a ' + String.fromCharCode(92) + '` b `c')).toBe('a ' + String.fromCharCode(92) + '` b `c')
    expect(maskCode('no code {pause}')).toBe('no code {pause}')
    expect(maskCode('``x` still open')).toBe('``x` still open')
    // a run that nothing closes is text as a whole: its second backtick does not open a span that a later single one closes
    expect(maskCode('``x {wpm 120}`')).toBe('``x {wpm 120}`')
    expect(maskCode('``x {wpm 120} `` {wpm 90}')).toBe(`${' '.repeat('``x {wpm 120} ``'.length)} {wpm 90}`)
  })
})

describe('durations and speeds', () => {
  test.each([
    ['2s', 2], ['1.5s', 1.5], ['1,5s', 1.5], ['1m', 60], ['1m30s', 90], ['0.5s', 0.5], ['2S', 2]
  ])('%s is %s seconds', (text, seconds) => expect(parseDuration(text)).toBe(seconds))

  test.each(['', '2', 's', 'm', '1h', '-2s', '2 s', 'fast', '1s30m'])('%j is not a duration', text => {
    expect(parseDuration(text)).toBeNull()
  })

  test('a speed is a percentage inside the allowed range', () => {
    expect(parsePace('50%')).toBe(0.5)
    expect(parsePace('160 %')).toBe(1.6)
    expect(parsePace('9%')).toBeNull()
    expect(parsePace('301%')).toBeNull()
    expect(parsePace('50')).toBeNull()
  })
})

describe('Speech mode', () => {
  test('is off by default: the marks are plain text', () => {
    const src = 'a {pause 2s} b {slow}c{/slow}'
    const tokens = tokenize(src, undefined, false)
    expect(speechTokens(tokens)).toHaveLength(0)
    expect(generator(tokens)).toBe(src)
  })

  test('the tokenizer called the old way (no options) never reads marks', () => {
    expect(speechTokens(tokenizer('a {pause 2s} b', { hasBeginRules: false }))).toHaveLength(0)
  })
})

describe('single marks', () => {
  test.each([
    ['a {beat} b', 'beat', {}],
    ['a {pause} b', 'pause', {}],
    ['a {pause 2s} b', 'pause', { seconds: 2 }],
    ['a {pause 1m30s} b', 'pause', { seconds: 90 }],
    ['a {pause: let it land} b', 'pause', { text: 'let it land' }],
    ['a {wait 5s: laugh} b', 'wait', { seconds: 5, text: 'laugh' }],
    ['a {wait} b', 'wait', {}],
    ['a {cue: look at the back row} b', 'cue', { text: 'look at the back row' }],
    ['a {wpm 140} b', 'wpm', { number: 140 }],
    ['a {budget 3m} b', 'budget', { seconds: 180 }],
    ['a {PAUSE 2S} b', 'pause', { seconds: 2 }]
  ])('%s is one mark and the text is unchanged', (src, name, expected) => {
    const tokens = tokenize(src)
    const found = speechTokens(tokens)
    expect(found).toHaveLength(1)
    expect(found[0].name).toBe(name)
    expect(found[0].role).toBe('point')
    for (const key of Object.keys(expected)) expect(found[0].info[key]).toBe(expected[key])
    // what is drawn is made from the same text, nothing is lost or added
    expect(generator(tokens)).toBe(src)
  })

  test('the range covers the mark and nothing else', () => {
    const src = 'Good evening {pause 2s} everybody'
    const [token] = speechTokens(tokenize(src))
    expect(src.substring(token.range.start, token.range.end)).toBe('{pause 2s}')
  })

  test('marks follow each other and keep the words between them', () => {
    const src = 'one {beat} two {pause 3s}{wait} three'
    const tokens = tokenize(src)
    expect(speechTokens(tokens).map(t => t.name)).toEqual(['beat', 'pause', 'wait'])
    expect(generator(tokens)).toBe(src)
  })
})

describe('what is not a mark', () => {
  test.each([
    ['a typo', '{pauze 2s}'],
    ['pandoc attributes', '![x](a.png){width=50%}'],
    ['a class', '{.red} text'],
    ['a template', '{{ name }}'],
    ['a known word with a bad value', '{pause fast}'],
    ['a pause that is too short', '{pause 0.01s}'],
    ['a pause that is too long', '{pause 11m}'],
    ['a value where none is allowed', '{beat 2s}'],
    ['a cue without a text', '{cue}'],
    ['a tone without a text', '{tone}x{/tone}'],
    ['a speed outside the range', '{wpm 10}'],
    ['a speed that is not a number', '{wpm fast}'],
    ['a budget without a length', '{budget}'],
    ['a note on a setting', '{wpm 140: why}'],
    ['a lone brace', 'a { b'],
    ['an unfinished mark', '{pause 2s'],
    ['a stray closer', '{/slow} text'],
    ['a name nobody defined', '{very-slow}x{/very-slow}']
  ])('%s stays text', (_, src) => {
    const tokens = tokenize(src)
    expect(speechTokens(tokens)).toHaveLength(0)
    expect(generator(tokens)).toBe(src)
  })

  test('an escaped brace is text', () => {
    const src = 'write \\{pause 2s\\} to pause'
    expect(speechTokens(tokenize(src))).toHaveLength(0)
  })

  test('code is left alone', () => {
    const src = 'use `{pause 2s}` to pause'
    const tokens = tokenize(src)
    expect(speechTokens(tokens)).toHaveLength(0)
    expect(generator(tokens)).toBe(src)
  })

  test('CriticMarkup keeps its own syntax', () => {
    const src = 'a {++added++} b {--gone--} c {==marked==} {>>note<<}'
    const tokens = tokenize(src)
    expect(speechTokens(tokens)).toHaveLength(0)
    expect(tokens.filter(t => t.type === 'critic')).toHaveLength(4)
  })
})

describe('pairs', () => {
  test('a pair holds the text between its marks', () => {
    const src = 'so {slow}very slowly{/slow} now'
    const tokens = tokenize(src)
    const [token] = speechTokens(tokens)
    expect(token.role).toBe('pair')
    expect(token.name).toBe('slow')
    expect(token.open).toBe('{slow}')
    expect(token.close).toBe('{/slow}')
    expect(token.children.map(t => t.raw).join('')).toBe('very slowly')
    expect(src.substring(token.range.start, token.range.end)).toBe('{slow}very slowly{/slow}')
    expect(src.substring(token.children[0].range.start, token.children[0].range.end)).toBe('very slowly')
    expect(generator(tokens)).toBe(src)
  })

  test('the closer may be written in capitals', () => {
    const tokens = tokenize('{LOUD}x{/Loud}')
    expect(speechTokens(tokens)[0].close).toBe('{/Loud}')
  })

  test('a pair that is never closed runs to the end of the paragraph', () => {
    const src = 'before {soft}whispered to the end'
    const tokens = tokenize(src)
    const [token] = speechTokens(tokens)
    expect(token.close).toBe('')
    expect(token.children.map(t => t.raw).join('')).toBe('whispered to the end')
    expect(token.range.end).toBe(src.length)
    expect(generator(tokens)).toBe(src)
  })

  test('a closer written inside a code span does not end the pair', () => {
    const src = '{slow}write `{/slow}` to stop{/slow} now'
    const tokens = tokenize(src)
    const [token] = speechTokens(tokens)
    expect(token.raw).toBe('{slow}write `{/slow}` to stop{/slow}')
    expect(token.children.map(t => t.type)).toContain('inline_code')
    expect(generator(tokens)).toBe(src)
    // two backticks, and a lone backtick that closes nothing
    expect(speechTokens(tokenize('{slow}a ``{/slow}`` b{/slow}'))[0].close).toBe('{/slow}')
    expect(speechTokens(tokenize('{slow}a ` b{/slow}'))[0].raw).toBe('{slow}a ` b{/slow}')
  })

  test('an escaped backtick is text: it does not open a code span', () => {
    const src = '{slow}a \\`{/slow}\\` b'
    const [token] = speechTokens(tokenize(src))
    expect(token.close).toBe('{/slow}')
    expect(token.raw).toBe('{slow}a \\`{/slow}')
    expect(findCloser('\\`{/x}\\`', 0, 'x')).toEqual({ index: 2, raw: '{/x}' })
    expect(isEscaped('\\`', 1)).toBe(true)
    expect(isEscaped('\\\\`', 2)).toBe(false)
    // and in the text without its marks
    expect(stripMarks('a \\`{beat}\\` b')).toBe('a \\`\\` b')
  })

  test('a pair whose only closer is inside code runs to the end of the paragraph', () => {
    const [token] = speechTokens(tokenize('{slow}a `{/slow}` b'))
    expect(token.close).toBe('')
  })

  test('pairs nest and single marks work inside', () => {
    const src = '{slow}a {loud}b{/loud} {pause 2s} c{/slow}'
    const tokens = tokenize(src)
    const [outer] = speechTokens(tokens)
    expect(outer.name).toBe('slow')
    expect(speechTokens(outer.children).map(t => t.name)).toEqual(['loud', 'pause'])
    expect(generator(tokens)).toBe(src)
  })

  test('Markdown inside a pair is still Markdown', () => {
    const [token] = speechTokens(tokenize('{emphasis}**bold** and *em*{/emphasis}'))
    expect(token.children.map(t => t.type)).toEqual(['strong', 'text', 'em'])
  })

  test('a tone carries its text', () => {
    const [token] = speechTokens(tokenize('{tone: dry irony}Of course.{/tone}'))
    expect(token.name).toBe('tone')
    expect(token.info.text).toBe('dry irony')
  })

  test('a mark inside a link text or a change is found', () => {
    const inLink = speechTokens(tokenize('[see {beat} this](http://example.com)')[0].children || [])
    expect(inLink.map(t => t.name)).toEqual(['beat'])
    const change = tokenize('{++add {pause 2s} it++}').find(t => t.type === 'critic')
    expect(speechTokens(change.children).map(t => t.name)).toEqual(['pause'])
  })

  test('a CriticMarkup comment is plain text, the marks in it are not read', () => {
    const tokens = tokenize('{>>@Ann 2026-10-07: add a {pause} here<<}')
    expect(tokens.filter(t => t.type === 'critic')).toHaveLength(1)
    expect(speechTokens(tokens)).toHaveLength(0)
  })

  test('a search match inside a pair is passed on to the tokens drawn from it', () => {
    const src = 'x {slow}one two{/slow} y'
    const at = src.indexOf('two')
    const [token] = speechTokens(tokenizer(src, {
      hasBeginRules: false,
      options: { speechMode: true },
      highlights: [{ start: at, end: at + 3, active: true }]
    }))
    expect(token.children.some(t => t.highlights && t.highlights.length)).toBe(true)
  })
})

describe('definitions', () => {
  test('a definition of each kind', () => {
    expect(parseDefine('very-slow pace 50%: half speed')).toMatchObject({ name: 'very-slow', kind: 'pace', speed: 0.5, perWord: 0, meaning: 'half speed', override: false })
    expect(parseDefine('word-by-word pace 60% +0.3s: one word at a time')).toMatchObject({ speed: 0.6, perWord: 0.3 })
    expect(parseDefine('long-pause pause 5s: before the key line')).toMatchObject({ kind: 'pause', seconds: 5 })
    expect(parseDefine('whisper span: barely audible')).toMatchObject({ kind: 'span', meaning: 'barely audible' })
    expect(parseDefine('wave note: wave to the room')).toMatchObject({ kind: 'note' })
  })

  test('the meaning keeps its colons and spaces', () => {
    expect(parseDefine('aside span: say this: quietly, to one side').meaning).toBe('say this: quietly, to one side')
  })

  test.each([
    ['a name that is too short', 'x pace 50%: m', 'name'],
    ['a name with capitals or a space', 'Very-Slow pace 50%: m', null],
    ['an unknown kind', 'whisper voice: m', 'kind'],
    ['no meaning', 'whisper span', 'meaning'],
    ['no speed', 'whisper pace: m', 'value'],
    ['a speed out of range', 'whisper pace 400%: m', 'value'],
    ['too much added per word', 'whisper pace 50% +3s: m', 'value'],
    ['no length', 'stop pause: m', 'value'],
    ['a value on a style', 'whisper span 50%: m', 'value'],
    ['the word define', 'define span: m', 'name'],
    ['a built-in word with another kind', 'slow span: m', 'builtin'],
    ['a built-in word that cannot change', 'loud span: m', 'builtin']
  ])('%s is not a definition', (_, body, error) => {
    const def = parseDefine(body)
    if (error === null) {
      // capitals are accepted and lower-cased: names do not depend on case
      expect(def.name).toBe('very-slow')
    } else {
      expect(def.error).toBe(error)
    }
  })

  test('a built-in word can change its numbers for one document', () => {
    expect(parseDefine('slow pace 60%')).toMatchObject({ name: 'slow', override: true, speed: 0.6 })
    expect(parseDefine('pause pause 2s')).toMatchObject({ override: true, seconds: 2 })
    const defs = definitions('{define slow pace 60%}', '{define pause pause 2s}')
    expect(lookupMark('slow', defs).speed).toBe(0.6)
    expect(lookupMark('fast', defs).speed).toBe(1.25)
    expect(lookupMark('pause', defs).seconds).toBe(2)
    expect(lookupMark('slow').speed).toBe(0.75)
  })

  test('definitions are collected from all paragraphs, the first of a name wins', () => {
    const { defs, problems } = collectDefinitions([
      '{define very-slow pace 50%: half speed}',
      'text {define whisper span: quiet} more text',
      '{define very-slow pace 40%: again}',
      '{define broken pace}',
      null
    ])
    expect([...defs.keys()]).toEqual(['very-slow', 'whisper'])
    expect(defs.get('very-slow').speed).toBe(0.5)
    expect(problems.map(p => p.error)).toEqual(['twice', 'meaning'])
  })

  test('a definition inside a code span is not one', () => {
    expect(collectDefinitions(['write `{define x pace 50%: m}` here']).defs.size).toBe(0)
  })

  test('a defined name is a mark in the document that defines it, and only there', () => {
    const defs = definitions('{define very-slow pace 50%: half speed}')
    const src = 'so {very-slow}this{/very-slow} now'
    const [token] = speechTokens(tokenize(src, defs))
    expect(token.role).toBe('pair')
    expect(token.info.entry.user).toBe(true)
    expect(token.info.entry.speed).toBe(0.5)
    expect(speechTokens(tokenize(src))).toHaveLength(0)
  })

  test('the kind decides how a defined word is used', () => {
    const defs = definitions(
      '{define very-slow pace 50%: m}', '{define long-pause pause 5s: m}',
      '{define whisper span: m}', '{define wave note: m}'
    )
    expect(['very-slow', 'long-pause', 'whisper', 'wave'].map(n => lookupMark(n, defs).role)).toEqual(['pair', 'point', 'pair', 'point'])
    expect(matchMark('{long-pause}', defs).entry.seconds).toBe(5)
    expect(matchMark('{long-pause 8s}', defs).seconds).toBe(8)
    expect(matchMark('{wave}', defs).role).toBe('point')
    expect(matchMark('{wave 2s}', defs)).toBeNull()
  })

  test('a definition is one chip in the text', () => {
    const src = '{define very-slow pace 50%: half speed}'
    const tokens = tokenize(src)
    const [token] = speechTokens(tokens)
    expect(token.role).toBe('define')
    expect(token.name).toBe('very-slow')
    expect(token.raw).toBe(src)
    expect(generator(tokens)).toBe(src)
    expect(speechTokens(tokenize('{define broken}'))).toHaveLength(0)
  })

  test('the key changes when a definition does', () => {
    const a = definitionsKey(definitions('{define x1 pace 50%: m}'))
    const b = definitionsKey(definitions('{define x1 pace 60%: m}'))
    expect(a).not.toBe(b)
    expect(definitionsKey(undefined)).toBe('')
  })
})

describe('how a mark is shown', () => {
  const chip = (src, defs) => describeMark(matchMark(src, defs), undefined, 'en-US').chip

  test('chips say what the mark does', () => {
    expect(chip('{beat}')).toBe('beat')
    expect(chip('{pause}')).toBe('pause 1 s')
    expect(chip('{pause 2s}')).toBe('pause 2 s')
    expect(chip('{pause 1.5s}')).toBe('pause 1.5 s')
    expect(chip('{pause 1m30s}')).toBe('pause 1 min 30 s')
    expect(chip('{wait}')).toBe('audience 3 s')
    expect(chip('{wait 5s: laugh}')).toBe('audience 5 s')
    expect(chip('{cue: look up}')).toBe('cue: look up')
    expect(chip('{wpm 140}')).toBe('140 words/min')
    expect(chip('{budget 3m}')).toBe('time 3 min')
  })

  test('a definition and a word of the user show their own values', () => {
    const defs = definitions('{define long-pause pause 5s: before the key line}', '{define wave note: wave}')
    expect(chip('{define very-slow pace 50%: half}')).toBe('very-slow = pace 50 %')
    expect(chip('{define word-by-word pace 60% +0.3s: x}')).toBe('word-by-word = pace 60 % +0.3 s')
    expect(chip('{define whisper span: x}')).toBe('whisper = style')
    expect(chip('{long-pause}', defs)).toBe('long-pause 5 s')
    expect(chip('{wave}', defs)).toBe('wave')
    expect(describeMark(matchMark('{long-pause}', defs)).title).toBe('before the key line')
  })

  test('the words come from the interface language', () => {
    const french = { pause: 'pause', wait: 'public', wpmUnit: 'mots/min', budget: 'durée' }
    expect(describeMark(matchMark('{wait 4s}'), french, 'fr-FR').chip).toBe('public 4 s')
    expect(describeMark(matchMark('{budget 3m}'), french, 'fr-FR').chip).toBe('durée 3 min')
    expect(describeMark(matchMark('{pause 1.5s}'), french, 'fr-FR').chip).toBe('pause 1,5 s')
  })

  test('seconds are written in minutes and seconds', () => {
    expect(formatSeconds(90, 'en-US')).toBe('1 min 30 s')
    expect(formatSeconds(60, 'en-US')).toBe('1 min')
    expect(formatSeconds(0.5, 'en-US')).toBe('0.5 s')
  })

  test('slower text is spaced wider, faster text narrower, words apart when a beat follows each', () => {
    const slow = paceStyle(lookupMark('slow'))
    const fast = paceStyle(lookupMark('fast'))
    expect(parseFloat(slow.letterSpacing)).toBeGreaterThan(0)
    expect(parseFloat(fast.letterSpacing)).toBeLessThan(0)
    const wbw = paceStyle(lookupMark('wbw', definitions('{define wbw pace 60% +0.3s: x}')))
    expect(wbw.wordSpacing).toBe('0.45em')
    expect(slow.wordSpacing).toBeUndefined()
  })

  test('a colour from the name is the same every time', () => {
    expect(hueOf('whisper')).toBe(hueOf('whisper'))
    expect(hueOf('whisper')).not.toBe(hueOf('sing-song'))
    expect(hueOf('whisper')).toBeLessThan(360)
  })
})

describe('the text without its marks', () => {
  test('single marks and the marks of a pair go, the words stay', () => {
    expect(stripMarks('Good evening {pause 2s}everybody. {slow}Slowly{/slow} now {cue: look up}and {tone: dry}done{/tone}.'))
      .toBe('Good evening everybody. Slowly now and done.')
  })

  test('definitions go with their meaning', () => {
    const text = '{define very-slow pace 50%: half speed}\nA {very-slow}word{/very-slow}.'
    const defs = definitions(text)
    expect(stripMarks(text, defs)).toBe('\nA word.')
  })

  test('what is not a mark stays', () => {
    const text = 'width {width=50%} and {pauze} and {/pauze} and {x'
    expect(stripMarks(text)).toBe(text)
  })

  test('a closer without its opener goes too: it is not a word', () => {
    expect(stripMarks('one {/slow}two')).toBe('one two')
  })

  test('code is not markup: a mark inside a code span stays', () => {
    expect(stripMarks('write `{pause 2s}` here {beat}now')).toBe('write `{pause 2s}` here now')
    expect(stripMarks('a ` lone backtick {beat}')).toBe('a ` lone backtick ')
  })

  test('a text without braces is returned as it is', () => {
    expect(stripMarks('plain text')).toBe('plain text')
    expect(stripMarks('')).toBe('')
  })
})

describe('the lines the Settings page writes', () => {
  // the starting set of Services/SpeechLibrary.cs, as Caret.ConverterTests checks it is written
  test.each([
    '{define very-slow pace 50%: meaning of very-slow}',
    '{define very-fast pace 160%: meaning of very-fast}',
    '{define word-by-word pace 60% +0.3s: meaning of word-by-word}',
    '{define long-pause pause 5s: meaning of long-pause}',
    '{define whisper span: meaning of whisper}',
    '{define sing-song span: meaning of sing-song}',
    '{define wave note: meaning of wave}',
    '{define very-slow pace 62.5% +0.25s: about half speed}',
    '{define long-pause pause 1.5s: x}'
  ])('%s is a definition', line => {
    const mark = matchMark(line, undefined)
    expect(mark).not.toBeNull()
    expect(mark.role).toBe('define')
  })
})
