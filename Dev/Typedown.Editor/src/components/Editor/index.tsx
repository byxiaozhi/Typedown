import { setScrollLoadId } from 'services/scrollbar'
import CodeMirror from "components/CodeMirror";
import MuyaEditor from "components/Muya";
import React, { useCallback, useEffect, useRef, useState } from "react";
import { remote } from "services/remote";
import transport from "services/transport";
import './index.scss'
import ExportHtml from "services/exportHtml";
import { errorText } from "services/errorText";
import { htmlToMarkdown } from "services/importHtml";
import { DEFAULT_TURNDOWN_CONFIG } from "components/Muya/lib/config";
import { getHtmlToc, getTOC } from "services/common";
import { resetUserIntent, takeChangeOrigin } from "services/changeOrigin";
import { sha256Hex } from "services/sha256";
import { classifierVersion } from "services/normalization";
import { applyHostBackground } from "services/theme";

/** The style element with this id, appended to the head on first use. */
// Each SVG drawing in the element (a diagram) replaced by a PNG of it, drawn at twice its size on white. The drawing is
// drawn on its own, with only its own styles: the page's (a dark theme) do not reach it. One that cannot be drawn
// (a canvas the browser will not read back) is left as it is.
const diagramsToPictures = async (root: HTMLElement) => {
    for (const svg of Array.from(root.querySelectorAll('svg'))) {
        if (svg.parentElement?.closest('svg')) continue
        try {
            const box = svg.viewBox?.baseVal
            const width = box && box.width > 0 ? box.width : parseFloat(svg.getAttribute('width') ?? '')
            const height = box && box.height > 0 ? box.height : parseFloat(svg.getAttribute('height') ?? '')
            if (!(width > 0 && height > 0)) continue
            const copy = svg.cloneNode(true) as SVGSVGElement
            copy.setAttribute('xmlns', 'http://www.w3.org/2000/svg')
            copy.setAttribute('width', String(width))
            copy.setAttribute('height', String(height))
            copy.style.maxWidth = ''
            const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(copy)], { type: 'image/svg+xml' }))
            try {
                const image = new Image()
                await new Promise((resolve, reject) => { image.onload = resolve; image.onerror = reject; image.src = url })
                const scale = 2
                const canvas = document.createElement('canvas')
                canvas.width = Math.ceil(width * scale)
                canvas.height = Math.ceil(height * scale)
                const context = canvas.getContext('2d')!
                context.fillStyle = '#ffffff'
                context.fillRect(0, 0, canvas.width, canvas.height)
                context.drawImage(image, 0, 0, canvas.width, canvas.height)
                const picture = document.createElement('img')
                picture.setAttribute('src', canvas.toDataURL('image/png'))
                picture.setAttribute('width', String(Math.round(width)))
                picture.setAttribute('alt', 'diagram')
                svg.replaceWith(picture)
            } finally {
                URL.revokeObjectURL(url)
            }
        } catch (e) {
            console.log('a diagram could not be made a picture', e)
        }
    }
}

const styleElement = (id: string) => {
    let style = document.getElementById(id) as HTMLStyleElement | null
    if (!style) {
        style = document.createElement('style')
        style.id = id
        document.head.appendChild(style)
    }
    return style
}

