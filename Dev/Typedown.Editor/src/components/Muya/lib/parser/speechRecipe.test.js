// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import { buildAny, buildRecipe, planEdit, splitRecipe, withDefinitions } from './speechEdit'
import { collectDefinitions } from './speech'

const defs = (...lines) => collectDefinitions(lines).defs
const reveal = '{pause 5s}{soft}{emphasis}{text}{/emphasis}{/soft}{wait}'
const write = (text, start, end, mark, d) => {
  const plan = planEdit(text, start, end, mark, d)
  return plan.ok ? text.substring(0, plan.start) + plan.replacement + text.substring(plan.end) : plan.reason
}

describe('a recipe is several marks in one click', () => {
  test('{text} is replaced by the selection, the marks go around it', () => {
    const mark = buildRecipe(reveal)
    expect(mark).toMatchObject({ role: 'pair', open: '{pause 5s}{soft}{emphasis}', close: '{/emphasis}{/soft}{wait}' })
    expect(write('We lost the file. Really.', 0, 17, mark)).toBe('{pause 5s}{soft}{emphasis}We lost the file.{/emphasis}{/soft}{wait} Really.')
  })

  test('with only a caret, the sentence at the caret is used', () => {
    const mark = buildRecipe('{tone: joke}{text}{/tone}{wait 3s: laugh}')
    expect(write('One. Two is here. Three.', 8, 8, mark)).toBe('One. {tone: joke}Two is here.{/tone}{wait 3s: laugh} Three.')
  })

  test('a recipe without {text} is written at the caret', () => {
    const mark = buildRecipe('{beat}{cue: look up}')
    expect(mark).toMatchObject({ role: 'point', open: '{beat}{cue: look up}' })
    expect(write('Hello world', 5, 5, mark)).toBe('Hello {beat}{cue: look up} world')
  })

  test('the marks written are ones the editor reads: marks of the document\'s own words too', () => {
    const d = defs('{define whisper span: quiet}')
    expect(buildRecipe('{whisper}{text}{/whisper}{beat}', d)).not.toBeNull()
    expect(buildRecipe('{whisper}{text}{/whisper}{beat}', undefined)).toBeNull()
  })

  test('a library word is usable before its definition is in the document', () => {
    const { defs: merged } = withDefinitions(undefined, ['{define very-slow pace 50%: half speed}'])
    expect(buildAny({ template: '{very-slow}{text}{/very-slow}{pause}' }, merged)).not.toBeNull()
  })
})

describe('what a recipe may not hold', () => {
  test.each([
    ['{text} twice', '{text}and{text}'],
    ['a split mark name', '{pa{text}use}'],
    ['a split mark value', '{pause 1{text}s}'],
    ['a split mark note', '{cue: look {text}up}'],
    ['a split closing mark', '{soft}{/so{text}ft}'],
    ['text after an opening brace', '{{text}pause}'],
    ['text before a closing brace', '{pause{text}}'],
    ['a pair left open', '{soft}{text}'],
    ['a pair closed in the wrong order', '{soft}{loud}{text}{/soft}{/loud}'],
    ['a closer with no opener', '{text}{/soft}'],
    ['a word that is not a mark', '{pauze}{text}'],
    ['a mark with a wrong value', '{pause fast}{text}'],
    ['a stray brace', '{text} {oops'],
    ['a definition', '{define x span: y}{text}'],
    ['a setting', '{wpm 140}{text}'],
    ['nothing at all', ''],
    ['only {text}', '{text}'],
    ['only spaces', '   ']
  ])('%s', (_, template) => {
    expect(splitRecipe(template)).toBeNull()
  })

  test('plain words between the marks are fine', () => {
    expect(splitRecipe('{pause} and then {text} done {beat}')).not.toBeNull()
  })

  test('buildAny builds a single mark from a spec, a recipe from a template', () => {
    expect(buildAny({ name: 'slow' }).role).toBe('pair')
    expect(buildAny({ template: reveal }).name).toBe('recipe')
    expect(buildAny({ template: '{nope}' })).toBeNull()
  })
})
