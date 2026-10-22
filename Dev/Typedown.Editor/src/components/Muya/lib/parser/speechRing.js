// The Speech ring: where everything is (docs/speech-marks-design.md, section 5.4).
//
// Pure functions, no DOM, so the geometry, the flick and the keyboard can be tested alone; services/speechRing.ts draws
// what they say. Coordinates are in pixels relative to the centre of the ring, x to the right and y downwards; angles
// are in degrees, 0 pointing to the right and growing clockwise, so -90 is straight up.

export const PETALS = ['time', 'pace', 'volume', 'tone', 'cue', 'mine']
export const MAX_ITEMS = 8 // an arc shows at most eight items; the eighth is "More..." when there are more

export const CENTER_RADIUS = 24 // the round button in the middle: the ordinary menu
export const PETAL_RADIUS = 66 // the distance of the petals from the centre
export const PETAL_SIZE = 27 // the radius of a petal
export const ARC_INNER = PETAL_RADIUS + PETAL_SIZE + 10 // the nearest an item of an arc comes to the centre
export const ARC_SPAN = 170 // an arc is not longer than this many degrees
export const ITEM_HEIGHT = 30
export const EDGE = 6 // how close to the border of the window anything may come

const rad = degrees => (degrees * Math.PI) / 180
const deg = radians => (radians * 180) / Math.PI
const normalize = angle => ((angle % 360) + 360) % 360

// The six petals, clockwise, the first straight up.
export const petalAngle = (index, count = PETALS.length) => -90 + (360 / count) * index

// The place of the petals: [{ x, y, angle }].
export const layoutPetals = (count = PETALS.length) =>
  Array.from({ length: count }, (_, i) => {
    const angle = petalAngle(i, count)
    return { angle, x: Math.cos(rad(angle)) * PETAL_RADIUS, y: Math.sin(rad(angle)) * PETAL_RADIUS }
  })

// At most MAX_ITEMS items: when there are more, the first seven and a "More..." entry that opens the group in the
// Speech card. The order is kept.
export const limitItems = (items, more) => {
  if (items.length <= MAX_ITEMS) return items
  return [...items.slice(0, MAX_ITEMS - 1), { ...more, more: true }]
}

// The width of an item from its label: wide enough to read, never wider than 136 px (the label is cut short, the
// full text is in its name for a screen reader and in the preview).
export const itemWidth = label => Math.max(56, Math.min(136, 22 + [...label].length * 7))

// Two boxes (centre and size) that overlap, with a small gap.
const overlap = (a, b, gap) => Math.abs(a.x - b.x) < (a.w + b.w) / 2 + gap && Math.abs(a.y - b.y) < (a.h + b.h) / 2 + gap

// The items of one petal along an outer arc, in the direction of the petal: the middle item (the middle two when there
// is an even number) lies in it, the others follow on both sides, the first item at the counter-clockwise end, so what
// means "more" (faster, longer, louder) is clockwise. Items are boxes { w, h }. Each box is put so its nearest edge is
// `inner` from the centre (a wide box at the side of the ring is further out than one at the top), then a little further
// along the arc until it does not overlap the one before it; when the arc would be longer than ARC_SPAN degrees the
// whole thing is tried again further out. -> { items: [{ x, y, w, h, angle }], inner, span }.
export const layoutArc = (centerAngle, sizes, gap = 4) => {
  if (!sizes.length) return { items: [], inner: ARC_INNER, span: 0 }
  let inner = ARC_INNER
  for (;;) {
    const at = (angle, i) => {
      const c = Math.cos(rad(angle))
      const s = Math.sin(rad(angle))
      const distance = inner + (sizes[i].w / 2) * Math.abs(c) + (sizes[i].h / 2) * Math.abs(s)
      return { x: c * distance, y: s * distance, w: sizes[i].w, h: sizes[i].h, angle }
    }
    // the next box away from `from`, in the direction `step` (+0.5 clockwise, -0.5 counter-clockwise)
    const away = (from, i, step) => {
      let a = from.angle
      for (let k = 0; k < 1440; k++) {
        a += step
        const box = at(a, i)
        if (!overlap(box, from, gap)) return box
      }
      return at(a, i)
    }
    const n = sizes.length
    const boxes = new Array(n)
    const mid = Math.floor((n - 1) / 2)
    boxes[mid] = at(centerAngle, mid)
    if (n % 2 === 0) {
      // two items share the middle: they are moved apart, by the same angle, until they do not touch
      let half = 0
      for (let k = 0; k < 720; k++) {
        half += 0.5
        if (!overlap(at(centerAngle - half, mid), at(centerAngle + half, mid + 1), gap)) break
      }
      boxes[mid] = at(centerAngle - half, mid)
      boxes[mid + 1] = at(centerAngle + half, mid + 1)
    }
    for (let i = mid - 1; i >= 0; i--) boxes[i] = away(boxes[i + 1], i, -0.5)
    for (let i = (n % 2 === 0 ? mid + 2 : mid + 1); i < n; i++) boxes[i] = away(boxes[i - 1], i, 0.5)
    const span = boxes[n - 1].angle - boxes[0].angle
    if (span <= ARC_SPAN || inner >= ARC_INNER + 120) return { items: boxes, inner, span }
    inner += 10
  }
}

// Where the ring is opened so it is not cut by the border of the window: the pointer's place, moved inwards when the
// petals would reach out of the window. `view` is { width, height }.
export const place = (x, y, view) => {
  const reach = PETAL_RADIUS + PETAL_SIZE + EDGE
  const clamp = (v, size) => (size < reach * 2 ? size / 2 : Math.min(Math.max(v, reach), size - reach))
  return { x: clamp(x, view.width), y: clamp(y, view.height) }
}

