export {};

const send = jest.fn();
let transport: typeof import('./transport').default;

beforeEach(() => {
    jest.resetModules();
    send.mockReset();
    Object.defineProperty(window, 'chrome', {
        configurable: true,
        value: { webview: { postMessage: send, addEventListener: jest.fn() } }
    });
    transport = require('./transport').default;
});

afterEach(() => {
    // A test that used fake timers must not leave them installed, or a later test's real awaits never resolve.
    jest.useRealTimers();
});

const messages = () => send.mock.calls.map(([message]) => JSON.parse(message));

function reconstruct() {
    let state = '';
    for (const message of messages()) {
        state = message.diff
            ? state.slice(0, message.start) + message.args + state.slice(message.end)
            : message.args;
    }
    return JSON.parse(state);
}

test.each(['OnScroll', 'SelectionFormats'])('deduplicates unchanged %s state', name => {
    transport.postMessage(name, { value: 1 });
    for (let i = 0; i < 100; i++) transport.postMessage(name, { value: 1 });
    expect(send).toHaveBeenCalledTimes(1);
    transport.postMessage(name, { value: 2 });
    expect(send).toHaveBeenCalledTimes(2);
    expect(reconstruct()).toEqual({ value: 2 });
});

test.each(['OpenURI', 'FileLoaded'])('preserves repeated %s events', name => {
    transport.postMessage(name, { text: 'same' });
    transport.postMessage(name, { text: 'same' });
    expect(send).toHaveBeenCalledTimes(2);
    expect(reconstruct()).toEqual({ text: 'same' });
});

test('reconstructs insertions, deletions and Unicode changes', () => {
    for (const text of ['hello', 'hello 世界 😀', '世界', '', 'new document']) {
        transport.postMessage('MarkdownChange', { text });
        expect(reconstruct()).toEqual({ text });
    }
});

test('does not advance the diff baseline after a failed send', () => {
    transport.postMessage('MarkdownChange', { text: 'initial' });
    send.mockImplementationOnce(() => { throw new Error('send failed'); });
    expect(() => transport.postMessage('MarkdownChange', { text: 'lost' })).toThrow('send failed');
    // The failed call was never delivered to the host.
    send.mock.calls.pop();
    transport.postMessage('MarkdownChange', { text: 'next' });
    expect(reconstruct()).toEqual({ text: 'next' });
});

// --- remoteFunction: every pending invoke is cleaned up (reply, timeout, or send failure) ---

const messageHandler = () =>
    (window as any).chrome.webview.addEventListener.mock.calls.find((c: any[]) => c[0] === 'message')[1];

const lastInvokeId = () => {
    const invokes = send.mock.calls.map(([m]) => JSON.parse(m)).filter((m: any) => m.type === 'invoke');
    return invokes[invokes.length - 1].id as string;
};

const respond = (id: string, payload: unknown) =>
    messageHandler()({ data: JSON.stringify({ name: id, args: payload }) });

test('invoke resolves/rejects on reply and leaves no listener behind', async () => {
    const { remoteFunction, _listenerCount } = require('./transport');
    const ok = remoteFunction('X')();
    const okId = lastInvokeId();
    respond(okId, { code: 0, data: 42 });
    await expect(ok).resolves.toBe(42);
    expect(_listenerCount(okId)).toBe(0);
    const bad = remoteFunction('X')();
    const badId = lastInvokeId();
    respond(badId, { code: 1, msg: 'nope' });
    await expect(bad).rejects.toThrow('nope');
    expect(_listenerCount(badId)).toBe(0);
});

test('invoke rejects when the host never replies within the deadline', async () => {
    jest.useFakeTimers();
    const { remoteFunction } = require('./transport');
    const { _listenerCount } = require('./transport');
    const p = remoteFunction('X', 1000)();
    const id = lastInvokeId();
    const rejected = expect(p).rejects.toThrow(/timed out/);
    jest.advanceTimersByTime(1000);
    await rejected;
    expect(_listenerCount(id)).toBe(0);
});

test('invoke with timeout 0 never times out (export/print) and still resolves', async () => {
    jest.useFakeTimers();
    const { remoteFunction } = require('./transport');
    const p = remoteFunction('X', 0)();
    jest.advanceTimersByTime(10 * 60 * 1000);
    respond(lastInvokeId(), { code: 0, data: 'done' });
    await expect(p).resolves.toBe('done');
});

test('invoke rejects and cleans up when the send throws', async () => {
    const { remoteFunction } = require('./transport');
    const { _listenerCount } = require('./transport');
    send.mockImplementationOnce(() => { throw new Error('boom'); });
    const p = remoteFunction('X')();
    const id = lastInvokeId();
    await expect(p).rejects.toThrow('boom');
    expect(_listenerCount(id)).toBe(0);
});

test('a resolved invoke does not fire a late timeout', async () => {
    jest.useFakeTimers();
    const { remoteFunction } = require('./transport');
    const p = remoteFunction('X', 1000)();
    respond(lastInvokeId(), { code: 0, data: 1 });
    await expect(p).resolves.toBe(1);
    jest.advanceTimersByTime(5000); // the timer was cleared on resolve; nothing rejects here
});
