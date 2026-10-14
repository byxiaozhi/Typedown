// Han (all its blocks) and kana are read a character at a time: each is one word. Hangul is written in words
// with spaces between them, so it counts as a word as Latin does, but its syllables are CJK characters too.
const CHARACTER_WORD = /[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}ー]/gu
const HANGUL = /\p{Script=Hangul}/gu
const WORDLIKE = /[\p{L}\p{N}]/u
const CJK_PER_MINUTE = 400
const WORDS_PER_MINUTE = 230

/**
 * What a reader sees of a Markdown line: no heading, list, quote or table marks, no emphasis, no link addresses
 * (their text stays, an image keeps its alt text), no HTML tags. Counts the headings, images and links on the way.
 */
const visibleLine = (line, found) => {
  // Most lines of prose carry no mark at all: they are what is seen, as they are. Each pass below is also run only
  // when its mark is on the line - on a long document every keystroke counts it again.
  if (!/[#>*+\-_=|[\]!<`~\\$\d]/.test(line)) return line
  if (/^\s*[-*_|:=]/.test(line)) {
    if (/^\s*([-*_])(\s*\1){2,}\s*$/.test(line)) return ''                        // a rule
    if (/^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(line)) return null // a table's alignment row: not even a break
    if (/^\s{0,3}(=+|-+)\s*$/.test(line)) return ''                                // a setext heading's underline
  }
  if (/^\s{0,3}#{1,6}(\s|$)/.test(line)) {
    found.headings++
    line = line.replace(/^\s{0,3}#{1,6}\s*/, '').replace(/\s#+\s*$/, '')
  }
  if (/^\s*[>\-*+\d]/.test(line)) {
    line = line.replace(/^(\s*>)+\s?/, '').replace(/^\s*([-*+]|\d+[.)])\s+(\[[ xX]\]\s+)?/, '')
  }
  if (line.includes('[')) {
    if (/^\s*\[[^\]]+\]:\s*\S/.test(line) && !/^\s*\[\^/.test(line)) return ''  // a link definition
    line = line
      .replace(/^\s*\[\^[^\]]+\]:\s*/, '')
      .replace(/\[\^[^\]]+\]/g, '')
      .replace(/!\[([^\]]*)\]\([^)]*\)|!\[([^\]]*)\]\[[^\]]*\]/g, (_, a, b) => { found.images++; return a ?? b ?? '' })
      .replace(/\[([^\]]*)\]\([^)]*\)|\[([^\]]+)\]\[[^\]]*\]/g, (_, a, b) => { found.links++; return a ?? b ?? '' })
  }
  if (line.includes('<')) {
    line = line
      .replace(/<img\b[^>]*>/gi, () => { found.images++; return '' })
      .replace(/<((https?|ftp|mailto):[^>\s]+)>/gi, (_, url) => { found.links++; return url })
      .replace(/<\/?[a-zA-Z][^>]*>/g, '')
  }
  if (line.includes('`')) line = line.replace(/(`+)([^`]+?)\1/g, '$2')
  if (/[*_~=]/.test(line)) {
    line = line
      .replace(/(\*\*|__|~~|==|\*|_)(?=\S)([^\n]*?\S)\1/g, '$2')
      .replace(/(\*\*|__|~~|==|\*|_)(?=\S)([^\n]*?\S)\1/g, '$2')
  }
  if (line.includes('\\')) line = line.replace(/\\([\\`*_{}[\]()#+\-.!~|<>$])/g, '$1')
  if (line.includes('|')) line = line.replace(/(^|\s)\|(?=\s|$)/g, ' ').replace(/^\s*\||\|\s*$/g, '')
  return line
}

/**
 * The document's numbers, counted on what a reader sees (see visibleLine): characters with and without spaces, CJK
 * characters, words (a Han or kana character is one), paragraphs, headings, images, links, and the minutes it takes
 * to read - its prose at about 400 CJK characters and 230 other words a minute, code blocks and formulas left out
 * of that time though counted elsewhere. `word` and `character` are the status bar's two numbers; `all` the
 * Markdown's length.
 */
export const wordCount = markdown => {
  const found = { headings: 0, images: 0, links: 0 }
  const prose = []
  const other = []                                   // code blocks and formulas: counted, not read through
  let fence = null
  let math = false
  let lines = markdown.split('\n')
  // Front matter is the document's settings, not its text.
  if (/^---\s*$/.test(lines[0] || '')) {
    const close = lines.findIndex((l, i) => i > 0 && /^(---|\.\.\.)\s*$/.test(l))
    if (close > 0) lines = lines.slice(close + 1)
  }
  for (const line of lines) {
    if (fence) {
      if (new RegExp('^\\s{0,3}' + fence[0] + '{' + fence.length + ',}\\s*$').test(line)) fence = null
      else other.push(line)
      continue
    }
    if (math) {
      if (/^\s*\$\$\s*$/.test(line)) math = false
      else other.push(line)
      continue
    }
    const open = /^\s{0,3}(`{3,}|~{3,})/.exec(line)
    if (open) { fence = open[1]; prose.push(''); continue }
    if (/^\s*\$\$\s*$/.test(line)) { math = true; prose.push(''); continue }
    const visible = visibleLine(line, found)
    if (visible !== null) prose.push(visible.includes('$') ? visible.replace(/\$([^$\n]+)\$/g, (_, f) => { other.push(f); return ' ' }) : visible)
  }
  const proseText = prose.join('\n')
  const otherText = other.join('\n')
  const count = t => {
    const characterWords = (t.match(CHARACTER_WORD) || []).length
    const hangul = (t.match(HANGUL) || []).length
    let words = 0
    for (const w of (characterWords ? t.replace(CHARACTER_WORD, ' ') : t).split(/\s+/)) if (WORDLIKE.test(w)) words++
    const spaces = (t.match(/[^\S\n]/g) || []).length
    const breaks = (t.match(/\n/g) || []).length
    return { characterWords, hangul, words, spaces, characters: t.length - spaces - breaks }
  }
  const read = count(proseText)
  const code = count(otherText)
  const all = {}
  for (const key in read) all[key] = read[key] + code[key]
  const minutes = read.characterWords / CJK_PER_MINUTE + read.words / WORDS_PER_MINUTE
  return {
    word: all.characterWords + all.words,
    character: all.characters,
    characterWithSpaces: all.characters + all.spaces,
    cjk: all.characterWords + all.hangul,
    otherWords: all.words,
    paragraph: proseText.split(/\n\s*\n/).filter(p => WORDLIKE.test(p)).length,
    headings: found.headings,
    images: found.images,
    links: found.links,
    readingMinutes: Math.round(minutes * 10) / 10,
    all: markdown.length
  }
}
