import Prism from 'prismjs'
// The loader needs only getDefer from the editor's utils, whose module pulls in the whole editor.
jest.mock('../utils', () => ({
  getDefer: () => {
    const defer = {}
    defer.promise = new Promise(resolve => { defer.resolve = resolve })
    return defer
  }
}))
import initLoadLanguage, { alias, transformAliasToOrigin } from './loadLanguage'

const loadLanguage = initLoadLanguage(Prism)
// The token types a block of `lang` is drawn with, the way the editor finds its grammar.
const tokenTypes = (code, lang) => {
  const grammar = Prism.languages[transformAliasToOrigin([lang])[0]]
  if (!grammar) return []
  const types = new Set()
  const walk = tokens => tokens.forEach(t => {
    if (typeof t === 'string') return
    types.add(t.type)
    if (Array.isArray(t.content)) walk(t.content)
  })
  walk(Prism.tokenize(code, grammar))
  return [...types]
}

const vue = '<template>\n  <div :class="x">{{ msg }}</div>\n</template>\n\n<script>\nexport default { data() { return { msg: 1 } } }\n</script>\n\n<style scoped>\n.a { color: red; }\n</style>\n'

describe('vue, jsx and tsx code blocks are highlighted', () => {
  test('vue is drawn as markup, its <script> and <style> as JavaScript and CSS', () => {
    expect(transformAliasToOrigin(['vue'])).toEqual(['markup'])
    expect(alias.vue).toBe('markup')
    const types = tokenTypes(vue, 'vue')
    expect(types).toEqual(expect.arrayContaining(['tag', 'attr-name', 'script', 'keyword', 'style', 'selector', 'property']))
  })

  test.each([
    ['jsx', 'const a = <div className="x">{y}</div>'],
    ['tsx', 'const a: number = 1; const b = <div className="x">{a}</div>']
  ])('%s has a grammar of its own', async (lang, code) => {
    await loadLanguage([lang])
    expect(Prism.languages[lang]).toBeDefined()
    expect(tokenTypes(code, lang)).toEqual(expect.arrayContaining(['keyword', 'tag', 'attr-name']))
  })
})
