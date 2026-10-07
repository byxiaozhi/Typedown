// Speech marks: delivery marks for a talk, written in the text itself (docs/speech-marks-design.md).
//
//   {pause 2s}   {beat}   {wait 5s: laugh}   {cue: look at the back row}   {wpm 140}   {budget 3m}
//   {slow}...{/slow}   {fast}   {loud}   {soft}   {emphasis}   {tone: dry irony}...{/tone}
//   {define very-slow pace 50%: about half speed}      (a word of the user's own, valid in this document)
//
// Pure functions only (no DOM, no Muya), so the rules can be tested alone. The tokenizer (parser/index.js) reads
// marks only when Speech mode is on, and only the built-in words and the words the document defines are marks:
// anything else, `{width=50%}` or a typo, stays plain text.

const NAME = '[A-Za-z][A-Za-z0-9-]*'
// {name}  {name value}  {name: text}  {name value: text}
const MARK = new RegExp(`^\\{(${NAME})(?: ([^{}\\n:]*))?(?::[ ]?([^{}\\n]*))?\\}`)
const DEFINE = /^\{define[ \t]+([^{}\n]*)\}/i
const NAME_OK = /^[a-z][a-z0-9-]{1,23}$/

export const PACE_MIN = 10 // percent
export const PACE_MAX = 300
export const PER_WORD_MAX = 2 // seconds added after every word
export const PAUSE_MIN = 0.1 // seconds
export const PAUSE_MAX = 600
export const BUDGET_MAX = 36000
export const WPM_MIN = 40
export const WPM_MAX = 400

// value: what may follow the name; text: the note after the colon.
export const BUILTIN = {
  beat: { role: 'point', kind: 'pause', seconds: 0.5, value: 'none', text: 'optional' },
  pause: { role: 'point', kind: 'pause', seconds: 1, value: 'duration', text: 'optional' },
  wait: { role: 'point', kind: 'pause', seconds: 3, value: 'duration', text: 'optional', audience: true },
  cue: { role: 'point', kind: 'note', value: 'none', text: 'required' },
  wpm: { role: 'point', kind: 'setting', value: 'wpm', text: 'none' },
  budget: { role: 'point', kind: 'setting', value: 'budget', text: 'none' },
  slow: { role: 'pair', kind: 'pace', speed: 0.75, value: 'none', text: 'optional', override: true },
  fast: { role: 'pair', kind: 'pace', speed: 1.25, value: 'none', text: 'optional', override: true },
  loud: { role: 'pair', kind: 'span', value: 'none', text: 'optional' },
  soft: { role: 'pair', kind: 'span', value: 'none', text: 'optional' },
  emphasis: { role: 'pair', kind: 'span', value: 'none', text: 'optional' },
  tone: { role: 'pair', kind: 'span', value: 'none', text: 'required' }
}
// the built-in words whose numbers a document may change for itself with a definition of the same kind
const OVERRIDABLE = ['slow', 'fast', 'beat', 'pause', 'wait']

const number = text => {
  const n = Number(String(text).replace(',', '.'))
  return Number.isFinite(n) ? n : null
}

// "2s", "1.5s", "1,5s", "1m", "1m30s" -> seconds, or null
export const parseDuration = text => {
  const m = /^(?:(\d+(?:[.,]\d+)?)m)?(?:(\d+(?:[.,]\d+)?)s)?$/i.exec((text || '').trim())
  if (!m || (m[1] === undefined && m[2] === undefined)) return null
  const seconds = (m[1] === undefined ? 0 : number(m[1]) * 60) + (m[2] === undefined ? 0 : number(m[2]))
  return Math.round(seconds * 1000) / 1000
}

// "50%" -> 0.5 (a speed as a fraction of the baseline), or null when outside the allowed range
export const parsePace = text => {
  const m = /^(\d+(?:[.,]\d+)?)\s*%$/.exec((text || '').trim())
  if (!m) return null
  const percent = number(m[1])
  return percent >= PACE_MIN && percent <= PACE_MAX ? percent / 100 : null
}

