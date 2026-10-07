import React, { useCallback, useEffect, useRef, useState } from "react";
import ExportHtml from "services/exportHtml";
import { getHtmlToc, getTOC } from "services/common";
import transport from "services/transport";
import './index.scss'

interface IPreview {
    markdown: string
    options: any
}

// Read-only rendered preview for the split view (code on the left, this on the right). Rendered
// with the same ExportHtml the HTML export uses, so the preview matches what an export produces.
// It lives in a sandboxed iframe without allow-scripts: nothing in the document can run, and
// allow-same-origin only lets this component read the frame's scroll size for scroll sync.
// ExportHtml emits a light-themed page; the background is made transparent so the editor's own
// background shows through, and in dark mode the text and code/table/quote colors are overridden
// so it stays readable. Colors are chosen from the theme name rather than read from the editor
// page, whose theme stylesheet loads asynchronously after a switch. Re-renders on ThemeChanged.
const DARK_TEXT = '#e3e6ec'

const Preview: React.FC<IPreview> = ({ markdown, options }) => {
    const [html, setHtml] = useState('')
    const [themeVersion, setThemeVersion] = useState(0)
    const frameRef = useRef<HTMLIFrameElement>(null)

    useEffect(() => transport.addListener('ThemeChanged', () => setThemeVersion(v => v + 1)), [])

    const syncScroll = useCallback(() => {
        const frame = frameRef.current?.contentWindow
        if (!frame) return
        const max = document.documentElement.scrollHeight - window.innerHeight
        const ratio = max > 0 ? window.scrollY / max : 0
        const frameMax = frame.document.documentElement.scrollHeight - frame.innerHeight
        frame.scrollTo(0, ratio * Math.max(frameMax, 0))
    }, [])

    useEffect(() => {
        let cancelled = false
        const timer = setTimeout(async () => {
            const baseUrl = window.basePath ? `file:///${window.basePath.replaceAll('\\', '/')}/` : undefined
            const dark = window.actualTheme === 'dark'
            const themeCss = `
                html, body, .markdown-body { background: transparent !important; }
                .markdown-body { max-width: none; padding: 24px 32px;
                    font-size: ${options?.fontSize ?? 16}px; line-height: ${options?.lineHeight ?? 1.6}; }
                ${dark ? `
                .markdown-body { color: ${DARK_TEXT} !important; }
                .markdown-body pre, .markdown-body code, .markdown-body tt { background: rgba(127,127,127,.18) !important; }
                code[class*="language-"], pre[class*="language-"] { color: inherit !important; text-shadow: none !important; }
                .markdown-body table tr, .markdown-body table tr:nth-child(2n) { background: transparent !important; }
                .markdown-body table td, .markdown-body table th { border-color: rgba(127,127,127,.35) !important; }
                .markdown-body blockquote { color: inherit !important; opacity: .8; border-left-color: rgba(127,127,127,.4) !important; }
                .markdown-body hr { background: rgba(127,127,127,.3) !important; }
                .markdown-body h1, .markdown-body h2 { border-bottom-color: rgba(127,127,127,.3) !important; }` : ''}`
            const page = await new ExportHtml(markdown, { ...options, baseUrl }).generate({
                printOptimization: false,
                title: '',
                toc: getHtmlToc(getTOC(markdown ?? '').toc),
                extraCss: themeCss,
            })
            if (!cancelled) setHtml(page)
        }, 250)
        return () => {
            cancelled = true
            clearTimeout(timer)
        }
    }, [markdown, options, themeVersion])

    useEffect(() => {
        addEventListener('scroll', syncScroll)
        return () => removeEventListener('scroll', syncScroll)
    }, [syncScroll])

    return (
        <iframe
            ref={frameRef}
            className="split-preview"
            title="Preview"
            sandbox="allow-same-origin"
            srcDoc={html}
            onLoad={syncScroll}
        />
    )
}

export default Preview
