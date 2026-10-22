// The split preview's "changes view" (docs/live-review-design.md, step L1): the text of a review as ReviewDiff writes it, in CriticMarkup,
// turned into tags the preview can draw in colour: additions, deletions, highlights, the small stamp that follows a change and the notes.
// Code is left alone (a fenced block and a code span are never read for marks), as everywhere else.

const FENCE = /^ {0,3}(`{3,}|~{3,})/

// `{>>@Name 2026-10-09<<}`: who and when, drawn small. Any other `{>>note<<}` is a note.
const STAMP = /\{>>@([^<]*?) (\d{4}-\d{2}-\d{2})<<\}/g
const NOTE = /\{>>([\s\S]*?)<<\}/g

const escapeRegExp = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

// The changes of this review are the ones followed by its own stamp: they are numbered in the order of the text (the same order as the
// host's list), so that the host can ask for one of them to be shown (the Review panel's click on a change).
const ownChanges = (line, own) => {
    if (!own) return line
    // the text of a change never holds what closes it, so a mark that is not followed by the stamp is not swallowed into the next one
    const pattern = new RegExp('\\{~~((?:(?!~>|~~\\})[\\s\\S])*)~>((?:(?!~~\\})[\\s\\S])*)~~\\}(?=' + own.after + ')|\\{\\+\\+((?:(?!\\+\\+\\})[\\s\\S])*)\\+\\+\\}(?=' + own.after + ')|\\{--((?:(?!--\\})[\\s\\S])*)--\\}(?=' + own.after + ')', 'g')
    return line.replace(pattern, (m, oldText, newText, added, deleted) => {
        const i = own.n++
        if (added !== undefined) return `<ins class="caret-add" data-change="${i}">${added}</ins>`
        if (deleted !== undefined) return `<del class="caret-del" data-change="${i}">${deleted}</del>`
        return `<del class="caret-del" data-change="${i}">${oldText}</del><ins class="caret-add" data-change="${i}">${newText}</ins>`
    })
}

// The host marks the changes with a stamp of its own (`own.after`) and says which stamp to show instead (`own.shown`): the author and the day.
// The stamp that closes right after the change (`<<}`) is shown with the day that change was first seen (`own.days`, in the order of the text,
// by `own.by`: `{>>@Name `); a stamp with a note (the code note) is of the day of the comparison (`own.shown`).
const showOwnStamp = (line, own) => {
    if (!own || !own.prefix) return line
    const pattern = new RegExp(escapeRegExp(own.prefix) + '(<<' + '\\}' + ')?', 'g')
    return line.replace(pattern, (m, end) => {
        if (end && own.days && own.by && own.s < own.days.length) return own.by + own.days[own.s++] + end
        return (own.shown || own.prefix) + (end || '')
    })
}

const marks = (line, own) => showOwnStamp(ownChanges(line, own), own)
    .replace(/\{~~([\s\S]*?)~>([\s\S]*?)~~\}/g, '<del class="caret-del">$1</del><ins class="caret-add">$2</ins>')
    .replace(/\{\+\+([\s\S]*?)\+\+\}/g, '<ins class="caret-add">$1</ins>')
    .replace(/\{--([\s\S]*?)--\}/g, '<del class="caret-del">$1</del>')
    .replace(/\{==([\s\S]*?)==\}/g, '<mark class="caret-hl">$1</mark>')
    .replace(STAMP, '<span class="caret-stamp">$1 · $2</span>')
    .replace(NOTE, '<span class="caret-note">$1</span>')
// A line outside fenced code, without the marks inside its code spans.
const lineToHtml = (line, own) => {
    const spans = []
    const hidden = line.replace(/(`+)(?!`)([\s\S]*?[^`])\1(?!`)/g, (m) => { spans.push(m); return `\uE000${spans.length - 1}\uE001` })
    return marks(hidden, own).replace(/\uE000(\d+)\uE001/g, (m, i) => spans[Number(i)])
}

export const criticToHtml = (text, stamp, shown, days, by) => {
    const cut = (s) => s.replace(/<<\}$/, '')
    const own = stamp ? { after: escapeRegExp(stamp), n: 0, s: 0, prefix: cut(stamp), shown: shown ? cut(shown) : null, days: days ?? null, by: by ?? null } : null
    let fence = null
    return String(text ?? '').split('\n').map((line) => {
        const m = FENCE.exec(line)
        if (fence) {
            if (m && m[1][0] === fence[0] && m[1].length >= fence.length) fence = null
            return line
        }
        if (m) { fence = m[1]; return line }
        return lineToHtml(line, own)
    }).join('\n')
}

// The colours of the changes in the preview, light and dark (the preview's own page does not know the editor's theme sheet).
export const changesCss = (dark) => `
    ins.caret-add { text-decoration: none; background: ${dark ? 'rgba(46,160,67,.28)' : '#d4f4dd'}; color: ${dark ? '#8ddb8c' : '#14532d'}; border-radius: 2px; padding: 0 1px; }
    del.caret-del { background: ${dark ? 'rgba(248,81,73,.25)' : '#fde0e0'}; color: ${dark ? '#ffa198' : '#7f1d1d'}; text-decoration: line-through; border-radius: 2px; padding: 0 1px; }
    mark.caret-hl { background: ${dark ? 'rgba(210,153,34,.35)' : '#fff3b0'}; color: inherit; border-radius: 2px; }
    span.caret-stamp { font-size: .7em; opacity: .55; margin-left: .3em; white-space: nowrap; }
    span.caret-note { background: ${dark ? 'rgba(210,153,34,.25)' : '#fff8d6'}; font-size: .85em; border-radius: 2px; padding: 0 3px; }`