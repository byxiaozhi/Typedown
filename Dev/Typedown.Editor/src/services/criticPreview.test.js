import { criticToHtml } from './criticPreview'

describe('the changes view of the split preview', () => {
  test('additions, deletions and replacements become tags', () => {
    expect(criticToHtml('A {++new++} word and {--an old--} one.')).toBe('A <ins class="caret-add">new</ins> word and <del class="caret-del">an old</del> one.')
    expect(criticToHtml('It {~~was late~>is on time~~}.')).toBe('It <del class="caret-del">was late</del><ins class="caret-add">is on time</ins>.')
  })

  test('the stamp after a change is drawn small, a note is a note, a highlight is a highlight', () => {
    expect(criticToHtml('{++x++}{>>@Ann 2026-10-09<<}')).toBe('<ins class="caret-add">x</ins><span class="caret-stamp">Ann · 2026-10-09</span>')
    expect(criticToHtml('{==text==}{>>@Ann 2026-10-09: check this<<}')).toContain('<mark class="caret-hl">text</mark>')
    expect(criticToHtml('{>>just a note<<}')).toBe('<span class="caret-note">just a note</span>')
  })

  test('a whole line added or deleted keeps its list or heading mark outside', () => {
    expect(criticToHtml('- {++a new item++}')).toBe('- <ins class="caret-add">a new item</ins>')
    expect(criticToHtml('## {--Old heading--}')).toBe('## <del class="caret-del">Old heading</del>')
  })

  test('code is never read for marks: a fenced block and a code span', () => {
    const fenced = '```js\nconst a = "{++not a mark++}"\n```\n{++real++}'
    expect(criticToHtml(fenced)).toBe('```js\nconst a = "{++not a mark++}"\n```\n<ins class="caret-add">real</ins>')
    expect(criticToHtml('use `{--x--}` and {--y--}')).toBe('use `{--x--}` and <del class="caret-del">y</del>')
  })

  test('text without marks is unchanged, and an empty text is empty', () => {
    expect(criticToHtml('# Title\n\nPlain text.')).toBe('# Title\n\nPlain text.')
    expect(criticToHtml('')).toBe('')
    expect(criticToHtml(undefined)).toBe('')
  })
  test('with the stamp of the review, its own changes are numbered in the order of the text; a mark that was there before is not', () => {
    const stamp = '{>>@Ann 2026-10-09<<}'
    const text = `A {~~old~>new~~}${stamp} and {++added++}${stamp}.\n\nAn old {--mark--}{>>@Bob 2026-01-01<<} and {--gone--}${stamp}.`
    const html = criticToHtml(text, stamp)
    expect(html).toContain('<del class="caret-del" data-change="0">old</del><ins class="caret-add" data-change="0">new</ins>')
    expect(html).toContain('<ins class="caret-add" data-change="1">added</ins>')
    expect(html).toContain('<del class="caret-del" data-change="2">gone</del>')
    // the older mark has no number
    expect(html).toContain('<del class="caret-del">mark</del>')
    expect(html.match(/data-change="/g).length).toBe(4)
  })
  test('the stamp of the host is shown as the author and the day; an earlier mark with the same stamp is not one of the changes', () => {
    const own = '{>>@caret-live-review 0001-01-01<<}'
    const shown = '{>>@Ann 2026-10-09<<}'
    const text = `Old {++mark++}${shown} and {++new++}${own}.`
    const html = criticToHtml(text, own, shown)
    expect(html).toContain('<ins class="caret-add">mark</ins><span class="caret-stamp">Ann · 2026-10-09</span>')
    expect(html).toContain('<ins class="caret-add" data-change="0">new</ins><span class="caret-stamp">Ann · 2026-10-09</span>')
    expect(html.match(/data-change="/g).length).toBe(1)
  })
  test('each change is shown with the day it was first seen; the note of the code is of the day of the comparison', () => {
    const own = '{>>@caret-live-review 0001-01-01<<}'
    const shown = '{>>@Ann 2026-10-09<<}'
    const text = `{++a++}${own} and {--b--}${own}.

{>>@caret-live-review 0001-01-01: code changed<<}`
    const html = criticToHtml(text, own, shown, ['2026-10-01', '2026-10-05'], '{>>@Ann ')
    expect(html).toContain('<ins class="caret-add" data-change="0">a</ins><span class="caret-stamp">Ann · 2026-10-01</span>')
    expect(html).toContain('<del class="caret-del" data-change="1">b</del><span class="caret-stamp">Ann · 2026-10-05</span>')
    expect(html).toContain('<span class="caret-note">@Ann 2026-10-09: code changed</span>')
  })
})