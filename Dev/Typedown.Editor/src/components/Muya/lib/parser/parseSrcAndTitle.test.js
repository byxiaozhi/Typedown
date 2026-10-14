import { parseSrcAndTitle } from './utils'

test('a destination in angle brackets keeps its spaces and loses its brackets', () => {
  expect(parseSrcAndTitle('<图 片.png>')).toEqual({ src: '图 片.png', title: '' })
  expect(parseSrcAndTitle('<a b.png> "A title"')).toEqual({ src: 'a b.png', title: 'A title' })
  expect(parseSrcAndTitle("<a b.md> 'single'")).toEqual({ src: 'a b.md', title: 'single' })
  expect(parseSrcAndTitle('<a\\>b.png>')).toEqual({ src: 'a>b.png', title: '' })
})

test('the other forms read as before', () => {
  expect(parseSrcAndTitle('a.png')).toEqual({ src: 'a.png', title: '' })
  expect(parseSrcAndTitle('a.png "T"')).toEqual({ src: 'a.png', title: 'T' })
  expect(parseSrcAndTitle('a%20b.png')).toEqual({ src: 'a%20b.png', title: '' })
  expect(parseSrcAndTitle('<a b.png')).toEqual({ src: '<a b.png', title: '' })
})
