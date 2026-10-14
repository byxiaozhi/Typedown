import katex from 'katex'
import { texColors } from './texColors'

const render = tex => katex.renderToString(texColors(tex), { throwOnError: true })

describe('colours KaTeX does not read are written as #hex', () => {
  test.each([
    ['CSS rgb()', '\\color{rgb(0,0,0)} {爱}', 'color:#000000'],
    ['CSS rgb() with spaces', '\\color{ rgb( 255 , 128 , 0 ) }x', 'color:#ff8000'],
    ['CSS rgba()', '\\color{rgba(255,0,0,0.5)}x', 'color:#ff000080'],
    ['\\textcolor', '\\textcolor{rgb(0,128,0)}{x}', 'color:#008000'],
    ['\\colorbox', '\\colorbox{rgb(255,255,0)}{x}', 'background-color:#ffff00'],
    ['\\fcolorbox, both colours', '\\fcolorbox{rgb(255,0,0)}{rgb(0,0,255)}{x}', 'background-color:#0000ff'],
    ['xcolor [RGB]', '\\color[RGB]{0,0,255}x', 'color:#0000ff'],
    ['xcolor [rgb]', '\\textcolor[rgb]{1,0.5,0}{x}', 'color:#ff8000'],
    ['xcolor [HTML]', '\\color[HTML]{00FF00}x', 'color:#00FF00']
  ])('%s', (_name, tex, css) => {
    expect(render(tex)).toContain(css)
  })

  test('the reported formula renders with all three colours', () => {
    const html = render('\\color{Pink} {我}\\color{rgb(0,0,0)} {爱}\\color{#008000} {你}')
    expect(html).toContain('color:Pink')
    expect(html).toContain('color:#000000')
    expect(html).toContain('color:#008000')
  })

  test.each([
    '\\color{red}x', '\\color{#123456}x', '\\text{rgb(1,2,3)}', 'f(x)=\\frac{1}{2}', '\\color{rgb(1,2)}x'
  ])('%s is left as it is', tex => {
    expect(texColors(tex)).toBe(tex)
  })
})
