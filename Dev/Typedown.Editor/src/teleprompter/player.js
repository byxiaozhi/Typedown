// The teleprompter's clockwork (docs/speech-marks-design.md, step 2): where a moment of the planned speech is in the plan,
// what the next pause is, which section it is, and what the speaking clock shows. No DOM, no timers: functions of the plan
// and of two numbers, `P` (the place in the planned speech, in seconds) and `elapsed` (the seconds the speaker has really
// been speaking). Pure, so it is tested alone; Teleprompter.tsx draws it and keeps the time.

import { trafficLight } from '../components/Muya/lib/parser/speechTiming'

export const STEP = 1.5 // seconds into a block after which "back" goes to the start of the same block

// The block that holds second `P` (the last one at the end of the talk), or null for an empty plan.
export const blockAt = (plan, P) => {
  const { blocks } = plan
  if (!blocks.length) return null
  if (P >= plan.total) return blocks[blocks.length - 1]
  let found = blocks[0]
  for (const block of blocks) {
    if (block.start <= P) found = block
    else break
  }
  return found
}

// Where second `P` is inside its block: the segment that holds it (a run of words or a pause) and how far through that
// segment it is (0 to 1). The teleprompter moves its reading line through the text with it.
// -> { block, segment, within } or null.
export const locate = (plan, P) => {
  const block = blockAt(plan, P)
  if (!block) return null
  const inside = Math.min(Math.max(P - block.start, 0), Math.max(block.end - block.start, 0))
  let found = null
  for (const segment of block.segments) {
    if (segment.type !== 'words' && segment.type !== 'pause') continue
    if (!segment.seconds) continue
    if (segment.at <= inside) found = segment
    if (segment.at + segment.seconds > inside) break
  }
  if (!found) {
    // nothing is timed in this block (a title): its first piece
    return { block, segment: block.segments[0], within: 0 }
  }
  return { block, segment: found, within: Math.min(1, Math.max(0, (inside - found.at) / found.seconds)) }
}

// The start of the block after the one that holds `P` (or the end of the talk).
export const nextBlockStart = (plan, P) => {
  for (const block of plan.blocks) if (block.start > P + 0.05) return block.start
  return plan.total
}

// The start of the block that holds `P`, or of the one before it when `P` is hardly into it.
export const prevBlockStart = (plan, P) => {
  const block = blockAt(plan, P)
  if (!block) return 0
  if (P - block.start > STEP) return block.start
  return block.index > 0 ? plan.blocks[block.index - 1].start : 0
}

// The next pause: { active, in | remaining, seconds, audience, name, note } or null when none is left. While `P` is in a
// pause it is `active` and `remaining` says how long it still lasts; before one, `in` says how long until it starts.
export const upcoming = (plan, P) => {
  const first = blockAt(plan, P)
  if (!first) return null
  for (let i = first.index; i < plan.blocks.length; i++) {
    const block = plan.blocks[i]
    for (const s of block.segments) {
      if (s.type !== 'pause') continue
      const from = block.start + s.at
      const to = from + s.seconds
      if (to <= P) continue
      if (from <= P) return { active: true, remaining: to - P, seconds: s.seconds, audience: s.audience, name: s.name, note: s.note }
      return { active: false, in: from - P, seconds: s.seconds, audience: s.audience, name: s.name, note: s.note }
    }
  }
  return null
}

// The section that holds `P`: the deepest one whose time contains it. -> { index, title, level, start, end, budget } or null.
export const sectionAt = (plan, P) => {
  let best = null
  plan.sections.forEach((s, index) => {
    const inside = P >= s.start && (P < s.end || (P >= plan.total && s.end >= plan.total))
    if (inside && (!best || s.level >= best.level)) best = { index, title: s.title, level: s.level, start: s.start, end: s.end, budget: s.budget }
  })
  return best
}

// What the speaking clock shows. `elapsed` is the time really spoken, `P` the place in the plan.
// -> { elapsed, total, remaining (negative: over), ahead (seconds the speaker is ahead of the plan; negative: behind),
//      light, section: { title, level, budget, left } | null }
export const clockState = (plan, elapsed, P) => {
  const section = sectionAt(plan, P)
  const target = plan.budget > 0 ? plan.budget : plan.total
  return {
    elapsed,
    total: plan.total,
    remaining: plan.total - elapsed,
    ahead: P - elapsed,
    light: trafficLight(elapsed, target),
    section: section ? { title: section.title, level: section.level, budget: section.budget, left: Math.max(0, section.end - P) } : null
  }
}
