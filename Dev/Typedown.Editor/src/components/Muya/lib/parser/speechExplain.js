// Speech marks: the explanation for an AI (docs/speech-marks-design.md, 6.3).
//
// A speaker who pastes a talk with delivery marks into an AI should not have to hope that it works out what the braces
// mean. This builds one plain text: what the text is, how a mark is written, every mark of Caret with its meaning, every
// mark of the speaker's own library with the speaker's own line about it, the planned times, and then (if asked) the
// speech itself. It is fixed English text plus the words of the speaker; nothing is sent anywhere by Caret, the text only
// goes to the clipboard. Pure functions, tested alone.

import { BUILTIN, matchMark, lookupMark, proseLines, collectDefinitions } from './speech'
import { tokenize, formatClock } from './speechTiming'

const INTRO = [
  'This is a speech script. It is written to be spoken aloud by a person, and it carries delivery marks: instructions to the speaker, written in curly braces inside the text.',
  'The marks are not words to be spoken and they are not part of the message. Please read the text with them in mind, and keep them exactly as they are unless I ask you to change them.'
].join('\n')

const HOW = [
  'How the marks are written:',
  '- {name}, {name value} and {name: note} are marks at one point of the text, for example {pause 2s} or {cue: look at the back row}.',
  '- {name}some words{/name} is a pair around words; a pair that is not closed runs to the end of its paragraph.',
  '- A line {define name kind value: meaning} near the top of the document defines a mark of the writer\'s own. The kinds are pace (a speed as a percentage of the normal speed, optionally +seconds added after every word), pause (a length), span (a manner or style for the words) and note (a cue to the speaker).',
  '- A length of time is written 2s, 1.5s, 1m or 1m30s; a speed is written 50%.',
  '- Anything in braces that is not one of the marks below is ordinary text.'
].join('\n')

const HOW_AS_WORDS = [
  'How the marks are written in this copy: the marks are written as words in round brackets so that they stand out from the speech.',
  '- (name), (name, 2 seconds) and (name: note) are marks at one point of the text, for example (pause, 2 seconds) or (cue: look at the back row).',
  '- (start name) and (end name) mark the beginning and the end of a stretch of words that the mark is about; a stretch that is not ended runs to the end of its paragraph.',
  '- Marks of the writer\'s own are listed below with their meaning.'
].join('\n')

// what every mark of Caret means, in the order of the Speech card; `speed` and `seconds` come from the marks themselves
const BUILT_IN = [
  ['beat', '{beat}', () => 'a very short pause, half a second.'],
  ['pause', '{pause} or {pause 2s}', () => 'a pause: one second, or the length given.'],
  ['wait', '{wait} or {wait 5s: laugh}', () => 'stop and wait for the audience for three seconds, or the length given: for a laugh, applause or an answer. The optional note says what is expected.'],
  ['cue', '{cue: look at the back row}', () => 'an instruction to the speaker (a gesture, a slide, an object, a breath). It is not spoken and takes no time of its own.'],
  ['slow', '{slow}words{/slow}', (speed, extra) => `speak these words slowly, at ${speed} % of the normal speed${perWordNote(extra)}.`],
  ['fast', '{fast}words{/fast}', (speed, extra) => `speak these words quickly, at ${speed} % of the normal speed${perWordNote(extra)}.`],
  ['loud', '{loud}words{/loud}', () => 'say these words louder and with more weight.'],
  ['soft', '{soft}words{/soft}', () => 'say these words more quietly and gently.'],
  ['emphasis', '{emphasis}words{/emphasis}', () => 'stress these words.'],
  ['tone', '{tone: dry irony}words{/tone}', () => 'say these words in the manner the note describes (for example dry irony, warm, urgent, humble, serious, or a joke).'],
  ['wpm', '{wpm 140}', () => 'the speaker\'s normal speed in words per minute from here on; it is the baseline of the time estimates.'],
  ['budget', '{budget 3m}', () => 'the time the section it stands in is meant to take (or the whole talk, when it comes before the first heading).']
]

const number = n => String(Math.round(n * 100) / 100)

// a pace that a document changed for itself may add seconds after every word
const perWordNote = extra => (extra ? `, and ${secondsInWords(extra)} are added after every word` : '')

export const secondsInWords = seconds => {
  const total = Math.round(seconds * 100) / 100
  if (total < 60) return `${number(total)} ${total === 1 ? 'second' : 'seconds'}`
  const m = Math.floor(total / 60)
  const s = Math.round((total - m * 60) * 100) / 100
  return `${m} ${m === 1 ? 'minute' : 'minutes'}${s ? ` ${number(s)} ${s === 1 ? 'second' : 'seconds'}` : ''}`
}

