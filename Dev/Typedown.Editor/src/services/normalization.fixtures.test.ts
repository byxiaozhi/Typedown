import { classifyNormalization, extractProtectedPayload } from './normalization';

// The shared protected-payload fixtures (docs/automation-fixtures/protected-payload.json).
// eslint-disable-next-line @typescript-eslint/no-var-requires
const fixtures = require('../../../../docs/automation-fixtures/protected-payload.json');

describe('loss of a protected payload is unsafe, with the reason', () => {
    for (const f of fixtures.loss) {
        test(`${f.category}: ${f.name}`, () => {
            const result = classifyNormalization(f.source, f.normalized);
            expect(result.pendingNormalization).toBe('unsafe');
            expect(result.reasons).toContain(f.reason);
        });
    }
});

describe('a difference of form is not unsafe', () => {
    for (const f of fixtures.safe) {
        test(f.name, () => {
            const result = classifyNormalization(f.source, f.normalized);
            expect(result.pendingNormalization).not.toBe('unsafe');
            expect(result.pendingNormalization).toBe('unknown');
        });
    }
});

describe('every preserve document is recognised as carrying its payload', () => {
    const key: Record<string, string> = { links: 'links', fences: 'fences', footnotes: 'footnotes', tasks: 'tasks', html: 'htmlTags', frontMatter: 'frontMatter', math: 'math', extensions: 'fences' };
    for (const f of fixtures.preserve) {
        test(`${f.category}: ${f.name}`, () => {
            const payload = extractProtectedPayload(f.markdown) as any;
            expect(payload[key[f.category]].length).toBeGreaterThan(0);
            expect(classifyNormalization(f.markdown, f.markdown).pendingNormalization).toBe('none');
        });
    }
});
