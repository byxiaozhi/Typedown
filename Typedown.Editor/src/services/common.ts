import { EXPORT_DOMPURIFY_CONFIG } from 'components/Muya/lib/config';
import Slugger from 'components/Muya/lib/parser/marked/slugger';
import { sanitize } from 'components/Muya/lib/utils';
import execAll from 'execall'

export const getTOC = (markdown: string) => {
    const lines = markdown.split('\n');
    const toc = []
    for (const line of lines) {
        const test1 = /^ {0,3}(#{1,6})\s.+/.test(line);
        const test2 = /^ {0,3}(#{1,6})$/.test(line);
        if (test1 || test2) {
            const content = test2 ? line.trim() : line.replace(/^\s*#{1,6}\s{1,}/, '').trim();
            let lvl = 0;
            for (let i = line.indexOf('#'); line[i] == '#'; i++) {
                lvl++;
            }
            const slug = Math.random()
            toc.push({
                content,
                lvl,
                slug
            })
        }
    }
    return { toc }
}

export const matchString = (text: string, value: string, options: any) => {
    const { searchIsCaseSensitive, searchIsWholeWord, searchIsRegexp } = options
    /* eslint-disable no-useless-escape */
    const SPECIAL_CHAR_REG = /[\[\]\\^$.\|\?\*\+\(\)\/]{1}/g
    /* eslint-enable no-useless-escape */
    let SEARCH_REG = null
    let regStr = value
    let flag = 'g'

    if (!searchIsCaseSensitive) {
        flag += 'i'
    }

    if (!searchIsRegexp) {
        regStr = value.replace(SPECIAL_CHAR_REG, (p) => {
            return p === '\\' ? '\\\\' : `\\${p}`
        })
    }

    if (searchIsWholeWord) {
        regStr = `\\b${regStr}\\b`
    }

    try {
        // Add try catch expression because not all string can generate a valid RegExp. for example `\`.
        SEARCH_REG = new RegExp(regStr, flag)
        return execAll(SEARCH_REG, text)
    } catch (err) {
        return []
    }
}

export const generateHtmlToc = (tocList: any, slugger: any, currentLevel: any, options: any): string => {
    if (!tocList || tocList.length === 0) {
        return ''
    }

    const topLevel = tocList[0].lvl
    if (!options.tocIncludeTopHeading && topLevel <= 1) {
        tocList.shift()
        return generateHtmlToc(tocList, slugger, currentLevel, options)
    } else if (topLevel <= currentLevel) {
        return ''
    }

    const { content, lvl } = tocList.shift()
    const slug = slugger.slug(content)

    let html = `<li><span><a class="toc-h${lvl}" href="#${slug}">${content}</a><span class="dots"></span></span>`

    // Generate sub-items
    if (tocList.length !== 0 && tocList[0].lvl > lvl) {
        html += '<ul>' + generateHtmlToc(tocList, slugger, lvl, options) + '</ul>'
    }

    html += '</li>' + generateHtmlToc(tocList, slugger, currentLevel, options)
    return html
}

export const getHtmlToc = (toc: any, options: any = {}) => {
    const list = JSON.parse(JSON.stringify(toc))
    const slugger = new Slugger()
    const tocList = generateHtmlToc(list, slugger, 0, options)
    if (!tocList) {
        return ''
    }

    const title = options.tocTitle ? options.tocTitle : 'Table of Contents'
    const html = `<div class="toc-container"><p class="toc-title">${title}</p><ul class="toc-list">${tocList}</ul></div>`
    return sanitize(html, EXPORT_DOMPURIFY_CONFIG)
}