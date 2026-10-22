// Where the changes of a tracked document are in the Visual view (docs/live-review-design.md, step L3).
//
// The host knows each change as text of the Markdown (what was there, what is there now, and some of the text just before and after
// it); the Visual view shows the document rendered, without its syntax. So a change is found by its words: the Markdown is reduced to
// the text a reader sees (plainOf) and looked for in the text of the page, in the order of the document, taking the place whose text
// before it agrees. A change that is not found is simply not drawn in place (it is still in the list and in the Split view).
//
// Plain functions, no DOM: tested in trackText.test.js. The DOM side is trackVisual.ts.

// The text of a piece of Markdown as it reads when rendered: links give their text, images and the marks of emphasis, headings, lists,
// quotes and code go, escapes lose their backslash. One run of white space is one space.
// `[label](target)` and `![alt](target)` give their label: the target is read to its closing parenthesis, counting the ones inside it
// (`[a](https://x.org/a_(b))`), which a pattern for "anything but a parenthesis" stops short of.
const withoutLinkTargets = (text) => {
    let out = ''
    let from = 0
    while (from < text.length) {
        const open = text.indexOf('[', from)
        if (open < 0) break
        let close = -1
        for (let k = open, depth = 0; k < text.length; k++) {
            if (text[k] === '[') depth++
            else if (text[k] === ']' && --depth === 0) { close = k; break }
        }
        let end = -1
        if (close >= 0 && text[close + 1] === '(') {
            for (let k = close + 1, depth = 0; k < text.length && text[k] !== '\n'; k++) {
                if (text[k] === '(') depth++
                else if (text[k] === ')' && --depth === 0) { end = k; break }
            }
        }
        if (end < 0) {
            out += text.slice(from, open + 1)
            from = open + 1
            continue
        }
        const image = open > from && text[open - 1] === '!'
        out += text.slice(from, image ? open - 1 : open) + text.slice(open + 1, close)
        from = end + 1
    }
    return out + text.slice(from)
}

export const plainOf = (md) => {
    let t = String(md ?? '')
    // an escaped mark is a letter of the text: kept out of the way (as a character of a private range) until the marks are gone
    t = t.replace(/\\([\\`*_{}[\]()#+\-.!|>~])/g, (m, c) => String.fromCharCode(0xE000 + c.charCodeAt(0)))
    t = withoutLinkTargets(t)
    t = t.replace(/^[ \t]{0,3}(?:#{1,6}[ \t]+|>[ \t]?|[-*+][ \t]+(?:\[[ xX]\][ \t]+)?|\d+[.)][ \t]+)/gm, '')
    t = t.replace(/\*\*|__|~~|`/g, '')
    t = t.replace(/\*/g, '')
    t = t.replace(/(^|\W)_+|_+(?=\W|$)/g, '$1')
    t = t.replace(/[\uE000-\uE07F]/g, c => String.fromCharCode(c.charCodeAt(0) - 0xE000))
    return t.replace(/\s+/g, ' ')
}

const escapeRegExp = (s) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

// A search for `text` that takes any run of white space of the page for the spaces of the text.
const searcher = (text) => new RegExp(escapeRegExp(text.trim()).replace(/ +/g, '\\s+'), 'g')

// All the places `text` is found in `flat` from `from`: [{ start, end }].
const findAll = (flat, text, from) => {
    const found = []
    if (!text.trim()) return found
    const re = searcher(text)
    re.lastIndex = from
    for (let m = re.exec(flat); m; m = re.exec(flat)) {
        found.push({ start: m.index, end: m.index + m[0].length })
        if (found.length > 200) break
        re.lastIndex = m.index + Math.max(1, m[0].length)
    }
    return found
}

const tail = (text, n) => {
    const t = plainOf(text).trimEnd()
    return t.length > n ? t.slice(t.length - n) : t
}

const head = (text, n) => {
    const t = plainOf(text).trimStart()
    return t.length > n ? t.slice(0, n) : t
}

// Whether the text of `flat` just before `index` ends with `before` (white space is one run, and the end of a block counts as one).
const endsWith = (flat, index, before) => {
    if (!before.trim()) return true
    const re = new RegExp(escapeRegExp(before.trim()).replace(/ +/g, '\\s+') + '\\s*$')
    return re.test(flat.slice(Math.max(0, index - before.length - 8), index))
}

const startsWith = (flat, index, after) => {
    if (!after.trim()) return true
    const re = new RegExp('^\\s*' + escapeRegExp(after.trim()).replace(/ +/g, '\\s+'))
    return re.test(flat.slice(index, index + after.length + 8))
}

// `flat`: the text of the page (the blocks of the document joined by a line break). `changes`: [{ index, kind, old, new, before, after }] in
// the order of the document. Returns, for each change that was found, { index, kind, start, end, at }: `start`..`end` is the new text
// (an empty range for a deletion) and `at` is where the change is (its start, or for a deletion the place the text was).
export const locateChanges = (flat, changes) => {
    const out = []
    let cursor = 0
    let floor = 0
    for (const change of changes || []) {
        const added = plainOf(change.new)
        const before = tail(change.before, 24)
        const after = head(change.after, 24)
        if (added.trim()) {
            const candidates = findAll(flat, added, cursor)
            if (!candidates.length) continue
            const best = candidates.find(c => endsWith(flat, c.start, before) && startsWith(flat, c.end, after))
                || candidates.find(c => endsWith(flat, c.start, before))
                || candidates[0]
            out.push({ index: change.index, kind: change.kind, start: best.start, end: best.end, at: best.start })
            floor = best.start
            cursor = best.end
            continue
        }
        // a deletion: where the text before it ends
        let at = cursor
        if (before.trim()) {
            // the text before it may end with the change before it (and start before that one), so the search starts a little before where that one starts
            const candidates = findAll(flat, before, Math.max(0, floor - before.length - 8)).filter(c => c.end >= cursor)
            if (!candidates.length) continue
            const best = candidates.find(c => startsWith(flat, c.end, after)) || candidates[0]
            at = best.end
        }
        out.push({ index: change.index, kind: change.kind, start: at, end: at, at })
        floor = at
        cursor = at
    }
    return out
}