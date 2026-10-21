import { scanSource } from './scan'

describe('scanSource', () => {
    it('finds the headings with their words', () => {
        const items = scanSource('# Title\n\ntext\n\n## Second part ##\n\n###### deep')
        expect(items.map(i => [i.kind, i.line, i.text])).toEqual([
            ['heading', 0, 'Title'],
            ['heading', 4, 'Second part'],
            ['heading', 6, 'deep'],
        ])
    })

    it('finds every kind of review mark, with the stamp of a change not counted as a comment', () => {
        const text = 'a {++new++} b {--old--} c {~~was~>is~~}{>>@Ann 2026-10-07<<} d {==seen==} e {>>@Jo 2026-10-08: shorten it<<}'
        const items = scanSource(text)
        expect(items.map(i => [i.kind, i.text])).toEqual([
            ['add', 'new'],
            ['del', 'old'],
            ['del', 'was'],
            ['add', 'is'],
            ['mark', 'seen'],
            ['comment', '@Jo 2026-10-08: shorten it'],
        ])
    })

    it('leaves the review marks out when they are not drawn', () => {
        expect(scanSource('x {++new++} y', false)).toEqual([])
    })

    it('finds the pauses only in Speech mode', () => {
        const text = 'one {pause 2s} two {beat} three {wait} {cue: smile} {slow}four{/slow}'
        expect(scanSource(text, true, false)).toEqual([])
        expect(scanSource(text, true, true).map(i => i.text)).toEqual(['{pause 2s}', '{beat}', '{wait}'])
    })

    it('does not look inside fenced code', () => {
        const text = '# Real\n\n```md\n# not a heading\n{++not a mark++}\n```\n\n~~~\n{pause}\n~~~\n\n## After'
        expect(scanSource(text, true, true).map(i => [i.kind, i.text])).toEqual([['heading', 'Real'], ['heading', 'After']])
    })

    it('a fence is closed by the same kind of fence, not by a shorter one or a line with words', () => {
        const text = '````\n```\n# inside\n```` not a close\n# still inside\n````\n# out'
        expect(scanSource(text).map(i => i.text)).toEqual(['out'])
    })

    it('gives the line of each item and copes with any line ending', () => {
        const items = scanSource('a\r\n# One\r\nb\r\n{==x==}\r\n')
        expect(items.map(i => [i.kind, i.line])).toEqual([['heading', 1], ['mark', 3]])
    })
})
