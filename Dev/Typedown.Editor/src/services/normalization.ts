// Classifies what Muya's normalization of a document would do to it once the reader first edits in visual mode.
//
// Muya imports Markdown into its own model and exports a normalized serialization. Until the first real edit the
// editor maps that normalized text back to the original source (`importedRef` in components/Muya), so an unedited
// document round-trips exactly. The first real edit makes the whole normalized serialization the document text.
// `source` vs `normalized` therefore predicts that first-edit rewrite, and automation writes are gated on it
// (docs/automation-api-spec.md, section 2.2):
//
//   none            - identical.
//   knownFormatting - only transforms on a proven whitelist, with every protected payload intact. The whitelist is
//                     empty in this version: it is filled from recorded first-edit baselines, never guessed.
//   unknown         - differs and cannot be shown safe. The default write policy rejects it.
//   unsafe          - a protected payload or a word of the text would be lost or altered.
//
// The protected payloads are scanned from the raw text by this module, not by Muya's parser: a defect in that
// parser would otherwise affect both sides of the comparison and hide itself. `unsafe` needs evidence of loss;
// anything merely different is `unknown`.

export type PendingNormalization = 'none' | 'knownFormatting' | 'unknown' | 'unsafe';

export interface NormalizationResult {
    pendingNormalization: PendingNormalization;
    classifierVersion: number;
    /** Stable English reasons, e.g. `fence-lost`, `link-lost`, `word-lost`, `text-added`, `formatting-changed`. */
    reasons: string[];
}

export const classifierVersion = 3;

export interface ProtectedPayload {
    /** Fenced code blocks as `info\u0000body`, where info is the full info string after the fence; `\u0000broken` is
     *  appended when a container line inside it would end it (see extractProtectedPayload). */
    fences: string[];
    /** Link and image destinations with their titles, from inline links, reference definitions and autolinks. */
    links: string[];
    /** Footnote references and definitions, as `ref:id` / `def:id`. */
    footnotes: string[];
    /** Task list states in document order, `x` or ` `. */
    tasks: string[];
    /** Raw HTML opening tags as `tag attr=value ...` with attributes sorted. */
    htmlTags: string[];
    /** The YAML front matter body, if the document starts with one. */
    frontMatter: string[];
    /** Display maths bodies between `$$` lines, and inline maths as `inline:body`. */
    math: string[];
    /** Letter/number runs of the whole text, in order. */
    words: string[];
}

