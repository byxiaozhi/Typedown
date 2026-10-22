// The teleprompter's plan (docs/speech-marks-design.md, step 2): the talk as a list of blocks (headings and paragraphs) in
// the order of the text, each with the time it starts and ends in the planned speech and the pieces it is made of: runs of
// words with the marks that style them, pauses, cues. It is made from the same arithmetic as the timing panel
// (speechTiming.js), so the teleprompter passes a paragraph in exactly the time the panel says. Pure functions.

import { computeTiming, timeParagraph, spoken, DEFAULT_WPM } from '../components/Muya/lib/parser/speechTiming'
import { WPM_MIN, WPM_MAX } from '../components/Muya/lib/parser/speech'

const plain = text => spoken(text).replace(/[ \t\r\n]+/g, ' ')

// -> { blocks, sections, total, budget, wpm }
//   blocks:   [{ index, kind: 'heading' | 'paragraph', level, title, line, start, end, section, segments }]
//   segments: { type: 'words', at, seconds, words, text, styles } | { type: 'title', at, seconds, text } |
//             { type: 'pause', at, seconds, audience, name, note } | { type: 'cue', at, name, text } | { type: 'open', at, name, kind, user, text } | { type: 'close', at, name }
//             `at` is the second of the block the piece starts at, `styles` the marks that are open around the words
//   sections: [{ title, level, start, end, budget }], one per heading; the preamble before the first heading is not in it
export const buildPlan = (markdown, options = {}) => {
  const wpm = Math.min(WPM_MAX, Math.max(WPM_MIN, Number(options.wpm) || DEFAULT_WPM))
  const headingsSpoken = !!options.headingsSpoken
  const timing = computeTiming(markdown, { wpm, headingsSpoken })
  const items = [
    ...timing.sections.map((s, i) => ({ kind: 'heading', line: s.line, text: s.text || '', title: s.title, level: s.level, section: i })),
    ...timing.paragraphs.map(p => ({ kind: 'paragraph', line: p.line, text: p.text || '', section: -2 }))
  ].sort((a, b) => a.line - b.line)

  const run = { defs: timing.defs, wpm, firstWpm: null }
  const blocks = []
  const sections = timing.sections.map(s => ({ title: s.title, level: s.level, start: 0, end: 0, budget: s.budget, seconds: s.seconds }))
  let t = 0
  let current = -1
  for (const item of items) {
    if (item.kind === 'heading') current = item.section
    const trace = []
    const result = timeParagraph(item.text, run, item.kind === 'heading' ? headingsSpoken : true, trace)
    let at = 0
    const segments = []
    for (const e of trace) {
      if (e.type === 'words') {
        const text = plain(e.text)
        if (!text.trim() && !e.seconds) continue
        segments.push({ type: 'words', at, seconds: e.seconds, words: e.words, text, styles: e.stack })
        at += e.seconds
      } else if (e.type === 'pause') {
        segments.push({ type: 'pause', at, seconds: e.seconds, audience: e.audience, name: e.name, note: e.note })
        at += e.seconds
      } else if (e.type === 'cue') {
        segments.push({ type: 'cue', at, name: e.name, text: e.text })
      } else if (e.type === 'open') {
        segments.push({ type: 'open', at, name: e.name, kind: e.kind, user: e.user, text: e.text })
      } else if (e.type === 'close') {
        segments.push({ type: 'close', at, name: e.name })
      }
    }
    // a heading is always shown, as the title of its section, even when it is not read aloud
    if (item.kind === 'heading' && !segments.some(s => s.type === 'words')) segments.unshift({ type: 'title', at: 0, seconds: 0, text: item.title })
    // a line that holds nothing to read or do (a definition, a setting) is not a block
    if (!segments.length) continue
    const block = {
      index: blocks.length,
      kind: item.kind,
      level: item.level || 0,
      title: item.title || '',
      line: item.line,
      start: t,
      end: t + result.seconds,
      section: item.kind === 'heading' ? item.section : current,
      segments
    }
    if (item.kind === 'heading' && sections[item.section]) sections[item.section].start = t
    t = block.end
    blocks.push(block)
  }
  // a section ends where its content ends: its start plus the time of everything under it
  sections.forEach(s => { s.end = Math.min(t, s.start + s.seconds) })
  return { blocks, sections, total: t, budget: timing.budget, wpm }
}
