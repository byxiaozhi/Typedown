import { applyChange, minimalChange } from './minimalChange';

const roundTrip = (before: string, after: string) => {
    const change = minimalChange(before, after);
    expect(applyChange(before, change)).toBe(after);
    return change;
};

test('equal texts need no change', () => {
    expect(minimalChange('a\nb\n', 'a\nb\n')).toBeNull();
});

test('a short replacement in a long text touches only the replaced word', () => {
    const before = '# Title\n\n' + 'line\n'.repeat(1000) + 'the old word\n' + 'tail\n'.repeat(1000);
    const after = before.replace('old', 'new');
    const change = roundTrip(before, after)!;
    expect(change.text).toBe('new');
    expect(change.to - change.from).toBe(3);
    expect(before.slice(change.from, change.to)).toBe('old');
});

test('insertions, deletions and repeated characters stay within the texts', () => {
    const cases: [string, string][] = [
        ['', 'abc'], ['abc', ''], ['aaa', 'aaaa'], ['aaaa', 'aa'], ['abcabc', 'abc'], ['x\n', 'x\n\n'], ['ab', 'ba'],
        ['one two three', 'one three'], ['start', 'restart'], ['end', 'endless'],
    ];
    for (const [before, after] of cases) {
        const change = roundTrip(before, after)!;
        expect(change.from).toBeGreaterThanOrEqual(0);
        expect(change.from).toBeLessThanOrEqual(change.to);
        expect(change.to).toBeLessThanOrEqual(before.length);
    }
});

test('a surrogate pair is never split', () => {
    // 😀 is 😀, 😁 is 😁: they share the first half.
    const change = roundTrip('a😀b', 'a😁b')!;
    expect(change.text).toBe('😁');
    // 😀 and 🙀 (🙀) share nothing but 🟀-like pairs can share the second half: 🈀 vs 😀.
    const second = roundTrip('a🈀b', 'a😀b')!;
    expect(second.text).toBe('😀');
    for (const c of [change, second]) {
        expect(c.text.length).toBe(2);
    }
});

test('random edits always reproduce the target', () => {
    let seed = 7;
    const random = (n: number) => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed % n; };
    const alphabet = ['a', 'b', '\n', ' ', '😀', 'é'];
    const word = (n: number) => Array.from({ length: n }, () => alphabet[random(alphabet.length)]);
    for (let i = 0; i < 500; i++) {
        // Cut by characters, not UTF-16 units, so the texts themselves hold whole pairs.
        const chars = word(random(30));
        const at = random(chars.length + 1);
        const before = chars.join('');
        const after = [...chars.slice(0, at), ...word(random(5)), ...chars.slice(Math.min(chars.length, at + random(5)))].join('');
        const change = minimalChange(before, after);
        expect(applyChange(before, change)).toBe(after);
        if (change) {
            expect(isWhole(change.text)).toBe(true);
            expect(isWhole(before.slice(change.from, change.to))).toBe(true);
        }
    }
});

// No lone surrogate at either end.
function isWhole(s: string) {
    if (!s) return true;
    const first = s.charCodeAt(0), last = s.charCodeAt(s.length - 1);
    return !(first >= 0xdc00 && first <= 0xdfff) && !(last >= 0xd800 && last <= 0xdbff);
}
