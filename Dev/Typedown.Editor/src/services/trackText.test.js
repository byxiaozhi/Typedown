import { plainOf, locateChanges } from './trackText'

describe('the text of Markdown as it reads', () => {
  test('emphasis, code and strike marks go, links give their text', () => {
    expect(plainOf('a **bold** and *slanted* and ~~struck~~ `code` word')).toBe('a bold and slanted and struck code word')
    expect(plainOf('see [the page](https://x.org/a_b) now')).toBe('see the page now')
  })

  test('the target of a link is read to its closing parenthesis, counting the ones inside it', () => {
    expect(plainOf('see [label](https://example.test/a_(b)) now')).toBe('see label now')
    expect(plainOf('an ![alt text](img(1).png) and [one](a) [two](b)')).toBe('an alt text and one two')
    expect(plainOf('a [lone bracket and (parentheses) here')).toBe('a [lone bracket and (parentheses) here')
  })

  test('the marks of headings, lists and quotes at the start of a line go', () => {
    expect(plainOf('## Title')).toBe('Title')
    expect(plainOf('- [x] done\n- item')).toBe('done item')
    expect(plainOf('1. first\n2) second')).toBe('first second')
    expect(plainOf('> quoted')).toBe('quoted')
  })

  test('underscores inside a word stay, escapes lose the backslash, white space is one space', () => {
    expect(plainOf('snake_case and _slanted_')).toBe('snake_case and slanted')
    expect(plainOf('a \\* star')).toBe('a * star')
    expect(plainOf('a   b\n\n c')).toBe('a b c')
  })
})

describe('finding the changes in the text of the page', () => {
  const flat = 'Informe\nLas ventas fueron bajas este mes y subieron un poco\nEl texto se queda\nUna tercera linea que se queda igual Fin'

  test('an addition is found by its words, a deletion where the text before it ends', () => {
    const found = locateChanges(flat, [
      { index: 0, kind: 'added', old: '', new: ' y subieron un poco', before: 'Las ventas fueron bajas este mes', after: '\n\n' },
      { index: 1, kind: 'deleted', old: 'El parrafo viejo se va.', new: '', before: 'y subieron un poco\n\n', after: '\n\nEl texto se queda' },
      { index: 2, kind: 'added', old: '', new: ' Fin', before: 'se queda igual.', after: '' },
    ])
    expect(found.map(f => f.index)).toEqual([0, 1, 2])
    expect(flat.slice(found[0].start, found[0].end)).toBe('y subieron un poco')
    expect(found[1].start).toBe(found[1].end)
    expect(flat.slice(0, found[1].at).endsWith('subieron un poco')).toBe(true)
    expect(flat.slice(found[2].start, found[2].end)).toBe('Fin')
  })

  test('a word that is on the page several times is the one whose text before it agrees', () => {
    const text = 'the cat sat. the dog sat. the bird sat.'
    const found = locateChanges(text, [{ index: 0, kind: 'added', old: '', new: ' sat', before: 'the dog', after: '.' }])
    expect(found).toHaveLength(1)
    expect(text.slice(0, found[0].start).endsWith('the dog ')).toBe(true)
  })

  test('changes are looked for in the order of the document, each after the one before', () => {
    const text = 'x one x two x'
    const found = locateChanges(text, [
      { index: 0, kind: 'added', old: '', new: 'x', before: '', after: '' },
      { index: 1, kind: 'added', old: '', new: 'x', before: 'one', after: '' },
    ])
    expect(found.map(f => f.start)).toEqual([0, 6])
  })

  test('markup in the added text does not stop it being found', () => {
    const found = locateChanges('A bold word here', [{ index: 0, kind: 'added', old: '', new: '**bold** word', before: 'A ', after: ' here' }])
    expect(found).toHaveLength(1)
    expect('A bold word here'.slice(found[0].start, found[0].end)).toBe('bold word')
  })

  test('a change that is not on the page is left out, and the others are still found', () => {
    const found = locateChanges('one two three', [
      { index: 0, kind: 'added', old: '', new: 'missing', before: '', after: '' },
      { index: 1, kind: 'added', old: '', new: 'three', before: 'two', after: '' },
    ])
    expect(found.map(f => f.index)).toEqual([1])
  })

  test('a deletion at the start of the document is at 0, and a replacement is found by its new text', () => {
    const found = locateChanges('Hello world', [
      { index: 0, kind: 'deleted', old: 'Oh ', new: '', before: '', after: 'Hello' },
      { index: 1, kind: 'replaced', old: 'earth', new: 'world', before: 'Hello', after: '' },
    ])
    expect(found[0].at).toBe(0)
    expect('Hello world'.slice(found[1].start, found[1].end)).toBe('world')
  })
  test('a deletion right after an addition: its text before it ends with that addition', () => {
    const flat = 'Informe\nLas ventas fueron bajas este mes. y subieron un poco\nUna tercera linea que se queda igual. Fin.'
    const found = locateChanges(flat, [
      { index: 0, kind: 'added', old: '', new: ' y subieron un poco', before: 'Las ventas fueron bajas este mes.', after: '\n\n\nUna tercera linea que se queda igual.' },
      { index: 1, kind: 'deleted', old: 'El parrafo viejo se va.', new: '', before: 'ueron bajas este mes. y subieron un poco', after: '\n\nUna tercera linea que se queda igual. ' },
    ])
    expect(found.map(f => f.index)).toEqual([0, 1])
    expect(flat.slice(0, found[1].at).endsWith('subieron un poco')).toBe(true)
  })})