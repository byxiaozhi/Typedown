// Speech marks: hints (docs/speech-marks-design.md, 5.6): a short list of plain facts about a talk, never advice on style.
//   over      a section (or the whole talk) is over its budget
//   joke      a {tone: joke} with no {wait} or {pause} right after it, so no room for the laugh
//   fast      more than a minute of {fast} (or any quicker pace) in a row
//   long      a pause longer than 10 seconds (probably a typo: 100s for 10s)
//   unknown   a {word} that looks like a mark but is not one (a typo, or a word the document does not define)
//   twice     a word that is defined twice (the first one is used)
//   baddef    a definition that does not hold (the reason is in `error`)
// Pure functions on the text, tested alone. The hints say what is there and where; what to do about it is the speaker's.

import { computeTiming, tokenize } from './speechTiming'
import { collectDefinitions, WPM_MIN, WPM_MAX } from './speech'

export const LONG_PAUSE = 10 // seconds
export const FAST_RUN = 60 // seconds of a quicker pace in a row

// The words a tone note may say for a joke, in the languages of the interface and Hungarian: the preset notes of the
// Speech card are written in the language the speaker uses, so a talk keeps the word it was made with.
export const JOKE_WORDS = ['joke', 'jokes', 'blague', 'broma', 'chiste', 'żart', 'zart', 'vicc', 'humor', 'humour']
const isJoke = text => JOKE_WORDS.includes((text || '').trim().toLowerCase())

// {name}, {/name}, {name value}, {name: note}: what looks like a mark. Not after a letter, a digit, a backslash or a closing
// brace ("\\frac{a}{b}", "x_{i}"): that is something else.
const LOOKS_LIKE = /(^|[^A-Za-z0-9\\}_^])\{(\/?)([A-Za-z][A-Za-z0-9-]{2,23})(?: [^{}\n:]*)?(?::[^{}\n]*)?\}/g

const maskedText = text => text
  .replace(/`+[^`]*`+/g, m => ' '.repeat(m.length))
  .replace(/\$[^$\n]+\$/g, m => ' '.repeat(m.length))

// -> [{ kind, line, prefix, nth, ...details }] in the order of the text; `line` is where the paragraph starts.
export const computeHints = (markdown, options = {}) => {
  const timing = options.timing || computeTiming(markdown, options)
  const defs = timing.defs
  // every place a hint can point to: the headings and the paragraphs, in the order of the text
  const items = [
    ...timing.sections.map(s => ({ line: s.line, prefix: s.prefix, nth: s.nth, text: s.text || '', heading: true })),
    ...timing.paragraphs.map(p => ({ line: p.line, prefix: p.prefix, nth: p.nth, text: p.text || '', heading: false }))
  ].sort((a, b) => a.line - b.line)
  const where = item => ({ line: item.line, prefix: item.prefix, nth: item.nth })
  const hints = []

  // 1. sections over their budget, and the whole talk
  if (timing.budget > 0 && timing.over > 0) hints.push({ kind: 'over', line: -1, prefix: '', nth: 0, title: '', seconds: timing.over, whole: true })
  for (const s of timing.sections) {
    if (s.budget > 0 && s.over > 0) hints.push({ kind: 'over', line: s.line, prefix: s.prefix, nth: s.nth, title: s.title, seconds: s.over, whole: false })
  }

  // 2. the marks, in order: pauses that are too long, a joke without room, a quick pace that goes on, words that are not marks
  let wpm = timing.wpm
  const seenWords = new Set()
  let fastSeconds = 0
  let fastFrom = null
  items.forEach((item, index) => {
    const tokens = tokenize(item.text, defs)
    const stack = []
    const jokes = []
    tokens.forEach((token, i) => {
      if (token.type === 'text') {
        // words at a speed: the time of a quick pace in a row
        const words = (token.text.match(/[\p{L}\p{N}]+(?:['’-][\p{L}\p{N}]+)*/gu) || []).length
        if (!item.heading && words) {
          let factor = 1
          let extra = 0
          for (const open of stack) { factor *= open.speed; extra += open.perWord }
          if (factor > 1) {
            if (fastFrom === null) fastFrom = item
            fastSeconds += (words / (wpm * factor)) * 60 + words * extra
            if (fastSeconds > FAST_RUN && !fastFrom.reported) {
              fastFrom.reported = true
              hints.push({ kind: 'fast', ...where(fastFrom), seconds: fastSeconds })
            }
          } else {
            fastSeconds = 0
            fastFrom = null
          }
        }
        // a {word} that is not a mark
        const text = maskedText(token.text)
        let m
        LOOKS_LIKE.lastIndex = 0
        while ((m = LOOKS_LIKE.exec(text))) {
          const word = m[3].toLowerCase()
          if (word === 'define' || seenWords.has(word)) continue
          seenWords.add(word)
          hints.push({ kind: 'unknown', ...where(item), word, closer: m[2] === '/' })
        }
      } else if (token.type === 'close') {
        const at = stack.map(s => s.name).lastIndexOf(token.name)
        if (at >= 0) stack.length = at
        if (token.name === 'tone' && jokes.length) {
          if (jokes.pop()) {
            // what comes next: another mark that is a pause, or nothing that is spoken
            let next = tokens.slice(i + 1).find(x => x.type !== 'text' || x.text.trim())
            if (!next) {
              const after = items[index + 1]
              next = after && tokenize(after.text, defs).find(x => x.type !== 'text' || x.text.trim())
            }
            const pause = next && next.type === 'mark' && next.mark.role === 'point' && next.mark.entry.kind === 'pause'
            if (!pause) hints.push({ kind: 'joke', ...where(item) })
          }
        }
      } else {
        const { mark } = token
        if (mark.role === 'pair') {
          stack.push({ name: mark.name, speed: mark.entry.speed || 1, perWord: mark.entry.perWord || 0 })
          if (mark.name === 'tone') jokes.push(isJoke(mark.text))
        } else if (mark.role === 'point') {
          if (mark.entry.kind === 'pause') {
            const seconds = mark.seconds || mark.entry.seconds || 0
            if (seconds > LONG_PAUSE) hints.push({ kind: 'long', ...where(item), seconds, word: mark.name })
          } else if (mark.name === 'wpm') {
            // brought into range like the timing of the talk does
            wpm = Math.min(WPM_MAX, Math.max(WPM_MIN, Number(mark.number) || wpm))
          }
        }
      }
    })
  })

  // 3. definitions
  const { problems } = collectDefinitions(items.map(i => i.text))
  const place = raw => items.find(i => i.text.includes(raw)) || items[0] || { line: 0, prefix: '', nth: 0 }
  for (const problem of problems) {
    const item = place(problem.raw)
    if (problem.error === 'twice') hints.push({ kind: 'twice', ...where(item), raw: problem.raw })
    else hints.push({ kind: 'baddef', ...where(item), raw: problem.raw, error: problem.error })
  }

  // in the order of the text; the hints of the whole talk first
  return hints.map((h, n) => ({ h, n })).sort((a, b) => (a.h.line - b.h.line) || (a.n - b.n)).map(x => x.h)
}
