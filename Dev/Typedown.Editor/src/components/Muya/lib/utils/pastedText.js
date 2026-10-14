// Plain text pasted with every line indented (a passage copied with its paragraphs indented by a tab or spaces) was
// read as Markdown, and four columns of indentation make an indented code block: the reader's text came in as code.
// Indentation that every line shares says nothing about the text's structure, so it is taken off first; indentation
// relative to the least indented line (a nested list, an indented line under another) is kept. A paste in which any
// line starts at the margin is left as it is.

const TAB_STOP = 4

const leadingColumns = line => {
  let columns = 0
  for (const ch of line) {
    if (ch === ' ') columns++
    else if (ch === '\t') columns += TAB_STOP - (columns % TAB_STOP)
    else break
  }
  return columns
}

// The line with its first `count` columns of indentation taken off; a tab that straddles the cut leaves the columns
// past it as spaces.
const removeColumns = (line, count) => {
  let columns = 0
  let i = 0
  while (i < line.length && columns < count) {
    const ch = line[i]
    if (ch === ' ') columns++
    else if (ch === '\t') columns += TAB_STOP - (columns % TAB_STOP)
    else break
    i++
  }
  return ' '.repeat(Math.max(0, columns - count)) + line.slice(i)
}

export const dedentPastedText = text => {
  if (!text) return text
  const lines = text.split('\n')
  // A CRLF line keeps its \r at the end; it is not indentation and does not make a line non-blank.
  const isBlank = line => /^[ \t]*\r?$/.test(line)
  let common = Infinity
  for (const line of lines) {
    if (isBlank(line)) continue
    common = Math.min(common, leadingColumns(line))
    if (common === 0) return text
  }
  if (common === Infinity) return text
  return lines.map(line => isBlank(line) ? line.replace(/^[ \t]+/, '') : removeColumns(line, common)).join('\n')
}
