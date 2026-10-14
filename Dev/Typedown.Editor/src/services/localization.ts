import { remote } from 'services/remote';
import transport from 'services/transport';

const names = [
    'InputFootnoteDefine',
    'InputYAMLFrontMatter',
    'InputMathFormula',
    'InputLanguageIdentifier',
    'ClickToAddAnImage',
    'LoadImageFail',
    'Footnote',
    'FirstEditWarning',
    'PlantUmlOff'
]

// These end up as CSS variables the editor's own placeholders read, so they are fetched rather than bound.
// The host says when the interface language changed; without that they would keep the language the editor
// started in, which is the one thing in the window that a language change used to leave behind.
// The Windows host serializes the answer with camel-cased keys (inputMathFormula), the Uno host keeps them as asked
// (InputMathFormula), and the stylesheet has used both: each string is set under both names, whichever came back.
// (FirstEditWarning read the one the Windows host never sends and showed an empty notice.)
const camel = (name: string) => name.charAt(0).toLowerCase() + name.slice(1)

const load = () => remote.getStringResources({ names }).then(dic => {
    for (const name of names) {
        const text = dic[name] ?? dic[camel(name)]
        if (text == null) continue
        // A CSS string: JSON's quoting escapes quotes and backslashes the way CSS reads them (a Welsh apostrophe
        // used to end the string early).
        document.documentElement.style.setProperty(`--${name}`, JSON.stringify(text))
        document.documentElement.style.setProperty(`--${camel(name)}`, JSON.stringify(text))
    }
}).catch(err => console.log('localization: the editor strings could not be read', err));

load();

transport.addListener('LanguageChanged', () => { load(); });
