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
import FormatPicker from 'components/Muya/lib/ui/formatPicker'
import { createApplicationMenuState } from "services/menuState";
import { classifyNormalization, extractProtectedPayload } from "services/normalization";
import { highlightExternalChange, scrollTargetForChange } from "services/externalChange";
import { setVimState } from "services/vim";
import { createReadingKeys } from "services/vimReading";
import 'components/Muya/themes/default.css'

interface IMuyaEditor {
    markdown: string
    cursor: any
    /** Incremented by the container whenever the host replaces the content; forces the effect below to re-check. */
    contentVersion?: number
    options: any
    searchOpen: number
    searchArg: { value: string, opt: any } | undefined
    scrollTopRef: React.MutableRefObject<number>
    /** True when scrollTopRef holds a remembered offset from the host for this load: restore it instead of chasing the caret. */
    scrollFromHostRef?: React.MutableRefObject<boolean>
    /** True when the new markdown is an edit of the text shown (automation): only the changed blocks are replaced. */
    localChangeRef?: React.MutableRefObject<boolean>
    scrollToChangeRef?: React.MutableRefObject<boolean>
    onMarkdownChange: (markdown: string) => void
    /** The shell puts a function here that reports the current text at once, for saves and exports. */
    flushRef?: React.MutableRefObject<(() => void) | null>
    /** Fired once host content has been pushed into the editor (see the FileLoaded handshake in Editor). */
    onContentApplied?: () => void
    onCursorChange: (cursor: any) => void
    /** Reports the outline, word count and caret line. The shell tags it with the load it belongs to. */
    onStateChange: (state: { wordCount: any, toc: any[], cur: any }) => void
    /** Reading mode only: the heading the page has been scrolled to. Highlight only — never a scroll. */
    /** Where the page was when it named the heading: its position among the headings, or that it was jumped to. */
    onOutlineCurrent: (slug: string, where: { y: number, index?: number, of?: number, top?: number, jump?: boolean }) => void
    onSearchArgChange: (arg: { value: string, opt: any } | undefined) => void
}

Muya.use(TablePicker)
Muya.use(CodePicker)
Muya.use(EmojiPicker)
Muya.use(ImageSelector)
Muya.use(ImageToolbar)
Muya.use(FrontMenu)
// The selection format bar: the plugin was never registered, so the host's handler for it never fired.
Muya.use(FormatPicker)
/** Selects the document's text (reading mode's select-all). */
function selectDocument(container: HTMLElement | undefined) {
    const selection = window.getSelection()
    if (!container || !selection) return
    const range = document.createRange()
    range.selectNodeContents(container)
    selection.removeAllRanges()
    selection.addRange(range)
}

Muya.use(LinkTools, { jumpClick: (linkInfo: { href: string }) => { transport.postMessage('OpenURI', { uri: linkInfo.href }) } })
Muya.use(TableBarTools)
Muya.use(FootnoteTool)

const STANDAR_Y = 320
// Where a heading goes when it is chosen in the outline: at the top, a line's breathing room below the
// edge. The caret's 320 put it a third of the way down, and the reader then saw the section before it
// at the top of the window — and the outline, reading the top, marked that one instead.
const OUTLINE_TOP = 16

/** Anything that means the reader is moving the page themselves; the scroll hold stops at the first of them. */
const GIVE_WAY = ['wheel', 'keydown', 'pointerdown', 'touchstart']

