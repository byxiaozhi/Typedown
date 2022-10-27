import { remote } from 'services/remote';

const names = [
    'InputFootnoteDefine',
    'InputYAMLFrontMatter',
    'InputMathFormula',
    'InputLanguageIdentifier',
    'ClickToAddAnImage',
    'LoadImageFail'
]

remote.getStringResources({ names }).then(dic => {
    for (const key in dic) {
        document.documentElement.style.setProperty(`--${key}`, `'${dic[key]}'`)
    }
});