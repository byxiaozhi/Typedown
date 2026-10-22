import fs from 'fs'
import path from 'path'

// teleprompter.css is part of the editor's bundle, so a rule without a scope applies to the editor too: `html, body, #root { overflow:
// hidden }` once stopped the page from scrolling, and the mouse wheel did nothing in the Code and Split views.
describe('the teleprompter style sheet', () => {
  const css = fs.readFileSync(path.join(__dirname, 'teleprompter.css'), 'utf8')

  test('does not put overflow: hidden on the page itself', () => {
    const rules = css.replace(/\/\*[\s\S]*?\*\//g, '').split('}').map(r => r.trim()).filter(Boolean)
    const bad = rules.filter(r => /overflow\s*:\s*hidden/.test(r) && /(^|,)\s*(html|body|#root)\s*(,|\{)/.test(r.split('{')[0]))
    expect(bad).toEqual([])
  })

  test('the rule for the page of the teleprompter is scoped by the class the page sets', () => {
    expect(css).toMatch(/html\.tp-page\s*,\s*html\.tp-page body\s*,\s*html\.tp-page #root\s*\{/)
    const page = fs.readFileSync(path.join(__dirname, 'Teleprompter.tsx'), 'utf8')
    expect(page).toContain("classList.add('tp-page')")
  })
})
