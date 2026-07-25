// First-strong-character direction detection (a practical subset of Unicode
// UAX#9 rules P2/P3), used instead of the HTML `dir="auto"` attribute.
// WebView2 does not reliably re-run `dir="auto"` detection as text is patched
// in by our virtual-dom renderer while typing, so we compute the direction
// ourselves on every render and emit a literal `ltr`/`rtl` value instead.
//
// Code point ranges (decimal), covering RTL scripts: Hebrew, Arabic, Syriac,
// Arabic Supplement, Thaana, NKo (0x0591-0x07FF), Hebrew/Arabic presentation
// forms A (0xFB1D-0xFDFF), Arabic presentation forms B (0xFE70-0xFEFF), plus
// explicit RTL marks (RLM 0x200F, RLE 0x202B, RLO 0x202E).
const RTL_RANGES = [
  [0x0591, 0x07FF],
  [0xFB1D, 0xFDFF],
  [0xFE70, 0xFEFF]
]
const RTL_MARKS = new Set([0x200F, 0x202B, 0x202E])

function isRtlCodePoint (codePoint) {
  if (RTL_MARKS.has(codePoint)) {
    return true
  }
  return RTL_RANGES.some(([start, end]) => codePoint >= start && codePoint <= end)
}

const LTR_LETTER_REG = /\p{L}/u

export function detectTextDirection (text) {
  if (!text) {
    return null
  }
  for (const ch of text) {
    const codePoint = ch.codePointAt(0)
    if (isRtlCodePoint(codePoint)) {
      return 'rtl'
    }
    if (LTR_LETTER_REG.test(ch)) {
      return 'ltr'
    }
  }
  return null
}

// Find the first non-empty `text` anywhere in a block's subtree.
export function findBlockText (block) {
  if (block.text) {
    return block.text
  }
  if (Array.isArray(block.children)) {
    for (const child of block.children) {
      const text = findBlockText(child)
      if (text) {
        return text
      }
    }
  }
  return ''
}
