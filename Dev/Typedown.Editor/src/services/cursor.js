export const adjustCursor = (cursor, preline, line, nextline) => {
    let newCursor = Object.assign({}, { line: cursor.line, ch: cursor.ch })
    // It's need to adjust the cursor when cursor is at begin or end in table row.
    if (/\|[^|]+\|.+\|\s*$/.test(line)) {
        if (/\|\s*:?-+:?\s*\|[:-\s|]+\|\s*$/.test(line)) { // cursor in `| --- | :---: |` :the second line of table
            newCursor.line += 1 // reset the cursor to the next line
            newCursor.ch = nextline.indexOf('|') + 1
        } else { // cursor is not at the second line to table
            if (cursor.ch <= line.indexOf('|')) newCursor.ch = line.indexOf('|') + 1
            if (cursor.ch >= line.lastIndexOf('|')) newCursor.ch = line.lastIndexOf('|') - 1
        }
    }

    // Need to adjust the cursor when cursor in the first or last line of code/math block.
    if (/```[\S]*/.test(line) || /^\$\$$/.test(line)) {
        if (typeof nextline === 'string' && /\S/.test(nextline)) {
            newCursor.line += 1
            newCursor.ch = 0
        } else if (typeof preline === 'string' && /\S/.test(preline)) {
            newCursor.line -= 1
            newCursor.ch = preline.length
        }
    }

    // Need to adjust the cursor when cursor at the begin of the list
    if (/[*+-]\s.+/.test(line) && newCursor.ch <= 1) {
        newCursor.ch = 2
    }

    // Need to adjust the cursor when cursor at the begin of the header
    if (/^ {0,3}(#{1,6})\s.+/.test(line) && newCursor.ch <= line.indexOf('# ') + 1) {
        newCursor.ch = line.indexOf('# ') + 2
    }

    // Need to adjust the cursor when cursor at blank line or in a line contains HTML tag.
    // set the newCursor to null, the new cursor will at the last line of document.
    if (!/\S/.test(line) || /<\/?([a-zA-Z\d-]+)(?=\s|>).*>/.test(line)) {
        newCursor = null
    }
    return newCursor
}