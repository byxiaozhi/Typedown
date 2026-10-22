import {
  PETALS, MAX_ITEMS, CENTER_RADIUS, PETAL_RADIUS, PETAL_SIZE, ARC_INNER, ARC_SPAN, EDGE,
  petalAngle, layoutPetals, limitItems, itemWidth, layoutArc, layoutArcIn, place, keepInside, hit, keyStep
} from './speechRing'

const boxesOverlap = (a, b) => Math.abs(a.x - b.x) < (a.w + b.w) / 2 && Math.abs(a.y - b.y) < (a.h + b.h) / 2
const sizes = n => Array.from({ length: n }, (_, i) => ({ w: itemWidth('x'.repeat(4 + (i % 5))), h: 30 }))

describe('the petals', () => {
  test('six petals, clockwise, the first straight up and the order fixed', () => {
    expect(PETALS).toEqual(['time', 'pace', 'volume', 'tone', 'cue', 'mine'])
    expect(petalAngle(0)).toBe(-90)
    expect(petalAngle(1)).toBe(-30)
    expect(petalAngle(3)).toBe(90)
    const [top, , , bottom] = layoutPetals()
    expect(top.y).toBeCloseTo(-PETAL_RADIUS)
    expect(top.x).toBeCloseTo(0)
    expect(bottom.y).toBeCloseTo(PETAL_RADIUS)
  })
})

describe('an arc of items', () => {
  test('no two items overlap, wherever the arc is, and none touches the petals', () => {
    for (const petal of [0, 1, 2, 3, 4, 5]) {
      for (const count of [1, 3, 5, 8]) {
        const { items } = layoutArc(petalAngle(petal), sizes(count))
        expect(items).toHaveLength(count)
        items.forEach((a, i) => items.slice(i + 1).forEach((b, j) => expect([petal, count, i, i + 1 + j, boxesOverlap(a, b)]).toEqual([petal, count, i, i + 1 + j, false])))
        items.forEach(box => {
          // the nearest point of the box is further out than the petals
          const nearestX = Math.max(Math.abs(box.x) - box.w / 2, 0)
          const nearestY = Math.max(Math.abs(box.y) - box.h / 2, 0)
          expect(Math.hypot(nearestX, nearestY)).toBeGreaterThanOrEqual(PETAL_RADIUS + PETAL_SIZE)
        })
      }
    }
  })

  test('the order is clockwise and the arc is centred on its petal', () => {
    const { items } = layoutArc(petalAngle(2), sizes(5))
    const angles = items.map(i => i.angle)
    expect([...angles].sort((a, b) => a - b)).toEqual(angles)
    // the middle item is in the direction of the petal
    expect(angles[2]).toBeCloseTo(petalAngle(2), 5)
    const even = layoutArc(petalAngle(2), sizes(4)).items.map(i => i.angle)
    expect(even[1]).toBeLessThan(petalAngle(2))
    expect(even[2]).toBeGreaterThan(petalAngle(2))
  })

  test('a long arc is moved further out rather than made too long', () => {
    const { span, inner } = layoutArc(-90, sizes(8))
    expect(span).toBeLessThanOrEqual(ARC_SPAN + 20)
    expect(inner).toBeGreaterThanOrEqual(ARC_INNER)
  })

  test('nothing to lay out', () => {
    expect(layoutArc(0, []).items).toEqual([])
  })

  test('more than eight items become seven and "More..."', () => {
    const items = Array.from({ length: 12 }, (_, i) => ({ label: `m${i}` }))
    const limited = limitItems(items, { label: 'More...' })
    expect(limited).toHaveLength(MAX_ITEMS)
    expect(limited[MAX_ITEMS - 1]).toEqual({ label: 'More...', more: true })
    expect(limited[0].label).toBe('m0')
    expect(limitItems(items.slice(0, 8), { label: 'More...' })).toHaveLength(8)
  })

  test('the width of an item follows its label, within limits', () => {
    expect(itemWidth('a')).toBe(56)
    expect(itemWidth('a very long label indeed, much too long')).toBe(136)
    expect(itemWidth('very-slow')).toBeGreaterThan(itemWidth('slow'))
  })
})

describe('keeping the ring in the window', () => {
  const view = { width: 800, height: 600 }
  const reach = PETAL_RADIUS + PETAL_SIZE + EDGE

  test('in the middle it stays where the pointer is', () => {
    expect(place(400, 300, view)).toEqual({ x: 400, y: 300 })
  })

  test('near a border or a corner it moves in, so no petal is cut', () => {
    expect(place(5, 300, view)).toEqual({ x: reach, y: 300 })
    expect(place(795, 595, view)).toEqual({ x: 800 - reach, y: 600 - reach })
  })

  test('a window too small for the ring puts it in the middle', () => {
    expect(place(10, 10, { width: 100, height: 100 })).toEqual({ x: 50, y: 50 })
  })

  test('items that would leave the window are moved back in', () => {
    const origin = { x: 20, y: 300 }
    const moved = keepInside([{ x: -150, y: 0, w: 80, h: 30 }, { x: 100, y: 0, w: 80, h: 30 }], origin, view)
    expect(origin.x + moved[0].x - 40).toBeGreaterThanOrEqual(EDGE)
    expect(moved[1]).toEqual({ x: 100, y: 0, w: 80, h: 30 })
  })
})

