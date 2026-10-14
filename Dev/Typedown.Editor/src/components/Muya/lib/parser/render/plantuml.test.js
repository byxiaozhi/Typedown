// The page (WebView2) has TextEncoder; this test environment's jsdom does not.
import { TextEncoder } from 'util'
global.TextEncoder = TextEncoder
const Diagram = require('./plantuml').default
const { plantUmlServer } = require('./plantuml')

// PlantUML's hex encoding: what plantuml.com draws. The address used to end in "undefined" (no encoder at all).
test('a diagram is addressed by its UTF-8 text in hex', () => {
  expect(Diagram.encode('Alice -> Bob: 你好')).toBe('~h416c696365202d3e20426f623a20e4bda0e5a5bd')
})

test('the image points at plantuml.com with that text', () => {
  const div = document.createElement('div')
  Diagram.parse('A -> B').insertImgElement(div)
  expect(div.querySelector('img').getAttribute('src')).toBe('https://www.plantuml.com/plantuml/svg/~h41202d3e2042')
})

test('a server of the person\'s own draws it instead', () => {
  const div = document.createElement('div')
  Diagram.parse('A -> B').insertImgElement(div, 'http://192.168.1.5:8080/plantuml/')
  expect(div.querySelector('img').getAttribute('src')).toBe('http://192.168.1.5:8080/plantuml/svg/~h41202d3e2042')
})

test('an export as pictures requests PNG from the configured server', () => {
  const div = document.createElement('div')
  Diagram.parse('A -> B').insertImgElement(div, 'http://192.168.1.5:8080/plantuml/', 'png')
  expect(div.querySelector('img').getAttribute('src')).toBe('http://192.168.1.5:8080/plantuml/png/~h41202d3e2042')
})

test('an address that is not http(s) is the default server', () => {
  for (const value of ['', '  ', 'javascript:alert(1)', 'file:///C:/x', 'plantuml.local']) {
    expect(plantUmlServer(value)).toBe('https://www.plantuml.com/plantuml')
  }
})
