// KaTeX reads a colour as a name or #hex only. Formulas written for MathJax (which takes any CSS colour) or for LaTeX's
// xcolor use other forms, and KaTeX refused the whole formula: \color{rgb(0,0,0)}, \color{rgba(...)},
// \color[RGB]{0,0,255}, \color[rgb]{1,0.5,0}, \color[HTML]{00FF00}. Those are written as #hex before KaTeX sees them;
// the document keeps what was typed. Anything else is passed through, so KaTeX still reports what it cannot read.

const COLOR_COMMANDS = 'color|textcolor|colorbox|fcolorbox'
const hex2 = n => Math.round(Math.min(255, Math.max(0, n))).toString(16).padStart(2, '0')

const NUMBER = '\\d+(?:\\.\\d*)?|\\.\\d+'
const CHANNEL = new RegExp(`^(?:${NUMBER})$`)

// rgb(r,g,b) / rgba(r,g,b,a) with 0-255 channels and a 0-1 alpha. (Percentages cannot be written: % starts a TeX
// comment.)
const cssToHex = (fn, args) => {
  const parts = args.split(',').map(part => part.trim())
  if (parts.length !== (fn === 'rgba' ? 4 : 3)) return null
  const values = []
  for (let i = 0; i < parts.length; i++) {
    if (!CHANNEL.test(parts[i])) return null
    const n = parseFloat(parts[i])
    values.push(i < 3 ? n : n * 255)
  }
  return '#' + values.map(hex2).join('')
}

// xcolor's models: RGB 0-255, rgb 0-1, HTML six hex digits.
const modelToHex = (model, spec) => {
  if (model === 'HTML') return /^[0-9a-fA-F]{6}$/.test(spec.trim()) ? `#${spec.trim()}` : null
  const parts = spec.split(',').map(part => part.trim())
  if (parts.length !== 3 || !parts.every(part => CHANNEL.test(part))) return null
  return '#' + parts.map(part => hex2(parseFloat(part) * (model === 'rgb' ? 255 : 1))).join('')
}

const CSS_COLOR = '\\{\\s*(rgba?)\\s*\\(([^(){}]*)\\)\\s*\\}'
// The first colour argument of any colour command, and \fcolorbox's second one.
const CSS_FIRST = new RegExp(`(\\\\(?:${COLOR_COMMANDS})\\s*)${CSS_COLOR}`, 'g')
const CSS_SECOND = new RegExp(`(\\\\fcolorbox\\s*\\{[^{}]*\\}\\s*)${CSS_COLOR}`, 'g')
const MODEL = new RegExp(`(\\\\(?:color|textcolor|colorbox))\\s*\\[\\s*(RGB|rgb|HTML)\\s*\\]\\s*\\{([^{}]*)\\}`, 'g')

export const texColors = tex => {
  if (!tex || !/rgb|\[\s*(?:RGB|HTML)\s*\]/.test(tex)) return tex
  const css = (whole, command, fn, args) => {
    const hex = cssToHex(fn, args)
    return hex ? `${command}{${hex}}` : whole
  }
  return tex
    .replace(CSS_FIRST, css)
    .replace(CSS_SECOND, css)
    .replace(MODEL, (whole, command, model, spec) => {
      const hex = modelToHex(model, spec)
      return hex ? `${command}{${hex}}` : whole
    })
}
