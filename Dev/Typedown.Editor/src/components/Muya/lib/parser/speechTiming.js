// Speech marks: how long a talk takes (docs/speech-marks-design.md, section 4).
//
// Rules and arithmetic only: no model, no network, nothing learned. The input is the text of the document; the steps are
// those of the design:
//   1. the text as if every review change were accepted (added text is spoken, deleted text and comments are not);
//   2. the marks are found (speech.js), code and front matter are left out;
//   3. what is spoken: paragraphs, list items, quotes and the text of links; not headings (unless asked), code, images,
//      URLs, table cells, review comments and the notes of {cue} and {tone};
//   4. words are counted: a run of letters or digits, with inner apostrophes and hyphens ("don't", "well-known") as one;
//   5. the time of a stretch of words is words / (words per minute x speed) x 60 seconds; pairs of marks that change the
//      pace multiply, and a pace a user defined with "+0.3s" adds those seconds after every word;
//   6. the seconds of {beat}, {pause}, {wait} and of the pauses a user defined are added;
//   7. the talk is grouped by section (a heading and everything up to the next heading of the same or a higher level) and
//      each section is compared with its {budget}.
// Pure functions on text, so everything here can be tested alone. A word count is a rough measure of speaking time: see the
// design for how rough.

import {
  matchMark, lookupMark, collectDefinitions, proseLines, stripMarks, CODE_SPAN, isEscaped, WPM_MIN, WPM_MAX
} from './speech'

export const DEFAULT_WPM = 130
export const AMBER_LIMIT = 1.1 // a section up to 10 % over its budget is amber, above that red

const clampWpm = wpm => {
  const n = Number(wpm)
  return Number.isFinite(n) ? Math.min(WPM_MAX, Math.max(WPM_MIN, n)) : DEFAULT_WPM
}

// Review changes accepted, as ReviewMarks.Resolve does for the review function: an addition is kept, a deletion goes, a
// substitution becomes its new text, a highlight keeps its text, a comment goes.
export const acceptChanges = text => text
  .replace(/\{~~([\s\S]*?)~>([\s\S]*?)~~\}/g, '$2')
  .replace(/\{\+\+([\s\S]*?)\+\+\}/g, '$1')
  .replace(/\{--([\s\S]*?)--\}/g, '')
  .replace(/\{==([\s\S]*?)==\}/g, '$1')
  .replace(/\{>>([\s\S]*?)<<\}/g, '')

const WORD = /[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*/gu

export const countWords = text => (text.match(WORD) || []).length

// The words of a stretch of text that are spoken: what the Markdown around them says is taken out.
export const spoken = text => text
  .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ') // an image and its alt text
  .replace(/!\[[^\]]*\]\[[^\]]*\]/g, ' ')
  .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1') // a link: its text
  .replace(/\[([^\]]*)\]\[[^\]]*\]/g, '$1')
  .replace(/\[\^[^\]]+\]/g, ' ') // a footnote mark
  .replace(/<(?:https?:\/\/|mailto:)[^>]*>/gi, ' ') // an address in brackets
  .replace(/(?:https?:\/\/|www\.)\S+/gi, ' ') // an address
  .replace(/`+[^`]*`+/g, ' ') // code
  .replace(/\$[^$\n]+\$/g, ' ') // math
  .replace(/<\/?[A-Za-z][^>]*>/g, ' ') // a tag
  .replace(/\\([\\`*_{}[\]()#+\-.!|~>$])/g, '$1')
  .replace(/[*~]+/g, '')
  .replace(/(^|[^\p{L}\p{N}])_+|_+(?=[^\p{L}\p{N}]|$)/gu, '$1')

const CLOSER = /^\{\/([A-Za-z][A-Za-z0-9-]*)\}/

// A paragraph as pieces: the text between marks, the marks, and the closers of pairs. Code spans are not markup.
export const tokenize = (text, defs) => {
  const tokens = []
  let buffer = ''
  let i = 0
  const flush = () => {
    if (buffer) tokens.push({ type: 'text', text: buffer })
    buffer = ''
  }
  while (i < text.length) {
    const c = text[i]
    if (c === '`' && !isEscaped(text, i)) {
      const span = CODE_SPAN.exec(text.substring(i))
      const length = span ? span[0].length : 1
      buffer += text.substring(i, i + length)
      i += length
      continue
    }
    // an escaped brace is plain text, not the start of a mark
    if (c === '{' && !isEscaped(text, i)) {
      const rest = text.substring(i)
      const mark = matchMark(rest, defs)
      if (mark) {
        flush()
        tokens.push({ type: 'mark', mark })
        i += mark.raw.length
        continue
      }
      const closer = CLOSER.exec(rest)
      const entry = closer && lookupMark(closer[1], defs)
      if (entry && entry.role === 'pair') {
        flush()
        tokens.push({ type: 'close', name: entry.name })
        i += closer[0].length
        continue
      }
    }
    buffer += c
    i++
  }
  flush()
  return tokens
}

