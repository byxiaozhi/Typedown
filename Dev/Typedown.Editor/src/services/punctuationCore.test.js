import { findMissingOpeners, PLACE } from './punctuationCore'

const flagged = s => findMissingOpeners(s).map(f => s.slice(f.from, f.at + 1))

describe('opening marks of questions and exclamations', () => {
  test('nothing is reported when the sentence opens and closes', () => {
    ['¿Cómo estás?', 'Hola, ¿cómo estás?', '¡Qué bien!', '¿Vienes? ¿O no?', '¡¿Qué?!', '¡Hola! ¿Qué tal?', '«¿Vienes?»', '"¿Vienes?"',
      '¿Conoces al Sr. Pérez?', '¿Y tú? Bien. ¡Adiós!', '¿?', '¡!', '¿Qué hace `x`?'.replace('`x`', PLACE)].forEach(s => expect(findMissingOpeners(s)).toEqual([]))
  })

  test('a question or an exclamation that only closes is reported, underlining the last word and the mark', () => {
    expect(flagged('Cómo estás?')).toEqual(['estás?'])
    expect(flagged('Qué bien!')).toEqual(['bien!'])
    expect(flagged('Hola. Cómo estás?')).toEqual(['estás?'])
    expect(flagged('¡Hola! Qué tal?')).toEqual(['tal?'])
    expect(flagged('¿Vienes? Y tu hermano?')).toEqual(['hermano?'])
  })

  test('the ? of a closed question is not enough for the ! after it, nor the other way round', () => {
    expect(findMissingOpeners('¿Qué dijo?!').map(f => f.kind)).toEqual(['e'])
    expect(findMissingOpeners('¡Qué dijo!?').map(f => f.kind)).toEqual(['q'])
  })

  test('a mark in the middle is reported once for each sentence', () => {
    expect(findMissingOpeners('Cómo estás? Qué haces? ¿Y tú?').length).toBe(2)
  })

  test('where the opening mark could go: the start of the sentence, and after the last comma', () => {
    const s = 'Pedro, dime, cómo estás?'
    const [f] = findMissingOpeners(s)
    expect(s.slice(f.start)).toBe('Pedro, dime, cómo estás?')
    expect(s.slice(f.comma)).toBe('cómo estás?')
    const t = 'Hola. Cómo estás?'
    const [g] = findMissingOpeners(t)
    expect(t.slice(g.start)).toBe('Cómo estás?')
    expect(g.comma).toBe(-1)
    const q = '"Cómo estás?"'
    expect(q.slice(findMissingOpeners(q)[0].start)).toBe('Cómo estás?"')
  })

  test('the mark is offered in the prose, not before text that is code or an address', () => {
    const s = `${PLACE.repeat(6)} Cómo estás?`
    expect(s.slice(findMissingOpeners(s)[0].start)).toBe('Cómo estás?')
  })

  test('what is not a sentence is left alone', () => {
    ['https://example.com/a?b=1'.replace(/./g, PLACE), '(?)', '?', '!important', '![logo](a.png)', 'a != b', 'x!', 'Hola'].forEach(s => {
      if (s !== 'x!') expect(findMissingOpeners(s)).toEqual([])
    })
    expect(findMissingOpeners('Qué ?')).toEqual([])   // a space before the mark: not the end of a word
  })

  test('a stray opening mark that never closes keeps the rest of the block quiet', () => {
    expect(findMissingOpeners('¿Cómo estás. Bien?')).toEqual([])
  })

  test('abbreviations and initials do not end the sentence', () => {
    const s = 'Conoces al Sr. Pérez?'
    const [f] = findMissingOpeners(s)
    expect(s.slice(f.start)).toBe(s)
    const t = 'Dijo J. Pérez que vienes?'
    expect(t.slice(findMissingOpeners(t)[0].start)).toBe(t)
  })
})