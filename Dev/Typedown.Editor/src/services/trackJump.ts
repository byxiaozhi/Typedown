// Live review: the Review panel asks to show a change (MainWindow.LiveReview.cs). In the source pane (CodeMirror) the place of the
// change is selected and brought into view, and lit for a moment; the preview pane scrolls to the same change (Preview/index.tsx).
// The offset is a position in the text of the document with its lines ended by a single line feed, which is how CodeMirror counts too.

const w = window as any

const style = document.createElement('style')
style.textContent = '.caret-track-flash{background:rgba(224,168,0,.38);border-radius:2px;}'
document.head.appendChild(style)

w.__caretTrack = {
    // Returns "source" when the source pane showed it, "preview" when only the preview did, "" when neither could.
    jump(index: number, offset: number, length: number): string {
        let where = ''
        const box = document.querySelector('.CodeMirror') as any
        const cm = box && box.CodeMirror
        if (cm) {
            const from = cm.posFromIndex(Math.max(0, offset))
            const to = cm.posFromIndex(Math.max(0, offset) + Math.max(0, length))
            cm.setSelection(from, to)
            cm.scrollIntoView({ from, to }, 140)
            const mark = length > 0 ? cm.markText(from, to, { className: 'caret-track-flash' }) : null
            if (mark) setTimeout(() => mark.clear(), 1600)
            where = 'source'
        }
        if (typeof w.__caretTrackPreview === 'function' && w.__caretTrackPreview(index)) where = where || 'preview'
        return where
    },
}

export {}
