const posted: any[] = [];
jest.mock('./transport', () => ({
    __esModule: true,
    default: { postMessageNoDiff: (name: string, args: unknown) => posted.push({ name, args }) },
}));

// eslint-disable-next-line import/first
import { setVimState, vimWantsKey, vimCommand } from './vim';
// eslint-disable-next-line import/first
import { createReadingKeys } from './vimReading';

const ctrl = (key: string, extra: Partial<KeyboardEvent> = {}) => ({ key, ctrlKey: true, shiftKey: false, altKey: false, metaKey: false, ...extra });

test('the host leaves Vim its Ctrl keys only where Vim uses them', () => {
    setVimState('normal');
    expect(posted.pop()).toEqual({ name: 'VimState', args: { state: 'normal' } });
    expect(vimWantsKey(ctrl('v'))).toBe(true);
    expect(vimWantsKey(ctrl('R'))).toBe(true);
    expect(vimWantsKey(ctrl('s'))).toBe(false);
    expect(vimWantsKey(ctrl('v', { shiftKey: true }))).toBe(false);
    expect(vimWantsKey({ key: 'v', ctrlKey: false, shiftKey: false, altKey: false })).toBe(false);

    setVimState('insert');
    expect(vimWantsKey(ctrl('v'))).toBe(false); // paste stays the application's
    expect(vimWantsKey(ctrl('['))).toBe(true);

    setVimState('reading');
    expect(vimWantsKey(ctrl('d'))).toBe(true);
    expect(vimWantsKey(ctrl('v'))).toBe(false);

    setVimState('off');
    expect(vimWantsKey(ctrl('d'))).toBe(false);
    expect((window as any).__typedownVimWants).toBe(vimWantsKey);
});

test('commands go to the bridge when it answers, else to the host', () => {
    posted.length = 0;
    vimCommand('write');
    expect(posted).toEqual([{ name: 'VimCommand', args: { command: 'write' } }]);
    (window as any).__typedownBridgeVim = (c: string) => c === 'find';
    vimCommand('find');
    vimCommand('quit');
    expect(posted.map(p => p.args.command)).toEqual(['write', 'quit']);
    delete (window as any).__typedownBridgeVim;
});

test('reading keys move the page: counts, ends, headings, marks', () => {
    let y = 1000;
    const tops = [-500, -100, 300, 900]; // headings relative to the window top
    const scroller = () => ({ top: () => y, by: (d: number) => { y += d; }, to: (v: number) => { y = Math.max(0, Math.min(v, 99999)); }, height: () => 600 });
    const headings = () => tops.map(t => ({ getBoundingClientRect: () => ({ top: t - (y - 1000) }) }) as unknown as HTMLElement);
    const keys = createReadingKeys(scroller, headings);
    const press = (key: string, extra: Partial<KeyboardEvent> = {}) => {
        const e = { key, ctrlKey: false, shiftKey: false, altKey: false, metaKey: false, target: null, preventDefault: jest.fn(), stopPropagation: jest.fn(), ...extra };
        keys(e as unknown as KeyboardEvent);
        return e;
    };
    press('j'); expect(y).toBe(1060);
    press('3'); press('k'); expect(y).toBe(880);
    press('d', { ctrlKey: true }); expect(y).toBe(1180);
    y = 1000;
    press(']'); press(']'); expect(y).toBe(1000 + 300 - 8); // the next heading below the top
    y = 1000;
    press('['); press('['); expect(y).toBe(1000 - 100 - 8); // the previous one above
    press('m'); press('a'); press('G'); expect(y).toBe(99999);
    press("'"); press('a'); expect(y).toBe(892);
    press('g'); press('g'); expect(y).toBe(0);
    const other = press('x');
    expect(other.preventDefault).not.toHaveBeenCalled();
});
