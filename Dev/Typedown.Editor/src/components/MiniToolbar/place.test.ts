import { activeButtons, placeToolbar } from './place'

const view = { width: 1000, height: 700 }
const size = { width: 200, height: 32 }

describe('placeToolbar', () => {
    it('centres the toolbar 8 px above the selection', () => {
        const at = placeToolbar({ left: 400, right: 500, top: 300, bottom: 320 }, size, view)
        expect(at).toEqual({ left: 350, top: 300 - 32 - 8, below: false })
    })

    it('goes below the selection when there is no room above', () => {
        const at = placeToolbar({ left: 400, right: 500, top: 20, bottom: 40 }, size, view)
        expect(at.below).toBe(true)
        expect(at.top).toBe(48)
    })

    it('stays inside the window at the left and clear of the right edge', () => {
        expect(placeToolbar({ left: 0, right: 20, top: 300, bottom: 320 }, size, view).left).toBe(8)
        expect(placeToolbar({ left: 980, right: 1000, top: 300, bottom: 320 }, size, view).left).toBe(1000 - 200 - 24)
    })

    it('stays in the window when the selection fills it, below or not', () => {
        const at = placeToolbar({ left: 100, right: 300, top: 2, bottom: 698 }, size, view)
        expect(at.below).toBe(true)
        expect(at.top).toBeLessThanOrEqual(700 - 32 - 8)
        expect(at.top).toBeGreaterThanOrEqual(8)
    })
})

describe('activeButtons', () => {
    it('turns on the buttons of the formats around the selection', () => {
        const on = activeButtons([{ type: 'strong' }, { type: 'em' }, { type: 'del' }, { type: 'link' }, { type: 'html_tag', tag: 'u' }])
        expect([...on].sort()).toEqual(['bold', 'italic', 'link', 'strike', 'underline'])
    })

    it('leaves out the formats that have no button, and html tags that are not an underline', () => {
        expect(activeButtons([{ type: 'inline_code' }, { type: 'html_tag', tag: 'sup' }, { type: 'image' }]).size).toBe(0)
    })

    it('copes with no formats', () => {
        expect(activeButtons([]).size).toBe(0)
        expect(activeButtons(undefined as any).size).toBe(0)
    })
})
