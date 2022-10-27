import { remoteFunction } from '../transport'

const remote = {
    getCurrentTheme: remoteFunction<undefined, string>('GetCurrentTheme'),
    contentLoaded: remoteFunction<undefined, string>('ContentLoaded'),
    writeAllText: remoteFunction<{ path: string, text: string }, boolean>('WriteAllText'),
    convertHTML: remoteFunction<{ path: string, html: string, format: string }, boolean>('ConvertHTML'),
    printHTML: remoteFunction<{ html: string }, boolean>('PrintHTML'),
    resizeTable: remoteFunction<{ row: number, column: number }, { row: number, column: number }>('ResizeTable'),
    loadImage: remoteFunction<{ url: string, width: number, height: number }, { url: string }>('LoadImage'),
    getSettings: remoteFunction<undefined, unknown>('GetSettings'),
    setClipboard: remoteFunction<{ type: string, data: unknown }, boolean>('SetClipboard'),
    getStringResources: remoteFunction<{ names: string[] }, { [name: string]: string }>('GetStringResources'),
}

export default remote;