// "+0.3s" -> 0.3 seconds added after every word, or null
const parsePerWord = text => {
  const m = /^\+(.+)$/.exec((text || '').trim())
  const seconds = m ? parseDuration(m[1]) : null
  return seconds !== null && seconds <= PER_WORD_MAX ? seconds : null
}

const inRange = (seconds, min, max) => seconds !== null && seconds >= min && seconds <= max

// The inside of a definition, after the word `define`: `name kind [value][: meaning]`.
// -> { name, kind, meaning, override, speed?, perWord?, seconds? } or { error }
export const parseDefine = body => {
  const colon = body.indexOf(':')
  const head = (colon < 0 ? body : body.substring(0, colon)).trim().split(/\s+/)
  const meaning = colon < 0 ? '' : body.substring(colon + 1).trim()
  const name = (head[0] || '').toLowerCase()
  const kind = (head[1] || '').toLowerCase()
  const rest = head.slice(2)
  if (!NAME_OK.test(name) || name === 'define') return { error: 'name' }
  if (!['pace', 'pause', 'span', 'note'].includes(kind)) return { error: 'kind' }
  const builtin = BUILTIN[name]
  const def = { name, kind, meaning, override: !!builtin }
  if (builtin) {
    // a built-in word keeps its meaning: only its numbers can change, and only with its own kind
    if (!OVERRIDABLE.includes(name) || builtin.kind !== kind) return { error: 'builtin' }
  } else if (!meaning) {
    return { error: 'meaning' }
  }
  if (kind === 'pace') {
    const speed = parsePace(rest[0])
    if (speed === null) return { error: 'value' }
    def.speed = speed
    def.perWord = 0
    if (rest.length > 2) return { error: 'value' }
    if (rest.length === 2) {
      def.perWord = parsePerWord(rest[1])
      if (def.perWord === null) return { error: 'value' }
    }
  } else if (kind === 'pause') {
    const seconds = parseDuration(rest[0])
    if (!inRange(seconds, PAUSE_MIN, PAUSE_MAX) || rest.length !== 1) return { error: 'value' }
    def.seconds = seconds
  } else if (rest.length) {
    return { error: 'value' }
  }
  return def
}

