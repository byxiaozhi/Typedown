// The rules of the punctuation hint (services/punctuation.js shows them): in Spanish a question or exclamation opens
// with ¿ or ¡ as well as closing with ? or !. A "?" or "!" that closes a sentence in which nothing opened is reported,
// with the two places where the missing mark could go; Caret never picks one for the user.
//
// The text of one block (a paragraph, a heading, a list item, a table cell) comes in as a string; the parts that are not
// prose (code, links, addresses) are replaced by PLACE, one character per character, so that every index still points at
// the same place in the page.

export const PLACE = '\uFFFC'

const LETTER = /[\p{L}\p{N}]/u
const CLOSE = /["'”’»)\]}*_~`]/
// Words that end in a full stop without ending the sentence: "¿Conoces al Sr. Pérez?"
const ABBREVIATION = /(?:^|[\s(¿¡«"“])(?:sr|sra|sres|srta|dr|dra|drs|lic|ing|arq|prof|profa|av|avda|ud|uds|etc|pág|págs|núm|tel|vs|aprox|dept|depto|cía|cia|gral|cap|art|ej|esq|admón|col|no|nº|n°)$/i
const OPENERS = /[\s"“'‘([«—–\-¿¡*_~`]/

const isLetter = ch => !!ch && LETTER.test(ch)

// Where the mark would start the sentence: past the spaces, quotes and dashes that open it, and past any text that is not prose
// (code, an address): the mark is typed in the prose, never inside or before such a part.
const insertionAt = (s, from, to) => {
    let i = from
    while (i < to && (OPENERS.test(s[i]) || s[i] === PLACE)) i++
    return i < to ? i : from
}

// The reports for one block: { kind: 'q' | 'e', at, from, start, comma }
//   at     the index of the "?" or "!" without an opening mark
//   from   the index where the word before it starts (what is underlined, up to and including the mark)
//   start  where a ¿ or ¡ would go at the start of the sentence
//   comma  where it would go after the last comma of the sentence, or -1
export const findMissingOpeners = (s) => {
    const found = []
    if (!s) return found
    let sentence = 0     // where the sentence we are in starts
    let open = { q: 0, e: 0 }
    let commas = []      // the commas of this sentence
    for (let i = 0; i < s.length; i++) {
        const ch = s[i]
        if (ch === '¿') open.q++
        else if (ch === '¡') open.e++
        else if (ch === ',') commas.push(i)
        else if (ch === '.' || ch === '…') {
            const next = s[i + 1]
            const ends = ch === '…' || (next === undefined || /\s/.test(next) || CLOSE.test(next))
            const before = s.slice(Math.max(0, i - 12), i)
            // "3.5", "www.x.com", "Sr." and an initial ("J. Pérez") do not end a sentence
            if (ends && !ABBREVIATION.test(before) && !/(?:^|\s)\p{Lu}$/u.test(before)) {
                sentence = i + 1
                commas = []
            }
        } else if (ch === '?' || ch === '!') {
            const prev = s[i - 1]
            const next = s[i + 1]
            const kind = ch === '?' ? 'q' : 'e'
            const after = next === undefined || /\s/.test(next) || CLOSE.test(next) || next === '?' || next === '!' || next === PLACE
            const closes = isLetter(prev) || prev === PLACE || prev === '?' || prev === '!' || prev === '.' || prev === '…' || (prev && CLOSE.test(prev))
            if (prev === '¿' || prev === '¡') {
                // "¿?" and "¡!": opened and closed at once
                if (open[kind] > 0) open[kind]--
            } else if (closes && after) {
                if (open[kind] > 0) open[kind]--
                else if (LETTER.test(s.slice(sentence, i))) {
                    let from = i
                    while (from > 0 && (isLetter(s[from - 1]) || s[from - 1] === PLACE || (/['’]/.test(s[from - 1]) && isLetter(s[from - 2])))) from--
                    const last = commas.length ? commas[commas.length - 1] : -1
                    const comma = last > sentence ? insertionAt(s, last + 1, i) : -1
                    found.push({ kind, at: i, from, start: insertionAt(s, sentence, i), comma: comma > insertionAt(s, sentence, i) ? comma : -1 })
                }
            }
            // after the last mark of a run ("?!", "?»", "?)") a new sentence starts
            if (next !== '?' && next !== '!') {
                let j = i + 1
                while (j < s.length && CLOSE.test(s[j])) j++
                if (j >= s.length || /\s/.test(s[j])) { sentence = j; commas = [] }
            }
        }
    }
    return found
}