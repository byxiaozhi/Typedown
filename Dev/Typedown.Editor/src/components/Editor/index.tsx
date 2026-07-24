import CodeMirror from "components/CodeMirror";
import MuyaEditor from "components/Muya";
import React, { useCallback, useEffect, useRef, useState } from "react";
import { remote } from "services/remote";
import transport from "services/transport";
import './index.scss'
import ExportHtml from "services/exportHtml";
import { htmlToMarkdown } from "services/importHtml";
import { DEFAULT_TURNDOWN_CONFIG } from "components/Muya/lib/config";
import { getHtmlToc, getTOC } from "services/common";

interface IEditorProps {
    markdown: string
    cursor?: any
    basePath: string | null
    options: any
    onMarkdownChange?: (markdown: string) => void
    onCursorChange?: (cursor: any) => void
}

const Editor: React.FC<IEditorProps> = (props) => {
    const { markdown: externalMarkdown, cursor: externalCursor, basePath, options: externalOptions, onMarkdownChange, onCursorChange } = props
    const [markdown, setMarkdown] = useState<string>(externalMarkdown);
    const markdownRef = useRef<string>(externalMarkdown);
    const [cursor, setCursor] = useState<any>(externalCursor);
    const cursorRef = useRef<any>(externalCursor);
    const [options, setOptions] = useState<any>(externalOptions);
    const optionsRef = useRef<any>();
    const skipMarkdownCallbackRef = useRef(false);
    const skipCursorCallbackRef = useRef(true);
    const [searchOpen, setSearchOpen] = useState(0);
    const [searchArg, setSearchArg] = useState<{ value: string, opt: any }>();
    const muyaScrollTopRef = useRef(0);
    const codeMirrorScrollRef = useRef(0);

    const OnFileLoaded = useCallback(() => setTimeout(() => transport.postMessage('FileLoaded', { text: markdownRef.current }), 100), [])

    useEffect(() => {
        OnFileLoaded();
    }, [OnFileLoaded]);

    useEffect(() => {
        optionsRef.current = options
    }, [options])

    useEffect(() => {
        setOptions(externalOptions)
    }, [externalOptions])

    useEffect(() => {
        window.basePath = basePath ?? ''
    }, [basePath])

    useEffect(() => {
        if (externalMarkdown != undefined && externalMarkdown !== markdownRef.current) {
            skipMarkdownCallbackRef.current = true
            markdownRef.current = externalMarkdown
            setMarkdown(externalMarkdown)
        }

        if (externalCursor !== cursorRef.current) {
            skipCursorCallbackRef.current = true
            cursorRef.current = externalCursor
            setCursor(externalCursor)
        }
    }, [externalCursor, externalMarkdown])

    useEffect(() => {
        if (skipMarkdownCallbackRef.current) {
            skipMarkdownCallbackRef.current = false
            return
        }

        if (markdown != undefined && markdownRef.current != markdown) {
            transport.postMessage('MarkdownChange', { text: markdown });
            onMarkdownChange?.(markdown)
            markdownRef.current = markdown
        }
    }, [markdown, onMarkdownChange])

    useEffect(() => {
        if (skipCursorCallbackRef.current) {
            skipCursorCallbackRef.current = false
            return
        }

        cursorRef.current = cursor
        onCursorChange?.(cursor)
        transport.postMessage('CursorChange', { cursor })
    }, [cursor, onCursorChange])

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
        setMarkdown(htmlToMarkdown(text, [], DEFAULT_TURNDOWN_CONFIG))
    }), [options]);

    useEffect(() => transport.addListener<{ text: string, basePath: string }>('LoadFile', ({ text, basePath }) => {
        window.basePath = basePath ?? ''
        setCursor(undefined)
        setMarkdown(text)
        markdownRef.current = text
        OnFileLoaded();
    }), [OnFileLoaded]);

    useEffect(() => transport.addListener<{ text: string, cursor: string, basePath: string }>('SetMarkdown', ({ text, cursor, basePath }) => {
        window.basePath = basePath ?? ''
        setCursor(cursor)
        setTimeout(() => setMarkdown(text))
        markdownRef.current = text
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

    if (!options) {
        return <></>
    }

    if (options.sourceCode) {
        return (
            <CodeMirror
                options={options}
                cursor={cursor}
                markdown={markdown ?? ''}
                searchOpen={searchOpen}
                searchArg={searchArg}
                scrollTopRef={codeMirrorScrollRef}
                onMarkdownChange={setMarkdown}
                onCursorChange={setCursor}
                onSearchArgChange={setSearchArg}
            />
        )
    } else {
        return (
            <MuyaEditor
                options={options}
                cursor={cursor}
                markdown={markdown ?? ''}
                searchOpen={searchOpen}
                searchArg={searchArg}
                scrollTopRef={muyaScrollTopRef}
                onMarkdownChange={setMarkdown}
                onCursorChange={setCursor}
                onSearchArgChange={setSearchArg}
            />
        )
    }
}

export default Editor;
