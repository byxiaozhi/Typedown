// The document map (DocumentMap/index.tsx): what the strip at the right edge marks. In the Visual view the page is asked (the
// elements that are drawn); in the Code view there are no such elements for the text that is out of sight, so the text itself is read
// here: headings, review marks (CriticMarkup) and speech pauses, line by line. Plain TypeScript, tested without a page.
export type TickKind = 'heading' | 'match' | 'current' | 'add' | 'del' | 'mark' | 'comment' | 'pause'

export interface ISourceItem {
    kind: TickKind
    line: number
    text: string
}

// The most marks the strip draws: a term that stands everywhere must not make the page slow.
export const MAX_TICKS = 2000

const FENCE = /^ {0,3}(`{3,}|~{3,})/
const HEADING = /^ {0,3}#{1,6}\s+(.*?)\s*#*\s*$/
// one review mark on a line: {++added++} {--deleted--} {~~old~>new~~} {==highlight==} {>>comment<<}
const MARK = /\{(\+\+|--|~~|==|>>)([\s\S]*?)(?:\+\+|--|~~|==|<<)\}/g
// the stamp written behind a change ({>>@Ann 2026-10-07<<}) belongs to that change; it is not a comment of its own
const STAMP = /^@[^<]*\d{4}-\d{2}-\d{2}\s*$/
// a pause in the speech marks: {pause}, {pause 2s}, {beat}, {wait}
const PAUSE = /\{(?:pause|beat|wait)\b[^}]*\}/g

const flat = (text: string) => text.replace(/\s+/g, ' ').trim()

// The things worth a tick in a Markdown text. `review`: the review marks are drawn (Settings > Editor > Show review marks);
// `speech`: Speech mode is on (the pauses are marks only then). Nothing inside fenced code counts.
export const scanSource = (markdown: string, review = true, speech = false): ISourceItem[] => {
    const items: ISourceItem[] = []
    let fence: string | null = null
    const lines = (markdown ?? '').split(/\r\n|\r|\n/)
    for (let line = 0; line < lines.length; line++) {
        const text = lines[line]
        const opens = FENCE.exec(text)
        if (fence) {
            if (opens && opens[1][0] === fence[0] && opens[1].length >= fence.length && text.trim() === opens[1]) fence = null
            continue
        }
        if (opens) { fence = opens[1]; continue }

        const heading = HEADING.exec(text)
        if (heading) items.push({ kind: 'heading', line, text: flat(heading[1]) })
        if (review && text.includes('{')) {
            for (const m of text.matchAll(MARK)) {
                const inner = flat(m[2])
                switch (m[1]) {
                    case '++': items.push({ kind: 'add', line, text: inner }); break
                    case '--': items.push({ kind: 'del', line, text: inner }); break
                    case '~~': {
                        const [from, to] = m[2].split('~>')
                        items.push({ kind: 'del', line, text: flat(from) })
                        items.push({ kind: 'add', line, text: flat(to ?? '') })
                        break
                    }
                    case '==': items.push({ kind: 'mark', line, text: inner }); break
                    default:
                        if (!STAMP.test(m[2])) items.push({ kind: 'comment', line, text: inner })
                }
            }
        }
        if (speech && text.includes('{'))
            for (const m of text.matchAll(PAUSE)) items.push({ kind: 'pause', line, text: flat(m[0]) })
        if (items.length >= MAX_TICKS) break
    }
    return items
}
