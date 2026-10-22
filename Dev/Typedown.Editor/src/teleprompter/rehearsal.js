// Rehearsal capture (docs/speech-marks-design.md, step 3). The speaker reads the talk aloud from the teleprompter and taps a few
// keys: next paragraph (this gives the time of each paragraph), a pause started or ended (the real length of the pause taken),
// "slower" or "faster than planned here", "laugh happened", "stumbled, did it again". Caret records only what was pressed and
// when, and works out planned against actual per paragraph, the pauses really taken, and the speaker's own pace in words per
// minute. No audio is heard, nothing is recognised: the speaker is the sensor, and the figures are as exact as the taps (about a
// second or two per paragraph). The result is a plain Markdown block that goes into a file beside the speech and, if the
// speaker wants, into "Copy for AI". Pure functions on the plan (plan.js) and a list of events, tested alone.

import { formatClock } from '../components/Muya/lib/parser/speechTiming'

// An event: { t, type, block } with `t` the seconds since the rehearsal started, `block` the index of the block of the plan
// the speaker is at, and `type` one of
//   'start' | 'next' | 'back' | 'end'                     where the speaker is (and when the rehearsal began and ended)
//   'pause-start' | 'pause-end'                           a pause taken
//   'slower' | 'faster' | 'laugh' | 'stumble'             a tap about the paragraph the speaker is in
export const MOVES = ['start', 'next', 'back', 'end']
export const TAPS = ['slower', 'faster', 'laugh', 'stumble']

const blockWords = block => block.segments.filter(s => s.type === 'words').reduce((n, s) => n + s.words, 0)
const blockPauses = block => block.segments.filter(s => s.type === 'pause')
const blockText = block => block.segments.filter(s => s.type === 'words').map(s => s.text).join('').replace(/\s+/g, ' ').trim()

// -> {
//      rows:       one per paragraph the speaker was at: { block, section, text, words, planned, actual, completed,
//                  taps: { slower, faster, laugh, stumble }, pauses: [{ seconds }], plannedPauses }
//      plannedTotal, actualTotal   over the paragraphs that were read (the planned time of those, and the time they took)
//      pace                        measured words per minute, or null when no paragraph can say it
//      finished                    whether the speaker went to the end of the talk
//    }
export const summarizeRun = (plan, events) => {
  const rows = new Map()
  const rowFor = index => {
    if (!rows.has(index)) {
      const block = plan.blocks[index]
      rows.set(index, {
        block: index,
        section: block && plan.sections[block.section] ? plan.sections[block.section].title : '',
        text: block ? blockText(block) : '',
        words: block ? blockWords(block) : 0,
        planned: block ? block.end - block.start : 0,
        actual: 0,
        completed: false,
        taps: { slower: 0, faster: 0, laugh: 0, stumble: 0 },
        pauses: [],
        plannedPauses: block ? blockPauses(block).length : 0
      })
    }
    return rows.get(index)
  }
  const lastSpoken = plan.blocks.reduce((n, b) => (blockWords(b) ? b.index : n), -1)
  let current = null
  let last = 0
  let openPause = null
  let ended = false
  for (const e of events) {
    if (current !== null) rowFor(current).actual += Math.max(0, e.t - last)
    last = e.t
    // a pause still open when the speaker moves on or ends the run ends there, in the paragraph it began in
    if (openPause && (e.type === 'next' || e.type === 'back' || e.type === 'end')) {
      rowFor(openPause.block).pauses.push({ seconds: Math.max(0, e.t - openPause.t) })
      openPause = null
    }
    if (e.type === 'pause-start') {
      openPause = { block: e.block, t: e.t }
    } else if (e.type === 'pause-end') {
      if (openPause) {
        rowFor(openPause.block).pauses.push({ seconds: Math.max(0, e.t - openPause.t) })
        openPause = null
      }
    } else if (TAPS.includes(e.type)) {
      rowFor(e.block !== undefined ? e.block : current).taps[e.type]++
    } else if (e.type === 'next') {
      if (current !== null) rowFor(current).completed = true
    } else if (e.type === 'end') {
      // the paragraph the speaker ends in counts as read when it is the last one of the talk
      if (current !== null && current >= lastSpoken) rowFor(current).completed = true
      ended = true
    }
    if (MOVES.includes(e.type)) current = e.type === 'end' ? null : e.block
    if (e.type === 'start' && current !== null) rowFor(current)
  }
  // a pause that was never ended ends with the rehearsal
  if (openPause && events.length) rowFor(openPause.block).pauses.push({ seconds: Math.max(0, last - openPause.t) })

  // paragraphs, and headings that are read aloud (a heading with no planned time is not a row)
  const list = [...rows.values()].filter(r => plan.blocks[r.block] && (plan.blocks[r.block].kind === 'paragraph' || r.planned > 0) && (r.actual > 0 || r.completed)).sort((a, b) => a.block - b.block)
  const read = list.filter(r => r.completed)
  // the pace: words over the time spent speaking (the pauses really taken left out), only from paragraphs that can tell it: ones with
  // no planned pause, or whose pauses the speaker tapped
  let words = 0
  let seconds = 0
  for (const r of read) {
    if (!r.words) continue
    if (r.plannedPauses && r.pauses.length < r.plannedPauses) continue
    const taken = r.pauses.reduce((n, p) => n + p.seconds, 0)
    const speaking = r.actual - taken
    if (speaking <= 0) continue
    words += r.words
    seconds += speaking
  }
  return {
    rows: list,
    plannedTotal: read.reduce((n, r) => n + r.planned, 0),
    actualTotal: read.reduce((n, r) => n + r.actual, 0),
    pace: seconds >= 5 && words >= 10 ? Math.round((words / seconds) * 60) : null,
    finished: ended && lastSpoken >= 0 && read.some(r => r.block === lastSpoken)
  }
}