const Editor: React.FC = () => {
    // The document text and cursor live in refs, not React state: a state update per keystroke would commit
    // a React render while the contenteditable has focus, and React then walks the whole editor DOM to
    // snapshot/restore the selection (O(document size) on every key press). Only host-driven content
    // replacements (LoadFile/SetMarkdown/ImportFile) bump `contentVersion` to push new text into the editor.
    const markdownRef = useRef<string>();
    // Reporting the text is throttled while typing; this runs it now (see Muya's flushContentChange).
    const flushRef = useRef<(() => void) | null>(null);
    const cursorRef = useRef<any>();
    const [contentVersion, setContentVersion] = useState(0);
    const [options, setOptions] = useState<any>();
    const optionsRef = useRef<any>();
    const [searchOpen, setSearchOpen] = useState(0);
    const [searchArg, setSearchArg] = useState<{ value: string, opt: any }>();
    const muyaScrollTopRef = useRef(0);
    const codeMirrorScrollRef = useRef(0);

    // Every host-driven load carries a `loadId`; it is echoed on everything the editor reports so the host can drop
    // messages that still belong to the previous document (a tab switch races with in-flight change events).
    const loadIdRef = useRef<number>()

    // The FileLoaded handshake reports the loaded source, preserving its spelling even though Muya normalizes
    // its internal model. The host's "saved" hash must come from the first change report *after* the editor
    // applied the new text — not from a fixed delay, which a large document or a slow first render overruns and
    // then leaves the file looking modified without an edit. The timers are only fallbacks.
    const fileLoadPending = useRef<{ armed: boolean, timer?: number }>()

    const flushFileLoaded = useCallback(() => {
        const pending = fileLoadPending.current
        if (!pending) return
        clearTimeout(pending.timer)
        fileLoadPending.current = undefined
        transport.postMessage('FileLoaded', { text: markdownRef.current, loadId: loadIdRef.current })
    }, [])

    const OnFileLoaded = useCallback(() => {
        clearTimeout(fileLoadPending.current?.timer)
        fileLoadPending.current = { armed: false, timer: window.setTimeout(flushFileLoaded, 3000) }
    }, [flushFileLoaded])

    // Set when the next content from the host is an automation edit of the text shown: the editor then applies only
    // the changed part and keeps the reader's cursor and scroll position. Loads replace the whole text.
    const localChangeRef = useRef(false)
    // The automation edit being applied asked to be scrolled into view (reveal: "change"); read once by the editor.
    const scrollToChangeRef = useRef(false)

    // An automation edit the page has been asked to apply (ApplyDocumentEdit), until it has replied.
    const pendingEditRef = useRef<{ operationId: string } | null>(null)

    // Reports what the page now holds for the edit in flight: the exact source and what the first visual edit would
    // do to it (services/normalization). Without Muya (source mode) nothing is known about the latter.
    const describeNormalization = useCallback(() => {
        const source = markdownRef.current ?? ''
        const sourceHash = sha256Hex(source)
        const pending = (window as any).__typedownPendingNormalization?.()
        const normalization = pending
            ? { pendingNormalization: pending.pendingNormalization, sourceHash, normalizedHash: sha256Hex(pending.normalized), reasons: pending.reasons, classifierVersion: pending.classifierVersion }
            : { pendingNormalization: 'unknown', sourceHash, normalizedHash: null, reasons: ['notEvaluated'], classifierVersion }
        return { sourceHash, normalization }
    }, [])

    const replyEditApplied = useCallback(() => {
        const edit = pendingEditRef.current
        if (!edit) return false
        pendingEditRef.current = null
        transport.postMessage('DocumentEditApplied', { operationId: edit.operationId, loadId: loadIdRef.current, outcome: 'applied', ...describeNormalization() })
        return true
    }, [describeNormalization])

    // Host -> editor: the document rendered as the exports render it (diagrams drawn, math typeset), serialized as XHTML
    // so that it parses as XML, for a host turning it into another format. With diagramsAsPictures each diagram is a
    // PNG on white (the export's light theme), for a format that cannot show SVG.
    useEffect(() => transport.addListener<{ token: number, diagramsAsPictures?: boolean }>('RenderXhtml', async ({ token, diagramsAsPictures }) => {
        flushRef.current?.()
        try {
            const html = await new ExportHtml(markdownRef.current ?? '', { ...optionsRef.current, diagramsAsPictures }).renderHtml(undefined)
            const body = new DOMParser().parseFromString(`<!DOCTYPE html><html><body>${html}</body></html>`, 'text/html').body
            if (diagramsAsPictures) await diagramsToPictures(body)
            transport.postMessage('RenderedXhtml', { token, xhtml: new XMLSerializer().serializeToString(body) })
        } catch (e) {
            transport.postMessage('RenderedXhtml', { token, error: errorText(e) })
        }
    }), []);

    // Host -> editor: how the editor is drawn right now (the settings it applied), for checking that a setting reached
    // the page and not only the host.
    useEffect(() => transport.addListener<{ token: number }>('QueryEditorStyle', ({ token }) => {
        const element = document.querySelector('#ag-editor-id') || document.querySelector('.CodeMirror')
        const style = element ? getComputedStyle(element) : null
        // Muya sets the text direction on each block (a dir attribute), not on the editor: read the first block's.
        const block = document.querySelector('#ag-editor-id > *') || element
        const direction = block ? getComputedStyle(block).direction : null
        // The first paragraph's own layout (indent, alignment, spacing): what styles for paragraphs do.
        const paragraph = document.querySelector('#ag-editor-id > p .ag-paragraph-content') as HTMLElement | null
        const p = paragraph ? getComputedStyle(paragraph) : null
        const pBlock = paragraph?.parentElement ? getComputedStyle(paragraph.parentElement) : null
        transport.postMessage('EditorStyle', {
            token, fontSize: style?.fontSize ?? null, lineHeight: style?.lineHeight ?? null, direction,
            fontFamily: style?.fontFamily ?? null,
            paragraph: p ? { textIndent: p.textIndent, textAlign: p.textAlign, marginTop: pBlock?.marginTop ?? null } : null,
        })
    }), []);

    // Host -> editor: answer once the page has drawn twice more (automation awaitPresentation). Two frames, because
    // the first callback runs before the frame that shows the latest change is painted.
    useEffect(() => transport.addListener<{ token: number }>('AwaitPresentation', ({ token }) => {
        // The viewport comes along: a host whose web view is a separate native window (Uno on X11) compares it with
        // the size it laid the view out at, to know the page has caught up with a resize.
        requestAnimationFrame(() => requestAnimationFrame(() => transport.postMessage('PresentationFrames', { token, width: window.innerWidth, height: window.innerHeight })))
    }), []);

    // Host -> editor: what the first visual edit would do to the text shown now (document.get's normalization).
    useEffect(() => transport.addListener<{ token: number }>('QueryNormalization', ({ token }) => {
        flushRef.current?.()
        transport.postMessage('NormalizationReport', { token, loadId: loadIdRef.current, ...describeNormalization() })
    }), [describeNormalization]);

    // Called by the child editor right after it applied host content; the next change report completes the handshake.
    // Text from the host's history (SetMarkdown) until the editor holds it: its load id takes effect then.
    const pendingSetRef = useRef<{ loadId?: number } | null>(null)

    const onContentApplied = useCallback(() => {
        const set = pendingSetRef.current
        if (set) {
            pendingSetRef.current = null
            if (typeof set.loadId === 'number') {
                loadIdRef.current = set.loadId
                setScrollLoadId(set.loadId)
            }
        }
        if (replyEditApplied()) return
        const pending = fileLoadPending.current
        if (!pending || pending.armed) return
        clearTimeout(pending.timer)
        pending.armed = true
        pending.timer = window.setTimeout(flushFileLoaded, 500)
    }, [flushFileLoaded, replyEditApplied])

    // Editor -> host: called by the child editor on every change; no React re-render involved.
    const onMarkdownChange = useCallback((markdown: string) => {
        if (markdown == undefined) return
        const pending = fileLoadPending.current
        if (pending) {
            // Before the new text is applied any report still describes the previous document: drop it.
            if (!pending.armed) return
            markdownRef.current = markdown
            resetUserIntent()
            flushFileLoaded()
            return
        }
        // Host history text on its way into the editor: until it is there, a report (a late one about the text
        // the editor held before) describes content the host has already replaced. Drop it, as for a load.
        if (pendingSetRef.current) return
        // While an automation edit is being applied the host holds the text it sent; the editor's reports of
        // applying it are not edits and must not advance the host's history or revision.
        if (pendingEditRef.current) {
            markdownRef.current = markdown
            return
        }
        if (markdownRef.current != markdown) {
            markdownRef.current = markdown
            transport.postMessage('MarkdownChange', { text: markdown, loadId: loadIdRef.current, origin: takeChangeOrigin() });
        }
    }, [flushFileLoaded])

    // The outline, the word count and the caret line, tagged with the load they describe. Without the tag
    // the host cannot tell a report about the document just left from one about the document now shown, and
    // a quick switch left it holding the previous document's outline — headings that are not in the
    // document on screen, which it then tried to scroll to.
    const onStateChange = useCallback((state: any) => {
        if (fileLoadPending.current && !fileLoadPending.current.armed) return
        transport.postMessage('StateChange', { state, loadId: loadIdRef.current, muya: true })
    }, [])

    // Which heading the reader has scrolled to, in reading mode. Deliberately not part of the state report:
    // the host decides where to scroll from `cur` in that report, so a heading sent that way comes back as a
    // scroll, which moves the page, which reports another heading.
    const onOutlineCurrent = useCallback((slug: string, where?: unknown) => {
        transport.postMessage('OutlineCurrent', { slug, where, loadId: loadIdRef.current })
    }, [])

    const onCursorChange = useCallback((cursor: any) => {
        if (fileLoadPending.current && !fileLoadPending.current.armed) return
        cursorRef.current = cursor
        transport.postMessage('CursorChange', { cursor, loadId: loadIdRef.current })
    }, [])

    // Host -> editor: replace the content without echoing it back as a MarkdownChange.
    // A remembered scroll offset (reading mode has no caret to remember) takes precedence over scrolling to the caret.
    const scrollFromHostRef = useRef(false)

    const setContentFromHost = useCallback((markdown: string, cursor?: any, scrollTop?: number | null, local = false) => {
        localChangeRef.current = local
        markdownRef.current = markdown
        cursorRef.current = cursor
        scrollFromHostRef.current = typeof scrollTop === 'number'
        muyaScrollTopRef.current = typeof scrollTop === 'number' ? scrollTop : 0
        codeMirrorScrollRef.current = typeof scrollTop === 'number' ? scrollTop : 0
        setContentVersion(v => v + 1)
    }, [])

    useEffect(() => {
        remote.getSettings().then(({ markdown, basePath, cursor, scrollTop, loadId, ...opt }: any) => {
            window.basePath = basePath
            loadIdRef.current = loadId
            setScrollLoadId(loadId)
            setOptions(opt)
            OnFileLoaded();
            setContentFromHost(markdown, cursor ?? undefined, scrollTop)
        })
    }, [OnFileLoaded, setContentFromHost]);

    useEffect(() => {
        optionsRef.current = options
    }, [options])

    // The host asks for this before it saves: whatever it holds must be what is on screen.
    // A custom theme, an edition's own styles and the user's own CSS are three style elements, in that order: a theme
    // sets the palette, and whatever the user writes in the settings still has the last word. Here, not in one editor, so they follow
    // a change in every mode (source mode kept the old theme until the mode was switched).
    useEffect(() => {
        const css = options?.themeCss || ''
        styleElement('typedown-theme-css').textContent = css
        // The host paints the body to match the window chrome; a theme that sets its own page colour should win.
        // Clearing it lets the next ThemeChanged from the host paint it again when the theme is switched off.
        if (css) document.body.style.backgroundColor = 'var(--editorBgColor)'
        else applyHostBackground()
    }, [options?.themeCss])

    useEffect(() => {
        styleElement('typedown-edition-css').textContent = options?.editionCss || ''
    }, [options?.editionCss])

    useEffect(() => {
        styleElement('typedown-custom-css').textContent = options?.customCss || ''
    }, [options?.customCss])

    useEffect(() => transport.addListener<{ token?: number }>('FlushContent', ({ token }) => {
        flushRef.current?.()
        transport.postMessage('ContentFlushed', { token: token ?? 0, text: markdownRef.current })
    }), []);

    useEffect(() => transport.addListener<IExportArgs>('Export', async ({ type, context, basePath, title, options }) => {
        flushRef.current?.()
        // The theme and the user's own CSS style the editor, so the exported file should carry them too —
        // otherwise a document looks different the moment it leaves the app.
        const styling = [optionsRef.current?.themeCss, optionsRef.current?.editionCss, optionsRef.current?.customCss].filter(Boolean).join('\n')
        const generateOption: any = { printOptimization: false, title, toc: getHtmlToc(getTOC(markdownRef.current ?? '').toc), ...options }
        if (styling) generateOption.extraCss = [generateOption.extraCss, styling].filter(Boolean).join('\n')
        const baseUrl = basePath ? `file:///${basePath.replaceAll('\\', '/')}/` : undefined
        if (type == 'print') {
            // Print this page — the document as the reader sees it, in the editor's theme — rather than an
            // export rendered with another stylesheet. The print rules in index.css take the editing chrome
            // out and lay every block out; the editor loses focus first so no block is the active one.
            try {
                (document.activeElement as HTMLElement | null)?.blur?.()
                await new Promise(r => requestAnimationFrame(() => r(null)))
                window.print()
                return
            } catch (e) {
                console.log('window.print failed, printing the export instead', e)
            }
        }
        // A failure is told to the host, which says it: the reader had picked a file and nothing came.
        let html: string
        try {
            html = await new ExportHtml(markdownRef.current, { ...optionsRef.current, baseUrl }).generate(generateOption)
        } catch (e) {
            console.log('export failed', e)
            const error = errorText(e)
            if (type == 'print') remote.printHTML({ error, context })
            else remote.exportCallback({ error, context })
            return
        }
        if (type == 'print') {
            remote.printHTML({ html, context })
        } else {
            remote.exportCallback({ html, context })
        }
    }), []);

    useEffect(() => transport.addListener<{ type: string, text: string }>('ImportFile', ({ text }) => {
        // Imported content is a genuine edit: show it and report it to the host. (A conversion that throws reaches the
        // host's log as a page error.)
        const markdown = htmlToMarkdown(text, [], DEFAULT_TURNDOWN_CONFIG)
        onMarkdownChange(markdown)
        setContentVersion(v => v + 1)
    }), [options, onMarkdownChange]);

    // Loads arrive one message each and are applied one per frame, the newest only. A key held down on
    // the tab shortcut sent five hundred of them in a few seconds; every one was rendered in turn, the host
    // dropped their reports as stale, and the reader waited a minute for the last to come round. Only the
    // load the host still wants can matter, and the host's stale-report rule already says which that is.
    const pendingLoadRef = useRef<{ text: string, basePath: string, cursor?: any, scrollTop?: number | null, loadId?: number } | null>(null)
    useEffect(() => transport.addListener<{ text: string, basePath: string, cursor?: any, scrollTop?: number | null, loadId?: number }>('LoadFile', (load) => {
        // A load replaces whatever an automation edit was applying (the host restoring it, or a tab switch): that
        // edit's outcome is unknown to the host, which then restores from its own copy.
        const edit = pendingEditRef.current
        if (edit) {
            pendingEditRef.current = null
            transport.postMessage('DocumentEditApplied', { operationId: edit.operationId, loadId: loadIdRef.current, outcome: 'failed', reason: 'superseded' })
        }
        const scheduled = pendingLoadRef.current != null
        pendingLoadRef.current = load
        if (scheduled) return
        requestAnimationFrame(() => {
            const pending = pendingLoadRef.current
            pendingLoadRef.current = null
            if (!pending) return
            const { text, basePath, cursor, scrollTop, loadId } = pending
            window.basePath = basePath
            loadIdRef.current = loadId
            setScrollLoadId(loadId)
            OnFileLoaded();
            setContentFromHost(text, cursor ?? undefined, scrollTop)
        })
    }), [OnFileLoaded, setContentFromHost]);

    // Host -> editor: an automation edit (docs/automation-api-spec.md, section 2.2, steps 6-7). In this one event the
    // page takes in its own throttled typing, compares the live text with the base the host checked, and either
    // refuses (the reader typed meanwhile) or starts applying; the reply follows once the editor holds the text.
    useEffect(() => transport.addListener<{ operationId: string, baseContentHash: string, text: string, scrollToChange?: boolean }>('ApplyDocumentEdit', ({ operationId, baseContentHash, text, scrollToChange }) => {
        const refuse = (outcome: string, reason: string) => transport.postMessage('DocumentEditApplied', { operationId, loadId: loadIdRef.current, outcome, reason })
        if (fileLoadPending.current || pendingEditRef.current) return refuse('failed', 'busy')
        flushRef.current?.()
        if (sha256Hex(markdownRef.current ?? '') !== baseContentHash) return refuse('conflict', 'baseContentHashMismatch')
        pendingEditRef.current = { operationId }
        resetUserIntent()
        const y = optionsRef.current?.sourceCode ? codeMirrorScrollRef.current : window.scrollY
        scrollToChangeRef.current = !!scrollToChange
        setContentFromHost(text, undefined, y, true)
    }), [setContentFromHost]);

    // Host -> editor: text from the host's own undo history. Reports about the text held before are dropped until the
    // new text is in the editor, which then reports under the given loadId: the host can tell the two apart.
    useEffect(() => transport.addListener<{ text: string, cursor: string, basePath: string, loadId?: number }>('SetMarkdown', ({ text, cursor, basePath, loadId }) => {
        // Only a message that names the folder changes it (Redo sent none, and every relative picture stopped loading).
        if (basePath) window.basePath = basePath
        pendingSetRef.current = { loadId }
        setContentFromHost(text, cursor)
    }), [setContentFromHost]);

    useEffect(() => transport.addListener<Record<string, unknown>>('SettingsChanged', (newOptions) => {
        // Finish any throttled edit before changing mode or unmounting its editor.
        if ('sourceCode' in newOptions || 'readOnly' in newOptions) flushRef.current?.()
        for (const name in newOptions) {
            const value = newOptions[name];
            if (name.startsWith('search'))
                setSearchArg(old => old ? { ...old, opt: { ...old.opt, [name]: value } } : old)
        }
        setOptions((oldOptions: any) => ({ ...oldOptions, ...newOptions }))
    }), []);

    useEffect(() => transport.addListener<{ open: number }>('SearchOpenChange', ({ open }) => {
        setSearchOpen(open)
    }), []);

    if (!options) {
        return <></>
    }

    if (options.sourceCode) {
        return (
            <CodeMirror
                options={options}
                cursor={cursorRef.current}
                markdown={markdownRef.current ?? ''}
                contentVersion={contentVersion}
                searchOpen={searchOpen}
                searchArg={searchArg}
                scrollTopRef={codeMirrorScrollRef}
                scrollFromHostRef={scrollFromHostRef}
                localChangeRef={localChangeRef}
                scrollToChangeRef={scrollToChangeRef}
                onMarkdownChange={onMarkdownChange}
                onContentApplied={onContentApplied}
                onStateChange={onStateChange}
                onCursorChange={onCursorChange}
                onSearchArgChange={setSearchArg}
            />
        )
    } else {
        return (
            <MuyaEditor
                options={options}
                cursor={cursorRef.current}
                markdown={markdownRef.current ?? ''}
                contentVersion={contentVersion}
                searchOpen={searchOpen}
                searchArg={searchArg}
                scrollTopRef={muyaScrollTopRef}
                scrollFromHostRef={scrollFromHostRef}
                localChangeRef={localChangeRef}
                scrollToChangeRef={scrollToChangeRef}
                flushRef={flushRef}
                onMarkdownChange={onMarkdownChange}
                onContentApplied={onContentApplied}
                onStateChange={onStateChange}
                onOutlineCurrent={onOutlineCurrent}
                onCursorChange={onCursorChange}
                onSearchArgChange={setSearchArg}
            />
        )
    }
}

export default Editor;
