import transport from 'services/transport';
import { remote } from 'services/remote';

transport.addListener('ThemeChanged', onThemeChanged)

remote.getCurrentTheme().then(arg => {
    onThemeChanged(arg);
    setTimeout(() => remote.contentLoaded(), 0);
})

function onThemeChanged({ theme, color, background }: any) {
    const editorStyleDocument = document.createElement("link");
    const prismjsStyleDocument = document.createElement("link");
    const codemirrorStyleDocument = document.createElement("link");
    editorStyleDocument.rel = "stylesheet";
    editorStyleDocument.type = "text/css"
    prismjsStyleDocument.rel = "stylesheet";
    prismjsStyleDocument.type = "text/css"
    codemirrorStyleDocument.rel = "stylesheet";
    codemirrorStyleDocument.type = "text/css"
    document.head.appendChild(editorStyleDocument)
    document.head.appendChild(prismjsStyleDocument)
    document.head.appendChild(codemirrorStyleDocument)
    switch (theme) {
        case "Light":
            editorStyleDocument.href = "theme/editor/light.theme.css"
            prismjsStyleDocument.href = "theme/prismjs/light.theme.css"
            codemirrorStyleDocument.href = "theme/codemirror/light.theme.css"
            break;
        case "Dark":
            editorStyleDocument.href = "theme/editor/dark.theme.css"
            prismjsStyleDocument.href = "theme/prismjs/dark.theme.css"
            codemirrorStyleDocument.href = "theme/codemirror/dark.theme.css"
            break;
    }

    const themeColorAlphas = [10, 20, 30, 40, 50, 60, 70, 80, 90]
    const { r, g, b, a } = color
    const { R: bgR, G: bgG, B: bgB, A: bgA } = background

    document.documentElement.style.setProperty('--themeColor', `rgba(${r}, ${g}, ${b}, ${a})`)
    themeColorAlphas.forEach(e => document.body.style.setProperty(`--themeColor${e}`, `rgba(${r}, ${g}, ${b}, ${a * (e / 100)})`))
    document.documentElement.style.background = `rgba(${bgR}, ${bgG}, ${bgB}, ${bgA})`
}

export { onThemeChanged }