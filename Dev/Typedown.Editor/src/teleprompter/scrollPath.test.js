import { groupLines, buildPath, yAt, follow, smooth, tAtY, nearestLine, steadyPath, pageY } from './scrollPath'

describe('teleprompter scroll path', () => {
  // three lines of a paragraph, 3 seconds each, 66 px apart; then a paragraph further down (a gap of 20 px more)
  const pieces = [
    { from: 0, to: 3, y: 33 },
    { from: 3, to: 6, y: 99 },
    { from: 6, to: 9, y: 165 },
    { from: 9, to: 12, y: 251 }
  ]

  test('pieces on the same line are one line, read from the first to the last second', () => {
    const lines = groupLines([
      { from: 0, to: 1, y: 33 },
      { from: 1, to: 2, y: 37 }, // a louder word sits a little lower
      { from: 2, to: 3, y: 30 },
      { from: 3, to: 6, y: 99 }
    ], 20)
    expect(lines).toEqual([{ from: 0, to: 3, y: 33 }, { from: 3, to: 6, y: 99 }])
  })

  test('rubbish pieces are left out', () => {
    expect(groupLines([{ from: NaN, to: 1, y: 3 }, { from: 0, to: 1, y: 3 }], 10)).toHaveLength(1)
  })

  test('the path is a function of time and holds still before the start and after the end', () => {
    const path = buildPath(groupLines(pieces, 20), 12)
    expect(path[0]).toEqual({ t: 0, y: 33 })
    for (let i = 1; i < path.length; i++) expect(path[i].t).toBeGreaterThan(path[i - 1].t)
    expect(yAt(path, -5)).toBe(33)
    expect(yAt(path, 0)).toBe(33)
    expect(yAt(path, 12)).toBe(251)
    expect(yAt(path, 99)).toBe(251)
  })

  test('the reading line glides from one line to the next: no jump', () => {
    const path = buildPath(groupLines(pieces, 20), 12)
    expect(yAt(path, 1.5)).toBeCloseTo(33, 6)
    expect(yAt(path, 4.5)).toBeCloseTo(99, 6)
    expect(yAt(path, 3)).toBeCloseTo(66, 6) // halfway between two lines at their boundary
    // the biggest step in 1/60 second anywhere is small, even across the gap between paragraphs
    let biggest = 0
    let previous = yAt(path, 0)
    for (let t = 1 / 60; t <= 12; t += 1 / 60) {
      const y = yAt(path, t)
      biggest = Math.max(biggest, Math.abs(y - previous))
      previous = y
    }
    expect(biggest).toBeLessThan(66 / 3 / 60 * 1.6 + 0.01)
  })

  test('it never goes back while the time goes on', () => {
    const path = buildPath(groupLines(pieces, 20), 12)
    let previous = -1
    for (let t = 0; t <= 12; t += 0.05) {
      const y = yAt(path, t)
      expect(y).toBeGreaterThanOrEqual(previous - 1e-9)
      previous = y
    }
  })

  test('two moments that are the same second leave one point', () => {
    const path = buildPath([{ from: 0, to: 0, y: 10 }, { from: 0, to: 0, y: 40 }], 5)
    expect(path.filter(k => k.t === 0)).toHaveLength(1)
    expect(yAt(path, 0)).toBe(40)
  })

  test('an empty page has an empty path', () => {
    expect(buildPath([], 10)).toEqual([])
    expect(yAt([], 3)).toBe(0)
  })

  test('smoothing spreads the gap between paragraphs, so the speed stays nearly the same', () => {
    // lines every 66 px, 4.5 s each, and 43 px more between the paragraphs (a margin)
    const lines = []
    let y = 33
    for (let p = 0; p < 6; p++) {
      for (let l = 0; l < 4; l++) { lines.push({ from: (p * 4 + l) * 4.5, to: (p * 4 + l + 1) * 4.5, y }); y += 66 }
      y += 43
    }
    const total = 24 * 4.5
    const raw = buildPath(lines, total)
    const soft = smooth(raw, 8)
    const peak = path => { let m = 0; for (let t = 5; t < total - 5; t += 0.25) m = Math.max(m, (yAt(path, t + 0.25) - yAt(path, t)) / 0.25); return m }
    expect(peak(soft)).toBeLessThan(peak(raw) * 0.88)
    // it stays near the true line (within half of the extra distance and a bit) and never goes back
    for (let t = 0; t <= total; t += 0.5) expect(Math.abs(yAt(soft, t) - yAt(raw, t))).toBeLessThan(45)
    let previous = -1
    for (let t = 0; t <= total; t += 0.25) { const v = yAt(soft, t); expect(v).toBeGreaterThanOrEqual(previous - 1e-9); previous = v }
    // the start and the end are where they were
    expect(yAt(soft, 0)).toBe(yAt(raw, 0))
    expect(yAt(soft, total)).toBe(yAt(raw, total))
  })

  test('smoothing leaves no step at the start or the end of the talk, at any window', () => {
    const lines = []
    for (let l = 0; l < 20; l++) lines.push({ from: l * 4.5, to: (l + 1) * 4.5, y: 33 + l * 66 })
    const raw = buildPath(lines, 90)
    for (const window of [2, 6, 18]) {
      const soft = smooth(raw, window)
      const average = (yAt(raw, 90) - yAt(raw, 0)) / 90
      for (const [from, to] of [[0, 4], [86, 90]]) {
        for (let t = from; t < to; t += 0.25) expect((yAt(soft, t + 0.25) - yAt(soft, t)) / 0.25).toBeLessThan(average * 3)
      }
      expect(yAt(soft, 0)).toBe(yAt(raw, 0))
      expect(yAt(soft, 90)).toBe(yAt(raw, 90))
    }
  })

  test('a path shorter than the smoothing window still starts and ends where it did', () => {
    const path = [{ t: 0, y: 0 }, { t: 0.5, y: 50 }, { t: 1, y: 100 }]
    const soft = smooth(path, 6)
    expect(yAt(soft, 0)).toBe(0)
    expect(yAt(soft, 1)).toBe(100)
    let previous = -1
    for (let t = 0; t <= 1; t += 0.05) { const y = yAt(soft, t); expect(y).toBeGreaterThanOrEqual(previous - 1e-9); previous = y }
  })

  test('the height of a line on the page is counted from the top, or from the bottom edge when the page is upside down', () => {
    const page = { top: 100, bottom: 700 }
    // a line 40 px high, 50 px below the top of the page as it is drawn
    expect(pageY({ top: 150, height: 40 }, page, false)).toBe(70)
    // the same line on the page turned upside down: it is drawn 50 px above the bottom edge
    expect(pageY({ top: 610, height: 40 }, page, true)).toBe(70)
    // so the lines still go down the page, in the same order, flipped or not
    const flipped = [{ top: 640, height: 40 }, { top: 580, height: 40 }].map(r => pageY(r, page, true))
    expect(flipped[1]).toBeGreaterThan(flipped[0])
  })

  test('a path too short to smooth is left as it is', () => {
    const path = [{ t: 0, y: 1 }, { t: 5, y: 9 }]
    expect(smooth(path, 6)).toBe(path)
  })

  test('the second at a height is the inverse of the height at a second', () => {
    const path = buildPath(groupLines(pieces, 20), 12)
    for (const t of [2, 3.7, 6, 8.2, 10]) expect(tAtY(path, yAt(path, t))).toBeCloseTo(t, 6)
    expect(tAtY(path, -100)).toBe(0)
    expect(tAtY(path, 9999)).toBe(12)
    expect(tAtY([], 5)).toBe(0)
  })

  test('a flat stretch of the path gives its first second', () => {
    expect(tAtY([{ t: 0, y: 10 }, { t: 4, y: 10 }, { t: 8, y: 50 }], 10)).toBe(0)
  })

  test('a click finds the nearest line, and nothing when it is far from every line', () => {
    const lines = [{ from: 0, to: 3, y: 33 }, { from: 3, to: 6, y: 99 }, { from: 9, to: 12, y: 251 }]
    expect(nearestLine(lines, 90, 40)).toEqual({ from: 3, to: 6, y: 99 })
    expect(nearestLine(lines, 150, 40)).toBeNull()
    expect(nearestLine([], 5, 40)).toBeNull()
  })

  test('a text at a constant speed goes straight from the start to the end, at the same pace everywhere', () => {
    const raw = buildPath(groupLines(pieces, 20), 12)
    const path = steadyPath(raw, [], 12)
    expect(yAt(path, 0)).toBe(33)
    expect(yAt(path, 12)).toBe(251)
    const slope = (yAt(path, 7) - yAt(path, 6)) / 1
    for (const t of [0.5, 3, 5, 9, 11]) expect((yAt(path, t + 1) - yAt(path, t)) / 1).toBeCloseTo(slope, 6)
  })

  test('a pause stands still for its seconds, on its line, and the ends keep the plan', () => {
    const raw = buildPath(groupLines(pieces, 20), 12)
    const path = steadyPath(raw, [{ from: 4, to: 7, y: 99 }], 12)
    expect(yAt(path, 4)).toBeCloseTo(99, 6)
    expect(yAt(path, 5.5)).toBeCloseTo(99, 6)
    expect(yAt(path, 7)).toBeCloseTo(99, 6)
    expect(yAt(path, 12)).toBe(251)
    // before and after the pause the speed is each its own constant, and it never goes back
    let previous = -1
    for (let t = 0; t <= 12; t += 0.1) { const y = yAt(path, t); expect(y).toBeGreaterThanOrEqual(previous - 1e-9); previous = y }
    const before = yAt(path, 3) - yAt(path, 2)
    expect(yAt(path, 1) - yAt(path, 0)).toBeCloseTo(before, 6)
  })

  test('pauses that touch, or lie on the same line, leave a path that is still a function of time', () => {
    const raw = buildPath(groupLines(pieces, 20), 12)
    const path = steadyPath(raw, [{ from: 3, to: 5, y: 99 }, { from: 5, to: 6, y: 99 }, { from: 6.01, to: 6.02, y: 99 }], 12)
    for (let i = 1; i < path.length; i++) expect(path[i].t).toBeGreaterThan(path[i - 1].t)
    expect(yAt(path, 5.5)).toBeCloseTo(99, 6)
  })

  test('a path that cannot be made constant (nothing to move) is the path of the plan', () => {
    const flat = buildPath([{ from: 0, to: 4, y: 10 }], 4)
    expect(steadyPath(flat, [], 4)).toBe(flat)
    expect(steadyPath([], [], 4)).toEqual([])
  })

  test('the follower eases to the target, the same whatever the speed of the screen, and rests', () => {
    const run = fps => {
      let pos = 0
      for (let i = 0; i < fps * 0.5; i++) pos = follow(pos, 100, 1 / fps)
      return pos
    }
    expect(run(60)).toBeCloseTo(run(144), 3)
    expect(run(60)).toBeGreaterThan(90)
    expect(follow(99.99, 100, 1 / 60)).toBe(100)
  })
})
