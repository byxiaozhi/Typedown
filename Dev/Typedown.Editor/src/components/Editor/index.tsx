import CodeMirror from "components/CodeMirror";
import MuyaEditor from "components/Muya";
import Preview from "components/Preview";
import SplitScrollbar from "./SplitScrollbar";
import DocumentMap from "components/DocumentMap";
import React, { useCallback, useEffect, useRef, useState } from "react";
import { remote } from "services/remote";
import transport from "services/transport";
import './index.scss'
import ExportHtml from "services/exportHtml";
import { renderDiagramsToPng } from "services/exportDiagrams";
import { htmlToMarkdown } from "services/importHtml";
import { DEFAULT_TURNDOWN_CONFIG } from "components/Muya/lib/config";
import { getHtmlToc, getTOC } from "services/common";
import { setTrackVisual } from "services/trackVisual";
import { setSpeechMode, setSpeechBaseline, setSpeechLibrary, setSpeechTimingOptions } from "services/speechPage";
import { setSpeechRing } from "services/speechRing";
import { collectDocumentDefinitions } from "components/Muya/lib/parser/speech";
import { computeTiming, formatClock, shapeBars } from "components/Muya/lib/parser/speechTiming";
import { computeHints } from "components/Muya/lib/parser/speechHints";

const Editor: React.FC = () => {
    const [markdown, setMarkdown] = useState<string>();
    const markdownRef = useRef<string>();
    // Counts the SetMarkdown messages (a tab switch, an undo): the editor is told of one even when the text is the same as before.
    const [markdownSetVersion, setMarkdownSetVersion] = useState(0);
    // The text as of the latest edit, set as the edit happens: `markdown` only reaches the host
    // (MarkdownChange) after React's next render. Flush reads it, see below.
    const latestRef = useRef<string>();
    const onMarkdownChange = useCallback((text: string) => {
        latestRef.current = text
        setMarkdown(text)
    }, [])
    const [cursor, setCursor] = useState<any>();
    const [options, setOptions] = useState<any>();
    const optionsRef = useRef<any>();
    const [searchOpen, setSearchOpen] = useState(0);
    const [searchArg, setSearchArg] = useState<{ value: string, opt: any }>();
    // Live review: the review text the host made from the baseline and the document (null when tracking is off), drawn by the split preview.
    const [tracked, setTracked] = useState<string | null>(null);
    const [trackStamp, setTrackStamp] = useState<string | null>(null);
    const [trackShown, setTrackShown] = useState<string | null>(null);
    const [trackDays, setTrackDays] = useState<string[] | null>(null);
    const [trackBy, setTrackBy] = useState<string | null>(null);
    const [trackChanges, setTrackChanges] = useState<any[] | null>(null);
    const [trackLabels, setTrackLabels] = useState<{ author: string, accept: string, reject: string } | null>(null);
    const muyaScrollTopRef = useRef(0);
    const codeMirrorScrollRef = useRef(0);

    const OnFileLoaded = useCallback(() => setTimeout(() => transport.postMessage('FileLoaded', { text: markdownRef.current }), 100), [])

    useEffect(() => {
        remote.getSettings().then(({ markdown, basePath, ...opt }: any) => {
            window.basePath = basePath
            setOptions(opt)
            setMarkdown(markdown)
            markdownRef.current = markdown
            latestRef.current = markdown
            OnFileLoaded();
        })
    }, [OnFileLoaded]);

    useEffect(() => {
        optionsRef.current = options
    }, [options])

    // Speech mode: the shortcuts work only in it, and the Speech card lists the words the document defines.
    const speechMode = !!options?.speechMode
    const definitionsRef = useRef<string>()
    useEffect(() => setSpeechMode(speechMode), [speechMode])
    // what the Speech ring shows (the same list as the Speech card, sent by the host with the settings); the marks of the
    // library in it are what the explanation for an AI lists
    useEffect(() => {
        setSpeechRing(options?.speechRing)
        const library: any[] = []
        const seen = new Set<string>()
        for (const group of options?.speechRing?.groups ?? []) {
            for (const item of group.items ?? []) {
                const spec = item.spec
                if (!item.mine || !spec || spec.template || !spec.definitions?.length || seen.has(spec.name)) continue
                seen.add(spec.name)
                library.push({ name: spec.name, kind: item.kind, meaning: item.meaning, definition: spec.definitions[0] })
            }
        }
        setSpeechLibrary(library)
    }, [options?.speechRing])
    useEffect(() => {
        if (!speechMode) { definitionsRef.current = undefined; return }
        if (markdown === undefined) return
        const { defs } = collectDocumentDefinitions(markdown)
        const list = JSON.stringify(Array.from(defs.values()).map(d => ({ name: d.name, kind: d.kind, meaning: d.meaning })))
        if (list === definitionsRef.current) return
        definitionsRef.current = list
        transport.postMessageNoDiff('SpeechDefinitions', { defs: JSON.parse(list) })
    }, [speechMode, markdown])

    // How long the talk takes (docs/speech-marks-design.md, section 4): worked out here, where the marks are read, a moment
    // after the text stops changing, and sent to the Speech card when it is not what was sent last.
    const timingRef = useRef<string>()
    const speechWpm = options?.speechWpm
    const headingsSpoken = !!options?.speechHeadingsSpoken
    useEffect(() => setSpeechTimingOptions({ wpm: speechWpm, headingsSpoken }), [speechWpm, headingsSpoken])
    useEffect(() => {
        if (!speechMode || markdown === undefined) { timingRef.current = undefined; return }
        const handle = setTimeout(() => {
            const t = computeTiming(markdown, { wpm: speechWpm, headingsSpoken })
            setSpeechBaseline(speechWpm)
            const hints: any[] = computeHints(markdown, { timing: t }).slice(0, 60)
            const bars: any[] = shapeBars(t.paragraphs, 120)
            const clock = (s: number) => formatClock(Math.abs(s))
            const payload = JSON.stringify({
                words: t.words,
                clock: formatClock(t.seconds),
                wpm: t.wpm,
                wpmInDocument: t.wpmInDocument,
                budget: t.budget,
                budgetClock: t.budget > 0 ? formatClock(t.budget) : '',
                light: t.light,
                over: t.over,
                overClock: clock(t.over),
                // the shape of the talk, one bar per paragraph (or per few of them in a long talk), and what to say about it
                shape: bars.map(b => ({
                    seconds: Math.round(b.seconds * 10) / 10, words: b.words, speed: Math.round(b.speed * 100) / 100, volume: Math.round(b.volume * 100) / 100,
                    pause: Math.round(b.pause * 10) / 10, audience: Math.round(b.audience * 10) / 10, prefix: b.prefix, nth: b.nth, count: b.count, clock: formatClock(b.seconds)
                })),
                hints: hints.map(h => ({
                    kind: h.kind, prefix: h.prefix, nth: h.nth, whole: !!h.whole, title: h.title || '', word: h.word || '', closer: !!h.closer,
                    raw: h.raw || '', error: h.error || '', clock: h.seconds === undefined ? '' : formatClock(h.seconds)
                })),
                sections: t.sections.slice(0, 200).map(s => ({
                    title: s.title, level: s.level, prefix: s.prefix, nth: s.nth, words: s.words, clock: formatClock(s.seconds),
                    budget: s.budget, budgetClock: s.budget > 0 ? formatClock(s.budget) : '', light: s.light, over: s.over, overClock: clock(s.over)
                }))
            })
            if (payload === timingRef.current) return
            timingRef.current = payload
            transport.postMessageNoDiff('SpeechTiming', JSON.parse(payload))
        }, 300)
        return () => clearTimeout(handle)
    }, [speechMode, markdown, speechWpm, headingsSpoken])

    useEffect(() => {
        if (markdown != undefined && markdownRef.current != markdown) {
            transport.postMessage('MarkdownChange', { text: markdown });
            markdownRef.current = markdown
        }
    }, [markdown])

    useEffect(() => {
        transport.postMessage('CursorChange', { cursor })
    }, [cursor])

    useEffect(() => transport.addListener<IExportArgs>('Export', async ({ type, context, basePath, title, options }) => {
        const generateOption = { printOptimization: false, title, toc: getHtmlToc(getTOC(markdownRef.current ?? '').toc), ...options }
        const baseUrl = basePath ? `file:///${basePath.replaceAll('\\', '/')}/` : undefined
        const html = await new ExportHtml(markdownRef.current, { ...optionsRef.current, baseUrl }).generate(generateOption)
        if (type == 'print') {
            remote.printHTML({ html, context })
        } else {
            remote.exportCallback({ html, context })
        }
    }), []);

    useEffect(() => transport.addListener<{ type: string, text: string }>('ImportFile', ({ text }) => {
        onMarkdownChange(htmlToMarkdown(text, [], DEFAULT_TURNDOWN_CONFIG))
    }), [options, onMarkdownChange]);

    useEffect(() => transport.addListener<{ text: string, basePath: string }>('LoadFile', ({ text, basePath }) => {
        window.basePath = basePath
        setCursor(undefined)
        setMarkdown(text)
        markdownRef.current = text
        latestRef.current = text
        OnFileLoaded();
    }), [OnFileLoaded]);

    useEffect(() => transport.addListener<{ text: string, cursor: string, basePath: string }>('SetMarkdown', ({ text, cursor, basePath }) => {
        window.basePath = basePath
        setCursor(cursor)
        setTimeout(() => {
            setMarkdown(text)
            setMarkdownSetVersion(version => version + 1)
        })
        markdownRef.current = text
        latestRef.current = text
    }), []);

    // Before the host switches tabs: answers with the text as of the latest edit. Messages arrive in
    // order, so every MarkdownChange sent before this answer has reached the host by then too.
    useEffect(() => transport.addListener<{ text: string | null, stamp?: string | null, shown?: string | null, days?: string[] | null, by?: string | null, changes?: any[] | null, author?: string, accept?: string, reject?: string }>('TrackedView', ({ text, stamp, shown, days, by, changes, author, accept, reject }) => {
        setTracked(typeof text === 'string' ? text : null)
        setTrackStamp(typeof stamp === 'string' ? stamp : null)
        setTrackShown(typeof shown === 'string' ? shown : null)
        setTrackDays(Array.isArray(days) ? days : null)
        setTrackBy(typeof by === 'string' ? by : null)
        setTrackChanges(Array.isArray(changes) ? changes : null)
        setTrackLabels({ author: author ?? '', accept: accept ?? 'Accept', reject: reject ?? 'Reject' })
    }), []);

    // File > Export > Word: the diagrams of the document as pictures (services/exportDiagrams.js)
    useEffect(() => transport.addListener<{ id: string, items: { type: string, code: string }[] }>('RenderDiagrams', async ({ id, items }) => {
        let images: ({ png: string, scale: number } | null)[]
        try {
            images = await renderDiagramsToPng(items)
        } catch (err) {
            images = items.map(() => null)
        }
        transport.postMessageNoDiff('DiagramsRendered', { id, images })
    }), []);

    useEffect(() => transport.addListener<{ id: string }>('Flush', ({ id }) => {
        transport.postMessageNoDiff('Flushed', { id, text: latestRef.current })
    }), []);

    useEffect(() => transport.addListener<Record<string, unknown>>('SettingsChanged', (newOptions) => {
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

    // The changes are drawn in place in the Visual view only (the Split view has the preview, the source pane has none).
    const visualTrack = options && !options.sourceCode ? trackChanges : null
    useEffect(() => { setTrackVisual(visualTrack, trackLabels ?? undefined) }, [visualTrack, trackLabels])

    if (!options) {
        return <></>
    }

    // The document map (View > Document map): beside the Visual view and the Code view; the Split view has its own scroll bar.
    const map = options.documentMap !== false && !(options.sourceCode && options.splitPreview)
        ? <DocumentMap source={!!options.sourceCode} markdown={markdown ?? ''} review={options.showReviewMarks !== false} speech={speechMode} />
        : null

    if (options.sourceCode) {
        const code = (
            <CodeMirror
                options={options}
                cursor={cursor}
                markdown={markdown ?? ''}
                searchOpen={searchOpen}
                searchArg={searchArg}
                scrollTopRef={codeMirrorScrollRef}
                onMarkdownChange={onMarkdownChange}
                onCursorChange={setCursor}
                onSearchArgChange={setSearchArg}
            />
        )
        if (!options.splitPreview) return <>{code}{map}</>
        return (
            <>
                <div className="split-code">{code}</div>
                <SplitScrollbar />
                <Preview markdown={markdown ?? ''} options={options} changes={tracked} stamp={trackStamp} shown={trackShown} days={trackDays} by={trackBy} />
            </>
        )
    } else {
        return (
            <>
                <MuyaEditor
                    options={options}
                    cursor={cursor}
                    markdown={markdown ?? ''}
                    markdownSetVersion={markdownSetVersion}
                    searchOpen={searchOpen}
                    searchArg={searchArg}
                    scrollTopRef={muyaScrollTopRef}
                    onMarkdownChange={onMarkdownChange}
                    onCursorChange={setCursor}
                    onSearchArgChange={setSearchArg}
                />
                {map}
            </>
        )
    }
}

export default Editor;