// A mark of the writer's own, in words: what it does.
const describeDefinition = def => {
  switch (def.kind) {
    case 'pace': {
      const percent = number(def.speed * 100)
      const extra = def.perWord ? `, and ${number(def.perWord)} seconds are added after every word` : ''
      return `pace: speak the words between the marks at ${percent} % of the normal speed${extra}.`
    }
    case 'pause': return `pause of ${secondsInWords(def.seconds)}.`
    case 'span': return 'a manner or style for the words between the two marks.'
    default: return 'a cue to the speaker; it is not spoken.'
  }
}

const formOf = def => {
  if (def.kind === 'pace' || def.kind === 'span') return `{${def.name}}words{/${def.name}}`
  return def.kind === 'pause' ? `{${def.name}} or {${def.name} 8s}` : `{${def.name}}`
}

// The marks of a text written as words: {pause 2s} -> (pause, 2 seconds), {slow} -> (start slow), {/slow} -> (end slow),
// {cue: look up} -> (cue: look up). Definition lines go (the explanation says what the words mean). Code and front matter
// are left as they are.
export const marksAsWords = markdown => {
  const lines = markdown.split(/\r?\n/)
  const prose = proseLines(lines)
  const { defs } = collectDefinitions(lines.filter((_, n) => prose[n]))
  const out = []
  // a line that held nothing but definitions goes; the blank line after it goes too when it would be doubled
  let dropped = false
  lines.forEach((line, n) => {
    if (dropped && !line.trim() && (out.length === 0 || !out[out.length - 1].trim())) return
    dropped = false
    if (!prose[n] || !line.includes('{')) { out.push(line); return }
    let text = ''
    for (const token of tokenize(line, defs)) {
      if (token.type === 'text') { text += token.text; continue }
      if (token.type === 'close') { text += `(end ${token.name})`; continue }
      const { mark } = token
      if (mark.role === 'define') continue
      if (mark.role === 'pair') { text += `(start ${mark.name}${mark.text ? `: ${mark.text}` : ''})`; continue }
      if (mark.name === 'wpm') { text += `(speaking speed: ${number(mark.number)} words per minute)`; continue }
      if (mark.name === 'budget') { text += `(time budget: ${secondsInWords(mark.seconds)})`; continue }
      const time = mark.seconds ? `, ${secondsInWords(mark.seconds)}` : ''
      text += `(${mark.name}${time}${mark.text ? `: ${mark.text}` : ''})`
    }
    if (text.trim() || !line.trim()) out.push(text)
    else dropped = true
  })
  // only the definition lines are taken out: the rest of the text, blank lines included, is as it was
  return out.join('\n')
}

// The names of the marks a document uses (and the definitions it makes), for the option that leaves out the rest.
const usedNames = (timing, defs) => {
  const used = new Set()
  const items = [...(timing ? timing.sections : []), ...(timing ? timing.paragraphs : [])]
  for (const item of items) {
    for (const token of tokenize(item.text || '', defs)) {
      if (token.type === 'close') used.add(token.name)
      else if (token.type === 'mark' && token.mark.role !== 'define') used.add(token.mark.name)
    }
  }
  return used
}

const timingLines = timing => {
  if (!timing || (!timing.words && !timing.seconds)) return ''
  const parts = [`Timing, estimated from the words and the marks (a rough guide): the speaking speed is ${number(timing.wpm)} words per minute; the planned total is ${formatClock(timing.seconds)}${timing.budget > 0 ? `, against a budget of ${formatClock(timing.budget)}` : ''}.`]
  const rows = (timing.sections || []).filter(s => s.title)
  if (rows.length) {
    parts.push('Sections:')
    for (const s of rows) {
      parts.push(`${'  '.repeat(Math.max(0, s.level - 1))}- ${s.title}: ${formatClock(s.seconds)}${s.budget > 0 ? ` (budget ${formatClock(s.budget)})` : ''}`)
    }
  }
  return parts.join('\n')
}

// -> the text for the clipboard.
// `options`: {
//    library:      the marks of the speaker's library: [{ name, kind, meaning, definition }], `definition` being the
//                  `{define ...}` line that makes the word a mark in a document
//    markdown:     the document (omit for the explanation only)
//    timing:       the result of computeTiming for the document (its times are added)
//    usedOnly:     only the marks the document uses (default: all of them)
//    asWords:      the marks written as words in round brackets
//    withText:     put the speech after the explanation (default: when there is a document)
//    rehearsal:    the last rehearsal of the talk as Markdown (the teleprompter's rehearse mode wrote it), or ''
//  }
const REHEARSAL_INTRO = 'The writer rehearsed this talk aloud with the teleprompter. The last rehearsal follows: for each paragraph the planned time, the time it really took and the difference, and the pauses really taken. The times are as exact as the keys the writer pressed (about a second or two per paragraph). Use it to see where the talk runs long or short, and to suggest cuts, additions or other delivery marks there.'

