import fs from 'fs'
import path from 'path'

// The sets of colors of the teleprompter (teleprompter.css): every palette must be readable, the text first of all. Pairs of colors
// that are written as plain hex colors are checked with the contrast of WCAG; the ones with transparency are left to the eye.
describe('the palettes of the teleprompter', () => {
  const css = fs.readFileSync(path.join(__dirname, 'teleprompter.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '')
  const palettes = {}
  for (const m of css.matchAll(/body\.(tp-[a-z-]+)\s*\{([^}]*)\}/g)) {
    const vars = {}
    for (const v of m[2].matchAll(/--(tp-[a-z-]+)\s*:\s*([^;]+);/g)) vars[v[1]] = v[2].trim()
    palettes[m[1]] = vars
  }

  const luminance = hex => {
    const c = [1, 3, 5].map(i => parseInt(hex.substr(i, 2), 16) / 255).map(v => (v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4)))
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]
  }
  const contrast = (a, b) => {
    const [x, y] = [luminance(a), luminance(b)].sort((p, q) => q - p)
    return (x + 0.05) / (y + 0.05)
  }
  const hex = v => /^#[0-9a-f]{6}$/i.test(v)

  test('the dark and light palettes and the four sets of colors are all there', () => {
    expect(Object.keys(palettes).sort()).toEqual(['tp-dark', 'tp-light', 'tp-pal-black', 'tp-pal-green', 'tp-pal-white', 'tp-pal-yellow'])
  })

  test('every palette defines every color the page uses', () => {
    const names = Object.keys(palettes['tp-dark']).sort()
    for (const [name, vars] of Object.entries(palettes)) expect([name, Object.keys(vars).sort()]).toEqual([name, names])
  })

  // [the text, its background, the least contrast]: the text of the talk must be very readable, the small text on chips and in
  // buttons at least as readable as the level AA of WCAG
  const pairs = [
    ['tp-text', 'tp-bg', 7],
    ['tp-chip-text', 'tp-chip', 7],
    ['tp-pause-text', 'tp-pause', 7],
    ['tp-audience-text', 'tp-audience', 7],
    ['tp-cue-text', 'tp-cue', 7],
    ['tp-accent', 'tp-bg', 4.5]
  ]
  for (const [name, vars] of Object.entries(palettes)) {
    test(`${name}: the colors can be read`, () => {
      for (const [fore, back, least] of pairs) {
        if (!hex(vars[fore]) || !hex(vars[back])) continue
        expect([name, fore, Math.round(contrast(vars[fore], vars[back]) * 10) / 10]).toEqual([name, fore, expect.any(Number)])
        expect(contrast(vars[fore], vars[back])).toBeGreaterThanOrEqual(least)
      }
    })
  }
})
