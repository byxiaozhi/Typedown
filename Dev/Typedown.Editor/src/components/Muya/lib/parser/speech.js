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

export const CODE_SPAN = /^(`+)(?!`)([\s\S]*?[^`])\1(?!`)/

// Is the character at `i` escaped by a backslash (an odd number of them right before it)? An escaped backtick is
// text, it does not open a code span.
export const isEscaped = (text, i) => {
  let slashes = 0
  while (i - slashes - 1 >= 0 && text[i - slashes - 1] === '\\') slashes++
  return slashes % 2 === 1
}

// The text with its code spans blanked out (same length, so positions stay): a span is closed by the same number of
// backticks that opened it, as the parser reads it, and an escaped backtick opens none. For finding marks that are not
// examples in code. A run of backticks that nothing closes is text.
export const maskCode = text => {
  let out = ''
  let i = 0
  while (i < text.length) {
    if (text[i] === '`' && !isEscaped(text, i)) {
      const span = CODE_SPAN.exec(text.substring(i))
      if (span) {
        out += ' '.repeat(span[0].length)
        i += span[0].length
        continue
      }
      // a run of backticks with no closer of its own length is text as a whole, not an opener from its second backtick on
      const run = /^`+/.exec(text.substring(i))[0]
      out += run
      i += run.length
      continue
    }
    out += text[i]
    i++
  }
  return out
}

// Where does the pair opened at `from` end? The first `{/name}` after it that is not inside a code span, or null.
export const findCloser = (src, from, name) => {
  const closer = new RegExp(`^\\{/${name.replace(/[^a-z0-9-]/gi, '')}\\}`, 'i')
  for (let i = from; i < src.length; i++) {
    if (src[i] === '`' && !isEscaped(src, i)) {
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
// keeps the words between them; a `{cue}`, `{tone}` or definition loses its note too. `tidy` also takes out the
// space that a mark leaves behind ("one {pause} two" is "one two", not "one  two").
export const stripMarks = (text, defs, tidy = false) => {
  let out = ''
  let i = 0
  let removed = false
  const gone = length => {
    i += length
    removed = true
    // a space that would now follow another space, or the start of the line, goes
    if (tidy && (out === '' || /\s$/.test(out)) && text[i] === ' ') i++
    // and the space before a mark that is followed by a full stop, a comma and the like ("End {beat}." is "End.")
    if (tidy && /[ \t]$/.test(out) && /^[.,;:!?)\]]/.test(text[i] || '')) out = out.replace(/[ \t]+$/, '')
  }
  while (i < text.length) {
    const at = text.substring(i).search(/[{`]/)
    if (at < 0) {
      out += text.substring(i)
      break
    }
    out += text.substring(i, i + at)
    i += at
    if (text[i] === '`' && isEscaped(text, i)) {
      // an escaped backtick is text
      out += '`'
      i++
      continue
    }
    if (text[i] === '`') {
      // a code span is not markup: kept as it is
      const span = CODE_SPAN.exec(text.substring(i))
      const length = span ? span[0].length : 1
      out += text.substring(i, i + length)
      i += length
      continue
    }
    const rest = text.substring(i)
    const mark = matchMark(rest, defs)
    if (mark) {
      gone(mark.raw.length)
      continue
    }
    const closer = /^\{\/([A-Za-z][A-Za-z0-9-]*)\}/.exec(rest)
    const entry = closer && lookupMark(closer[1], defs)
    if (entry && entry.role === 'pair') {
      gone(closer[0].length)
      continue
    }
    out += '{'
    i++
  }
  // a mark at the end of the line leaves the space before it
  return tidy && removed ? out.replace(/[ \t]+$/, '') : out
}

// The width of a line's leading white space in columns (a tab goes to the next multiple of four).
const indentOf = line => {
  let width = 0
  for (const c of line) {
    if (c === ' ') width++
    else if (c === '\t') width += 4 - (width % 4)
    else break
  }
  return width
}

const LIST_MARKER = /^[ \t]*([-*+]|\d{1,9}[.)])([ \t]+|$)/
const HEADING_OR_RULE = /^ {0,3}(?:#{1,6}(?:[ \t]|$)|([-*_])(?:[ \t]*\1){2,}[ \t]*$|=+[ \t]*$)/

// Which lines of a document are prose, where speech marks live: not fenced code, not indented code and not a front
// matter block. A front matter block is one that is closed (`---` first, `---` or `...` later): a lone `---` at the
// start is a rule, and the text after it is prose.
//
// Indented code is a line indented four columns beyond the block it is in (the page, or the content of a list item),
// and it goes on while lines are indented that far. It starts after a blank line or after a heading, a rule or a
// fence, but never interrupts a paragraph. A list item's content starts after its marker, so under `- item` an
// indented line is the item's own text and code needs six spaces.
export const proseLines = lines => {
  const prose = []
  let fence = null
  let prevBlank = true
  let paragraph = false // the line before was text of a paragraph, which indented code cannot interrupt
  let indentedCode = false
  const items = [] // the content columns of the list items the line is in, innermost last
  const closing = lines[0] === '---' ? lines.findIndex((l, i) => i > 0 && (l === '---' || l === '...')) : -1
  for (let n = 0; n < lines.length; n++) {
    if (closing > 0 && n <= closing) {
      prose.push(false)
      continue
    }
    const line = lines[n]
    const f = /^ {0,3}(`{3,}|~{3,})/.exec(line)
    if (fence) {
      prose.push(false)
      if (f && f[1][0] === fence[0] && f[1].length >= fence.length && !/\S/.test(line.substring(f[0].length))) {
        fence = null
        paragraph = false
      }
      continue
    }
    if (f) {
      fence = f[1]
      prose.push(false)
      paragraph = false
      indentedCode = false
      continue
    }
    if (!line.trim()) {
      // a blank line holds no mark; it does not end an indented code block yet (the next line may go on with it)
      prose.push(true)
      prevBlank = true
      continue
    }
    const indent = indentOf(line)
    const marker = LIST_MARKER.exec(line)
    // a line that is not indented as far as the item it follows leaves that item (after a blank line, or when it is
    // a new item); one that goes on right after the text of the item is carried on from it
    if (prevBlank || marker) while (items.length && items[items.length - 1] > indent) items.pop()
    const base = items.length ? items[items.length - 1] : 0
    if (indent >= base + 4 && (indentedCode || prevBlank || !paragraph)) {
      indentedCode = true
      prose.push(false)
      prevBlank = false
      paragraph = false
      continue
    }
    indentedCode = false
    if (marker && indent - base < 4) {
      // the content of the item starts after the marker and the spaces that follow it (one when there are five or more)
      const spaces = marker[2].length
      items.push(indent + marker[1].length + (spaces >= 1 && spaces <= 4 ? spaces : 1))
      paragraph = true
    } else {
      paragraph = !HEADING_OR_RULE.test(line)
    }
    prevBlank = false
    prose.push(true)
  }
  return prose
}

// The definitions a document has in its prose: the same lines as the marks are read from, so an example inside a code block
// is not taken for a definition of the document.
export const collectDocumentDefinitions = markdown => {
  const lines = markdown.split(/\r?\n/)
  const prose = proseLines(lines)
  return collectDefinitions(lines.filter((_, n) => prose[n]))
}

// A whole document without its speech marks and definitions (Edit > Remove all speech marks). The definitions are
// read from the document itself; fenced code and a front matter block are left as they are; a line that held
// nothing but marks goes, without leaving a doubled blank line.
export const stripMarkdown = markdown => {
  const crlf = markdown.includes('\r\n')
  const lines = markdown.replace(/\r\n/g, '\n').split('\n')
  const prose = proseLines(lines)
  const { defs } = collectDefinitions(lines.filter((_, n) => prose[n]))
  const out = []
  let droppedBefore = false
  for (let n = 0; n < lines.length; n++) {
    let line = lines[n]
    if (prose[n] && line.includes('{')) {
      const stripped = stripMarks(line, defs, true)
      if (stripped !== line) {
        if (!stripped.trim()) {
          droppedBefore = true
          continue
        }
        line = stripped
      }
    }
    // a blank line that would now follow another blank line (or the start) goes
    if (!line.trim() && droppedBefore && (out.length === 0 || !out[out.length - 1].trim())) continue
    droppedBefore = false
    out.push(line)
  }
  return out.join(crlf ? '\r\n' : '\n')
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

// The colours a user's mark can have (Settings > Speech marks); the style sheet and the theme files have a pair of
// colours for each (--speechRedBg and --speechRedText ...), for the light and the dark theme.
export const SWATCHES = ['red', 'orange', 'yellow', 'green', 'teal', 'blue', 'purple', 'pink']

// How the user chose to draw a word of theirs: { color, icon } from the library the host sent ({ name: { color, icon } }).
export const styleOf = (styles, name) => {
  const style = styles && styles[name]
  if (!style) return { color: '', icon: '' }
  return {
    color: SWATCHES.includes(style.color) ? style.color : '',
    icon: typeof style.icon === 'string' && [...style.icon].length === 1 ? style.icon : ''
  }
}