const signed = seconds => {
  const rounded = Math.round(seconds)
  return `${rounded >= 0 ? '+' : '-'}${formatClock(Math.abs(rounded))}`
}

const cell = text => text.replace(/\|/g, '/').replace(/\s+/g, ' ').trim()
const shorten = (text, n = 48) => (text.length > n ? `${text.substring(0, n - 1).trimEnd()}…` : text)

// The run as a block of Markdown for a file beside the speech (fixed English: it is read by the speaker and by an AI).
// `date` is a Date; `baseline` the words per minute the plan was made with.
export const formatRun = (run, { date = new Date(), baseline = 130 } = {}) => {
  const two = n => String(n).padStart(2, '0')
  const when = `${date.getFullYear()}-${two(date.getMonth() + 1)}-${two(date.getDate())} ${two(date.getHours())}:${two(date.getMinutes())}`
  const lines = [`## Rehearsal ${when}`, '']
  if (!run.rows.length) {
    lines.push('Nothing was timed: no paragraph was read.', '')
    return lines.join('\n')
  }
  const readCount = run.rows.filter(r => r.completed).length
  lines.push(
    `The speaker read the talk aloud and tapped the keys; the times are as exact as the taps. ${readCount} of ${run.rows.length} paragraphs were read to the end${run.finished ? ' (the whole talk)' : ''}.`,
    `Planned ${formatClock(run.plannedTotal)}, read in ${formatClock(run.actualTotal)} (${signed(run.actualTotal - run.plannedTotal)}).` +
      (run.pace ? ` Measured pace: ${run.pace} words per minute (the plan used ${baseline}).` : ' The pace could not be measured (too little read, or pauses not tapped).'),
    '',
    '| # | Section | Paragraph | Planned | Actual | Difference | Notes |',
    '|--:|---|---|--:|--:|--:|---|'
  )
  run.rows.forEach((r, i) => {
    const notes = []
    if (r.taps.slower) notes.push(r.taps.slower > 1 ? `slower than planned x${r.taps.slower}` : 'slower than planned')
    if (r.taps.faster) notes.push(r.taps.faster > 1 ? `faster than planned x${r.taps.faster}` : 'faster than planned')
    if (r.taps.laugh) notes.push(r.taps.laugh > 1 ? `laugh x${r.taps.laugh}` : 'laugh')
    if (r.taps.stumble) notes.push(r.taps.stumble > 1 ? `stumbled, did it again x${r.taps.stumble}` : 'stumbled, did it again')
    if (r.pauses.length) notes.push(`paused ${r.pauses.map(p => `${Math.round(p.seconds)} s`).join(', ')}${r.plannedPauses ? ` (planned ${r.plannedPauses})` : ' (not planned)'}`)
    if (!r.completed) notes.push('stopped here')
    lines.push(`| ${i + 1} | ${cell(r.section) || '-'} | ${cell(shorten(r.text)) || '-'} | ${formatClock(r.planned)} | ${formatClock(r.actual)} | ${signed(r.actual - r.planned)} | ${notes.join('; ') || '-'} |`)
  })
  lines.push('')
  return lines.join('\n')
}

// The file beside a speech: its text, with the block of a run added at the end (a title is put on a new file).
export const appendRun = (existing, block, title = 'Rehearsals') => {
  const base = (existing || '').replace(/\s+$/, '')
  return `${base ? `${base}\n\n` : `# ${title}\n\n`}${block.replace(/\s+$/, '')}\n`
}

// The last run of such a file: from its last "## Rehearsal" heading to the end, or '' when there is none.
export const lastRun = text => {
  const at = (text || '').lastIndexOf('\n## Rehearsal ')
  const start = at >= 0 ? at + 1 : (text || '').startsWith('## Rehearsal ') ? 0 : -1
  return start >= 0 ? text.substring(start).trim() : ''
}
