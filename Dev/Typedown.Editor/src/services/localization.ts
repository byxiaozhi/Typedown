import { remote } from 'services/remote';

const names = [
    'InputFootnoteDefine',
    'InputYAMLFrontMatter',
    'InputMathFormula',
    'InputLanguageIdentifier',
    'ClickToAddAnImage',
    'LoadImageFail',
    'Footnote'
]

remote.getStringResources({ names }).then(dic => {
    for (const key in dic) {
        document.documentElement.style.setProperty(`--${key}`, `'${dic[key]}'`)
    }
});

// The words on the chips of the speech marks (Muya/lib/parser/speech.js), by the label each one stands for
const speechNames: Record<string, string> = {
    beat: 'SpeechLabelBeat',
    pause: 'SpeechLabelPause',
    wait: 'SpeechLabelWait',
    cue: 'SpeechLabelCue',
    note: 'SpeechLabelCue',
    wpmUnit: 'SpeechLabelWpmUnit',
    budget: 'SpeechLabelBudget',
    pace: 'SpeechLabelPace',
    span: 'SpeechLabelSpan'
}

export const speechLabels: Promise<Record<string, string>> = remote
    .getStringResources({ names: Array.from(new Set(Object.values(speechNames))) })
    .then(dic => Object.fromEntries(Object.entries(speechNames).map(([label, name]) => [label, dic[name]])))
