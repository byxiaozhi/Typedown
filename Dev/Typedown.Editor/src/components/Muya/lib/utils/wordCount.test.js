import { wordCount } from './wordCount'

// The count is made on what a reader sees: Markdown marks and link addresses are not words, and every CJK script is
// counted as such (only the basic Han block was, so kana, Hangul and the extension blocks were taken for Latin words).
describe('wordCount', () => {
  it('counts the visible text, not the Markdown', () => {
    const c = wordCount('# Title\n\nHello **world**, see [the docs](https://example.com/a/very/long/address) and ![a cat](cat.png).\n')
    // Title Hello world see the docs and a cat
    expect(c.word).toBe(9)
    expect(c.headings).toBe(1)
    expect(c.links).toBe(1)
    expect(c.images).toBe(1)
    expect(c.paragraph).toBe(2)
    expect(c.character).toBe('TitleHelloworld,seethedocsandacat.'.length)
  })

  it('counts Han in every block, kana and Hangul as CJK characters', () => {
    const c = wordCount('汉字𠀀𪚥 ひらがなカタカナー 한국어 문장\n')
    // 4 Han + 4 hiragana + 5 katakana with the long mark, each a word; Hangul in two words
    expect(c.cjk).toBe(4 + 4 + 5 + 5)
    expect(c.word).toBe(13 + 2)
    expect(c.otherWords).toBe(2)
  })

  it('leaves code and formulas out of the reading time but counts them', () => {
    const prose = 'word '.repeat(230)
    const code = '```\n' + 'code '.repeat(460) + '\n```\n\n$$\nx+y\n$$\n'
    const plain = wordCount(prose)
    const withCode = wordCount(prose + '\n\n' + code)
    expect(plain.readingMinutes).toBe(1)
    expect(withCode.readingMinutes).toBe(1)
    expect(withCode.word).toBe(230 + 460 + 1)
  })

  it('reads 400 CJK characters a minute', () => {
    expect(wordCount('字'.repeat(800)).readingMinutes).toBe(2)
  })

  it('skips front matter, list and quote marks, and keeps a table one paragraph', () => {
    const c = wordCount('---\ntitle: x\n---\n- one\n- [x] two\n\n> three\n\n| a | b |\n|---|---|\n| c | d |\n')
    expect(c.word).toBe(7)
    expect(c.paragraph).toBe(3)
  })

  it('is empty for an empty document', () => {
    const c = wordCount('')
    expect([c.word, c.character, c.paragraph, c.readingMinutes]).toEqual([0, 0, 0, 0])
  })
})
