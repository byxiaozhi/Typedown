import '../components/Muya/lib/config'
import { buildPlan } from './plan'
import { summarizeRun, formatRun, appendRun, lastRun } from './rehearsal'

const words = n => Array.from({ length: n }, () => 'word').join(' ')

describe('a rehearsal of a talk', () => {
  // blocks: 0 heading (not read aloud), 1 paragraph 65 words (30 s), 2 paragraph 65 words + 5 s pause, 3 paragraph 13 words
  const plan = buildPlan(['# Opening', '', words(65), '', `${words(65)} {pause 5s}`, '', words(13)].join('\n'))
  const ev = (t, type, block) => ({ t, type, block })

  test('the plan under test is what the cases below assume', () => {
    expect(plan.blocks.map(b => b.kind)).toEqual(['heading', 'paragraph', 'paragraph', 'paragraph'])
    expect(plan.blocks[1].end - plan.blocks[1].start).toBeCloseTo(30, 5)
    expect(plan.blocks[2].end - plan.blocks[2].start).toBeCloseTo(35, 5)
  })

  const full = [
    ev(0, 'start', 1), ev(25, 'next', 2), ev(30, 'pause-start', 2), ev(38, 'pause-end', 2), ev(60, 'next', 3), ev(66, 'end', 3)
  ]

  test('time per paragraph, the pauses taken, and the pace without the pauses', () => {
    const run = summarizeRun(plan, full)
    expect(run.rows.map(r => [r.block, Math.round(r.actual)])).toEqual([[1, 25], [2, 35], [3, 6]])
    expect(run.rows[1].pauses).toEqual([{ seconds: 8 }])
    expect(run.rows.every(r => r.completed)).toBe(true)
    expect(run.finished).toBe(true)
    expect(run.plannedTotal).toBeCloseTo(30 + 35 + (13 / 130) * 60, 5)
    expect(run.actualTotal).toBeCloseTo(66, 5)
    // 143 words in 66 - 8 = 58 s
    expect(run.pace).toBe(Math.round((143 / 58) * 60))
  })

  test('a paragraph with a planned pause that was not tapped does not count for the pace', () => {
    const run = summarizeRun(plan, [ev(0, 'start', 1), ev(25, 'next', 2), ev(60, 'next', 3), ev(66, 'end', 3)])
    expect(run.pace).toBe(Math.round(((65 + 13) / (25 + 6)) * 60))
  })

  test('taps are kept for the paragraph they were pressed in', () => {
    const run = summarizeRun(plan, [ev(0, 'start', 1), ev(10, 'slower', 1), ev(12, 'laugh', 1), ev(14, 'laugh', 1), ev(26, 'next', 2), ev(30, 'stumble', 2), ev(50, 'end', 2)])
    expect(run.rows[0].taps).toEqual({ slower: 1, faster: 0, laugh: 2, stumble: 0 })
    expect(run.rows[1].taps.stumble).toBe(1)
    expect(run.rows[1].completed).toBe(false)
    expect(run.finished).toBe(false)
  })

  test('going back counts the second reading on top of the first, and an open pause ends with the run', () => {
    const run = summarizeRun(plan, [ev(0, 'start', 1), ev(20, 'next', 2), ev(25, 'back', 1), ev(40, 'pause-start', 1), ev(45, 'end', 1)])
    expect(run.rows[0].actual).toBeCloseTo(20 + 20, 5)
    expect(run.rows[0].pauses).toEqual([{ seconds: 5 }])
  })

  test('a pause still open when the speaker moves on ends there, in the paragraph it began in', () => {
    const run = summarizeRun(plan, [ev(0, 'start', 1), ev(20, 'pause-start', 1), ev(25, 'next', 2), ev(60, 'next', 3), ev(66, 'end', 3)])
    expect(run.rows[0].pauses).toEqual([{ seconds: 5 }])
    expect(run.rows[1].pauses).toEqual([])
  })

  test('nothing read, nothing said', () => {
    expect(summarizeRun(plan, []).rows).toEqual([])
    expect(summarizeRun(plan, [ev(0, 'start', 1), ev(2, 'end', 1)]).pace).toBeNull()
    expect(formatRun(summarizeRun(plan, []), { date: new Date(2026, 9, 8, 9, 5) })).toContain('Nothing was timed')
  })

  test('the block of Markdown for the file beside the speech', () => {
    const text = formatRun(summarizeRun(plan, full), { date: new Date(2026, 9, 8, 9, 5), baseline: 130 })
    expect(text.split('\n')[0]).toBe('## Rehearsal 2026-10-08 09:05')
    expect(text).toContain('| 1 | Opening | word word word')
    expect(text).toContain('paused 8 s (planned 1)')
    expect(text).toContain('Measured pace: 148 words per minute (the plan used 130)')
    expect(text).toContain('Planned 1:11, read in 1:06 (-0:05)')
  })

  test('runs are added at the end of the file and the last one can be found again', () => {
    const a = formatRun(summarizeRun(plan, full), { date: new Date(2026, 9, 8, 9, 5) })
    const b = formatRun(summarizeRun(plan, full), { date: new Date(2026, 9, 9, 10, 0) })
    const file = appendRun(appendRun('', a, 'Rehearsals of Talk'), b)
    expect(file.startsWith('# Rehearsals of Talk\n\n## Rehearsal 2026-10-08')).toBe(true)
    expect(file.match(/^## Rehearsal /gm)).toHaveLength(2)
    expect(lastRun(file).startsWith('## Rehearsal 2026-10-09 10:00')).toBe(true)
    expect(lastRun('# nothing')).toBe('')
  })
})