// Definitions found in the texts of a document's paragraphs. The first definition of a name wins; the others,
// and the ones with errors, are listed in `problems` (the hints of the timing panel show them).
export const collectDefinitions = texts => {
  const defs = new Map()
  const problems = []
  for (const text of texts) {
    if (!text || !/\{define/i.test(text)) continue
    // a code span is not markup: blank it out, keeping the length
    const plain = text.replace(/`[^`\n]*`/g, m => ' '.repeat(m.length))
    const re = /\{define[ \t]+([^{}\n]*)\}/gi
    let m
    while ((m = re.exec(plain))) {
      const def = parseDefine(m[1])
      if (def.error) problems.push({ raw: m[0], error: def.error })
      else if (defs.has(def.name)) problems.push({ raw: m[0], error: 'twice' })
      else defs.set(def.name, def)
    }
  }
  return { defs, problems }
}

// A stable text for a set of definitions: when it changes, everything drawn from the old one is drawn again.
export const definitionsKey = defs => (defs ? JSON.stringify([...defs.values()]) : '')

// What a word means in this document: the built-in word (with the document's own numbers, if it changed them) or
// a word the document defines. null when it is neither.
export const lookupMark = (name, defs) => {
  const key = name.toLowerCase()
  const def = defs && defs.get(key)
  const builtin = BUILTIN[key]
  if (builtin) {
    const entry = { ...builtin, name: key, user: false }
    if (def && def.override) {
      if (def.speed !== undefined) entry.speed = def.speed
      if (def.seconds !== undefined) entry.seconds = def.seconds
      if (def.perWord) entry.perWord = def.perWord
    }
    return entry
  }
  if (!def || def.override) return null
  const role = def.kind === 'pace' || def.kind === 'span' ? 'pair' : 'point'
  return {
    role,
    kind: def.kind,
    name: def.name,
    user: true,
    meaning: def.meaning,
    speed: def.speed,
    perWord: def.perWord,
    seconds: def.seconds,
    value: def.kind === 'pause' ? 'duration' : 'none',
    text: 'optional'
  }
}

// Checks what follows the name against what the word allows.
// -> { value, seconds?, number?, text } or null when the mark is not valid
const checkArguments = (entry, value, text) => {
  const result = { value: value || '', text: text === undefined ? '' : text.trim() }
  if (entry.text === 'required' && !result.text) return null
  if (entry.text === 'none' && text !== undefined) return null
  switch (entry.value) {
    case 'none':
      if (result.value) return null
      break
    case 'duration':
      if (result.value) {
        const seconds = parseDuration(result.value)
        if (!inRange(seconds, PAUSE_MIN, PAUSE_MAX)) return null
        result.seconds = seconds
      }
      break
    case 'budget': {
      const seconds = parseDuration(result.value)
      if (!inRange(seconds, 1, BUDGET_MAX)) return null
      result.seconds = seconds
      break
    }
    case 'wpm': {
      const wpm = /^\d+(?:[.,]\d+)?$/.test(result.value) ? number(result.value) : null
      if (wpm === null || wpm < WPM_MIN || wpm > WPM_MAX) return null
      result.number = wpm
      break
    }
    default:
      return null
  }
  return result
}

// Is there a mark at the start of `src` (which starts with "{")?
// -> { role: 'point' | 'pair' | 'define', raw, name, entry, value, seconds, number, text, def? } or null.
// For a pair this is the opener; the closer is found by the tokenizer with `findCloser`.
export const matchMark = (src, defs) => {
  const d = DEFINE.exec(src)
  if (d) {
    const def = parseDefine(d[1])
    if (def.error) return null
    return { role: 'define', raw: d[0], name: def.name, entry: null, def, text: def.meaning }
  }
  const m = MARK.exec(src)
  if (!m) return null
  const entry = lookupMark(m[1], defs)
  if (!entry) return null
  const args = checkArguments(entry, (m[2] || '').trim(), m[3])
  if (!args) return null
  return { role: entry.role, raw: m[0], name: entry.name, entry, ...args }
}

const CODE_SPAN = /^(`+)(?!`)([\s\S]*?[^`])\1(?!`)/

// Where does the pair opened at `from` end? The first `{/name}` after it that is not inside a code span, or null.
export const findCloser = (src, from, name) => {
  const closer = new RegExp(`^\\{/${name.replace(/[^a-z0-9-]/gi, '')}\\}`, 'i')
  for (let i = from; i < src.length; i++) {
    if (src[i] === '`') {
      // code is not markup: a closer written inside a code span is text
      const span = CODE_SPAN.exec(src.substring(i))
      if (span) i += span[0].length - 1
    } else if (src[i] === '{') {
      const m = closer.exec(src.substring(i))
      if (m) return { index: i, raw: m[0] }
    }
  }
  return null
}