const fenceOpen = /^( {0,3})(`{3,}|~{3,})(.*)$/;
const wordPattern = new RegExp('[\\p{L}\\p{N}]+', 'gu');
const footnotePattern = /\[\^([^\]\s]+)\](:?)/g;
const inlineLinkPattern = /!?\[(?:[^\]\\]|\\.)*\]\(\s*<?([^\s)>]*)>?(?:\s+("[^"]*"|'[^']*'|\([^)]*\)))?\s*\)/g;
const referencePattern = /^ {0,3}\[(?!\^)(?:[^\]\\]|\\.)+\]:\s*<?(\S+?)>?(?:\s+("[^"]*"|'[^']*'|\([^)]*\)))?\s*$/;
const autolinkPattern = /<((?:https?|ftp|mailto):[^\s<>]+)>/g;
const htmlTagPattern = /<([A-Za-z][A-Za-z0-9-]*)((?:\s+[^\s"'>/=]+(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s"'=<>`]+))?)*)\s*\/?>/g;
const htmlAttributePattern = /([^\s"'>/=]+)(?:\s*=\s*("[^"]*"|'[^']*'|[^\s"'=<>`]+))?/g;
// Inline maths: `$body$` where the body neither starts nor ends with a space (so "$5 and $6" is not maths), outside
// code spans. `$$` is display maths, handled per line above.
const inlineMathPattern = /(?<![\\$])\$(?!\$)(?!\s)([^$\n]+?)(?<![\\\s])\$(?!\$)/g;
const codeSpanPattern = /(`+)[^`]*?\1/g;

const containerStart = /^\s*(?:[-*+]\s|\d+[.)]\s|>)/;
const taskPattern = /^\s*(?:[-*+]|\d+[.)])\s+\[([ xX])\](?=\s)/;

// Identifiers that are not text: task markers, reference labels and footnote ids. They are protected (or merely
// representational) elsewhere, and counting them as words would call `[X]` -> `[x]` or a reference link turned
// inline a lost word.
const identifierPatterns = [
    /^(\s*(?:[-*+]|\d+[.)])\s+)\[[ xX]\](?=\s)/gm,
    /^( {0,3})\[(?!\^)(?:[^\]\\]|\\.)+\]:/gm,
    /\]\[(?:[^\]\\]|\\.)*\]/g,
    /\[\^[^\]\s]+\]/g,
];

function wordsOf(markdown: string): string[] {
    let text = markdown;
    for (const pattern of identifierPatterns) text = text.replace(pattern, (_m, lead) => typeof lead === 'string' ? lead : ']');
    return text.match(wordPattern) ?? [];
}

const unquote = (value: string) => value.length >= 2 && /^["'(]/.test(value) ? value.slice(1, -1) : value;

/** Scans the protected payloads of a Markdown document. Line endings are expected to be `\n`. */
export function extractProtectedPayload(markdown: string): ProtectedPayload {
    const payload: ProtectedPayload = { fences: [], links: [], footnotes: [], tasks: [], htmlTags: [], frontMatter: [], math: [], words: [] };
    const lines = markdown.split('\n');
    let i = 0;
    if (lines[0] === '---') {
        const end = lines.indexOf('---', 1);
        if (end > 0) {
            payload.frontMatter.push(lines.slice(1, end).join('\n'));
            i = end + 1;
        }
    }
    const prose: string[] = [];
    for (; i < lines.length; i++) {
        const open = fenceOpen.exec(lines[i]);
        if (open) {
            const marker = open[2];
            const body: string[] = [];
            let broken = false;
            let j = i + 1;
            for (; j < lines.length; j++) {
                const close = new RegExp(`^ {0,3}${marker[0] === '`' ? '`' : '~'}{${marker.length},}\\s*$`);
                if (close.test(lines[j])) break;
                // CommonMark: a fence indented by n spaces takes up to n spaces off each line of its content.
                // This scanner does not track containers, so an indented fence is taken as possibly inside a list
                // or quote: a less indented line that starts one of those would end that container, and the fence
                // with it, in a real parser. Such a fence is recorded as broken rather than as its text.
                const indent = lines[j].length - lines[j].trimStart().length;
                if (indent < open[1].length && containerStart.test(lines[j])) broken = true;
                body.push(lines[j].replace(new RegExp(`^ {0,${open[1].length}}`), ''));
            }
            payload.fences.push(`${open[3].trim()}\u0000${body.join('\n')}${broken ? '\u0000broken' : ''}`);
            i = j;
            continue;
        }
        if (lines[i].trim() === '$$') {
            const end = lines.findIndex((line, k) => k > i && line.trim() === '$$');
            if (end > 0) {
                payload.math.push(lines.slice(i + 1, end).join('\n').trim());
                i = end;
                continue;
            }
        }
        const reference = referencePattern.exec(lines[i]);
        if (reference) payload.links.push(`${reference[1]}\u0000${unquote(reference[2] ?? '')}`);
        const task = taskPattern.exec(lines[i]);
        if (task) payload.tasks.push(task[1].toLowerCase());
        prose.push(lines[i]);
    }
    const text = prose.join('\n');
    for (const match of text.matchAll(inlineLinkPattern)) payload.links.push(`${match[1]}\u0000${unquote(match[2] ?? '')}`);
    for (const match of text.matchAll(autolinkPattern)) payload.links.push(`${match[1]}\u0000`);
    for (const match of text.replace(codeSpanPattern, '').matchAll(inlineMathPattern)) payload.math.push(`inline:${match[1]}`);
    for (const match of text.matchAll(footnotePattern)) payload.footnotes.push(`${match[2] ? 'def' : 'ref'}:${match[1]}`);
    for (const match of text.matchAll(htmlTagPattern)) {
        const attributes = [...match[2].matchAll(htmlAttributePattern)].map(a => `${a[1].toLowerCase()}=${unquote(a[2] ?? '')}`).sort();
        payload.htmlTags.push([match[1].toLowerCase(), ...attributes].join(' '));
    }
    payload.words = wordsOf(markdown);
    return payload;
}

/** Items of `expected` (as a multiset) that `actual` does not contain. */
function missing(expected: string[], actual: string[]): string[] {
    const counts = new Map<string, number>();
    for (const item of actual) counts.set(item, (counts.get(item) ?? 0) + 1);
    const lost: string[] = [];
    for (const item of expected) {
        const left = counts.get(item) ?? 0;
        if (left > 0) counts.set(item, left - 1);
        else lost.push(item);
    }
    return lost;
}

/** Predicts what Muya's first-edit serialization would do to `source`. Both texts use `\n` line endings. */
export function classifyNormalization(source: string, normalized: string): NormalizationResult {
    if (source === normalized) return { pendingNormalization: 'none', classifierVersion, reasons: [] };
    const before = extractProtectedPayload(source);
    const after = extractProtectedPayload(normalized);
    const reasons: string[] = [];
    const lossChecks: [keyof ProtectedPayload, string][] = [
        ['fences', 'fence-lost'], ['links', 'link-lost'], ['footnotes', 'footnote-lost'], ['htmlTags', 'html-lost'],
        ['frontMatter', 'front-matter-lost'], ['math', 'math-lost'], ['words', 'word-lost'],
    ];
    for (const [key, reason] of lossChecks)
        if (missing(before[key], after[key]).length) reasons.push(reason);
    if (before.tasks.join('') !== after.tasks.join('')) reasons.push('task-state-changed');
    if (reasons.length) return { pendingNormalization: 'unsafe', classifierVersion, reasons };
    if (missing(after.words, before.words).length) reasons.push('text-added');
    reasons.push('formatting-changed');
    return { pendingNormalization: 'unknown', classifierVersion, reasons };
}
