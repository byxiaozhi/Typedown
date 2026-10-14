/**
 * What went wrong, as text a person can read: a library may throw something that is not an Error (Mermaid throws an
 * object), and String() of that is "[object Object]".
 */
export const errorText = (e: unknown): string => {
    if (e instanceof Error) return e.message || e.name
    if (typeof e === 'string') return e
    if (e && typeof e === 'object') {
        const o = e as any
        if (typeof o.message === 'string' && o.message) return o.message
        if (typeof o.str === 'string' && o.str) return o.str
        try { return JSON.stringify(e) } catch { /* fall through */ }
    }
    return String(e)
}
