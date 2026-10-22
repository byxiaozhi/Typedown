// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import {
  acceptChanges, countWords, spoken, computeTiming, estimateText, wpmAfter, formatClock, trafficLight, shapeBars, DEFAULT_WPM
} from './speechTiming'
import { collectDefinitions } from './speech'

// n words that are all one word each; at 130 words per minute 130 of them take a minute
const words = n => Array.from({ length: n }, () => 'word').join(' ')
const time = (text, options) => computeTiming(text, options)
const seconds = (text, options) => time(text, options).seconds

describe('words', () => {
  test.each([
    ['one two three', 3],
    ["don't stop", 2],
    ['a well-known fact', 3],
    ['in 2026 it was 3 percent', 6],
    // a run of digits is one word, so a decimal number is two (the rule of the design, a rough measure)
    ['it was 3.5 percent', 5],
    ['', 0],
    ['   ', 0],
    ['—  …', 0],
    ['Árvíztűrő tükörfúrógép', 2],
    ['Zażółć gęślą jaźń', 3]
  ])('%s', (text, count) => expect(countWords(text)).toBe(count))
})

describe('what is spoken in a line', () => {
  test('a link is its text, an image and an address are not spoken', () => {
    expect(countWords(spoken('click [this page](https://example.com/x) now'))).toBe(4)
    expect(countWords(spoken('see ![a very long alt text](a.png) here'))).toBe(2)
    expect(countWords(spoken('visit https://example.com/some/long/path or www.example.org today'))).toBe(3)
    expect(countWords(spoken('mail <https://example.com> please'))).toBe(2)
  })

  test('code, math, tags and footnote marks are not spoken, emphasis marks are not words', () => {
    expect(countWords(spoken('run `npm install now` first'))).toBe(2)
    expect(countWords(spoken('the value $x_i + y$ is small'))).toBe(4)
    expect(countWords(spoken('a <b>bold</b> word[^1]'))).toBe(3)
    expect(countWords(spoken('**very** _important_ ~~old~~ text'))).toBe(4)
    expect(countWords(spoken('keep snake_case together'))).toBe(4)
  })
})

describe('review changes first', () => {
  test('added text is spoken, deleted text and comments are not', () => {
    expect(acceptChanges('a {++b ++}c {--d --}e {~~f~>g~~} {==h==}{>>note<<}')).toBe('a b c e g h')
    expect(seconds(`${words(65)} {++${words(65)}++} {--${words(130)}--} {>>${words(130)}<<}`)).toBeCloseTo(60, 5)
  })
})

describe('time of words', () => {
  test('130 words at 130 per minute take a minute, and the baseline is the speaker\'s', () => {
    expect(DEFAULT_WPM).toBe(130)
    expect(seconds(words(130))).toBeCloseTo(60, 5)
    expect(seconds(words(130), { wpm: 65 })).toBeCloseTo(120, 5)
    expect(seconds(words(130), { wpm: 260 })).toBeCloseTo(30, 5)
  })

  test('a baseline outside 40 to 400 is brought into it', () => {
    expect(seconds(words(40), { wpm: 1 })).toBeCloseTo(60, 5)
    expect(seconds(words(400), { wpm: 5000 })).toBeCloseTo(60, 5)
    expect(seconds(words(130), { wpm: 'nonsense' })).toBeCloseTo(60, 5)
  })

  test('slow and fast change the speed, and what is outside a pair is not changed', () => {
    expect(seconds(`{slow}${words(130)}{/slow}`)).toBeCloseTo(80, 5)
    expect(seconds(`{fast}${words(130)}{/fast}`)).toBeCloseTo(48, 5)
    expect(seconds(`${words(130)} {slow}${words(13)}{/slow}`)).toBeCloseTo(60 + 8, 5)
  })

  test('nested pairs multiply: slow inside fast is 0.75 x 1.25', () => {
    expect(seconds(`{fast}{slow}${words(130)}{/slow}{/fast}`)).toBeCloseTo(60 / 0.9375, 5)
    expect(seconds(`{fast}${words(65)} {slow}${words(65)}{/slow}{/fast}`)).toBeCloseTo(30 / 1.25 + 30 / 0.9375, 5)
  })

  test('a pair that is not closed runs to the end of the paragraph and no further', () => {
    expect(seconds(`{slow}${words(130)}\n\n${words(130)}`)).toBeCloseTo(80 + 60, 5)
  })

  test('marks that only style or cue do not change the time, and their notes are not spoken', () => {
    expect(seconds(`{loud}${words(130)}{/loud}`)).toBeCloseTo(60, 5)
    expect(seconds(`{tone: some very long note about the way to say it}${words(130)}{/tone}`)).toBeCloseTo(60, 5)
    expect(seconds(`{cue: look at the back row and smile warmly} ${words(130)}`)).toBeCloseTo(60, 5)
  })

  test('the pauses are added: beat 0.5 s, pause 1 s or the length written, wait 3 s, and counted as audience time', () => {
    const t = time('{beat} {pause} {pause 2.5s} {wait} {wait 10s}')
    expect(t.seconds).toBeCloseTo(0.5 + 1 + 2.5 + 3 + 10, 5)
    expect(t.pause).toBeCloseTo(17, 5)
    expect(t.audience).toBeCloseTo(13, 5)
    expect(t.words).toBe(0)
  })

  test('{wpm N} changes the baseline from there on, across paragraphs', () => {
    const t = time(`${words(130)}\n\n{wpm 65}\n\n${words(65)}`)
    expect(t.seconds).toBeCloseTo(60 + 60, 5)
    expect(t.wpm).toBe(65)
    expect(t.wpmInDocument).toBe(true)
    expect(time('x').wpmInDocument).toBe(false)
    expect(time('x', { wpm: 150 }).wpm).toBe(150)
  })
})

