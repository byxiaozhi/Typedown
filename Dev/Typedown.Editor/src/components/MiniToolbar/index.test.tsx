import React from 'react'
import { act, render, screen } from '@testing-library/react'
import MiniToolbar from './index'

jest.mock('services/remote', () => ({ remote: { getStringResources: () => Promise.resolve({}) } }))
jest.mock('services/transport', () => ({ __esModule: true, default: { postMessageNoDiff: jest.fn() } }))

// The part of the editor's event centre the toolbar uses.
const makeEditor = () => {
    const listeners: Record<string, ((arg?: any) => void)[]> = {}
    return {
        eventCenter: {
            subscribe: (name: string, fn: (arg?: any) => void) => { (listeners[name] ||= []).push(fn) },
            unsubscribe: (name: string, fn: (arg?: any) => void) => { listeners[name] = (listeners[name] || []).filter(f => f !== fn) },
            dispatch: (name: string, arg?: any) => (listeners[name] || []).slice().forEach(f => f(arg))
        }
    }
}

const selectWords = () => {
    const p = document.createElement('p')
    p.textContent = 'some selected words'
    document.body.appendChild(p)
    const range = document.createRange()
    range.selectNodeContents(p)
    const selection = window.getSelection()!
    selection.removeAllRanges()
    selection.addRange(range)
    return p
}

describe('MiniToolbar', () => {
    beforeEach(() => {
        jest.useFakeTimers()
        // jsdom lays nothing out: the selected words are somewhere on the screen
        ;(Range.prototype as any).getBoundingClientRect = () => ({ left: 100, right: 200, top: 100, bottom: 120, width: 100, height: 20 })
    })

    afterEach(() => {
        jest.useRealTimers()
        window.getSelection()?.removeAllRanges()
        document.body.innerHTML = ''
    })

    // the labels arrive from the host a moment after the first render
    const mount = async (editor: any) => {
        await act(async () => {
            render(<MiniToolbar editor={editor} review={false} speech={false} onFormat={() => { }} onParagraph={() => { }} />)
        })
    }

    const pick = (editor: any) => act(() => {
        editor.eventCenter.dispatch('muya-format-picker', { reference: {}, formats: [] })
    })

    it('shows 250 ms after the selection is made', async () => {
        const editor = makeEditor()
        await mount(editor)
        selectWords()
        pick(editor)
        act(() => { jest.advanceTimersByTime(249) })
        expect(screen.queryByRole('toolbar')).toBeNull()
        act(() => { jest.advanceTimersByTime(1) })
        expect(screen.getByRole('toolbar')).toBeTruthy()
    })

    it('hides when the content is replaced, though nothing told it the selection was gone', async () => {
        const editor = makeEditor()
        await mount(editor)
        selectWords()
        pick(editor)
        act(() => { jest.advanceTimersByTime(250) })
        expect(screen.getByRole('toolbar')).toBeTruthy()

        act(() => { editor.eventCenter.dispatch('muya-markdown-set') })
        expect(screen.queryByRole('toolbar')).toBeNull()
    })

    it('does not show a toolbar that was waiting when the content is replaced', async () => {
        const editor = makeEditor()
        await mount(editor)
        selectWords()
        pick(editor)
        act(() => { editor.eventCenter.dispatch('muya-markdown-set') })
        act(() => { jest.advanceTimersByTime(1000) })
        expect(screen.queryByRole('toolbar')).toBeNull()
    })

    it('still goes with Esc and with a click elsewhere', async () => {
        const editor = makeEditor()
        await mount(editor)
        selectWords()
        pick(editor)
        act(() => { jest.advanceTimersByTime(250) })
        act(() => { document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })) })
        expect(screen.queryByRole('toolbar')).toBeNull()

        selectWords()
        pick(editor)
        act(() => { jest.advanceTimersByTime(250) })
        expect(screen.getByRole('toolbar')).toBeTruthy()
        act(() => { document.body.dispatchEvent(new Event('pointerdown', { bubbles: true })) })
        expect(screen.queryByRole('toolbar')).toBeNull()
    })
})
