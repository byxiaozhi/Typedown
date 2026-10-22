// The matches of a search as short lines of text, for the inspector pane's Find tab: the match with some of the text on
// either side of it, on one line. The host gets at most `MAX_SNIPPETS` of them (and the count of all), so that a term that
// stands everywhere does not send the whole document.
export interface ISearchSnippet {
    before: string
    match: string
    after: string
}

export const MAX_SNIPPETS = 200

const BEFORE = 28
const AFTER = 48
const LONGEST_MATCH = 120

const oneLine = (text: string) => text.replace(/\s+/g, ' ')

// `start` and `end` are positions in `text`. The words around the match are the ones on its own line (a line of the source is
// one row, however long the text is); a cut text starts or ends with an ellipsis.
export const snippetOf = (text: string, start: number, end: number): ISearchSnippet => {
    const matchEnd = Math.min(end, start + LONGEST_MATCH)
    const lineStart = text.lastIndexOf('\n', start - 1) + 1
    const nextBreak = text.indexOf('\n', matchEnd)
    const lineEnd = nextBreak < 0 ? text.length : nextBreak
    const from = Math.max(lineStart, start - BEFORE)
    const to = Math.min(lineEnd, matchEnd + AFTER)
    return {
        before: (from > lineStart ? '…' : '') + oneLine(text.slice(from, start)),
        match: oneLine(text.slice(start, matchEnd)) + (matchEnd < end ? '…' : ''),
        after: oneLine(text.slice(matchEnd, to)) + (to < lineEnd ? '…' : ''),
    }
}
