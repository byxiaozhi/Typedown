import { fragmentToPlainText, selectionToPlainText } from './plainText';

// The page as Muya draws it (taken from the editor, katex shortened): syntax markers in .ag-remove spans, a formula's
// source next to its drawing, an image as a span carrying its source.
const page = `
<h1 id="h" class="ag-paragraph atx"><a class="ag-front-icon" contenteditable="false"><span class="icon">H₁</span></a><span class="ag-paragraph ag-atx-line"><span class="ag-hide ag-remove">#</span><span class="ag-header-tight-space ag-remove"> </span><span class="ag-plain-text">Heading </span><span class="ag-hide ag-remove">**</span><strong class="ag-inline-rule"><span class="ag-plain-text">bold</span></strong><span class="ag-hide ag-remove">**</span></span></h1>
<p id="p" class="ag-paragraph"><a class="ag-front-icon" contenteditable="false"><span class="icon">P</span></a><span class="ag-paragraph ag-paragraph-content"><span class="ag-plain-text">Para </span><span class="ag-gray ag-remove">**</span><strong class="ag-inline-rule"><span class="ag-plain-text">b</span></strong><span class="ag-gray ag-remove">**</span><span class="ag-plain-text"> </span><span class="ag-hide ag-remove">\`</span><code class="ag-inline-rule">c</code><span class="ag-hide ag-remove">\`</span><span class="ag-plain-text"> </span><span class="ag-hide ag-remove">[</span><a class="ag-inline-rule" href="https://e.com"><span class="ag-plain-text">link</span></a><span class="ag-hide ag-remove">](</span><span class="ag-hide ag-remove">https://e.com "T"</span><span class="ag-hide ag-remove">)</span><span class="ag-plain-text"> </span><span class="ag-inline-image" data-raw="![alt \\] text](real.png)"><a class="ag-image-icon-success" contenteditable="false"><span class="icon"></span></a><span class="ag-image-container"><img src="real.png" alt="x"></span></span><span class="ag-plain-text"> </span><span class="ag-hide ag-math-marker">$</span><span class="ag-hide ag-math"><span class="ag-inline-rule ag-math-text">x^2</span><span class="ag-math-render" contenteditable="false"><span class="katex">𝑥2x2</span></span></span><span class="ag-hide ag-math-marker">$</span><span class="ag-plain-text"> </span><span class="ag-hide ag-output-remove">&lt;kbd&gt;</span><kbd class="ag-inline-rule ag-raw-html"><span class="ag-plain-text">K</span></kbd><span class="ag-hide ag-output-remove">&lt;/kbd&gt;</span><span class="ag-plain-text"> a</span><sup class="ag-inline-footnote-identifier ag-inline-rule"><span class="ag-hide ag-remove">[^</span><a>1</a><span class="ag-hide ag-remove">]</span></sup><span class="ag-plain-text">.</span></span></p>
<ol id="ol" class="ag-paragraph ag-order-list" start="3"><a class="ag-front-icon" contenteditable="false"><span class="icon"></span></a><li id="li1" class="ag-paragraph ag-list-item"><p class="ag-paragraph"><span class="ag-paragraph-content"><span class="ag-plain-text">three</span></span></p></li><li id="li2" class="ag-paragraph ag-list-item"><p class="ag-paragraph"><span class="ag-paragraph-content"><span class="ag-plain-text">four</span></span></p><ul class="ag-paragraph ag-bullet-list"><li id="li3" class="ag-paragraph ag-list-item"><p class="ag-paragraph"><span class="ag-paragraph-content"><span class="ag-plain-text">nested</span></span></p></li></ul></li></ol>
<ul class="ag-paragraph ag-task-list"><li class="ag-paragraph ag-list-item ag-task-list-item"><input class="ag-task-list-item-checkbox" type="checkbox" checked=""><p class="ag-paragraph"><span class="ag-paragraph-content"><span class="ag-plain-text">done</span></span></p></li><li class="ag-paragraph ag-list-item ag-task-list-item"><input class="ag-task-list-item-checkbox" type="checkbox"><p class="ag-paragraph"><span class="ag-paragraph-content"><span class="ag-plain-text">todo</span></span></p></li></ul>
<blockquote class="ag-paragraph"><a class="ag-front-icon" contenteditable="false"><span class="icon"></span></a><p class="ag-paragraph"><span class="ag-paragraph-content"><span class="ag-plain-text">quote</span></span></p></blockquote>
<figure class="ag-paragraph" data-role="TABLE"><a class="ag-front-icon" contenteditable="false"><span class="icon"></span></a><table class="ag-paragraph"><thead><tr><th><span class="ag-cell-content"><span class="ag-plain-text">a</span></span></th><th><span class="ag-cell-content"><span class="ag-plain-text">b</span></span></th></tr></thead><tbody><tr><td><span class="ag-cell-content"><span class="ag-plain-text">1</span></span></td><td><span class="ag-cell-content"><span class="ag-hide ag-remove">**</span><strong class="ag-inline-rule"><span class="ag-plain-text">2</span></strong><span class="ag-hide ag-remove">**</span></span></td></tr></tbody></table></figure>
<pre class="ag-paragraph ag-fence-code" data-role="fencecode"><a class="ag-front-icon" contenteditable="false"><span class="icon"></span></a><a class="ag-code-copy" contenteditable="false"><span class="icon"></span></a><span class="ag-paragraph ag-language-input">js</span><code class="ag-paragraph"><span class="ag-paragraph ag-code-content"><span class="token keyword">const</span> x = 1;
y();</span></code></pre>
<figure class="ag-paragraph ag-container-block" data-role="MULTIPLEMATH"><a class="ag-front-icon" contenteditable="false"><span class="icon">f</span></a><pre class="ag-paragraph ag-multiple-math" data-role="multiplemath"><code><span class="ag-paragraph ag-code-content">y=2</span></code></pre><div class="ag-paragraph ag-container-preview" contenteditable="false"><span class="katex-display">y=2y=2</span></div></figure>
<p class="ag-paragraph" data-role="hr"><span class="ag-thematic-break-line"><span class="ag-plain-text">---</span></span></p>
<figure class="ag-paragraph" data-role="FOOTNOTE"><a class="ag-front-icon" contenteditable="false"><span class="icon"></span></a><span class="ag-paragraph ag-footnote-input"><span class="ag-plain-text">1</span></span><p class="ag-paragraph"><span class="ag-paragraph-content"><span class="ag-plain-text">Note.</span></span></p><span class="ag-footnote-backlink"><span class="icon"></span></span></figure>`;

