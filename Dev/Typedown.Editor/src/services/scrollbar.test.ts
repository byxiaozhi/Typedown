import transport from './transport';

jest.mock('./transport', () => ({
    __esModule: true,
    default: { postMessage: jest.fn(), addListener: jest.fn() }
}));

let frame: FrameRequestCallback | undefined;
let resized: () => void;
const requestFrame = jest.fn();

beforeAll(() => {
    window.requestAnimationFrame = requestFrame;
    window.ResizeObserver = class {
        constructor(callback: ResizeObserverCallback) {
            resized = () => callback([], this);
        }
        observe() {}
        unobserve() {}
        disconnect() {}
    };
    require('./scrollbar');
});

beforeEach(() => {
    jest.clearAllMocks();
    frame = undefined;
    requestFrame.mockImplementation((callback: FrameRequestCallback) => {
        frame = callback;
        return 0; // Zero is a valid request ID and must still count as pending.
    });
});

function flushFrame() {
    const callback = frame;
    frame = undefined;
    callback?.(0);
}

test('coalesces scroll, resize and observer bursts into one latest-state message', () => {
    for (let i = 0; i < 100; i++) {
        window.dispatchEvent(new Event('scroll'));
        window.dispatchEvent(new Event('resize'));
        resized();
    }
    expect(requestFrame).toHaveBeenCalledTimes(1);
    expect(transport.postMessage).not.toHaveBeenCalled();
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 123 });
    flushFrame();
    expect(transport.postMessage).toHaveBeenCalledTimes(1);
    expect(transport.postMessage).toHaveBeenCalledWith('OnScroll', expect.objectContaining({ scrollY: 123 }));
});

test('sends again on a subsequent frame', () => {
    window.dispatchEvent(new Event('scroll'));
    flushFrame();
    window.dispatchEvent(new Event('scroll'));
    flushFrame();
    expect(transport.postMessage).toHaveBeenCalledTimes(2);
});
