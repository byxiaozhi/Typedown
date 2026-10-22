// The teleprompter's scroll path: where the page must be at every second of the planned speech so that the text moves
// smoothly. The page is measured once (every line of the text, with the seconds the plan gives it) and the path is a
// line through the middle of each text line, drawn in time: between two lines the page glides, it never jumps, and
// nothing is measured while it moves. No DOM, no timers: plain functions, tested alone; Teleprompter.tsx measures and draws.
//
// A piece is { from, to, y }: the seconds a line (or a part of one) is read, and the height of its middle on the page.

// Pieces that lie on the same line of the page (their middles are closer than `tolerance`: a louder or softer word sits a
// little higher or lower than its neighbours) are one line, read from the first second to the last. -> [{ from, to, y }]
export const groupLines = (pieces, tolerance) => {
  const sorted = pieces.filter(p => isFinite(p.from) && isFinite(p.to) && isFinite(p.y)).sort((a, b) => a.from - b.from || a.y - b.y)
  const lines = []
  for (const p of sorted) {
    const last = lines[lines.length - 1]
    if (last && Math.abs(p.y - last.y) < tolerance) {
      last.to = Math.max(last.to, p.to)
    } else {
      lines.push({ from: p.from, to: p.to, y: p.y })
    }
  }
  return lines
}

// The key points of the path: the middle of each line in time, at the height of the middle of the line, with the start and the
// end of the talk held still. -> [{ t, y }] with `t` rising.
export const buildPath = (lines, total) => {
  if (!lines.length) return []
  const keys = lines.map(l => ({ t: (l.from + l.to) / 2, y: l.y }))
  const path = []
  const push = key => {
    const last = path[path.length - 1]
    if (last && key.t <= last.t) {
      // the same moment (a heading of no time): the later one wins, so the path stays a function of time
      last.y = key.y
      return
    }
    path.push(key)
  }
  if (keys[0].t > 0) push({ t: 0, y: keys[0].y })
  keys.forEach(push)
  const end = Math.max(total, path[path.length - 1].t)
  if (end > path[path.length - 1].t) push({ t: end, y: path[path.length - 1].y })
  return path
}

// The height of the reading line at second `t`: straight lines between the key points, held before the first and after the last.
export const yAt = (path, t) => {
  if (!path.length) return 0
  if (t <= path[0].t) return path[0].y
  const last = path[path.length - 1]
  if (t >= last.t) return last.y
  let lo = 0
  let hi = path.length - 1
  while (hi - lo > 1) {
    const mid = (lo + hi) >> 1
    if (path[mid].t <= t) lo = mid
    else hi = mid
  }
  const a = path[lo]
  const b = path[hi]
  return a.y + (b.y - a.y) * ((t - a.t) / (b.t - a.t))
}

// The height of the middle of a rectangle (a line of the text) on the page, from the top of the page: `rect` and `page` are what the
// browser reports in the window. When the page is turned upside down (Flip) the browser reports it turned: the top of the page is its
// bottom edge there, and the heights are counted from it.
export const pageY = (rect, page, flipped) => (flipped ? page.bottom - (rect.top + rect.height / 2) : rect.top + rect.height / 2 - page.top)

// The inverse of yAt: the second at which the reading line is at height `y` (the first such second where the path stands still).
// The wheel moves the page by a distance; this says which moment of the talk that is. Clamped to the ends of the path.
export const tAtY = (path, y) => {
  if (!path.length) return 0
  if (y <= path[0].y) return path[0].t
  const last = path[path.length - 1]
  if (y >= last.y) return last.t
  let lo = 0
  let hi = path.length - 1
  while (hi - lo > 1) {
    const mid = (lo + hi) >> 1
    if (path[mid].y < y) lo = mid
    else hi = mid
  }
  const a = path[lo]
  const b = path[hi]
  if (b.y === a.y) return a.t
  return a.t + (b.t - a.t) * ((y - a.y) / (b.y - a.y))
}

