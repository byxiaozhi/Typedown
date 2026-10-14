// mermaid.min.js is 3.5 MB of script, and loading it from index.html made every start parse it first (about a quarter
// of a second) although few documents draw a diagram. The editor looks the library up as window.mermaid only when it
// draws one (renderers/index.js, waitForGlobal, which polls until it is there), so the first look-up is what loads it.
// From the Uno edition (Typedown-Uno #2), where it was measured; Tools/EditorBench/mermaid-lazy-check.js checks it.
(function () {
    var requested = false;
    Object.defineProperty(window, 'mermaid', {
        configurable: true,
        get: function () {
            if (!requested) {
                requested = true;
                var script = document.createElement('script');
                script.src = './mermaid.min.js';
                // A load that failed is tried again at the next look-up: otherwise every later diagram waited for a
                // library that was never coming.
                script.onerror = function () { requested = false; script.remove(); };
                document.head.appendChild(script);
            }
            return undefined;
        },
        // mermaid.min.js ends with globalThis["mermaid"] = ...: from then on it is a plain property.
        set: function (value) {
            Object.defineProperty(window, 'mermaid', { value: value, writable: true, configurable: true, enumerable: true });
        }
    });
})();
