import { expandStructuralTabs } from './lexer'

test('tabs that shape blocks are expanded, tabs in text are kept', () => {
  expect(expandStructuralTabs('\tindented code')).toBe('    indented code')
  expect(expandStructuralTabs('-\titem')).toBe('-   item')
  expect(expandStructuralTabs('>\tquote')).toBe('>   quote')
  expect(expandStructuralTabs('#\tTitle')).toBe('#   Title')
  expect(expandStructuralTabs('word\tword')).toBe('word\tword')
  expect(expandStructuralTabs('- a\tb')).toBe('- a\tb')
})

test('fenced code keeps its tabs; only the fence indentation is expanded', () => {
  const go = '```go\nfunc main() {\n\tfmt.Println("x")\n}\n```\nafter\ttab\n'
  expect(expandStructuralTabs(go)).toBe(go)
  const make = '~~~make\nall:\n\tgo build ./...\n~~~\n'
  expect(expandStructuralTabs(make)).toBe(make)
  expect(expandStructuralTabs('  ```\n  \tx\n  ```')).toBe('  ```\n  \tx\n  ```')
  // After the fence closes, prefixes are expanded again.
  expect(expandStructuralTabs('```\n\tkept\n```\n\tcode again')).toBe('```\n\tkept\n```\n    code again')
})
