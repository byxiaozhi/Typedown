// config and utils import each other; loading config first is the order the app gets from its bundler
import '../components/Muya/lib/config'
import { buildPlan } from './plan'
import { blockAt, locate, nextBlockStart, prevBlockStart, upcoming, sectionAt, clockState } from './player'

const words = n => Array.from({ length: n }, () => 'word').join(' ')

describe('the plan of a talk', () => {
  const talk = [
    '# Opening {budget 1m}', '',
    `${words(65)} {pause 5s} ${words(65)}`, '',
    '## Detail', '',
    `{slow}${words(65)}{/slow} {wait 3s: laugh}`, '',
    '# Close', '',
    '{cue: smile} The end now.'
  ].join('\n')
  const plan = buildPlan(talk)

  test('headings and paragraphs in the order of the text, each starting where the one before ended', () => {
    expect(plan.blocks.map(b => [b.kind, b.title || b.segments[0].type])).toEqual([
      ['heading', 'Opening'], ['paragraph', 'words'], ['heading', 'Detail'], ['paragraph', 'open'], ['heading', 'Close'], ['paragraph', 'cue']
    ])
    plan.blocks.forEach((b, i) => { if (i) expect(b.start).toBeCloseTo(plan.blocks[i - 1].end, 8) })
    expect(plan.blocks[0].start).toBe(0)
  })

  test('the total is the time of the timing panel: words and pauses, headings not read', () => {
    // 130 words 60 s + 5 s pause; 65 words slowly 40 s + 3 s wait; 3 words 1.38 s
    expect(plan.total).toBeCloseTo(60 + 5 + (65 / (130 * 0.75)) * 60 + 3 + (3 / 130) * 60, 5)
    expect(plan.budget).toBe(60)
  })

  test('the pieces of a paragraph: words with their marks, pauses, cues, in order, with the second they start at', () => {
    const first = plan.blocks[1]
    expect(first.segments.map(s => [s.type, Math.round(s.at * 10) / 10])).toEqual([['words', 0], ['pause', 30], ['words', 35]])
    expect(first.segments[1]).toMatchObject({ type: 'pause', seconds: 5, audience: false, name: 'pause' })
    const second = plan.blocks[3]
    expect(second.segments.map(s => s.type)).toEqual(['open', 'words', 'close', 'pause'])
    expect(second.segments[1].styles.map(s => s.name)).toEqual(['slow'])
    expect(second.segments[3]).toMatchObject({ audience: true, note: 'laugh' })
    expect(plan.blocks[5].segments.map(s => s.type)).toEqual(['cue', 'words'])
    expect(plan.blocks[5].segments[0].text).toBe('smile')
  })

  test('a heading is shown as a title even when it is not read; read aloud it is words', () => {
    expect(plan.blocks[0].segments).toEqual([{ type: 'title', at: 0, seconds: 0, text: 'Opening' }])
    const spokenPlan = buildPlan('# Hello there\n\ntext', { headingsSpoken: true })
    expect(spokenPlan.blocks[0].segments[0].type).toBe('words')
    expect(spokenPlan.blocks[0].end).toBeGreaterThan(0)
  })

  test('sections with the time they start and end, subsections inside their parent', () => {
    const at = i => Math.round(plan.blocks[i].start)
    expect(plan.sections.map(s => [s.title, s.level, Math.round(s.start), Math.round(s.end)])).toEqual([
      ['Opening', 1, 0, at(4)], ['Detail', 2, at(2), at(4)], ['Close', 1, at(4), Math.round(plan.total)]
    ])
  })

  test('a pace in the document and the baseline of Settings are used', () => {
    expect(buildPlan(words(130), { wpm: 65 }).total).toBeCloseTo(120, 5)
    expect(buildPlan(`{wpm 260}\n\n${words(130)}`).total).toBeCloseTo(30, 5)
  })

  test('definitions and settings alone are no block, and an empty text is an empty plan', () => {
    expect(buildPlan('{define wave note: wave}\n\n{wpm 140}').blocks).toEqual([])
    expect(buildPlan('').total).toBe(0)
    expect(buildPlan('text with {pause 2s}').blocks[0].segments.map(s => s.type)).toEqual(['words', 'pause'])
  })

  test('what is not spoken (an image, an address) is not shown', () => {
    const p = buildPlan('see ![alt text](a.png) https://example.com here')
    expect(p.blocks[0].segments.map(s => s.text.replace(/\s+/g, ' ').trim())).toEqual(['see here'])
  })
})

