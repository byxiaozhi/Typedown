// config and utils import each other; loading config first is the order the app gets from its bundler
import '../config'
import fs from 'fs'
import path from 'path'
import { matchMark, BUILTIN } from './speech'

// The starter templates with speech marks (Dev/Typedown.WinUI/Templates/<language>/): every mark in them is a real mark, written
// with the English words (they are the same in every language), and the pairs close. A typo here would show as plain braces.
// The marks quoted in the note at the top are escaped (\{pause\}): as plain text they must not count as marks of the talk.
const root = path.resolve(__dirname, '../../../../../../Typedown.WinUI/Templates')
const languages = ['en', 'es', 'fr', 'pl', 'pt']
const speechFiles = lang => fs.readdirSync(path.join(root, lang)).filter(f => /\.md$/.test(f)).map(f => ({ name: f, text: fs.readFileSync(path.join(root, lang, f), 'utf8') })).filter(f => f.text.includes('{budget'))
const WORDS = Object.keys(BUILTIN).join('|')

describe('the starter templates with speech marks', () => {
  languages.forEach(lang => {
    test(`${lang}: two of them, with marks that are all valid and closed`, () => {
      const files = speechFiles(lang)
      expect(files.length).toBe(2)
      files.forEach(({ name, text }) => {
        const marks = text.match(/(?<!\\)\{\/?[A-Za-z][^{}\n]*\}/g) || []
        expect(marks.length).toBeGreaterThan(15)
        // a mark word with no closing brace ("{pause 2s") is not matched above: every unescaped opening of a known word must be a whole mark
        const openings = text.match(new RegExp(`(?<!\\\\)\\{/?(?:${WORDS})\\b`, 'gi')) || []
        const whole = marks.filter(m => new RegExp(`^\\{/?(?:${WORDS})\\b`, 'i').test(m))
        expect([name, openings.length]).toEqual([name, whole.length])
        const open = {}
        marks.forEach(m => {
          if (m.startsWith('{/')) {
            const word = m.slice(2, -1).toLowerCase()
            // a closer needs its opener before it: "{/slow}{slow}" is not a pair
            expect([name, m, (open[word] || 0) > 0]).toEqual([name, m, true])
            open[word] = (open[word] || 0) - 1
          } else {
            const parsed = matchMark(m)
            expect([name, m, !!parsed]).toEqual([name, m, true])
            if (parsed.role === 'pair') open[parsed.name] = (open[parsed.name] || 0) + 1
          }
        })
        Object.keys(open).forEach(k => expect([name, k, open[k]]).toEqual([name, k, 0]))
      })
    })
  })
})