const container = () => {
    document.body.innerHTML = `<div id="ag-editor-id">${page}</div>`;
    return document.getElementById('ag-editor-id')!;
};

test('the whole page reads as plain text: no Markdown, every block in its place', () => {
    expect(fragmentToPlainText(container())).toBe([
        'Heading bold',
        '',
        'Para b c link alt ] text x^2 K a[1].',
        '',
        '3. three',
        '4. four',
        '    • nested',
        '',
        '☑ done',
        '☐ todo',
        '',
        'quote',
        '',
        'a\tb',
        '1\t2',
        '',
        'const x = 1;',
        'y();',
        '',
        'y=2',
        '',
        '[1] Note.',
    ].join('\n'));
});

test('a selection inside one paragraph is its text only', () => {
    const root = container();
    const strong = root.querySelector('#p strong')!;
    const link = root.querySelector('#p a.ag-inline-rule')!;
    const range = document.createRange();
    range.setStart(strong.firstChild!.firstChild!, 0);
    range.setEnd(link.firstChild!.firstChild!, 2);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
    expect(selectionToPlainText()).toBe('b c li');
});

test('a selection starting in a later list item keeps that item\'s number', () => {
    const root = container();
    const range = document.createRange();
    range.setStart(root.querySelector('#li2 .ag-plain-text')!.firstChild!, 0);
    range.setEnd(root.querySelector('#li3 .ag-plain-text')!.firstChild!, 6);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
    expect(selectionToPlainText()).toBe('4. four\n    • nested');
});

test('a word picked out of a list item is only that word', () => {
    const root = container();
    const text = root.querySelector('#li2 .ag-plain-text')!.firstChild!;
    const range = document.createRange();
    range.setStart(text, 1);
    range.setEnd(text, 3);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
    expect(selectionToPlainText()).toBe('ou');
});

test('nothing selected copies nothing', () => {
    container();
    window.getSelection()!.removeAllRanges();
    expect(selectionToPlainText()).toBe('');
});