const MuyaEditor: React.FC<IMuyaEditor> = (props) => {
    const { onMarkdownChange, onCursorChange, onStateChange, onOutlineCurrent } = props
    const [editor, setEditor] = useState<Muya>();
    const [marginTop, setMarginTop] = useState(0);
    const markdownRef = useRef('');
    // Import/export normalizes Markdown. Keep the original text while the document
    // still exports to its initial value, including after an undo back to that value.
    const importedRef = useRef<{ source: string, normalized: string }>();
    const [firstEditWarning, setFirstEditWarning] = useState(false)
    const firstEditWarningTimerRef = useRef<number | null>(null)
    const showFirstEditWarning = useCallback((show: boolean) => {
        if (firstEditWarningTimerRef.current !== null) window.clearTimeout(firstEditWarningTimerRef.current)
        firstEditWarningTimerRef.current = null
        setFirstEditWarning(show)
        if (show) firstEditWarningTimerRef.current = window.setTimeout(() => {
            firstEditWarningTimerRef.current = null
            setFirstEditWarning(false)
        }, 10000)
    }, [])
    useEffect(() => () => {
        if (firstEditWarningTimerRef.current !== null) window.clearTimeout(firstEditWarningTimerRef.current)
    }, [])
    const searchArgRef = useRef<any>();
    const cursorRef = useRef<any>();
    const optionsRef = useRef<any>(props.options);
    // The heading the outline was last told about in reading mode, and the moment until which a jump the
    // reader asked for stands: the jump's own scroll must not be re-read as the reader moving on.
    const readingSlugRef = useRef<string | null>(null)
    const jumpHoldRef = useRef(0)
    // Where the caret was at the last selection change, so a report of an unmoved caret is not followed.
    const lastSelectionRef = useRef('')

    const relativeScroll = useCallback((delta: number) => {
        window.scrollBy(0, delta)
    }, [])

    const scrollToElement = useCallback((selector, offset = STANDAR_Y) => {
        if (editor == null) {
            return;
        }
        const anchor = document.querySelector(selector)
        if (anchor) {
            const { y } = anchor.getBoundingClientRect()
            relativeScroll(y - offset)
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
        optionsRef.current = props.options
    }, [props.options])

    useEffect(() => {
        markdownRef.current = ''
        importedRef.current = undefined
    }, [editor])

    useEffect(() => {
        search(props.searchArg)
    }, [editor, props.searchArg, search])

    // One editor, one document. Keeping the document a tab switch moved away from — a second editor held in
    // memory so returning to it was instant — was tried and taken out again. It bought about two seconds on
    // a switch between long documents and cost five faults: a new editor rendering into the document on
    // screen and wiping it, one held in memory answering the keyboard and deleting an image nobody could
    // see, the report after a switch reaching nobody so the outline described the previous document, a loop
    // with the outline that made the page twitch, and one that only ever appeared with the host running and
    // that nothing here could reproduce. Every one was found in use, none by a check.
    //
    // Two things it left behind are worth keeping, because both are right on their own: StateRender renders
    // into its own container rather than looking its root up in the document, and the setters below only
    // push a setting that has actually changed instead of re-rendering the whole document each time.

    // Several of Muya's setters re-render the whole document, which is fine once and wasteful every time
    // after: the editor remembers what has been applied to it, so a setting is only pushed when it changed.
    const applyOnce = (muya: any, key: string, value: unknown, apply: () => void) => {
        if (!muya) return
        const applied = muya.__applied || (muya.__applied = {})
        const encoded = JSON.stringify(value ?? null)
        if (applied[key] === encoded) return
        applied[key] = encoded
        apply()
    }

    useEffect(() => {
        const ele = document.getElementById('editor')
        const o = optionsRef.current
        const muya = new Muya(ele, o);
        // The constructor already built the document with these, so they count as applied.
        (muya as any).__applied = {
            font: JSON.stringify({ fontSize: o?.fontSize, lineHeight: o?.lineHeight }),
            direction: JSON.stringify(o?.textDirection ?? null),
            spellcheck: JSON.stringify(!!o?.spellcheckEnabled),
            renderPlantUml: JSON.stringify([!!o?.renderPlantUml, o?.plantUmlServer ?? '']),
            listIndentation: JSON.stringify(o?.listIndentation ?? null),
            readOnly: JSON.stringify(!!o?.readOnly)
        };
        (window as any).__typedownMuya = muya; // for Tools/EditorBench and DevTools inspection
        // What the next real visual edit would rewrite in the document (services/normalization): the loaded
        // source against Muya's export of it. Once an edit has made the export the text there is nothing left
        // to rewrite. Null before anything was loaded.
        // Also returns the normalized text itself, for its hash in the automation edit reply.
        const pendingNormalization = () => {
            const imported = importedRef.current
            if (!imported) return null
            return markdownRef.current === imported.source
                ? { ...classifyNormalization(imported.source, imported.normalized), normalized: imported.normalized }
                : { ...classifyNormalization(markdownRef.current, markdownRef.current), normalized: markdownRef.current }
        };
        (window as any).__typedownPendingNormalization = pendingNormalization
        // For Tools/EditorBench: the same classifier, to judge what a real edit did.
        ;(window as any).__typedownNormalization = { classifyNormalization, extractProtectedPayload }
        setEditor(muya)
        return () => {
            // Source mode has no Muya: a hook left behind would describe the document as it was when Muya went.
            if ((window as any).__typedownPendingNormalization === pendingNormalization) delete (window as any).__typedownPendingNormalization
            muya.destroy()
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    useEffect(() => {
        editor && Object.assign(editor.options, props.options)
    }, [editor, props.options])

    useEffect(() => {
        // Focus mode dims everything but the active block, and reading mode has no active block: together they
        // would grey out the whole document, so reading mode wins.
        editor?.setFocusMode(props.options.focusMode && !props.options.readOnly)
    }, [editor, props.options.focusMode, props.options.readOnly])

    useEffect(() => {
        // Vim keys move around in reading mode (services/vimReading); the visual editor has none.
        if (!props.options?.vimMode || !props.options?.readOnly) {
            setVimState('off')
            return
        }
        setVimState('reading')
        const listener = createReadingKeys(
            () => ({ top: () => window.scrollY, by: dy => window.scrollTo({ top: window.scrollY + dy, behavior: 'instant' as ScrollBehavior }), to: y => window.scrollTo({ top: Math.min(y, document.documentElement.scrollHeight), behavior: 'instant' as ScrollBehavior }), height: () => window.innerHeight }),
            () => Array.from(document.querySelectorAll<HTMLElement>('#editor h1, #editor h2, #editor h3, #editor h4, #editor h5, #editor h6')))
        window.addEventListener('keydown', listener, true)
        return () => window.removeEventListener('keydown', listener, true)
    }, [props.options?.vimMode, props.options?.readOnly])

    useEffect(() => {
        const font = { fontSize: props.options?.fontSize, lineHeight: props.options?.lineHeight }
        applyOnce(editor, 'font', font, () => editor?.setFont(font))
    }, [editor, props.options?.fontSize, props.options?.lineHeight])

    // The tab size setting reaches both what a tab key inserts (Muya) and how wide a tab character is drawn
    // (CSS tab-size, inherited from the root below); without the latter, pasted code with tabs showed
    // eight columns per tab whatever the setting said.
    useEffect(() => {
        const tab = Number(props.options?.tabSize) || 4
        applyOnce(editor, 'tabSize', tab, () => editor?.setTabSize(tab))
    }, [editor, props.options?.tabSize])

    useEffect(() => {
        applyOnce(editor, 'direction', props.options?.textDirection, () => editor?.setTextDirection(props.options?.textDirection))
    }, [editor, props.options?.textDirection])

    useEffect(() => {
        // Chromium/WebView2 spell checking on the contenteditable root (upstream #57)
        const on = !!props.options?.spellcheckEnabled
        applyOnce(editor, 'spellcheck', on, () => editor?.setOptions({ spellcheckEnabled: on }))
    }, [editor, props.options?.spellcheckEnabled])

    useEffect(() => {
        // PlantUML is drawn by plantuml.com from the block's source: only when the person turned it on. Rendering again
        // draws the blocks shown, or puts the notice back.
        // The same when the server changes (Settings > Editor > PlantUML server).
        const on = !!props.options?.renderPlantUml
        const server = props.options?.plantUmlServer ?? ''
        applyOnce(editor, 'renderPlantUml', [on, server], () => editor?.setOptions({ renderPlantUml: on, plantUmlServer: server }, true))
    }, [editor, props.options?.renderPlantUml, props.options?.plantUmlServer])

    useEffect(() => {
        // Muya: 'dfm' = 4-space nested indentation, otherwise the number of spaces after the list marker (1-4).
        const value = props.options?.listIndentation
        applyOnce(editor, 'listIndentation', value, () => editor?.setListIndentation(value === 'dfm' ? 'dfm' : (parseInt(value, 10) || 1)))
    }, [editor, props.options?.listIndentation])

    useEffect(() => transport.addListener<{ slug: string }>('ScrollTo', ({ slug }) => {
        scrollToElement(`#${slug}`, OUTLINE_TOP)
        // The reader chose this heading, so this is the one the outline marks — also at the end of the
        // document, where the page cannot scroll far enough to bring it to the top and reading the top
        // would name the section before it.
        if (props.options?.readOnly && readingSlugRef.current !== slug) {
            readingSlugRef.current = slug
            jumpHoldRef.current = performance.now() + 300
            onOutlineCurrent(slug, { y: Math.round(window.scrollY), jump: true })
        }
    }), [editor, scrollToElement, props.options?.readOnly, onOutlineCurrent]);

    useEffect(() => transport.addListener('UpdateParagraph', type => {
        if (optionsRef.current?.readOnly) return
        editor?.updateParagraph(type)
    }), [editor]);

    useEffect(() => transport.addListener('InsertParagraph', pos => {
        if (optionsRef.current?.readOnly) return
        editor?.insertParagraph(pos, '', true)
    }), [editor]);

    useEffect(() => transport.addListener('DeleteParagraph', () => {
        if (optionsRef.current?.readOnly) return
        editor?.deleteParagraph()
    }), [editor]);

    useEffect(() => transport.addListener('Duplicate', () => {
        if (optionsRef.current?.readOnly) return
        editor?.duplicate()
    }), [editor]);

    useEffect(() => transport.addListener('Format', type => {
        if (optionsRef.current?.readOnly) return
        editor?.format(type)
    }), [editor]);

    useEffect(() => transport.addListener('DeleteSelection', () => {
        if (optionsRef.current?.readOnly) return
        editor?.delete()
    }), [editor]);

    useEffect(() => transport.addListener('SelectAll', () => {
        // Reading mode: the page is not editable and never has the focus Muya's select-all asks for, so it
        // selected nothing (and a copy after it copied nothing). The whole document is selected as text instead.
        if (optionsRef.current?.readOnly) {
            selectDocument(editor?.container)
            return
        }
        editor?.selectAll()
    }), [editor]);

    useEffect(() => transport.addListener<any>('Copy', arg => {
        editor?.clipboard.copy(arg)
    }), [editor]);

    useEffect(() => transport.addListener<any>('Cut', arg => {
        if (optionsRef.current?.readOnly) return
        editor?.clipboard.cut(arg)
    }), [editor]);

    useEffect(() => transport.addListener<any>('Paste', arg => {
        if (optionsRef.current?.readOnly) return
        editor?.clipboard.paste(arg)
    }), [editor]);

    useEffect(() => transport.addListener<string>('InsertTable', arg => {
        if (optionsRef.current?.readOnly) return
        editor?.createTable(arg)
    }), [editor]);

    useEffect(() => transport.addListener<string>('InsertImage', arg => {
        if (optionsRef.current?.readOnly) return
        editor?.insertImage(arg)
    }), [editor]);

    useEffect(() => transport.addListener<{ src: string, alt?: string, title?: string }[]>('InsertImages', arg => {
        if (optionsRef.current?.readOnly) return
        editor?.insertImages(arg)
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
            // Back from the search with the keyboard in the page: the editor has it, not the body.
            if (document.hasFocus()) restoreEditorFocus(editor)
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

    // What the last contentChange reported, so the outline can be re-sent with a different current heading
    // without the document having changed. The heading elements are looked up once per document.

    // The outline used to follow the page here in reading mode, where there is no caret for it to follow.
    // It was taken out: it reported the heading it had scrolled to through StateChange, the same message
    // the host reads `cur` from to decide where to scroll — so the page scrolled, reported a heading, was
    // scrolled to it and reported again, with nobody touching it. The outline flickered and collapsed as
    // the host rebuilt it, text could not be dragged because the selection died on every render, and it
    // ended in a crash.
    //
    // Following the page is worth having, but not on the channel the host uses to move the page. It needs a
    // message of its own that only ever sets the highlight. Until then the highlight stays on the caret,
    // which in reading mode means it stays where the caret was left.

    // Reading mode has no caret, so the outline has nothing to follow. Anchor it to the page instead: the
    // section being read is the one whose heading has passed the top of the window. The headings are the
    // top-level h1..h6 of the document and their element id is the slug the outline uses, so this needs
    // nothing from the host and reports nothing but a slug.
    useEffect(() => {
        if (!editor || !props.options?.readOnly) return
        let frame = 0
        let retries = 60
        const anchor = () => {
            frame = 0
            if (performance.now() < jumpHoldRef.current) return
            const root = editor.container?.querySelector?.('#ag-editor-id') || document.getElementById('ag-editor-id')
            const heads = root ? Array.from(root.querySelectorAll(':scope > h1, :scope > h2, :scope > h3, :scope > h4, :scope > h5, :scope > h6')) as HTMLElement[] : []
            if (!heads.length) return
            // Every position read in one pass, before looking at any of them. A binary search reading one
            // rectangle at a time was asking the browser where things are while the page was still moving —
            // each comparison saw a different scroll position, so "the tops only increase" stopped being
            // true and the answer landed anywhere: scrolling down reported headings from further up.
            const tops = heads.map(h => h.getBoundingClientRect().top)
            // Only a finished layout is worth reading. While the document is still being laid out the
            // headings that have no box yet all sit at zero, which reads as "already scrolled past", and the
            // outline was seen to jump to the last heading, then the fifth, then the right one — all in the
            // first tenth of a second after opening. Positions in document order are what a real layout has.
            // An unfinished one is looked at again next frame, for a while: giving up on it left the
            // outline pointing at a place the page had merely passed through on its way somewhere else.
            for (let i = 1; i < tops.length; i++) if (tops[i] <= tops[i - 1]) {
                if (retries-- > 0) frame = requestAnimationFrame(anchor)
                return
            }
            retries = 60
            // A heading counts as reached once it is where a jump would have put it, not only once it has
            // gone past the edge: the two have to agree, or a chosen heading is marked as the one before.
            let found = -1
            for (let i = 0; i < tops.length; i++) { if (tops[i] <= OUTLINE_TOP + 1) found = i; else break }
            const chosen = found >= 0 ? found : 0
            const slug = heads[chosen].id
            if (!slug || slug === readingSlugRef.current) return
            readingSlugRef.current = slug
            // The position goes with it: in the log the heading can then be checked against where the page
            // actually was, which is the only way to tell a wrong answer from a document that simply moved.
            props.onOutlineCurrent(slug, { y: Math.round(window.scrollY), index: chosen, of: heads.length, top: Math.round(tops[chosen]) })
        }
        const onScroll = () => { if (!frame) frame = requestAnimationFrame(anchor) }
        window.addEventListener('scroll', onScroll, { passive: true })
        // Headings also move under a page that has not scrolled: diagrams and images finish rendering
        // and everything below them shifts. The document's height changes with it, and that is watched
        // too — the outline was left on the heading that had been at the top before the diagrams came in.
        const root = editor.container?.querySelector?.('#ag-editor-id') || document.getElementById('ag-editor-id')
        const sizes = typeof ResizeObserver === 'function' && root ? new ResizeObserver(onScroll) : null
        if (root) sizes?.observe(root)
        frame = requestAnimationFrame(anchor)
        return () => {
            window.removeEventListener('scroll', onScroll)
            sizes?.disconnect()
            if (frame) cancelAnimationFrame(frame)
            readingSlugRef.current = null
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [editor, props.options?.readOnly, props.contentVersion])

    const lastRenderedThemeRef = useRef<string | undefined>(window.actualTheme);
    useEffect(() => transport.addListener('ThemeChanged', () => {
        if (editor) {
            // Defer so the theme service's listener has updated window.actualTheme first. A full re-render is only
            // needed to recolor mermaid diagrams, and only when the theme really changed (the host also posts
            // ThemeChanged right after startup, which would otherwise re-render the whole document a second time).
            setTimeout(() => {
                const theme = window.actualTheme
                if (theme === lastRenderedThemeRef.current) return
                lastRenderedThemeRef.current = theme
                const { contentState } = editor as any
                const hasMermaid = (blocks: any[]): boolean => blocks.some(b => b.functionType === 'mermaid' || (b.children?.length && hasMermaid(b.children)))
                if (hasMermaid(contentState.getBlocks()))
                    contentState.render(true)
            }, 0)
        }
    }), [editor]);

    useEffect(() => editor?.on('selectionChange', (selection: any) => {
        const menuState = createApplicationMenuState(selection)
        const selectionText = window.getSelection()?.toString();
        transport.postMessage('SelectionChange', { selection, menuState, selectionText });
        // Only a caret the reader moved is worth following. A load reports a selection too, through two
        // timers, which on a long document fire a good while after the page has been put where the
        // document was left — and following that "movement" took the page to wherever the caret had been
        // remembered, the top of the document as often as not. That was the jump to the top after a tab
        // switch, and in reading mode there is no caret to follow at all.
        // And only a caret that actually moved. Muya reports a selection change on every key-up, modifier
        // keys included, so releasing Ctrl after Ctrl+clicking an anchor link reported the unmoved caret —
        // now above the window, since the page had just gone to the anchor — and the rule below scrolled
        // straight back up to it. The link "did nothing". Same for Ctrl+wheel after scrolling away.
        const at = `${selection?.start?.key}:${selection?.start?.offset}:${selection?.end?.key}:${selection?.end?.offset}`
        const moved = at !== lastSelectionRef.current
        lastSelectionRef.current = at
        if (selection?.fromLoad || props.options?.readOnly || !moved) return
        const { y } = selection.cursorCoords
        // Typewriter mode centres the caret line.
        if (props.options?.typewriter) {
            relativeScroll(y - window.innerHeight / 2 + 136);
        } else if (window.innerHeight - y < 100) {
            relativeScroll(y - window.innerHeight + 100);
        } else if (y < 100 && window.scrollY > 0) {
            // Keep the caret visible when it moves above the viewport (Up arrow / Page Up), upstream #51.
            relativeScroll(y - 100);
        }
    }), [editor, props.options?.typewriter, props.options?.readOnly, relativeScroll])

    useEffect(() => editor?.on('selectionFormats', (formats: any) => {
        const fotmats_simple = formats.map((e: any) => ({ type: e.type, tag: e.tag }));
        transport.postMessage('SelectionFormats', { formats: fotmats_simple });
    }), [editor])

    // Past this many blocks the browser is told it may skip laying out what is off screen (see the
    // .ag-long-document rule). Below it the bookkeeping costs more than it saves.
    const longDocumentBlocks = 6000

    // The class has to be on the element before the first render, or the browser lays the whole document out
    // once and then has to take it apart again — which costs more than it saves. Before the document is
    // parsed the only measure available is its length; afterwards the block count corrects it.
    // Raised from forty thousand: while the blocks off screen are unlaid out the page can only guess its own
    // height, and the scrollbar is drawn from that guess — the thumb ends up too small and the document runs
    // away from the pointer. Documents this side of three hundred thousand characters open in about a second
    // and type in under twenty milliseconds anyway, so they keep an exact scrollbar instead.
    const longDocumentChars = 300000

    const markLongDocument = useCallback((markdown?: string) => {
        // On the wrapper, not on the editor element itself: rendering patches that element and would strip a
        // class it does not know about.
        const root = document.getElementById('editor')
        if (!root) return
        const blocks = (editor as any)?.contentState?.blocks?.length ?? 0
        const long = markdown != null ? markdown.length > longDocumentChars : blocks > longDocumentBlocks
        root.classList.toggle('ag-long-document', long)
    }, [editor])

    useEffect(() => editor?.on('contentChange', ({ markdown, wordCount, cursor, toc: { toc, cur } }: any) => {
        const imported = importedRef.current
        const text = imported && markdown === imported.normalized ? imported.source : markdown
        markdownRef.current = text;
        markLongDocument()

        // 同步内容与光标
        onMarkdownChange(text)
        onCursorChange(cursor)

        // StateChange 必须在 onMarkdownChange、onCursorChange 之后发送，否则会导致编辑器内容/光标不同步
        onStateChange({ wordCount, toc, cur })
    }), [editor, markLongDocument, onMarkdownChange, onCursorChange, onStateChange])

    useEffect(() => {
        if (!editor || !props.flushRef) return
        const flush = () => editor.flushContentChange()
        props.flushRef.current = flush
        return () => {
            if (props.flushRef?.current === flush) props.flushRef.current = null
        }
    }, [editor, props.flushRef])

    useEffect(() => {
        const ele = document.getElementById('editor');
        if (ele) {
            ele.style.boxSizing = `border-box`
            ele.style.minHeight = `100vh`
            if (props.options?.typewriter && !props.options?.readOnly) {
                ele.style.paddingTop = `calc(50vh - ${136 - marginTop}px)`
                ele.style.paddingBottom = 'calc(50vh - 54px)'
            } else {
                ele.style.paddingTop = `${marginTop}px`
                ele.style.paddingBottom = '0'
            }
        }
    }, [marginTop, props.options?.typewriter, props.options?.readOnly])

    useEffect(() => {
        document.body.style.setProperty('--editorAreaWidth', props.options?.editorAreaWidth)
    }, [props.options?.editorAreaWidth])

    useEffect(() => {
        document.body.classList.toggle('hide-paragraph-marker', props.options?.showParagraphMarker === false)
    }, [props.options?.showParagraphMarker])

    useEffect(() => {
        const readOnly = !!props.options?.readOnly
        document.body.classList.toggle('read-only', readOnly)
        editor?.container?.setAttribute('contenteditable', String(!readOnly))
        // Re-render so the Markdown markers and the editing affordances of the active block disappear (and come
        // back with the caret when reading mode is switched off) instead of waiting for the next edit.
        applyOnce(editor, 'readOnly', readOnly, () => editor?.contentState?.render(!readOnly, true))
    }, [editor, props.options?.readOnly])

    // A paste the browser does itself (Ctrl+V reaching the page while the host's shortcut did not take it) put the
    // clipboard's HTML straight into the document, as foreign elements Muya does not know and never reports: the
    // document on screen and the text saved parted ways. Every paste goes through the host's (Edit > Paste), which
    // brings images and files along and hands the page the text and HTML; reading mode takes none.
    useEffect(() => {
        if (!editor) return
        const onPaste = (event: ClipboardEvent) => {
            const container = editor.container as HTMLElement
            if (!(event.target instanceof Node) || !container.contains(event.target)) return
            event.preventDefault()
            event.stopPropagation()
            if (optionsRef.current?.readOnly) return
            transport.postMessage('PasteRequested', { type: 'normal' })
        }
        // Copy and cut the same way: the browser's own copy (Ctrl+C reaching the page) put the page's markup on the
        // clipboard, with the editor's internal picture addresses Word cannot load and text without the Markdown.
        const onCopy = (event: ClipboardEvent) => {
            const container = editor.container as HTMLElement
            const state = editor.contentState as any
            // Table cells selected as cells: their own copy (a Markdown table), which nothing called.
            if (state?.selectedTableCells) {
                event.preventDefault()
                event.stopPropagation()
                state.docCopyHandler()
                if (event.type === 'cut' && !optionsRef.current?.readOnly) state.docCutHandler()
                return
            }
            const selection = window.getSelection()
            const text = !!selection && !selection.isCollapsed && selection.rangeCount > 0 && selection.getRangeAt(0).intersectsNode(container)
            if (!text && !state?.selectedImage) return
            event.preventDefault()
            event.stopPropagation()
            if (event.type === 'cut' && !optionsRef.current?.readOnly) editor.clipboard.cut({ type: 'normal', copyInfo: null })
            else editor.clipboard.copy({ type: 'normal', copyInfo: null })
        }
        document.addEventListener('paste', onPaste, true)
        document.addEventListener('copy', onCopy, true)
        document.addEventListener('cut', onCopy, true)
        return () => {
            document.removeEventListener('paste', onPaste, true)
            document.removeEventListener('copy', onCopy, true)
            document.removeEventListener('cut', onCopy, true)
        }
    }, [editor])

    // Reading mode has no caret, so Muya reports no selection change, and the host kept Copy disabled whatever
    // the reader selected. Here the page says whether text in the document is selected; the host enables the
    // copy commands by it (EditorViewModel.OnReadingSelectionChange).
    useEffect(() => {
        if (!editor || !props.options?.readOnly) return
        let last: boolean | null = null
        let frame = 0
        const report = () => {
            frame = 0
            const selection = window.getSelection()
            const container = editor.container as HTMLElement
            const selected = !!selection && !selection.isCollapsed && selection.rangeCount > 0 &&
                selection.getRangeAt(0).intersectsNode(container) && selection.toString().trim() !== ''
            if (selected === last) return
            last = selected
            transport.postMessage('ReadingSelectionChange', { selected })
        }
        const onChange = () => { if (!frame) frame = requestAnimationFrame(report) }
        // The browser's own Ctrl+A would select the whole page, the tools around the document included.
        const onKey = (event: KeyboardEvent) => {
            if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && event.key.toLowerCase() === 'a') {
                event.preventDefault()
                selectDocument(editor.container)
            }
        }
        document.addEventListener('selectionchange', onChange)
        document.addEventListener('keydown', onKey, true)
        report()
        return () => {
            document.removeEventListener('selectionchange', onChange)
            document.removeEventListener('keydown', onKey, true)
            if (frame) cancelAnimationFrame(frame)
        }
    }, [editor, props.options?.readOnly])

    useEffect(() => {
        if (props.options?.fontFamily) {
            document.body.style.setProperty('--editorFontFamily', props.options.fontFamily)
        } else {
            document.body.style.removeProperty('--editorFontFamily')
        }
    }, [props.options?.fontFamily])

    // Content is applied after every font/direction/padding/CSS-variable effect above so that the render's
    // synchronous selection placement forces exactly one layout; style changes after the render would make
    // the following focus() force a second full layout of the document.
    useEffect(() => {
        cursorRef.current = props.cursor
    }, [props.cursor])

    // Puts the page back where the document was left and keeps it there while the layout settles. Shared by
    // both ways a document arrives: rebuilt from its text, or restored from the one kept in memory.
    // The hold of the load before this one. Switching tabs quickly starts a new load while the previous
    // hold is still putting the page back to its own offset: two holds then pull the page to two places
    // every frame until the older one's budget runs out. It is ended here before the next one begins.
    const holdStopRef = useRef<() => void>(() => { })
    const settleScroll = useCallback((scrollTop: number, keepScroll: boolean) => {
        holdStopRef.current()
        holdStopRef.current = () => { }
        window.scrollTo(window.scrollX, scrollTop)
        if (!keepScroll) scrollToCursorIfInvisible()
        // One flag for everything that puts the page back, the frame-by-frame hold and the delayed second
        // go alike: the moment the reader moves the page, none of them may touch it again. The delayed one
        // used to ignore this and yanked the page back a tenth of a second after a drag.
        let yielded = false
        const giveWay = () => { yielded = true; for (const e of GIVE_WAY) window.removeEventListener(e, giveWay) }
        for (const e of GIVE_WAY) window.addEventListener(e, giveWay, { once: true, passive: true })
        // Laying a long document out goes on for a while after the first paint, and the page can be put
        // back to the top by that work with nobody scrolling it — which is what made switching tabs show
        // the top of the document for a moment. Keep putting it back until the layout has settled, or
        // until the reader scrolls themselves.
        if (keepScroll && scrollTop > 0) {
            // Counted in frames, not milliseconds: laying out a long document blocks the main thread for
            // whole seconds, and a deadline in wall-clock time would expire while nothing could run.
            // Only frames in which the document is tall enough to be scrolled that far count towards the
            // budget: while it is still being built the offset cannot be applied at all, the page sits at
            // the top, and a budget spent there ran out just as the document became tall enough — the
            // reader then saw the top of the new document for a third of a second before it moved.
            let frames = 40
            let cap = 600
            let done = false
            const startedAt = { innerHeight: window.innerHeight, scrollHeight: document.documentElement.scrollHeight }
            // A viewport with no height is a page that is not in a window yet: the host navigates the web
            // view to this page while putting it back into a rebuilt window, and the document is laid out
            // at no width, absurdly tall, before the window arrives. An offset applied then lands somewhere
            // near the start once the real layout comes, and the browser keeps that content in view — the
            // page was at the top after leaving the settings, every time. Such frames do not count.
            const tall = () => window.innerHeight > 0 && document.documentElement.scrollHeight >= scrollTop + window.innerHeight
            // The knock arrives as a scroll event, so answer it there as well as on the next frame:
            // during the layout a frame can be a hundred milliseconds long, and that is a hundred
            // milliseconds of looking at the top of the document. Correcting puts scrollY back where it
            // belongs, so the event this fires in turn finds nothing to do.
            const putBack = () => { if (!done && !yielded && Math.abs(window.scrollY - scrollTop) > 2) window.scrollTo(window.scrollX, scrollTop) }
            const stop = (why: string) => {
                done = true; window.removeEventListener('scroll', putBack); for (const e of GIVE_WAY) window.removeEventListener(e, giveWay)
                // What the hold saw, for the host's log: the only way to tell from a machine one cannot watch.
                transport.postMessage('ScrollSettled', { asked: scrollTop, at: Math.round(window.scrollY), why, framesLeft: frames, capLeft: cap, innerHeight: window.innerHeight, scrollHeight: document.documentElement.scrollHeight, startedAt })
            }
            const hold = () => {
                if (done) return
                // Dragging the scrollbar raises no wheel event, so watching the wheel alone left the hold
                // pulling the page back from under the pointer — the reader drags, it yanks.
                if (yielded) return stop('reader moved')
                if (cap-- <= 0) return stop('gave up')
                if (tall() && frames-- <= 0) return stop('held')
                putBack()
                requestAnimationFrame(hold)
            }
            window.addEventListener('scroll', putBack, { passive: true })
            requestAnimationFrame(hold)
            holdStopRef.current = () => { if (!done) stop('replaced') }
        }
        setTimeout(() => {
            if (!yielded) {
                window.scrollTo(window.scrollX, scrollTop)
                if (!keepScroll) scrollToCursorIfInvisible()
            }
            // The hold above decides for itself when it is over; ending it here ended it after six frames,
            // whatever its budget said. The reader's own movement still ends everything at once.
            if (!keepScroll) giveWay()
            if (props.scrollFromHostRef) props.scrollFromHostRef.current = false
            search(searchArgRef.current)
        }, 100);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [props.scrollFromHostRef, scrollToCursorIfInvisible, search])

    // The host takes the web view out of its window and puts it back when it navigates between its own
    // pages (leaving the settings does that), and the browser is at the top when it comes back. The host
    // sends the offset it last heard of; the page waits for a window to exist again before going there.
    useEffect(() => transport.addListener<{ y: number }>('RestoreScroll', ({ y }) => {
        if (!(y > 0)) return
        let tries = 120
        const go = () => { if (window.innerHeight > 0 || tries-- <= 0) settleScroll(y, true); else requestAnimationFrame(go) }
        go()
    }), [settleScroll]);

    useEffect(() => {
        if (!editor) return
        const local = props.localChangeRef?.current
        if (props.localChangeRef) props.localChangeRef.current = false
        const scrollToChange = !!props.scrollToChangeRef?.current
        if (props.scrollToChangeRef) props.scrollToChangeRef.current = false
        if (importedRef.current && markdownRef.current === props.markdown) { props.onContentApplied?.(); return }
        // After an actual edit the host may hand the exported text back. Reuse the
        // existing model when it already matches, avoiding another load and caret scroll.
        if (editor.getMarkdown() === props.markdown) {
            markdownRef.current = props.markdown
            importedRef.current = { source: props.markdown, normalized: props.markdown }
            showFirstEditWarning(false)
            props.onContentApplied?.()
            return
        }
        markdownRef.current = props.markdown
        markLongDocument(props.markdown)
        // An automation edit keeps the blocks it did not change, with the reader's cursor and scroll position; the
        // browser keeps what is on screen in place when blocks above it change. Otherwise, a whole new document.
        const blocksBefore = local ? editor.contentState.blocks : null
        const replacedLocally = !!local && editor.replaceMarkdownLocally(props.markdown)
        if (!replacedLocally) editor.setMarkdown(props.markdown, cursorRef.current)
        if (blocksBefore && props.options?.highlightAutomationChanges !== false)
            highlightExternalChange(editor.changedBlockKeys(blocksBefore).map((key: string) => document.getElementById(key)))
        ;(window as any).__typedownLastApply = replacedLocally ? 'local' : 'whole'
        importedRef.current = { source: props.markdown, normalized: editor.getMarkdown() }
        // Text the first visual edit would change in a way that loses something (today: some raw HTML) gets a notice
        // that does not block editing; source mode keeps it exactly.
        showFirstEditWarning(classifyNormalization(props.markdown, importedRef.current.normalized).pendingNormalization === 'unsafe')
        // reveal: "change": the first changed block into view unless the reader can already see it. Measured in page
        // coordinates against where the page is (a local edit) or is about to be put back (a whole new document).
        let changeY: number | null = null
        if (blocksBefore && scrollToChange) {
            const key = editor.changeAnchorKey(blocksBefore)
            const element = key ? document.getElementById(key) : null
            if (element) {
                const rect = element.getBoundingClientRect()
                changeY = scrollTargetForChange(rect.top + window.scrollY, rect.bottom + window.scrollY,
                    replacedLocally ? window.scrollY : props.scrollTopRef.current)
            }
        }
        if (replacedLocally) {
            if (props.scrollFromHostRef) props.scrollFromHostRef.current = false
            if (changeY !== null) window.scrollTo(window.scrollX, changeY)
        }
        else if (changeY !== null) settleScroll(changeY, true)
        else settleScroll(props.scrollTopRef.current, !!props.scrollFromHostRef?.current)
        props.onContentApplied?.()
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [editor, props.markdown, props.contentVersion, props.scrollTopRef, settleScroll, markLongDocument, showFirstEditWarning])

    useEffect(() => {
        try {
            editor?.focus()
        } catch (err) {
            console.log(err)
        }
    }, [editor])

    // The host hands the page the keyboard (a new tab, the find bar closed, a menu or a dialog gone) while nothing in it
    // has the focus - the search took it away, or the document was replaced under it: the keys went to the page's body
    // and nowhere. Then the editor takes it, with its caret where it was.
    useEffect(() => {
        if (!editor) return
        const onFocus = () => restoreEditorFocus(editor)
        window.addEventListener('focus', onFocus)
        return () => window.removeEventListener('focus', onFocus)
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
            tabSize: Number(props.options?.tabSize) || 4,
            fontSize: props.options?.fontSize,
            lineHeight: props.options?.lineHeight,
            fontFamily: props.options?.fontFamily ? `${props.options.fontFamily}, "Open Sans", "Segoe UI", sans-serif` : undefined
        }}>
            {/* A wrapper that always exists: Muya replaces #editor's node, so React must never insert next to it -
                inserting the notice before #editor threw and took the whole page down. */}
            <div className="td-first-edit-warning-slot">
                {firstEditWarning && !props.options?.readOnly &&
                    <div className="td-first-edit-warning" role="status">
                        <span className="td-first-edit-warning-text" />
                        <button className="td-first-edit-warning-close" aria-label="Close" onClick={() => showFirstEditWarning(false)}>×</button>
                    </div>}
            </div>
            <div id="editor" />
        </div>
    )

}


/**
 * The page has the keyboard and nothing in it has the focus (the body has it): the editor takes it. A selection the
 * reader made in the text is kept - only the focus goes back to the text - and reading mode, where nothing is typed,
 * is left alone: Muya's focus() puts the caret back and so dropped a selection made to copy from.
 */
function restoreEditorFocus(editor: any) {
    try {
        if (document.activeElement && document.activeElement !== document.body) return
        const container = editor?.container as HTMLElement | undefined
        if (!container || container.getAttribute('contenteditable') !== 'true') return
        const selection = document.getSelection()
        if (selection && selection.rangeCount > 0 && !selection.isCollapsed && container.contains(selection.anchorNode)) {
            container.focus({ preventScroll: true })
            return
        }
        editor.focus()
    } catch (err) {
        console.log(err)
    }
}

export default MuyaEditor