describe('marks a user defined', () => {
  const defs = [
    '{define very-slow pace 50%: half speed}',
    '{define word-by-word pace 60% +0.3s: one word at a time}',
    '{define long-pause pause 5s: before the key line}',
    '{define whisper span: barely audible}',
    '{define wave note: wave}',
    '{define slow pace 60%}'
  ].join('\n')

  test('a pace by its percentage, and the seconds added after every word', () => {
    expect(seconds(`${defs}\n\n{very-slow}${words(130)}{/very-slow}`)).toBeCloseTo(120, 5)
    // the example of the design: ten words take 10 / 130 x 60 / 0.6 = 7.7 s, plus 3 s
    expect(seconds(`${defs}\n\n{word-by-word}${words(10)}{/word-by-word}`)).toBeCloseTo(7.6923 + 3, 3)
  })

  test('a pause of the user adds its seconds, a style or a cue adds none', () => {
    expect(seconds(`${defs}\n\n{long-pause} {long-pause 8s} {wave} {whisper}x{/whisper}`)).toBeCloseTo(13 + 60 / 130, 5)
  })

  test('a built-in word with the numbers the document changed for itself', () => {
    expect(seconds(`${defs}\n\n{slow}${words(130)}{/slow}`)).toBeCloseTo(60 / 0.6, 5)
  })

  test('a word the document does not define is no mark and stays text', () => {
    expect(seconds('{very-slow}one two{/very-slow}')).toBeCloseTo((4 / 130) * 60, 5)
  })
})

describe('what is not spoken', () => {
  test('headings are not spoken unless asked, and the marks in them still count', () => {
    expect(seconds(`# ${words(130)}\n\n${words(130)}`)).toBeCloseTo(60, 5)
    expect(seconds(`# ${words(130)}\n\n${words(130)}`, { headingsSpoken: true })).toBeCloseTo(120, 5)
    expect(time('# Title {pause 4s}').seconds).toBeCloseTo(4, 5)
  })

  test('code, front matter, images, tables and html comments are left out', () => {
    const text = [
      '---', `title: ${words(130)}`, '---', '',
      '```js', words(130), '```', '',
      `    ${words(130)}`, '',
      `![${words(130)}](a.png)`, '',
      '| a | b |', '|---|---|', `| ${words(130)} | x |`, '',
      words(130)
    ].join('\n')
    expect(seconds(text)).toBeCloseTo(60, 5)
  })

  test('marks in code are not marks', () => {
    expect(seconds('```\n{pause 9s}\n```\n\nword `{pause 9s}` word')).toBeCloseTo((2 / 130) * 60, 5)
  })

  test('list items, tasks and quotes are spoken, with their markers left out', () => {
    expect(countWordsOf('- one two\n- [ ] three four\n1. five six\n> seven eight\n> > nine ten')).toBe(10)
  })

  test('a definition line is no speech', () => {
    expect(time('{define slow pace 60%}\n{define wave note: wave}').words).toBe(0)
  })
})

function countWordsOf (text) { return time(text).words }