// `speedWords`, `loudWords` and `softWords` weigh the words by the speed they are spoken at and by whether they are inside
// {loud} or {soft}: they are what the shape of the talk is drawn from.
const nothing = () => ({ words: 0, seconds: 0, pause: 0, audience: 0, budget: 0, speedWords: 0, loudWords: 0, softWords: 0 })

const sum = (a, b) => ({
  words: a.words + b.words,
  seconds: a.seconds + b.seconds,
  pause: a.pause + b.pause,
  audience: a.audience + b.audience,
  budget: a.budget + b.budget,
  speedWords: a.speedWords + b.speedWords,
  loudWords: a.loudWords + b.loudWords,
  softWords: a.softWords + b.softWords
})

// The time of one paragraph. `run` holds what goes on from one paragraph to the next: the definitions of the document and
// the words per minute in force ({wpm 140} changes it from there on). `wordsSpoken` is false for a heading that is not
// read aloud: its marks still count (a {budget} in a heading), its words do not.
// -> { words, seconds, pause, audience, budget }, seconds being everything: words and pauses.
// `trace`, an array, is given what happens in order (the teleprompter is made from it): { type: 'words', text, words, seconds, stack },
// { type: 'pause', seconds, audience, name, note }, { type: 'cue', name, text }, { type: 'open', ... } and { type: 'close', name }.
export const timeParagraph = (text, run, wordsSpoken = true, trace = null) => {
  const out = nothing()
  const stack = []
  for (const token of tokenize(text, run.defs)) {
    if (token.type === 'text') {
      if (!wordsSpoken) continue
      const words = countWords(spoken(token.text))
      if (!words) {
        if (trace) trace.push({ type: 'words', text: token.text, words: 0, seconds: 0, stack: stack.map(s => ({ ...s })) })
        continue
      }
      let factor = 1
      let extra = 0
      for (const open of stack) {
        factor *= open.speed
        extra += open.perWord
      }
      out.words += words
      out.speedWords += words * factor
      if (stack.some(open => open.name === 'loud')) out.loudWords += words
      if (stack.some(open => open.name === 'soft')) out.softWords += words
      const own = (words / (run.wpm * factor)) * 60 + words * extra
      out.seconds += own
      if (trace) trace.push({ type: 'words', text: token.text, words, seconds: own, stack: stack.map(s => ({ ...s })) })
    } else if (token.type === 'close') {
      const at = stack.map(s => s.name).lastIndexOf(token.name)
      if (at >= 0) stack.length = at
      if (trace) trace.push({ type: 'close', name: token.name })
    } else {
      const { mark } = token
      if (mark.role === 'pair') {
        stack.push({ name: mark.name, speed: mark.entry.speed || 1, perWord: mark.entry.perWord || 0, kind: mark.entry.kind, user: !!mark.entry.user, text: mark.text || '' })
        if (trace) trace.push({ type: 'open', name: mark.name, kind: mark.entry.kind, user: !!mark.entry.user, text: mark.text || '', meaning: mark.entry.meaning || '' })
      } else if (mark.role === 'point') {
        if (mark.entry.kind === 'pause') {
          const seconds = mark.seconds || mark.entry.seconds || 0
          out.seconds += seconds
          out.pause += seconds
          if (mark.entry.audience) out.audience += seconds
          if (trace) trace.push({ type: 'pause', seconds, audience: !!mark.entry.audience, name: mark.name, user: !!mark.entry.user, note: mark.text || '' })
        } else if (mark.entry.kind === 'note') {
          if (trace) trace.push({ type: 'cue', name: mark.name, user: !!mark.entry.user, text: mark.text || '', meaning: mark.entry.meaning || '' })
        } else if (mark.name === 'wpm') {
          run.wpm = clampWpm(mark.number)
          if (run.firstWpm === null) run.firstWpm = run.wpm
        } else if (mark.name === 'budget') {
          out.budget += mark.seconds
        }
      }
    }
  }
  return out
}

