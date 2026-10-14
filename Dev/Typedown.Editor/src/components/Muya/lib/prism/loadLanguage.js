import components from 'prismjs/components.js'
import getLoader from 'prismjs/dependencies'
import { getDefer } from '../utils'
/**
 * The set of all languages which have been loaded using the below function.
 *
 * @type {Set<string>}
 */
export const loadedLanguages = new Set(['markup', 'css', 'clike', 'javascript'])
export const languages = components.languages
// Prism ships no Vue grammar; a .vue single-file component is closest to markup (its <template> is HTML,
// with <script>/<style> sections). Aliasing it to markup at least highlights the template rather than
// leaving a ```vue block plain. jsx/tsx (React) are real Prism languages and already highlight.
export const alias = { 'c++': 'cpp', 'vue': 'markup' }

// 补充别名
Object.keys(alias).forEach(name => Object.assign(languages[alias[name]], { alias: [...languages[alias[name]].alias ?? [], name] }))

// Look for the origin languge by alias
export const transformAliasToOrigin = langs => {
  const result = []
  for (const lang of langs) {
    if (languages[lang]) {
      result.push(lang)
    } else {
      const language = Object.keys(languages).find(name => {
        const l = languages[name]
        if (l.alias) {
          return l.alias === lang || Array.isArray(l.alias) && l.alias.includes(lang)
        }
        return false
      })

      if (language) {
        result.push(language)
      } else {
        // The lang is not exist, the will handle in `initLoadLanguage`
        result.push(lang)
      }
    }
  }

  return result
}

function initLoadLanguage(Prism) {
  return async function loadLanguages(langs) {
    // If no argument is passed, load all components
    if (!langs) {
      langs = Object.keys(languages).filter(lang => lang !== 'meta')
    }

    if (langs && !langs.length) {
      return Promise.reject(new Error('The first parameter should be a list of load languages or single language.'))
    }

    if (!Array.isArray(langs)) {
      langs = [langs]
    }

    const promises = []
    // The user might have loaded languages via some other way or used `prism.js` which already includes some
    // We don't need to validate the ids because `getLoader` will ignore invalid ones
    const loaded = [...loadedLanguages, ...Object.keys(Prism.languages)]

    getLoader(components, langs, loaded).load(async lang => {
      const defer = getDefer()
      promises.push(defer.promise)
      if (!(lang in components.languages)) {
        defer.resolve({
          lang,
          status: 'noexist'
        })
      } else if (loadedLanguages.has(lang)) {
        defer.resolve({
          lang,
          status: 'cached'
        })
      } else {
        delete Prism.languages[lang]
        // A language whose grammar does not load leaves its block unhighlighted, and the others go on: the promise
        // for it was never settled, and everything waiting on Promise.all waited for good.
        try {
          await import('prismjs/components/prism-' + lang)
          defer.resolve({
            lang,
            status: 'loaded'
          })
          loadedLanguages.add(lang)
        } catch (err) {
          console.log(`prism: the ${lang} grammar could not be loaded`, err)
          defer.resolve({
            lang,
            status: 'noexist'
          })
        }
      }
    })

    return Promise.all(promises)
  }
}

export default initLoadLanguage