// The line of the text nearest to height `y` (a click), if it is within `tolerance` of it: { from, to, y } or null.
// A click between paragraphs, far from any line, moves nothing.
export const nearestLine = (lines, y, tolerance) => {
  let best = null
  let distance = Infinity
  for (const line of lines) {
    const d = Math.abs(line.y - y)
    if (d < distance) { best = line; distance = d }
  }
  return best && distance <= tolerance ? best : null
}

// The path with its corners rubbed off: every point is the average of the path over `window` seconds around it. The distance
// between two paragraphs (their margin, a heading) is more than between two lines, and straight lines between the middles of
// lines would make the page hurry there; this spreads it over a few seconds, so the speed stays nearly the same all through the
// talk, and the reading line is never further from its line than half of that distance. The start and the end stay where they are.
export const smooth = (path, window = 6, step = Math.max(0.25, window / 24)) => {
  if (path.length < 3 || window <= 0) return path
  const first = path[0].t
  const last = path[path.length - 1].t
  const half = window / 2
  const out = []
  for (let t = first; t < last + step / 2; t += step) {
    const at = Math.min(t, last)
    let sum = 0
    let n = 0
    for (let u = at - half; u <= at + half + 1e-9; u += step) {
      sum += yAt(path, u)
      n++
    }
    out.push({ t: at, y: sum / n })
  }
  if (out[out.length - 1].t < last) out.push({ t: last, y: out[out.length - 1].y })
  // The average lags at the ends (the path is held there, the average is behind it): the difference is taken away gradually over one
  // window, so the path starts and ends exactly where it did without a step.
  const lag0 = path[0].y - out[0].y
  const lag1 = path[path.length - 1].y - out[out.length - 1].y
  const ease = x => { const k = Math.min(1, Math.max(0, x)); return k * k * (3 - 2 * k) }
  // a path shorter than the window: the two corrections share the whole path, so each end still ends where it was
  const span = Math.max(1e-6, Math.min(window, last - first))
  let top = -Infinity
  for (const point of out) {
    point.y += lag0 * (1 - ease((point.t - first) / span)) + lag1 * ease((point.t - (last - span)) / span)
    top = Math.max(top, point.y)
    point.y = top
  }
  return out
}

// The path of a text that moves at a constant speed: straight from the start to the end, standing still for the seconds of every pause
// on the line the pause is on. It keeps the plan only where it matters: the talk starts and ends when the plan says, and every pause
// starts and ends when the plan says; between two pauses the pace of the words, slow and fast marks and the length of the lines do not
// matter, the page moves evenly. `raw` is the path of the plan (buildPath), `pauses` are { from, to, y }.
export const steadyPath = (raw, pauses, total) => {
  if (raw.length < 2) return raw
  const y0 = raw[0].y
  const y1 = raw[raw.length - 1].y
  if (!(y1 > y0)) return raw
  const path = []
  const push = (t, y) => {
    const last = path[path.length - 1]
    if (last && t <= last.t) {
      // the same moment: the later place wins, so the path stays a function of time
      last.y = Math.max(last.y, y)
      return
    }
    path.push({ t, y: Math.max(y, last ? last.y : y) })
  }
  push(0, y0)
  const holds = pauses.filter(p => p.to - p.from > 0.05).sort((a, b) => a.from - b.from)
  for (const p of holds) {
    const y = Math.min(y1, Math.max(y0, p.y))
    push(p.from, y)
    push(p.to, y)
  }
  push(Math.max(total, path[path.length - 1].t + 0.001), y1)
  return path
}

// One step of the follower that draws the page: it moves from `pos` toward `target` and eases out, whatever the speed of the
// screen (`dt` in seconds, `tau` the time it takes to cover most of the way). A tiny rest is closed at once, so the page stops.
export const follow = (pos, target, dt, tau = 0.18) => {
  const gap = target - pos
  if (Math.abs(gap) < 0.05) return target
  return pos + gap * (1 - Math.exp(-Math.max(dt, 0) / tau))
}