// The text without its marks and definitions: what is left is the text to be spoken. A pair loses its two marks and
// keeps the words between them; a `{cue}`, `{tone}` or definition loses its note too.
export const stripMarks = (text, defs) => {
  let out = ''
  let i = 0
  while (i < text.length) {
    const at = text.substring(i).search(/[{`]/)
    if (at < 0) {
      out += text.substring(i)
      break
    }
    out += text.substring(i, i + at)
    i += at
    if (text[i] === '`') {
      // a code span is not markup: kept as it is
      const close = text.indexOf('`', i + 1)
      const end = close < 0 || text.substring(i, close).includes('\n') ? i + 1 : close + 1
      out += text.substring(i, end)
      i = end
      continue
    }
    const rest = text.substring(i)
    const mark = matchMark(rest, defs)
    if (mark) {
      i += mark.raw.length
      continue
    }
    const closer = /^\{\/([A-Za-z][A-Za-z0-9-]*)\}/.exec(rest)
    const entry = closer && lookupMark(closer[1], defs)
    if (entry && entry.role === 'pair') {
      i += closer[0].length
      continue
    }
    out += '{'
    i++
  }
  return out
}

// --- how a mark is shown ---

export const DEFAULT_LABELS = {
  beat: 'beat',
  pause: 'pause',
  wait: 'audience',
  cue: 'cue',
  wpm: 'speed',
  budget: 'time',
  define: 'defined',
  pace: 'pace',
  span: 'style',
  note: 'cue',
  wpmUnit: 'words/min'
}

const formatNumber = (n, locale) => {
  try {
    return new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(n)
  } catch (e) {
    return String(n)
  }
}

// 90 -> "1 min 30 s", 2 -> "2 s", 0.5 -> "0.5 s"
export const formatSeconds = (seconds, locale) => {
  const whole = Math.floor(seconds / 60)
  const rest = Math.round((seconds - whole * 60) * 100) / 100
  const parts = []
  if (whole) parts.push(`${formatNumber(whole, locale)} min`)
  if (rest || !whole) parts.push(`${formatNumber(rest, locale)} s`)
  return parts.join(' ')
}

// The text of a chip and its tooltip: { chip, title }. `labels` are the words in the user interface language.
export const describe = (info, labels = DEFAULT_LABELS, locale) => {
  const L = { ...DEFAULT_LABELS, ...labels }
  const { entry } = info
  if (info.role === 'define') {
    const { def } = info
    let value = ''
    if (def.kind === 'pace') value = `${formatNumber(def.speed * 100, locale)} %${def.perWord ? ` +${formatSeconds(def.perWord, locale)}` : ''}`
    else if (def.kind === 'pause') value = formatSeconds(def.seconds, locale)
    return { chip: `${def.name} = ${L[def.kind]}${value ? ` ${value}` : ''}`, title: def.meaning }
  }
  switch (info.name) {
    case 'beat':
      return { chip: L.beat, title: info.text }
    case 'pause':
    case 'wait':
      return { chip: `${info.name === 'wait' ? L.wait : L.pause} ${formatSeconds(info.seconds || entry.seconds, locale)}`, title: info.text }
    case 'cue':
      return { chip: `${L.cue}: ${info.text}`, title: info.text }
    case 'wpm':
      return { chip: `${formatNumber(info.number, locale)} ${L.wpmUnit}`, title: '' }
    case 'budget':
      return { chip: `${L.budget} ${formatSeconds(info.seconds, locale)}`, title: '' }
    default:
      break
  }
  if (entry.user && entry.role === 'point') {
    const value = entry.kind === 'pause' ? ` ${formatSeconds(info.seconds || entry.seconds, locale)}` : ''
    return { chip: `${entry.name}${value}`, title: [entry.meaning, info.text].filter(Boolean).join(' - ') }
  }
  return { chip: '', title: entry.user ? entry.meaning : info.text }
}

// The look of the text inside a pair, from its kind: letter-spacing follows the speed (slower = wider), the
// word-spacing follows the seconds added after every word.
export const paceStyle = entry => {
  const speed = entry.speed || 1
  const spacing = Math.max(-0.08, Math.min(0.3, (1 / speed - 1) * 0.2))
  const style = { letterSpacing: `${Math.round(spacing * 1000) / 1000}em` }
  if (entry.perWord) style.wordSpacing = `${Math.min(1, Math.round(entry.perWord * 1.5 * 100) / 100)}em`
  return style
}

// A colour for a word of the user's, the same on every PC: a hue from the name.
export const hueOf = name => {
  let h = 0
  for (const c of name) h = (h * 31 + c.charCodeAt(0)) % 360
  return h
}
