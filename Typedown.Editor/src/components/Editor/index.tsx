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

const Editor: React.FC = () => {
    const [markdown, setMarkdown] = useState<string>();
    const markdownRef = useRef<string>();
    const [cursor, setCursor] = useState<any>();
    const [options, setOptions] = useState<any>();
    const optionsRef = useRef<any>();
    const [searchOpen, setSearchOpen] = useState(0);
    const [searchArg, setSearchArg] = useState<{ value: string, opt: any }>();
    const muyaScrollTopRef = useRef(0);
    const codeMirrorScrollRef = useRef(0);

    const OnFileLoaded = useCallback(() => setTimeout(() => transport.postMessage('FileLoaded', { text: markdownRef.current }), 100), [])

    useEffect(() => {
        remote.getSettings().then(({ markdown, ...opt }: any) => {
            setOptions(opt)
            setMarkdown(markdown)
            markdownRef.current = markdown
            OnFileLoaded();
        })
    }, [OnFileLoaded]);

    useEffect(() => {
        optionsRef.current = options
    }, [options])

    useEffect(() => {
        if (markdown != undefined && markdownRef.current != markdown) {
            transport.postMessage('MarkdownChange', { text: markdown });
            markdownRef.current = markdown
        }
    }, [markdown])

    useEffect(() => {
        transport.postMessage('CursorChange', { cursor })
    }, [cursor])

    useEffect(() => transport.addListener<{ type: string, path: string, title: string }>('Export', async ({ type, path, title }) => {
        const generateOption = { printOptimization: false, title, toc: getHtmlToc(getTOC(markdownRef.current ?? '').toc) }
        const text = await new ExportHtml(markdownRef.current, optionsRef.current).generate(generateOption)
        if (type == 'html') {
            remote.writeAllText({ path, text })
        } else if (type == 'pdf') {
            remote.convertHTML({ path, html: text, format: type })
        } else if (type == 'print') {
            remote.printHTML({ html: text })
        }
    }), []);

    useEffect(() => transport.addListener<{ type: string, text: string }>('ImportFile', ({ type, text }) => {
        setMarkdown(htmlToMarkdown(text, [], DEFAULT_TURNDOWN_CONFIG))
    }), [options]);

    useEffect(() => transport.addListener<string>('LoadFile', text => {
        setCursor({ anchor: { line: 0, ch: 0 }, focus: { line: 0, ch: 0 } })
        setMarkdown(text)
        markdownRef.current = text
        OnFileLoaded();
    }), [OnFileLoaded]);

    useEffect(() => transport.addListener<{ text: string, cursor: string }>('SetMarkdown', ({ text, cursor }) => {
        setCursor(cursor)
        setTimeout(() => setMarkdown(text))
        markdownRef.current = text
    }), []);

    useEffect(() => transport.addListener<Record<string, unknown>>('SettingsChanged', (newOptions) => {
        console.log(newOptions)
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

    if (options?.sourceCode) {
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