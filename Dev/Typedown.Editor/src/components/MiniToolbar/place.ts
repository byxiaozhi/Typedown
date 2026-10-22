// The floating toolbar over a selection (MiniToolbar/index.tsx): where it goes, and which of its buttons are on. Plain TypeScript,
// tested without a page.
export interface IRect {
    left: number
    top: number
    right: number
    bottom: number
}

export interface ISize {
    width: number
    height: number
}

// The height of the toolbar and its distance from the selection.
export const TOOLBAR_HEIGHT = 32
export const TOOLBAR_GAP = 8

// The toolbar is centred over the selection, 8 px above it; when there is no room above, 8 px below; always inside the window, and
// clear of the document map at the right edge (`rightEdge`).
export const placeToolbar = (
    selection: IRect,
    size: ISize,
    view: ISize,
    margin = 8,
    rightEdge = 24
): { left: number, top: number, below: boolean } => {
    const centre = (selection.left + selection.right) / 2
    const left = Math.max(margin, Math.min(centre - size.width / 2, view.width - size.width - rightEdge))
    const above = selection.top - size.height - TOOLBAR_GAP
    if (above >= margin) return { left, top: above, below: false }
    const below = selection.bottom + TOOLBAR_GAP
    return { left, top: Math.min(below, Math.max(margin, view.height - size.height - margin)), below: true }
}

export type ToggleButton = 'bold' | 'italic' | 'underline' | 'strike' | 'link'

// The buttons that are on for the formats the selection lies in (the tokens `selectionFormats` of the editor gives: `html_tag` with the
// tag `u` is the underline).
export const activeButtons = (formats: { type: string, tag?: string }[]): Set<ToggleButton> => {
    const on = new Set<ToggleButton>()
    for (const format of formats ?? []) {
        if (format.type === 'strong') on.add('bold')
        else if (format.type === 'em') on.add('italic')
        else if (format.type === 'del') on.add('strike')
        else if (format.type === 'link') on.add('link')
        else if (format.type === 'html_tag' && format.tag === 'u') on.add('underline')
    }
    return on
}