describe('sections and budgets', () => {
  const talk = [
    `# Opening {budget 1m}`, '', words(130), '',
    '# Middle {budget 2m}', '', words(130), '',
    '## Detail', '', words(130), '',
    '# Close', '', words(65)
  ].join('\n')

  test('a section holds everything up to the next heading of the same or a higher level', () => {
    const t = time(talk)
    expect(t.sections.map(s => [s.title, s.level, Math.round(s.seconds)])).toEqual([
      ['Opening', 1, 60], ['Middle', 1, 120], ['Detail', 2, 60], ['Close', 1, 30]
    ])
    expect(t.seconds).toBeCloseTo(60 + 120 + 30, 5)
    expect(t.words).toBe(130 * 3 + 65)
  })

  test('each section against its own budget, with a light and the seconds over or to spare', () => {
    const [opening, middle, , close] = time(talk).sections
    expect(opening.budget).toBe(60)
    expect(opening.light).toBe('green')
    expect(opening.over).toBeCloseTo(0, 5)
    expect(middle.budget).toBe(120)
    expect(middle.light).toBe('green')
    expect(close.budget).toBe(0)
    expect(close.light).toBe('')
  })

  test('the light is green up to the budget, amber up to 10 % over, red above', () => {
    expect(trafficLight(60, 60)).toBe('green')
    expect(trafficLight(30, 60)).toBe('green')
    expect(trafficLight(66, 60)).toBe('amber')
    expect(trafficLight(66.5, 60)).toBe('red')
    expect(trafficLight(100, 0)).toBe('')
    const over = time(`# A {budget 1m}\n\n${words(143)}`).sections[0]
    expect(over.light).toBe('amber')
    expect(over.over).toBeCloseTo(6, 5)
    expect(time(`# A {budget 1m}\n\n${words(195)}`).sections[0].light).toBe('red')
  })

  test('the budget of the whole talk: one before the first heading, else the sections that are not inside another', () => {
    expect(time(`{budget 10m}\n\n${talk}`).budget).toBe(600)
    expect(time(talk).budget).toBe(180)
    expect(time(`# A {budget 1m}\n\n## B {budget 5m}\n\n# C {budget 2m}`).budget).toBe(180)
    expect(time('no budget at all').budget).toBe(0)
  })

  test('the total has a light of its own', () => {
    const t = time(`{budget 1m}\n\n${words(130)}\n\n${words(13)}`)
    expect(t.light).toBe('amber')
    expect(t.over).toBeCloseTo(6, 5)
  })

  test('text before the first heading counts in the total, not in a row', () => {
    const t = time(`${words(130)}\n\n# A\n\n${words(65)}`)
    expect(t.sections).toHaveLength(1)
    expect(t.seconds).toBeCloseTo(90, 5)
    expect(t.sections[0].seconds).toBeCloseTo(30, 5)
  })

  test('a heading written with a line of = or - under it is a heading', () => {
    const t = time(`Title\n=====\n\n${words(130)}\n\nSub\n---\n\n${words(65)}`)
    expect(t.sections.map(s => [s.title, s.level])).toEqual([['Title', 1], ['Sub', 2]])
    expect(t.seconds).toBeCloseTo(90, 5)
  })

  test('the title is plain text: the marks and the Markdown are taken out', () => {
    expect(time('## Open **now** with a [link](http://x.y) {budget 3m} {pause}').sections[0].title).toBe('Open now with a link')
  })

  test('the lines of the sections and of the paragraphs are where they are in the text', () => {
    const t = time('# A\n\ntext one\n\n## B\n\ntext two')
    expect(t.sections.map(s => s.line)).toEqual([0, 4])
    expect(t.paragraphs.map(p => p.line)).toEqual([2, 6])
  })
})

describe('a stretch of text on its own (the preview of the ring)', () => {
  test('the same arithmetic, with the definitions in force', () => {
    const { defs } = collectDefinitions(['{define very-slow pace 50%: half speed}'])
    expect(estimateText(words(130), defs)).toBeCloseTo(60, 5)
    expect(estimateText(`{very-slow}${words(130)}{/very-slow}`, defs)).toBeCloseTo(120, 5)
    expect(estimateText(`{slow}${words(130)}{/slow}{wait 3s: laugh}`, undefined, 130)).toBeCloseTo(83, 5)
    expect(estimateText(words(130), undefined, 65)).toBeCloseTo(120, 5)
    expect(estimateText(`{--${words(130)}--}${words(65)}`, undefined)).toBeCloseTo(30, 5)
  })
})

