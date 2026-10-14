import Lexer from './lexer'

const lex = markdown => new Lexer({ gfm: true, disableInline: true, frontMatter: true }).lex(markdown)
  .map(token => token.type === 'frontmatter' ? `frontmatter:${token.lang}:${JSON.stringify(token.text)}` : token.type)

describe('front matter at the top of a document', () => {
  test.each([
    ['content right below the closing fence', '---\ntitle: x\n---\n# H\n', ['frontmatter:yaml:"title: x\\n"', 'heading']],
    ['text right below the closing fence', '---\ntitle: x\ntags: [a, b]\n---\ntext\n', ['frontmatter:yaml:"title: x\\ntags: [a, b]\\n"', 'paragraph']],
    ['a blank line below it', '---\ntitle: x\n---\n\ntext\n', ['frontmatter:yaml:"title: x\\n"', 'paragraph']],
    ['nothing below it', '---\ntitle: x\n---\n', ['frontmatter:yaml:"title: x\\n"']],
    ['nothing below it, no last newline', '---\ntitle: x\n---', ['frontmatter:yaml:"title: x\\n"']],
    ['fences with trailing spaces', '---  \ntitle: x\n--- \n\ntext', ['frontmatter:yaml:"title: x\\n"', 'paragraph']],
    ['a value holding dashes', '---\ntitle: a---b\nrule: ---x\n---\ntext', ['frontmatter:yaml:"title: a---b\\nrule: ---x\\n"', 'paragraph']],
    ['TOML right above content', '+++\ntitle = "x"\n+++\n# H', ['frontmatter:toml:"title = \\"x\\"\\n"', 'heading']]
  ])('%s', (_name, markdown, expected) => {
    expect(lex(markdown)).toEqual(expected)
  })

  test.each([
    ['not at the top', 'intro\n\n---\ntitle: x\n---\n', 'frontmatter'],
    ['a rule with no closing fence', '---\ntext\n', 'frontmatter']
  ])('%s is not front matter', (_name, markdown, absent) => {
    expect(lex(markdown).some(type => type.startsWith(absent))).toBe(false)
  })
})