describe('where a moment is in the plan', () => {
  const plan = buildPlan(`# A\n\n${words(130)} {pause 10s} ${words(130)}\n\n${words(65)}`)

  test('the block and the piece that hold a second, and how far into the piece', () => {
    // a heading that takes no time sits before the paragraph that starts at the same second: the paragraph is the one at 0
    expect(blockAt(plan, 0).kind).toBe('paragraph')
    expect(plan.blocks[0].kind).toBe('heading')
    const at = locate(plan, 30)
    expect(at.segment.type).toBe('words')
    expect(at.within).toBeCloseTo(0.5, 5)
    const inPause = locate(plan, 65)
    expect(inPause.segment.type).toBe('pause')
    expect(inPause.within).toBeCloseTo(0.5, 5)
    expect(locate(plan, plan.total).block).toBe(plan.blocks[plan.blocks.length - 1])
    expect(locate({ blocks: [], total: 0, sections: [] }, 0)).toBeNull()
  })

  test('next and back by blocks', () => {
    const [h, first, second] = plan.blocks
    // from the start, "next" is the paragraph after the first one (the first one is the one being read)
    expect(nextBlockStart(plan, 0)).toBe(second.start)
    expect(nextBlockStart(plan, 30)).toBeCloseTo(second.start, 5)
    expect(nextBlockStart(plan, second.start + 5)).toBeCloseTo(plan.total, 5)
    expect(prevBlockStart(plan, first.start + 30)).toBeCloseTo(first.start, 5)
    expect(prevBlockStart(plan, first.start + 0.5)).toBe(h.start)
    expect(prevBlockStart(plan, 0)).toBe(0)
  })

  test('the next pause: how long until it, and how long it still lasts while in it', () => {
    expect(upcoming(plan, 0)).toMatchObject({ active: false, seconds: 10 })
    expect(upcoming(plan, 0).in).toBeCloseTo(60, 5)
    expect(upcoming(plan, 62)).toMatchObject({ active: true, seconds: 10 })
    expect(upcoming(plan, 62).remaining).toBeCloseTo(8, 5)
    expect(upcoming(plan, 80)).toBeNull()
  })
})

describe('the speaking clock', () => {
  const plan = buildPlan(`# One {budget 1m}\n\n${words(130)}\n\n## Inner\n\n${words(65)}\n\n# Two {budget 30s}\n\n${words(65)}`)

  test('the section of a moment: the deepest one', () => {
    expect(sectionAt(plan, 10).title).toBe('One')
    expect(sectionAt(plan, 70).title).toBe('Inner')
    expect(sectionAt(plan, 100).title).toBe('Two')
    expect(sectionAt(plan, plan.total).title).toBe('Two')
  })

  test('elapsed, left, ahead or behind, the section and its time left', () => {
    const c = clockState(plan, 20, 20)
    expect(c.total).toBeCloseTo(60 + 30 + 30, 5)
    expect(c.remaining).toBeCloseTo(100, 5)
    expect(c.ahead).toBe(0)
    expect(c.section.title).toBe('One')
    expect(c.section.left).toBeCloseTo(70, 5)
    expect(clockState(plan, 20, 35).ahead).toBe(15)
    expect(clockState(plan, 50, 35).ahead).toBe(-15)
  })

  test('the light follows the time spoken against the budget of the talk, or its planned total', () => {
    expect(plan.budget).toBe(90)
    expect(clockState(plan, 80, 80).light).toBe('green')
    expect(clockState(plan, 95, 95).light).toBe('amber')
    expect(clockState(plan, 120, 120).light).toBe('red')
    const none = buildPlan(words(130))
    expect(clockState(none, 70, 60).light).toBe('red')
    expect(clockState(none, 70, 60).remaining).toBeCloseTo(-10, 5)
  })
})