describe('the pace in force after a stretch of text', () => {
  test('the last {wpm N} counts, none leaves the pace as it was, one out of range is no mark', () => {
    expect(wpmAfter('no marks here', undefined, 130)).toBe(130)
    expect(wpmAfter('{wpm 100} text {wpm 150} more', undefined, 130)).toBe(150)
    expect(wpmAfter('{wpm 5000}', undefined, 130)).toBe(130)
    expect(wpmAfter('a `{wpm 100}` in code', undefined, 120)).toBe(120)
    // an escaped brace is text
    expect(wpmAfter('see \\{wpm 65} here', undefined, 120)).toBe(120)
    // the words of the escaped text are spoken (pause, 9s, word) and no pause is added
    expect(seconds('\\{pause 9s} word', undefined)).toBeCloseTo((3 / 130) * 60, 5)
  })

  test('so a stretch after a later {wpm} is timed at that pace', () => {
    const before = wpmAfter('first {wpm 65} second', undefined, 130)
    expect(estimateText(words(65), undefined, before)).toBeCloseTo(60, 5)
  })
})

describe('the clock', () => {
  test.each([
    [0, '0:00'], [5, '0:05'], [59.6, '1:00'], [90, '1:30'], [600, '10:00'], [3599, '59:59'], [3725, '1:02:05'], [-4, '0:00']
  ])('%s s is %s', (s, text) => expect(formatClock(s)).toBe(text))
})

describe('the shape of the talk: what each paragraph is like', () => {
  test('the pace against the baseline and how loud, weighted by the words', () => {
    const t = time(`{fast}${words(10)}{/fast} ${words(10)}\n\n{slow}${words(10)}{/slow}\n\n{loud}${words(10)}{/loud}\n\n{soft}${words(5)}{/soft} ${words(5)}`)
    const [a, b, c, d] = t.paragraphs
    expect(a.speed).toBeCloseTo(1.125, 5)
    expect(b.speed).toBeCloseTo(0.75, 5)
    expect(c.volume).toBeCloseTo(1, 5)
    expect(d.volume).toBeCloseTo(-0.5, 5)
    expect(c.speed).toBeCloseTo(1, 5)
  })

  test('pauses and audience time of a paragraph', () => {
    const p = time(`${words(13)} {pause 2s} {wait 3s}`).paragraphs[0]
    expect(p.pause).toBeCloseTo(5, 5)
    expect(p.audience).toBeCloseTo(3, 5)
    expect(p.seconds).toBeCloseTo(6 + 5, 5)
  })

  test('where a paragraph is, for scrolling to it: its start as the editor shows it, and which of the same start it is', () => {
    const t = time('# Part\n\n- first item here\n- first item here\n\n> quoted words\n\n## Part')
    expect(t.paragraphs.map(p => [p.prefix, p.nth])).toEqual([['first item here', 0], ['first item here', 1], ['quoted words', 0]])
    expect(t.sections.map(s => [s.prefix, s.nth])).toEqual([['# Part', 0], ['## Part', 0]])
    expect(time(`${'x'.repeat(100)}`).paragraphs[0].prefix).toHaveLength(60)
  })

  test('a long talk is drawn with at most so many bars, neighbours put together', () => {
    const text = Array.from({ length: 300 }, (_, i) => (i % 2 ? `{fast}${words(13)}{/fast}` : words(13))).join('\n\n')
    const t = time(text)
    const bars = shapeBars(t.paragraphs, 100)
    expect(bars.length).toBeLessThanOrEqual(100)
    expect(bars.reduce((n, b) => n + b.count, 0)).toBe(300)
    expect(bars.reduce((n, b) => n + b.seconds, 0)).toBeCloseTo(t.seconds, 5)
    expect(bars[0].line).toBe(t.paragraphs[0].line)
    // three paragraphs to a bar: normal, quick, normal and then quick, normal, quick
    expect(bars[0].speed).toBeCloseTo((1 + 1.25 + 1) / 3, 5)
    expect(bars[1].speed).toBeCloseTo((1.25 + 1 + 1.25) / 3, 5)
    expect(shapeBars(t.paragraphs.slice(0, 5), 100)).toHaveLength(5)
  })
})

describe('large and odd texts', () => {
  test('an empty text, Windows line endings and a very long text', () => {
    expect(time('').seconds).toBe(0)
    expect(time('').sections).toEqual([])
    expect(time(`${words(130)}\r\n\r\n${words(130)}`).seconds).toBeCloseTo(120, 5)
    // a long talk is timed in one pass (how fast is not asserted: it depends on the machine)
    const long = time(Array.from({ length: 3000 }, (_, i) => `## Section ${i} {budget 1m}\n\n${words(60)} {pause}`).join('\n\n'))
    expect(long.sections).toHaveLength(3000)
    expect(long.words).toBe(3000 * 60)
  })
})
