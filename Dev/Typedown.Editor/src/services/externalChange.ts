// Showing the reader what a connected program just changed (assets/styles/index.css, .td-external-change).

export const EXTERNAL_CHANGE_CLASS = 'td-external-change';
/** As long as the fade in the stylesheet. */
export const EXTERNAL_CHANGE_MS = 2000;

/** Highlights these elements briefly; elements re-rendered meanwhile simply lose it. */
export function highlightExternalChange(elements: (Element | null)[]) {
    const shown = elements.filter((e): e is Element => !!e);
    for (const element of shown) {
        // Restart the animation when the same block changes again before it ended.
        element.classList.remove(EXTERNAL_CHANGE_CLASS);
        void (element as HTMLElement).offsetWidth;
        element.classList.add(EXTERNAL_CHANGE_CLASS);
    }
    setTimeout(() => shown.forEach(e => e.classList.remove(EXTERNAL_CHANGE_CLASS)), EXTERNAL_CHANGE_MS);
}

/**
 * reveal: "change" (docs/automation-api-spec.md, section 2.3): where to scroll the page so a change that spans top..bottom
 * (page coordinates) can be seen when the page is at viewY, or null when it already can: it starts on screen and
 * either fits or starts in the upper half. Otherwise its start goes a third of the way down the window.
 */
export function scrollTargetForChange(top: number, bottom: number, viewY: number): number | null {
    const height = window.innerHeight
    if (!(height > 0)) return null
    if (top >= viewY && top < viewY + height && (bottom <= viewY + height || top < viewY + height / 2)) return null
    return Math.max(0, Math.round(top - height / 3))
}
