import React, { useCallback, useEffect, useRef, useState } from "react";
import transport from "services/transport";
import { matchString } from 'services/common'
import { UnControlled as CodeMirror } from 'react-codemirror2';
import 'codemirror/lib/codemirror.css';
import { getTOC } from "services/common";
require('codemirror/mode/markdown/markdown');

interface ICodeMirrorEditor {
    markdown: string
    cursor: any
    options: any
    searchOpen: number
    searchArg: { value: string, opt: any } | undefined
    scrollTopRef: React.MutableRefObject<number>
    onMarkdownChange: (markdown: string) => void
    onCursorChange: (cursor: any) => void
    onSearchArgChange: (arg: { value: string, opt: any } | undefined) => void
}

const STANDAR_Y = 320

const CodeMirrorEditor: React.FC<ICodeMirrorEditor> = (props) => {
    const [editor, setEditor] = useState<any>();
    const [marginTop, setMarginTop] = useState(0);
    const matchsRef = useRef<any[]>([]);
    const matchIndexRef = useRef(0);
    const markdownRef = useRef('');
    const searchArgRef = useRef<any>();
    const cursorRef = useRef<any>();

    const relativeScroll = useCallback((delta: number) => {
        window.scrollBy(0, delta)
    }, [])

    const scrollToElement = useCallback((selector) => {
        if (editor == null) {
            return;
        }
        const anchor = document.querySelector(selector)
        if (anchor) {
            const { y } = anchor.getBoundingClientRect()
            relativeScroll(y - STANDAR_Y)
        }
    }, [editor, relativeScroll])

    const scrollToElementIfInvisible = useCallback((selector) => {
        const anchor = document.querySelector(selector)
        if (anchor) {
            const { y } = anchor.getBoundingClientRect()
            if (y < 0 || y > window.innerHeight) {
                scrollToElement(selector)
            }
        }
    }, [scrollToElement])

    const setMatchSelection = useCallback((match) => {
        if (match) {
            const head = editor.posFromIndex(match.index);
            const anchor = editor.posFromIndex(match.index + match.match.length);
            editor.setSelection(anchor, head);
        }
    }, [editor])

    const search = useCallback(({ value, opt }) => {
        const lastMatch = matchsRef.current[matchIndexRef.current];
        matchsRef.current.forEach(({ marker }) => marker.clear())
        if ((value == null || value.length == 0)) {
            if (lastMatch?.index >= 0) {
                const head = editor.posFromIndex(lastMatch.index);
                editor.setSelection(head, head);
            }
            return;
        }
        let startIndex = 0
        if (opt.selection) {
            const { anchor, head } = opt.selection;
            startIndex = Math.min(editor.indexFromPos(anchor), editor.indexFromPos(head))
        }
        const matchs = matchString(editor.getValue(), value, opt)
        for (matchIndexRef.current = 0;
            matchIndexRef.current < matchs.length && matchs[matchIndexRef.current].index < startIndex;
            matchIndexRef.current++
        );
        setMatchSelection(matchs[matchIndexRef.current])
        matchsRef.current = matchs.map(({ index, match, subMatches }, i) => ({
            marker: editor.markText(
                editor.posFromIndex(index),
                editor.posFromIndex(index + match.length),
                { className: i == matchIndexRef.current ? 'ag-highlight' : 'ag-selection' }
            ),
            index,
            match,
            subMatches
        }))
    }, [editor, setMatchSelection])

    const find = useCallback(({ action }) => {
        if (action == 'next') {
            matchIndexRef.current++;
        } else {
            matchIndexRef.current--;
        }
        if (matchIndexRef.current < 0) {
            matchIndexRef.current = matchsRef.current.length - 1;
        }
        if (matchIndexRef.current >= matchsRef.current.length) {
            matchIndexRef.current = 0;
        }
        setMatchSelection(matchsRef.current[matchIndexRef.current])
        matchsRef.current.forEach((item, i) => {
            item.marker.clear()
            item.marker = editor.markText(
                editor.posFromIndex(item.index),
                editor.posFromIndex(item.index + item.match.length),
                { className: i == matchIndexRef.current ? 'ag-highlight' : 'ag-selection' }
            )
        })
    }, [editor, setMatchSelection])

    const replaceOne = useCallback((match, replaceValue, offset = 0) => {
        const text = editor.getValue();
        const start = match.index + offset;
        const end = start + match.match.length;
        const value = text.substring(0, start) + replaceValue + text.substring(end);
        editor.setValue(value);
        return replaceValue.length - match.match.length;
    }, [editor])

    const replace = useCallback(({ value, opt }) => {
        if (opt.isSingle) {
            if (!matchsRef.current[matchIndexRef.current])
                return;
            const offset = replaceOne(matchsRef.current[matchIndexRef.current], value)
            matchsRef.current = matchsRef.current.map((e, i) => {
                if (i < matchIndexRef.current) return e;
                e.index += offset;
                return e;
            }).filter((e, i) => i != matchIndexRef.current)
            matchIndexRef.current--;
            find({ action: 'next' })
        } else {
            if (!matchsRef.current)
                return;
            let offset = 0;
            for (const match of matchsRef.current) {
                offset += replaceOne(match, value, offset)
            }
        }
    }, [find, replaceOne])

    useEffect(() => transport.addListener<{ text: string }>('Paste', ({ text }) => {
        editor?.replaceSelection(text)
    }), [editor]);

    useEffect(() => transport.addListener('DeleteSelection', () => {
        editor?.replaceSelection('')
    }), [editor]);

    useEffect(() => transport.addListener('Copy', () => {
        document.execCommand('copy')
    }), []);

    useEffect(() => transport.addListener('Cut', () => {
        document.execCommand('cut')
    }), []);

    useEffect(() => transport.addListener('SelectAll', () => {
        editor?.execCommand('selectAll')
    }), [editor]);

    useEffect(() => {
        searchArgRef.current = props.searchArg;
    }, [props.searchArg])

    useEffect(() => {
        markdownRef.current = ''
    }, [editor])

    useEffect(() => {
        if (markdownRef.current != props.markdown && editor) {
            markdownRef.current = props.markdown
            const { anchor, head } = cursorRef.current ?? {}
            editor.setValue(markdownRef.current)
            if (anchor && head)
                editor.setSelection(anchor, head, { scroll: true })
            window.scrollTo(window.scrollX, props.scrollTopRef.current)
            if (searchArgRef.current?.value != "" && searchArgRef.current?.opt?.selection) {
                const { value, opt } = searchArgRef.current
                const selection = opt.selection.head ? opt.selection : undefined
                search({ value, opt: { ...opt, selection } })
            }
        }
    }, [editor, props.markdown, props.scrollTopRef, search])

    useEffect(() => {
        if (props.searchArg && editor) {
            const { value, opt } = props.searchArg
            const selection = opt.selection.head ? opt.selection : undefined
            search({ value, opt: { ...opt, selection } })
        }
    }, [editor, props.searchArg, search])

    useEffect(() => {
        const { anchor, focus: head } = props.cursor ?? {}
        cursorRef.current = { anchor, head }
    }, [editor, props.cursor])

    const handleCodeMirrorState = useCallback((value: string) => {
        const wordCount = { character: value.length, word: value.split(' ').length }
        const { toc } = getTOC(value)
        const state = { wordCount, toc, cur: toc[0] }
        transport.postMessage('StateChange', { state, codeMirror: true })
    }, [])

    const handleCodeMirrorContent = useCallback((cm: any, data: any, value: string) => {
        handleCodeMirrorState(value)
        markdownRef.current = value;
        props.onMarkdownChange(value)
    }, [handleCodeMirrorState, props])

    const handleCodeMirrorSelection = useCallback((cm: any, data: any) => {
        const { anchor, head } = data.ranges[0]
        cursorRef.current = { anchor, head }
        props.onCursorChange({ anchor, focus: head })
        const selectionText = cm.getSelection();
        transport.postMessage('CodeMirrorSelectionChange', { cursor: cursorRef.current, selectionText })
    }, [props])

    useEffect(() => {
        setMarginTop(currentMarginTop => {
            const newMarginTop = { 0: 0, 1: 50, 2: 90 }[props.searchOpen] ?? 0;
            relativeScroll(newMarginTop - currentMarginTop)
            return newMarginTop;
        })
    }, [props.searchOpen, relativeScroll])

    useEffect(() => transport.addListener<{ open: number }>('SearchOpenChange', ({ open }) => {
        if (open == 0) {
            matchsRef.current.forEach(({ marker }) => marker.clear())
            matchsRef.current = []
            props.onSearchArgChange(undefined)
        }
    }), [editor, props, relativeScroll]);

    useEffect(() => transport.addListener<{ value: string, opt: any }>('Search', (arg) => {
        props.onSearchArgChange(arg)
        setTimeout(() => scrollToElementIfInvisible('.ag-highlight'), 0)
    }), [editor, props, scrollToElementIfInvisible, search]);

    useEffect(() => transport.addListener<{ action: string }>('Find', (arg) => {
        find(arg)
        setTimeout(() => scrollToElementIfInvisible('.ag-highlight'), 0)
    }), [editor, find, scrollToElementIfInvisible]);

    useEffect(() => transport.addListener<{ value: string, opt: any }>('Replace', (arg) => {
        replace(arg)
    }), [replace]);

    useEffect(() => {
        editor?.focus()
    }, [editor])

    useEffect(() => {
        const onscroll = () => {
            props.scrollTopRef.current = window.scrollY
        }
        addEventListener('scroll', onscroll);
        return () => removeEventListener('scroll', onscroll)
    }, [props.scrollTopRef])

    return (
        <div
            className="cm-s-one-dark"
            style={{
                boxSizing: 'border-box',
                paddingTop: marginTop,
                paddingLeft: 14,
                paddingRight: 14,
                fontSize: props.options?.fontSize,
                lineHeight: props.options?.lineHeight
            }}>
            <CodeMirror
                ref={(ref: any) => ref && setEditor(ref.editor)}
                options={{
                    theme: 'one-dark',
                    mode: 'markdown',
                    lineNumbers: true,
                    lineWrapping: true
                }}
                onChange={handleCodeMirrorContent}
                onSelection={handleCodeMirrorSelection}
            />
        </div>
    )
}

export default CodeMirrorEditor