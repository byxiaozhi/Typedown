import { scrollTargetForChange } from './externalChange';

// A window 900 px high: a change counts as seen when it starts on screen and either fits or starts in the upper half.
beforeAll(() => { Object.defineProperty(window, 'innerHeight', { configurable: true, value: 900 }); });

test('a change on screen does not move the page', () => {
    expect(scrollTargetForChange(1200, 1260, 1000)).toBeNull();
    expect(scrollTargetForChange(1000, 1900, 1000)).toBeNull();
});

test('a change below or above the window puts its start a third of the way down', () => {
    expect(scrollTargetForChange(5000, 5040, 0)).toBe(4700);
    expect(scrollTargetForChange(100, 140, 3000)).toBe(0);
    expect(scrollTargetForChange(400, 440, 3000)).toBe(100);
});

test('a change that starts low on screen and runs past the bottom is brought up', () => {
    expect(scrollTargetForChange(1700, 2400, 1000)).toBe(1400);
    // Starting in the upper half it stays, however long it is.
    expect(scrollTargetForChange(1300, 4000, 1000)).toBeNull();
});

test('a page with no window yet is left alone', () => {
    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 0 });
    expect(scrollTargetForChange(5000, 5040, 0)).toBeNull();
    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 900 });
});
