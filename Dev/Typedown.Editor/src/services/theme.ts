import transport from 'services/transport';
import { remote } from 'services/remote';

transport.addListener('ThemeChanged', onThemeChanged)

remote.getCurrentTheme().then(arg => {
    onThemeChanged(arg);
    setTimeout(() => remote.contentLoaded(), 0);
})

function onThemeChanged({ theme, accentColor, background }: any) {
    
    let editorStyleDocument = document.getElementById("link_style_editor") as HTMLLinkElement;
    let prismjsStyleDocument = document.getElementById("link_style_prismjs") as HTMLLinkElement;
    let codemirrorStyleDocument = document.getElementById("link_style_codemirror") as HTMLLinkElement;

    if (!editorStyleDocument) {
        editorStyleDocument = document.createElement("link");
        editorStyleDocument.rel = "stylesheet";
        editorStyleDocument.id = "link_style_editor";
        document.head.appendChild(editorStyleDocument)
    }

    if (!prismjsStyleDocument) {
        prismjsStyleDocument = document.createElement("link");
        prismjsStyleDocument.rel = "stylesheet";
        prismjsStyleDocument.id = "link_style_prismjs";
        document.head.appendChild(prismjsStyleDocument)
    }

    if (!codemirrorStyleDocument) {
        codemirrorStyleDocument = document.createElement("link");
        codemirrorStyleDocument.rel = "stylesheet";
        codemirrorStyleDocument.id = "link_style_codemirror";
        document.head.appendChild(codemirrorStyleDocument)
    }

    theme = theme?.toLowerCase()

    switch (theme) {
        case "light":
            editorStyleDocument.href = "theme/editor/light.theme.css"
            prismjsStyleDocument.href = "theme/prismjs/light.theme.css"
            codemirrorStyleDocument.href = "theme/codemirror/light.theme.css"
            break;
        case "dark":
            editorStyleDocument.href = "theme/editor/dark.theme.css"
            prismjsStyleDocument.href = "theme/prismjs/dark.theme.css"
            codemirrorStyleDocument.href = "theme/codemirror/dark.theme.css"
            break;
    }

    const themeColorAlphas = [10, 20, 30, 40, 50, 60, 70, 80, 90]
    const { r, g, b, a } = accentColor
    const { R: bgR, G: bgG, B: bgB, A: bgA } = background

    document.documentElement.style.setProperty('--themeColor', `rgba(${r}, ${g}, ${b}, ${a})`)
    themeColorAlphas.forEach(e => document.body.style.setProperty(`--themeColor${e}`, `rgba(${r}, ${g}, ${b}, ${a * (e / 100)})`))
    document.documentElement.style.background = `rgba(${bgR}, ${bgG}, ${bgB}, ${bgA})`
    document.documentElement.style.setProperty('color-scheme', theme)

    window.actualTheme = theme
}