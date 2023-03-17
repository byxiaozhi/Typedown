import { remoteFunction } from '../transport'

const remote = {
    getCurrentTheme: remoteFunction<undefined, string>('GetCurrentTheme'),
    contentLoaded: remoteFunction<undefined, string>('ContentLoaded'),
    exportCallback: remoteFunction<{ html: string, context: unknown }, boolean>('ExportCallback'),
    printHTML: remoteFunction<{ html: string, context: unknown }, boolean>('PrintHTML'),
    resizeTable: remoteFunction<{ row: number, column: number }, { row: number, column: number }>('ResizeTable'),
    loadImage: remoteFunction<{ url: string, width: number, height: number }, { url: string }>('LoadImage'),
    getSettings: remoteFunction<undefined, unknown>('GetSettings'),
    setClipboard: remoteFunction<{ type: string, data: unknown }, boolean>('SetClipboard'),
    getStringResources: remoteFunction<{ names: string[] }, { [name: string]: string }>('GetStringResources'),
    openNewWindow: remoteFunction<string, undefined>('OpenNewWindow'),
}

export default remote;