export const buildExplanation = (options = {}) => {
  const library = options.library || []
  const markdown = options.markdown || ''
  const timing = options.timing || null
  const withText = options.withText === undefined ? !!markdown : options.withText && !!markdown
  const asWords = !!options.asWords
  const lines = markdown.split(/\r?\n/)
  const prose = proseLines(lines)
  const { defs } = collectDefinitions(lines.filter((_, n) => prose[n]))
  const used = options.usedOnly ? usedNames(timing, defs) : null
  const wanted = name => !used || used.has(name)

  const parts = [INTRO, asWords ? HOW_AS_WORDS : HOW]

  const builtIn = BUILT_IN.filter(([name]) => wanted(name)).map(([name, form, text]) => {
    const entry = lookupMark(name, defs)
    const percent = entry && entry.speed ? number(entry.speed * 100) : ''
    const perWord = entry && entry.perWord ? entry.perWord : 0
    const changed = defs.get(name) && defs.get(name).override ? ' (this document changes the numbers of this mark in its own definition line)' : ''
    return `- ${asWords ? shown(name, form) : form}: ${text(percent, perWord)}${changed}`
  })
  if (builtIn.length) parts.push(['Marks built into the writing tool:', ...builtIn].join('\n'))

  // the marks of the speaker's own: those of the library, and those the document defines itself
  const own = []
  const listed = new Set()
  for (const mark of library) {
    const read = mark.definition ? matchMark(mark.definition, undefined) : null
    if (!read || read.role !== 'define' || !wanted(mark.name)) continue
    listed.add(mark.name)
    // a definition the document carries wins over the library: its numbers and its meaning are the ones the speech is written with
    const fromDocument = defs.get(mark.name) && !defs.get(mark.name).override
    const def = fromDocument ? defs.get(mark.name) : read.def
    const meaning = fromDocument ? (def.meaning || mark.meaning || '') : (mark.meaning || def.meaning || '')
    own.push(`- ${asWords ? shown(mark.name, formOf(def)) : formOf(def)}: ${meaning}${meaning ? ' ' : ''}(${describeDefinition(def).replace(/\.$/, '')}).${asWords || defs.has(mark.name) ? '' : ` To use it in a text that does not have it yet, add the line ${mark.definition}`}`)
  }
  for (const def of defs.values()) {
    if (def.override || listed.has(def.name) || !wanted(def.name)) continue
    own.push(`- ${asWords ? shown(def.name, formOf(def)) : formOf(def)}: ${def.meaning ? `${def.meaning} ` : ''}(${describeDefinition(def).replace(/\.$/, '')}).`)
  }
  if (own.length) parts.push(["Marks of the writer's own:", ...own].join('\n'))

  const time = timingLines(timing)
  if (time) parts.push(time)

  const rehearsal = (options.rehearsal || '').replace(/\r\n/g, '\n').trim()
  if (rehearsal) parts.push(`${REHEARSAL_INTRO}\n\n${rehearsal}`)

  let text = parts.join('\n\n')
  if (withText) {
    text += `\n\nThe speech follows.\n\n-----\n\n${asWords ? marksAsWords(markdown) : markdown.replace(/\r\n/g, '\n')}\n`
  } else {
    text += '\n'
  }
  return text
}

// In words mode the forms are shown as the marks appear in the text, so "{pause 2s}" becomes "(pause, 2 seconds)".
function shown (name, form) {
  const entry = BUILTIN[name]
  if (entry && entry.role === 'pair') return `(start ${name}) ... (end ${name})`
  if (name === 'beat') return '(beat)'
  if (entry && entry.kind === 'pause') return name === 'wait' ? '(wait) or (wait, 5 seconds: laugh)' : `(${name}) or (${name}, 2 seconds)`
  if (name === 'cue') return '(cue: look at the back row)'
  if (name === 'wpm') return '(speaking speed: 140 words per minute)'
  if (name === 'budget') return '(time budget: 3 minutes)'
  if (/\}words\{\//.test(form)) return `(start ${name}) ... (end ${name})`
  return form.includes(' or ') ? `(${name}) or (${name}, 8 seconds)` : `(${name})`
}

