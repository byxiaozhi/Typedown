import transport from 'services/transport';
import { remote } from 'services/remote';

transport.addListener('ThemeChanged', onThemeChanged)

// The host shows the editor once told the content is loaded: told whatever happens to the theme (a theme that could
// not be read or applied left the editor invisible).
remote.getCurrentTheme()
    .then(arg => onThemeChanged(arg))
    .catch(err => console.log('theme: the current theme could not be applied', err))
    .finally(() => setTimeout(() => remote.contentLoaded(), 0))

function getorCreateStyle(id: string) {
    let style = document.getElementById(id) as HTMLLinkElement;
    if (!style) {
        style = document.createElement("link");
        style.rel = "stylesheet";
        style.id = id;
        // Before the custom theme and the user's CSS (components/Editor), which set the same variables and must
        // come later to win, whichever of them was created first.
        document.head.insertBefore(style, document.getElementById('typedown-theme-css') ?? document.getElementById('typedown-custom-css'))
    }
    return style;
}

let hostBackground = ''

/**
 * The page colour the host asks for (the theme's, or transparent over Mica). A custom theme paints the page itself
 * (var(--editorBgColor), set when its CSS arrives) and is left alone; the editor calls this again when a custom theme
 * is switched off, so the order the two messages arrive in does not matter.
 */
export function applyHostBackground() {
    if (hostBackground && !document.getElementById('typedown-theme-css')?.textContent)
        document.body.style.backgroundColor = hostBackground
}

function onThemeChanged(arg: any) {
    let { theme } = arg ?? {};
    const { accentColor, background } = arg ?? {};
    const editorStyleDocument = getorCreateStyle("link_style_editor");
    const prismjsStyleDocument = getorCreateStyle("link_style_prismjs");
    const codemirrorStyleDocument = getorCreateStyle("link_style_codemirror");

    theme = theme?.toLowerCase()
    editorStyleDocument.href = `theme/editor/${theme}.theme.css`
    prismjsStyleDocument.href = `theme/prismjs/${theme}.theme.css`
    codemirrorStyleDocument.href = `theme/codemirror/${theme}.theme.css`

    const themeColorAlphas = [10, 20, 30, 40, 50, 60, 70, 80, 90]
    const { r, g, b, a } = accentColor
    // The host's colours arrive camel-cased (r, g, b, a) like the accent; upper case is read too, for an older host.
    const bgR = background?.r ?? background?.R, bgG = background?.g ?? background?.G
    const bgB = background?.b ?? background?.B, bgA = (background?.a ?? background?.A) / 255

    hostBackground = `rgba(${bgR}, ${bgG}, ${bgB}, ${bgA})`
    applyHostBackground()
    document.documentElement.style.setProperty('--actualTheme', theme)
    document.documentElement.style.setProperty('--themeColor', `rgba(${r}, ${g}, ${b}, ${a})`)
    themeColorAlphas.forEach(e => document.documentElement.style.setProperty(`--themeColor${e}`, `rgba(${r}, ${g}, ${b}, ${a * (e / 100)})`))

    window.actualTheme = theme
}