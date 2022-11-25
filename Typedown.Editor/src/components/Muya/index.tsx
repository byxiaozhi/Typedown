import React, { useCallback, useEffect, useRef, useState } from "react";
import transport from "services/transport";
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

    const search = useCallback(arg => {
        if (arg?.value != "" && arg?.opt?.selection && editor) {
            const { value, opt } = arg
            const selection = opt.selection.start ? opt.selection : undefined
            editor.search(value, { ...opt, selection })
        }
    }, [editor])

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

    useEffect(() => {
        editor?.setFocusMode(props.options.focusMode)
    }, [editor, props.options.focusMode])

    useEffect(() => {
        editor?.setFont({ fontSize: props.options?.fontSize, lineHeight: props.options?.lineHeight })
    }, [editor, props.options?.fontSize, props.options?.lineHeight])

    useEffect(() => transport.addListener<{ slug: string }>('ScrollTo', ({ slug }) => {
        scrollToElement(`#${slug}`)
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
        setTimeout(() => scrollToElementIfInvisible('.ag-highlight'), 0)
    }), [editor, scrollToElementIfInvisible]);

    useEffect(() => transport.addListener<{ value: string, opt: unknown }>('Replace', ({ value, opt }) => {
        editor?.replace(value, opt)
    }), [editor, scrollToElement]);

    useEffect(() => {
        setMarginTop(currentMarginTop => {
            const newMarginTop = { 0: 0, 1: 50, 2: 90 }[props.searchOpen] ?? 0;
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
        props.onMarkdownChange(markdown)
        props.onCursorChange(cursor)
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