// How long a stretch of text takes on its own: the review changes accepted, the marks in it counted. For the preview of
// the Speech ring ("0:05 -> 0:09"): the same arithmetic as the whole talk. `defs` are the definitions in force.
export const estimateText = (text, defs, wpm = DEFAULT_WPM) =>
  timeParagraph(acceptChanges(text), { defs, wpm: clampWpm(wpm), firstWpm: null }).seconds

// The words per minute in force after a stretch of text that may hold {wpm N} marks (the last one counts), starting from `wpm`.
// For the preview of the ring: a stretch is timed at the pace of the place it is in, not at the first {wpm} of the document.
export const wpmAfter = (text, defs, wpm = DEFAULT_WPM) => {
  let current = clampWpm(wpm)
  for (const token of tokenize(text, defs)) {
    if (token.type === 'mark' && token.mark.name === 'wpm') current = clampWpm(token.mark.number)
  }
  return current
}

// 90 -> "1:30", 3725 -> "1:02:05": whole seconds, rounded, the same in every language.
export const formatClock = seconds => {
  const total = Math.max(0, Math.round(seconds))
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const s = total % 60
  const two = n => String(n).padStart(2, '0')
  return h ? `${h}:${two(m)}:${two(s)}` : `${m}:${two(s)}`
}

// green up to the budget, amber up to 10 % over it, red above; '' when there is no budget
export const trafficLight = (seconds, budget) => {
  if (!(budget > 0)) return ''
  const ratio = seconds / budget
  return ratio <= 1 ? 'green' : ratio <= AMBER_LIMIT ? 'amber' : 'red'
}

const ATX = /^ {0,3}(#{1,6})(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$/
const SETEXT = /^ {0,3}(=+|-+)[ \t]*$/
const RULE = /^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$/
const LIST = /^[ \t]*(?:[-*+]|\d{1,9}[.)])[ \t]+(.*)$/
const TASK = /^\[[ xX]\][ \t]+/
const TABLE_SEPARATOR = /^[ \t]*\|?(?:[ \t]*:?-+:?[ \t]*\|)+[ \t]*(?::?-*:?)?[ \t]*\|?[ \t]*$/

// How the page finds a paragraph again to scroll to it: the start of its text as the editor shows it (the paragraph blocks
// of the editor hold the text of the file, without the marker of a list item or a quote), and which of the paragraphs
// that start the same way it is.
export const locatorOf = text => text.trim().substring(0, 60)

