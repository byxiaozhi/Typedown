import { createHash } from 'crypto';
import { TextEncoder } from 'util';
import { sha256Hex } from './sha256';

// jsdom has no TextEncoder in this Jest setup.
(global as any).TextEncoder = (global as any).TextEncoder ?? TextEncoder;

test('matches the standard vectors', () => {
    expect(sha256Hex('')).toBe('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855');
    expect(sha256Hex('abc')).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
    expect(sha256Hex('abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq')).toBe('248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1');
});

test('matches Node for UTF-8 text at every padding boundary', () => {
    for (const base of ['a', '中', '😀', 'é']) {
        for (let n = 0; n < 130; n++) {
            const text = base.repeat(n) + '\n';
            expect(sha256Hex(text)).toBe(createHash('sha256').update(text, 'utf8').digest('hex'));
        }
    }
});
