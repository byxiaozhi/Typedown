import React, { useCallback, useEffect, useRef, useState } from "react";
import transport from "services/transport";
import { speechLabels } from "services/localization";
import { setSpeechEditor } from "services/speechPage";
import Muya from 'components/Muya/lib'
import TablePicker from 'components/Muya/lib/ui/tablePicker'
import CodePicker from 'components/Muya/lib/ui/codePicker'
import EmojiPicker from 'components/Muya/lib/ui/emojiPicker'
import ImageSelector from 'components/Muya/lib/ui/imageSelector'
import ImageToolbar from 'components/Muya/lib/ui/imageToolbar'
import LinkTools from 'components/Muya/lib/ui/linkTools'
import TableBarTools from 'components/Muya/lib/ui/tableTools'
import FootnoteTool from 'components/Muya/lib/ui/footnoteTool'
import FrontMenu from 'components/Muya/lib/ui/frontMenu'
import { createApplicationMenuState } from "services/menuState";
import 'components/Muya/themes/default.css'

interface IMuyaEditor {
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

Muya.use(TablePicker)
Muya.use(CodePicker)
Muya.use(EmojiPicker)
Muya.use(ImageSelector)
Muya.use(ImageToolbar)
Muya.use(FrontMenu)
Muya.use(LinkTools, { jumpClick: (linkInfo: { href: string }) => { transport.postMessage('OpenURI', { uri: linkInfo.href }) } })
Muya.use(TableBarTools)
Muya.use(FootnoteTool)

const STANDAR_Y = 320

// Double-clicking a word on Windows selects it with its trailing space, and formatting that
// selection produced "**word **", which isn't valid bold/italic markdown and showed up as literal
// asterisks. Before a Format command, drop trailing whitespace from a selection that ends inside a
// text node (the double-click case); anything else is left as is.
const trimSelectionTrailingSpace = () => {
    const sel = window.getSelection()
    if (!sel || sel.isCollapsed || sel.rangeCount === 0) return
    const range = sel.getRangeAt(0)
    const { endContainer, endOffset } = range
    if (endContainer.nodeType !== Node.TEXT_NODE) return
    const before = (endContainer.textContent ?? '').substring(0, endOffset)
    const trailing = before.length - before.trimEnd().length
    if (trailing === 0 || trailing >= range.toString().length) return
    range.setEnd(endContainer, endOffset - trailing)
    sel.removeAllRanges()
    sel.addRange(range)
}

const MuyaEditor: React.FC<IMuyaEditor> = (props) => {
    const [editor, setEditor] = useState<Muya>();
    const [marginTop, setMarginTop] = useState(0);
    const markdownRef = useRef('');
    const searchArgRef = useRef<any>();
    const cursorRef = useRef<any>();
    const optionsRef = useRef<any>(props.options);

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

    const scrollToCursor = useCallback(() => {
        relativeScroll(editor?.getSelection().cursorCoords.y - STANDAR_Y)
    }, [editor, relativeScroll])

    const scrollToCursorIfInvisible = useCallback(() => {
        try {
            const y = editor?.getSelection().cursorCoords.y;
            if (y < 0 || y > window.innerHeight) {
                relativeScroll(y - STANDAR_Y)
            }
        } catch (err) {
            console.log(err)
        }
    }, [editor, relativeScroll])

    // Tells the host how many matches there are and which one is current ("3 of 12" in the Find panel).
    const reportSearchResult = useCallback(() => {
        const { matches, index, value } = (editor as any)?.contentState?.searchMatches ?? { matches: [], index: -1, value: '' }
        // `value` is the term the result belongs to: the host ignores a result that is not for the term in its box
        transport.postMessageNoDiff('SearchResult', { count: matches.length, index, value: value ?? '' })
    }, [editor])

    const search = useCallback(arg => {
        if (!editor) return
        const { contentState } = editor as any
        if (!arg?.value) {
            // The box was emptied (or the panel closed): nothing stays marked in the text.
            const hadMatches = contentState.searchMatches.matches.length > 0
            contentState.searchMatches.matches = []
            contentState.searchMatches.value = ''
            if (hadMatches) contentState.render(false)
            reportSearchResult()
            return
        }
        // The host sends the term and the options only. The first match to show is the one at or after the caret.
        const { value, opt } = arg
        const caret = contentState.cursor
        const selection = opt?.selection?.start
            ? opt.selection
            : caret?.start?.key ? { start: caret.start, end: caret.end } : undefined
        editor.search(value, { ...opt, selection })
        reportSearchResult()
    }, [editor, reportSearchResult])

    useEffect(() => {
        searchArgRef.current = props.searchArg
    }, [props.searchArg])

    useEffect(() => {
        markdownRef.current = ''
    }, [editor])

    useEffect(() => {
        cursorRef.current = props.cursor
    }, [props.cursor])

    useEffect(() => {
        if (markdownRef.current != props.markdown && editor) {
            markdownRef.current = props.markdown
            editor.setMarkdown(props.markdown, cursorRef.current)
            const scrollTop = props.scrollTopRef.current;
            window.scrollTo(window.scrollX, scrollTop)
            scrollToCursorIfInvisible()
            setTimeout(() => {
                window.scrollTo(window.scrollX, scrollTop)
                scrollToCursorIfInvisible()
                search(searchArgRef.current)
            }, 100);
        }
    }, [editor, props.markdown, props.scrollTopRef, scrollToCursorIfInvisible, scrollToElementIfInvisible, search])

    useEffect(() => {
        search(props.searchArg)
    }, [editor, props.searchArg, search])

    useEffect(() => {
        const ele = document.getElementById('editor');
        const muya = new Muya(ele, optionsRef.current);
        setEditor(muya);
        return () => muya.destroy()
    }, []);

    useEffect(() => {
        editor && Object.assign(editor.options, props.options)
    }, [editor, props.options])

    // The review marks are drawn from the tokens, so the paragraphs are drawn again (without the cached tokens) when the setting changes.
    useEffect(() => {
        editor?.contentState.render(false, true)
    }, [editor, props.options?.showReviewMarks])

    // Speech mode (View menu) switched on or off: the marks are read or not, so everything is drawn again.
    const speechModeRef = useRef<boolean>(!!props.options?.speechMode)
    useEffect(() => {
        const on = !!props.options?.speechMode
        if (!editor || speechModeRef.current === on) return
        speechModeRef.current = on
        editor.setOptions({}, true)
        // the outline and the word count follow the mode (after this render: the listener that sends them is set again in it)
        setTimeout(() => editor.dispatchChangeContentChange(), 0)
    }, [editor, props.options?.speechMode])

    // The colour or symbol of a mark of the user's own was changed in Settings: the marks are drawn again.
    useEffect(() => {
        if (editor && editor.options.speechMode) editor.setOptions({}, true)
    }, [editor, props.options?.speechStyles])

    // The Speech card and the speech shortcuts (services/speechPage.ts) write into this editor.
    useEffect(() => {
        setSpeechEditor(editor)
        return () => setSpeechEditor(undefined)
    }, [editor])

    // The words on the chips ("pause 2 s") in the language of the interface.
    useEffect(() => {
        if (!editor) return
        let cancelled = false
        speechLabels.then(labels => {
            if (cancelled) return
            editor.options.speechLabels = labels
            if (editor.options.speechMode) editor.setOptions({}, true)
        })
        return () => { cancelled = true }
    }, [editor])

    useEffect(() => {
        editor?.setFocusMode(props.options.focusMode)
    }, [editor, props.options.focusMode])

    useEffect(() => {
        editor?.setFont({ fontSize: props.options?.fontSize, lineHeight: props.options?.lineHeight })
    }, [editor, props.options?.fontSize, props.options?.lineHeight])

    useEffect(() => transport.addListener<{ slug: string }>('ScrollTo', ({ slug }) => {
        scrollToElement(`#${slug}`)
    }), [editor, scrollToElement]);

    // The inspector pane's Review list: goes to the `nth` place (0 for the first) where `text` stands, selects it and shows it.
    useEffect(() => transport.addListener<{ text: string, nth: number }>('ScrollToText', ({ text, nth }) => {
        if (!editor || !text) return
        const { contentState } = editor as any
        let seen = 0
        for (let block = contentState.getFirstBlock(); block; block = contentState.findNextBlockInLocation(block)) {
            if (typeof block.text !== 'string') continue
            for (let at = block.text.indexOf(text); at >= 0; at = block.text.indexOf(text, at + text.length)) {
                if (seen++ !== nth) continue
                contentState.cursor = { start: { key: block.key, offset: at }, end: { key: block.key, offset: at + text.length } }
                contentState.setCursor()
                scrollToElement(`#${block.key}`)
                return
            }
        }
    }), [editor, scrollToElement]);

    useEffect(() => transport.addListener('UpdateParagraph', type => {
        editor?.updateParagraph(type)
    }), [editor]);

    useEffect(() => transport.addListener('InsertParagraph', pos => {
        editor?.insertParagraph(pos, '', true)
    }), [editor]);

    useEffect(() => transport.addListener('DeleteParagraph', () => {
        editor?.deleteParagraph()
    }), [editor]);

    useEffect(() => transport.addListener('Duplicate', () => {
        editor?.duplicate()
    }), [editor]);

    useEffect(() => transport.addListener('Format', type => {
        trimSelectionTrailingSpace()
        editor?.format(type)
    }), [editor]);

    useEffect(() => transport.addListener('DeleteSelection', () => {
        editor?.delete()
    }), [editor]);

    useEffect(() => transport.addListener('SelectAll', () => {
        editor?.selectAll()
    }), [editor]);

    useEffect(() => transport.addListener<any>('Copy', arg => {
        editor?.clipboard.copy(arg)
    }), [editor]);

    useEffect(() => transport.addListener<any>('Cut', arg => {
        editor?.clipboard.cut(arg)
    }), [editor]);

    useEffect(() => transport.addListener<any>('Paste', arg => {
        editor?.clipboard.paste(arg)
    }), [editor]);

    useEffect(() => transport.addListener<string>('InsertTable', arg => {
        editor?.createTable(arg)
    }), [editor]);

    useEffect(() => transport.addListener<string>('InsertImage', arg => {
        editor?.insertImage(arg)
    }), [editor]);

    useEffect(() => transport.addListener<{ value: string, opt: unknown }>('Search', (arg) => {
        props.onSearchArgChange(arg)
        setTimeout(() => scrollToElementIfInvisible('.ag-highlight'), 0)
    }), [editor, props, scrollToElementIfInvisible]);

    useEffect(() => transport.addListener<{ action: string }>('Find', ({ action }) => {
        editor?.find(action)
        reportSearchResult()
        setTimeout(() => scrollToElementIfInvisible('.ag-highlight'), 0)
    }), [editor, reportSearchResult, scrollToElementIfInvisible]);

    useEffect(() => transport.addListener<{ value: string, opt: unknown }>('Replace', ({ value, opt }) => {
        editor?.replace(value, opt)
        reportSearchResult()
        // the next match may be off screen after a replacement
        setTimeout(() => scrollToElementIfInvisible('.ag-highlight'), 0)
    }), [editor, reportSearchResult, scrollToElement, scrollToElementIfInvisible]);

    useEffect(() => {
        setMarginTop(currentMarginTop => {
            const newMarginTop = { 0: 0, 1: 75, 2: 115 }[props.searchOpen] ?? 0;   // the height of the Find panel: Find only, Find and Replace
            relativeScroll(newMarginTop - currentMarginTop)
            return newMarginTop;
        })
    }, [props.searchOpen, relativeScroll])

    useEffect(() => transport.addListener<{ open: number }>('SearchOpenChange', ({ open }) => {
        if (open == 0 && editor) {
            const { contentState } = editor as any;
            contentState.setCursorToHighlight();
            contentState.searchMatches.matches = [];
            contentState.render(true);
            props.onSearchArgChange(undefined)
        }
    }), [editor, props, relativeScroll]);

    useEffect(() => transport.addListener<Record<string, unknown>>('SettingsChanged', (newOptions) => {
        for (const name in newOptions) {
            const value = newOptions[name];
            if (name == 'focusMode') {
                editor?.setFocusMode(value)
            } else if (name == 'typewriter') {
                value && scrollToCursor()
            }
        }
    }), [editor, scrollToCursor]);

    useEffect(() => editor?.on('selectionChange', (selection: any) => {
        const menuState = createApplicationMenuState(selection)
        const selectionText = window.getSelection()?.toString();
        transport.postMessage('SelectionChange', { selection, menuState, selectionText });
        const { y } = selection.cursorCoords
        props.options?.typewriter && relativeScroll(y - window.innerHeight / 2 + 136);
        window.innerHeight - y < 100 && relativeScroll(y - window.innerHeight + 100);
    }), [editor, props.options?.typewriter, relativeScroll])

    useEffect(() => editor?.on('selectionFormats', (formats: any) => {
        const fotmats_simple = formats.map((e: any) => ({ type: e.type, tag: e.tag }));
        transport.postMessage('SelectionFormats', { formats: fotmats_simple });
    }), [editor])

    useEffect(() => editor?.on('contentChange', ({ markdown, wordCount, cursor, toc: { toc, cur } }: any) => {
        markdownRef.current = markdown;

        // 同步内容与光标
        props.onMarkdownChange(markdown)
        props.onCursorChange(cursor)

        // StateChange 必须在 onMarkdownChange、onCursorChange 之后发送，否则会导致编辑器内容/光标不同步
        transport.postMessage('StateChange', { state: { wordCount, toc, cur }, muya: true });
    }), [editor, props])

    useEffect(() => {
        const ele = document.getElementById('editor');
        if (ele) {
            ele.style.boxSizing = `border-box`
            ele.style.minHeight = `100vh`
            if (props.options?.typewriter) {
                ele.style.paddingTop = `calc(50vh - ${136 - marginTop}px)`
                ele.style.paddingBottom = 'calc(50vh - 54px)'
            } else {
                ele.style.paddingTop = `${marginTop}px`
                ele.style.paddingBottom = '0'
            }
        }
    }, [marginTop, props.options?.typewriter])

    useEffect(() => {
        document.body.style.setProperty('--editorAreaWidth', props.options?.editorAreaWidth)
    }, [props.options?.editorAreaWidth])

    useEffect(() => {
        try {
            editor?.focus()
        } catch (err) {
            console.log(err)
        }
    }, [editor])

    useEffect(() => {
        const onscroll = () => {
            props.scrollTopRef.current = window.scrollY
        }
        addEventListener('scroll', onscroll);
        return () => removeEventListener('scroll', onscroll)
    }, [props.scrollTopRef])

    return (
        <div style={{
            fontSize: props.options?.fontSize,
            lineHeight: props.options?.lineHeight
        }}>
            <div id="editor" />
        </div>
    )

}

export default MuyaEditor