describe('an arc near the border of the window', () => {
  const view = { width: 1000, height: 700 }
  const inside = (items, origin) => items.every(i =>
    origin.x + i.x - i.w / 2 >= EDGE && origin.x + i.x + i.w / 2 <= view.width - EDGE &&
    origin.y + i.y - i.h / 2 >= EDGE && origin.y + i.y + i.h / 2 <= view.height - EDGE)

  test('in the middle it is where its petal says', () => {
    const { turn, items } = layoutArcIn(petalAngle(1), sizes(4), { x: 500, y: 350 }, view)
    expect(turn).toBe(0)
    expect(inside(items, { x: 500, y: 350 })).toBe(true)
  })

  test('near a border it turns away from it, and no item overlaps another or leaves the window', () => {
    for (const origin of [{ x: 500, y: 110 }, { x: 890, y: 590 }, { x: 110, y: 350 }, { x: 500, y: 600 }]) {
      for (const petal of [0, 1, 2, 3, 4, 5]) {
        const { items, fitted } = layoutArcIn(petalAngle(petal), sizes(6), origin, view)
        expect(fitted).toBe(true)
        expect(inside(items, origin)).toBe(true)
        items.forEach((a, i) => items.slice(i + 1).forEach(b => expect(boxesOverlap(a, b)).toBe(false)))
      }
    }
  })

  test('in a corner where nothing fits the items are moved in, never out', () => {
    const origin = { x: 100, y: 100 }
    for (const petal of [0, 1, 2, 3, 4, 5]) {
      expect(inside(layoutArcIn(petalAngle(petal), sizes(8), origin, view).items, origin)).toBe(true)
    }
  })

  test('the order along the arc stays clockwise after turning', () => {
    const { items } = layoutArcIn(petalAngle(0), sizes(5), { x: 500, y: 110 }, view)
    const angles = items.map(i => i.angle)
    expect([...angles].sort((a, b) => a - b)).toEqual(angles)
  })
})

describe('the flick: what is under the pointer', () => {
  test('the middle is the ordinary menu', () => {
    expect(hit(0, 0, -1, null)).toEqual({ kind: 'center' })
    expect(hit(CENTER_RADIUS - 1, 0, -1, null)).toEqual({ kind: 'center' })
  })

  test('a petal is a whole sector, so a quick movement in its direction is enough', () => {
    expect(hit(0, -50, -1, null)).toEqual({ kind: 'petal', petal: 0 })
    expect(hit(5, -60, -1, null)).toEqual({ kind: 'petal', petal: 0 })
    expect(hit(60, -30, -1, null)).toEqual({ kind: 'petal', petal: 1 })
    expect(hit(0, 70, -1, null)).toEqual({ kind: 'petal', petal: 3 })
    expect(hit(-55, -30, -1, null)).toEqual({ kind: 'petal', petal: 5 })
  })

  test('far from the ring, and not on an item, is nothing', () => {
    expect(hit(0, -300, -1, null)).toEqual({ kind: 'none' })
  })

  test('an item of the open arc, by its box', () => {
    const { items } = layoutArc(petalAngle(1), sizes(4))
    const target = items[2]
    expect(hit(target.x, target.y, 1, items)).toEqual({ kind: 'item', petal: 1, item: 2 })
    expect(hit(target.x + target.w / 2 + 2, target.y, 1, items).kind).not.toBe('none')
    // without an open arc the same place is nothing
    expect(hit(target.x, target.y, -1, null).kind).toBe('none')
  })
})

describe('the keyboard', () => {
  const counts = [4, 2, 3, 7, 6, 0]
  const start = { level: 'petals', petal: 0, item: 0 }

  test('arrows go around the petals, and round again', () => {
    expect(keyStep(start, 'ArrowRight', counts).state.petal).toBe(1)
    expect(keyStep(start, 'ArrowLeft', counts).state.petal).toBe(5)
    expect(keyStep({ ...start, petal: 5 }, 'ArrowDown', counts).state.petal).toBe(0)
  })

  test('Enter opens the arc of a petal, an empty petal does not open', () => {
    const open = keyStep({ ...start, petal: 3 }, 'Enter', counts)
    expect(open.state).toEqual({ level: 'arc', petal: 3, item: 0 })
    expect(open.action).toEqual({ open: 3 })
    expect(keyStep({ ...start, petal: 5 }, 'Enter', counts).action).toBeNull()
  })

  test('on an arc the arrows move along it, Enter applies', () => {
    let { state } = keyStep({ ...start, petal: 1 }, 'Enter', counts)
    state = keyStep(state, 'ArrowRight', counts).state
    expect(state.item).toBe(1)
    state = keyStep(state, 'ArrowRight', counts).state
    expect(state.item).toBe(0)
    expect(keyStep(state, 'Enter', counts).action).toEqual({ apply: { petal: 1, item: 0 } })
  })

  test('digits pick an item of the open arc, and not one that is not there', () => {
    const arc = { level: 'arc', petal: 1, item: 0 }
    expect(keyStep(arc, '2', counts).action).toEqual({ apply: { petal: 1, item: 1 } })
    expect(keyStep(arc, '3', counts).action).toBeNull()
  })

  test('digits open a petal when no arc is open', () => {
    expect(keyStep(start, '4', counts).action).toEqual({ open: 3 })
    expect(keyStep(start, '6', counts).action).toBeNull()
  })

  test('Escape closes at every level and Backspace goes back to the petals', () => {
    expect(keyStep(start, 'Escape', counts).action).toBe('close')
    expect(keyStep({ level: 'arc', petal: 1, item: 1 }, 'Escape', counts).action).toBe('close')
    expect(keyStep({ level: 'arc', petal: 1, item: 1 }, 'Backspace', counts).state).toEqual({ level: 'petals', petal: 1, item: 0 })
  })
})