const titleOf = (text, defs) => stripMarks(text, defs, true)
  .replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
  .replace(/[*_~`]/g, '')
  .replace(/\s+/g, ' ')
  .trim()

// The whole talk. `options`: { wpm (the baseline of the speaker, default 130), headingsSpoken (default false) }.
// -> {
//      words, seconds, pause, audience,          the whole talk
//      wpm, wpmInDocument,                       the baseline at the start, and whether the document says it ({wpm N})
//      budget, light, over,                      the budget of the whole talk (0 when there is none), its light, and the
//                                                seconds over it (negative: to spare)
//      sections: [{ title, level, line, words, seconds, pause, audience, budget, light, over }],   one per heading, with
//                                                the time of everything under it, subsections included
//      paragraphs: [{ line, words, seconds, pause, audience }]                  for the shape of the talk
//    }
export const computeTiming = (markdown, options = {}) => {
  const baseline = clampWpm(options.wpm === undefined ? DEFAULT_WPM : options.wpm)
  const headingsSpoken = !!options.headingsSpoken
  const accepted = acceptChanges(markdown)
  const lines = accepted.split(/\r?\n/)
  const prose = proseLines(lines)
  const { defs } = collectDefinitions(lines.filter((_, n) => prose[n]))
  const run = { defs, wpm: baseline, firstWpm: null }

  const sections = [{ title: '', level: 0, line: 0, own: nothing() }]
  const paragraphs = []
  const seen = new Map()
  const place = text => {
    const prefix = locatorOf(text)
    const nth = seen.get(prefix) || 0
    seen.set(prefix, nth + 1)
    return { prefix, nth }
  }
  let current = sections[0]
  let para = []
  let paraLine = 0
  const addTo = (section, t) => { section.own = sum(section.own, t) }
  const flush = () => {
    if (!para.length) return
    const t = timeParagraph(para.join(' '), run)
    addTo(current, t)
    paragraphs.push({
      line: paraLine,
      ...place(para[0]),
      text: para.join(' '),
      words: t.words,
      seconds: t.seconds,
      pause: t.pause,
      audience: t.audience,
      // the pace the words are spoken at, against the baseline (1 is the baseline), and how loud (+1) or soft (-1) they are
      speed: t.words ? t.speedWords / t.words : 1,
      volume: t.words ? (t.loudWords - t.softWords) / t.words : 0
    })
    para = []
  }
  const heading = (level, text, n) => {
    flush()
    const section = { title: titleOf(text, defs), level, line: n, own: nothing(), text, ...place(lines[n]) }
    sections.push(section)
    current = section
    addTo(section, timeParagraph(text, run, headingsSpoken))
  }

  for (let n = 0; n < lines.length; n++) {
    if (!prose[n]) { flush(); continue }
    let line = lines[n]
    if (!line.trim()) { flush(); continue }
    // a table: its cells are not spoken (the line with the dashes follows the first row)
    if (!para.length && line.includes('|') && n + 1 < lines.length && lines[n + 1].includes('|') && TABLE_SEPARATOR.test(lines[n + 1])) {
      while (n < lines.length && lines[n].trim() && prose[n]) n++
      continue
    }
    const atx = ATX.exec(line)
    if (atx) { heading(atx[1].length, atx[2] || '', n); continue }
    // a line of text with a line of = or - under it is a heading too
    if (!para.length && !LIST.test(line) && !RULE.test(line) && n + 1 < lines.length && prose[n + 1]) {
      const setext = SETEXT.exec(lines[n + 1])
      if (setext) { heading(setext[1][0] === '=' ? 1 : 2, line.trim(), n); n++; continue }
    }
    if (RULE.test(line)) { flush(); continue }
    line = line.replace(/^( {0,3}>[ \t]?)+/, '')
    if (!line.trim()) { flush(); continue }
    const item = LIST.exec(line)
    if (item) {
      flush()
      line = item[1].replace(TASK, '')
    }
    if (!para.length) paraLine = n
    para.push(line.trim())
  }
  flush()

  // a section holds what is under it: the sections after it that are deeper
  const rows = sections.map((section, i) => {
    let total = section.own
    for (let j = i + 1; j < sections.length && sections[j].level > section.level; j++) total = sum(total, sections[j].own)
    return { section, total }
  })
  const row = ({ section, total }) => ({
    title: section.title,
    level: section.level,
    line: section.line,
    prefix: section.prefix,
    nth: section.nth,
    text: section.text,
    words: total.words,
    seconds: total.seconds,
    pause: total.pause,
    audience: total.audience,
    budget: section.own.budget,
    light: trafficLight(total.seconds, section.own.budget),
    over: section.own.budget > 0 ? total.seconds - section.own.budget : 0
  })
  const listed = rows.slice(1).map(row)

  // the budget of the whole talk: one written before the first heading, else those of the sections that are not inside
  // another section
  let budget = sections[0].own.budget
  if (!(budget > 0)) {
    const open = []
    budget = 0
    for (const section of sections.slice(1)) {
      while (open.length && open[open.length - 1] >= section.level) open.pop()
      if (!open.length) budget += section.own.budget
      open.push(section.level)
    }
  }
  const whole = rows[0].total
  return {
    words: whole.words,
    seconds: whole.seconds,
    pause: whole.pause,
    audience: whole.audience,
    wpm: run.firstWpm === null ? baseline : run.firstWpm,
    wpmInDocument: run.firstWpm !== null,
    budget,
    light: trafficLight(whole.seconds, budget),
    over: budget > 0 ? whole.seconds - budget : 0,
    sections: listed,
    paragraphs,
    defs
  }
}

// The paragraphs as at most `max` bars for the shape of the talk (docs/speech-marks-design.md, 5.6): in a long talk
// neighbouring paragraphs are put together (their times added, their pace and volume weighted by their words). A bar keeps
// the place of the first paragraph in it.
export const shapeBars = (paragraphs, max = 120) => {
  if (paragraphs.length <= max) return paragraphs.map(p => ({ ...p, count: 1 }))
  const size = Math.ceil(paragraphs.length / max)
  const bars = []
  for (let i = 0; i < paragraphs.length; i += size) {
    const group = paragraphs.slice(i, i + size)
    const words = group.reduce((n, p) => n + p.words, 0)
    bars.push({
      line: group[0].line,
      prefix: group[0].prefix,
      nth: group[0].nth,
      words,
      seconds: group.reduce((n, p) => n + p.seconds, 0),
      pause: group.reduce((n, p) => n + p.pause, 0),
      audience: group.reduce((n, p) => n + p.audience, 0),
      speed: words ? group.reduce((n, p) => n + p.speed * p.words, 0) / words : 1,
      volume: words ? group.reduce((n, p) => n + p.volume * p.words, 0) / words : 0,
      count: group.length
    })
  }
  return bars
}
