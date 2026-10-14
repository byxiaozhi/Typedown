import transport from 'services/transport';
import { errorText } from 'services/errorText';

/**
 * An error in the page that nothing caught - a listener that threw, a promise nobody awaited - goes to the host's log
 * (PageError), not only to the console nobody sees. Each message once, at most thirty a page.
 */
export const installPageErrorReporting = () => {
    const seen = new Set<string>();
    const report = (error: unknown, where?: string) => {
        const message = errorText(error)
        const key = `${message}@${where ?? ''}`
        if (seen.has(key) || seen.size >= 30) return
        seen.add(key)
        try {
            transport.postMessage('PageError', { message, where: where ?? null, stack: (error as any)?.stack ?? null })
        } catch {
            // The host is not there (the page is going): nothing to tell.
        }
    }
    window.addEventListener('error', e => report(e.error ?? e.message, e.filename ? `${e.filename}:${e.lineno}:${e.colno}` : undefined))
    window.addEventListener('unhandledrejection', e => report(e.reason, 'unhandled rejection'))
}
