// The smallest replacement that turns one text into another: the common beginning and end are left alone, so an
// editor given only the middle keeps the reader's place (automation writes, docs/automation-api-spec.md section 2.2).

export interface TextChange {
    /** Offset in the old text where the replaced part starts (UTF-16 code units). */
    from: number
    /** Offset in the old text where the replaced part ends. */
    to: number
    /** What replaces it. */
    text: string
}

const isHighSurrogate = (code: number) => code >= 0xd800 && code <= 0xdbff;
const isLowSurrogate = (code: number) => code >= 0xdc00 && code <= 0xdfff;

/** Null when the texts are equal. The change never splits a surrogate pair. */
export function minimalChange(before: string, after: string): TextChange | null {
    if (before === after) return null;
    const limit = Math.min(before.length, after.length);
    let start = 0;
    while (start < limit && before.charCodeAt(start) === after.charCodeAt(start)) start++;
    // Stopped after the first half of a pair whose second halves differ: start at the pair.
    if (start > 0 && isHighSurrogate(before.charCodeAt(start - 1))) start--;
    let end = 0;
    while (end < limit - start && before.charCodeAt(before.length - 1 - end) === after.charCodeAt(after.length - 1 - end)) end++;
    // Stopped before the second half of a pair whose first halves differ: end after the pair.
    if (end > 0 && isLowSurrogate(before.charCodeAt(before.length - end))) end--;
    return { from: start, to: before.length - end, text: after.slice(start, after.length - end) };
}

/** Applies a change to the text it was computed from. */
export function applyChange(before: string, change: TextChange | null): string {
    return change ? before.slice(0, change.from) + change.text + before.slice(change.to) : before;
}
