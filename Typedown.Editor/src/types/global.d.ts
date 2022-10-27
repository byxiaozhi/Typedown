
export declare global {

    interface Window {
        chrome: {
            webview: {
                addEventListener: <T>(eventName: string, handler: (arg: Event & { data: T }) => void) => void
                postMessage: <T>(arg: T) => void
            }
        },
        onHostMessage: (arg: string) => void;
        postHostMessage: (arg: string) => void;
    }

    const CefSharp: {
        PostMessage: (arg: any) => void;
        OnMessage: (arg: any) => void;
    }
}