// Items of an arc moved back into the window when the ring is near its border (`origin` is the ring's centre in
// the window). Moving can bring two items near each other, which is better than cutting one off.
export const keepInside = (items, origin, view) =>
  items.map(item => {
    const x = Math.min(Math.max(origin.x + item.x, EDGE + item.w / 2), view.width - EDGE - item.w / 2) - origin.x
    const y = Math.min(Math.max(origin.y + item.y, EDGE + item.h / 2), view.height - EDGE - item.h / 2) - origin.y
    return { ...item, x, y }
  })

// How far the items of an arc reach out of the window, in pixels (0 when they are all inside).
const overflow = (items, origin, view) =>
  items.reduce((sum, item) => {
    const left = EDGE - (origin.x + item.x - item.w / 2)
    const right = origin.x + item.x + item.w / 2 - (view.width - EDGE)
    const top = EDGE - (origin.y + item.y - item.h / 2)
    const bottom = origin.y + item.y + item.h / 2 - (view.height - EDGE)
    return sum + Math.max(0, left) + Math.max(0, right) + Math.max(0, top) + Math.max(0, bottom)
  }, 0)

// The arc of a petal, turned around the ring when it would leave the window: the direction of the petal first, then up
// to 90 degrees either way (the nearest turn that fits). The order along the arc is always clockwise, so what the
// position means is kept. When no turn fits, the one that leaves least outside is used and the items are moved in.
// -> { items, inner, span, turn, fitted }
export const layoutArcIn = (centerAngle, sizes, origin, view) => {
  let best = null
  for (const turn of [0, 12, -12, 24, -24, 36, -36, 48, -48, 60, -60, 75, -75, 90, -90, 110, -110, 130, -130, 160, -160, 180]) {
    const laid = layoutArc(centerAngle + turn, sizes)
    const out = overflow(laid.items, origin, view)
    if (out === 0) return { ...laid, turn, fitted: true }
    if (!best || out < best.out) best = { laid, out, turn }
  }
  return { ...best.laid, items: keepInside(best.laid.items, origin, view), turn: best.turn, fitted: false }
}

// What is under the pointer, for the flick (the right button held, the pointer moved, the button let go) and for
// moving over the ring. `dx`, `dy` are relative to the centre of the ring; `open` is the index of the petal whose arc
// is open (or -1) and `arc` its items from layoutArc. A petal is a sector of the circle around the centre, so a quick
// movement in its direction is enough.
// -> { kind: 'center' } | { kind: 'petal', petal } | { kind: 'item', petal, item } | { kind: 'none' }
export const hit = (dx, dy, open, arc, count = PETALS.length) => {
  const distance = Math.hypot(dx, dy)
  if (distance <= CENTER_RADIUS) return { kind: 'center' }
  if (open >= 0 && arc) {
    for (let i = 0; i < arc.length; i++) {
      const item = arc[i]
      if (Math.abs(dx - item.x) <= item.w / 2 + 3 && Math.abs(dy - item.y) <= item.h / 2 + 3) return { kind: 'item', petal: open, item: i }
    }
  }
  if (distance <= PETAL_RADIUS + PETAL_SIZE + 14) {
    const size = 360 / count
    const angle = normalize(deg(Math.atan2(dy, dx)) + 90 + size / 2)
    return { kind: 'petal', petal: Math.floor(angle / size) % count }
  }
  return { kind: 'none' }
}

// The keyboard. `state` is { level: 'petals' | 'arc', petal, item }; `key` is a name of KeyboardEvent.key; `counts` are
// the numbers of items of each petal. -> { state, action } where action is null, 'close', { open: petal },
// { apply: { petal, item } } or 'back'.
export const keyStep = (state, key, counts) => {
  const petals = counts.length
  const next = (n, size, step) => (size ? (n + step + size) % size : 0)
  const forward = key === 'ArrowRight' || key === 'ArrowDown'
  const backward = key === 'ArrowLeft' || key === 'ArrowUp'
  if (key === 'Escape') return { state, action: 'close' }
  if (state.level === 'petals') {
    if (forward || backward) return { state: { ...state, petal: next(state.petal, petals, forward ? 1 : -1) }, action: null }
    if (key === 'Enter' || key === ' ') {
      if (!counts[state.petal]) return { state, action: null }
      return { state: { level: 'arc', petal: state.petal, item: 0 }, action: { open: state.petal } }
    }
    if (/^[1-9]$/.test(key) && Number(key) <= petals && counts[Number(key) - 1]) {
      const petal = Number(key) - 1
      return { state: { level: 'arc', petal, item: 0 }, action: { open: petal } }
    }
    return { state, action: null }
  }
  const size = counts[state.petal]
  if (forward || backward) return { state: { ...state, item: next(state.item, size, forward ? 1 : -1) }, action: null }
  if (key === 'Home') return { state: { ...state, item: 0 }, action: null }
  if (key === 'End') return { state: { ...state, item: Math.max(0, size - 1) }, action: null }
  if (key === 'Backspace') return { state: { level: 'petals', petal: state.petal, item: 0 }, action: 'back' }
  if (key === 'Enter' || key === ' ') return { state, action: { apply: { petal: state.petal, item: state.item } } }
  if (/^[1-9]$/.test(key) && Number(key) <= size) {
    const item = Number(key) - 1
    return { state: { ...state, item }, action: { apply: { petal: state.petal, item } } }
  }
  return { state, action: null }
}
