import { remoteFunction } from '../transport'

export interface ActivateTabPayload {
    path: string | null;
    text: string;
    dirty: boolean;
}

export interface SaveTabPayload {
    path: string | null;
    text: string;
    saveAs: boolean;
}

export interface SaveTabSuccessResult {
    path: string;
    basePath: string;
}

export const buildActivateTabPayload = (path: string | null, text: string, dirty: boolean): ActivateTabPayload => ({
    path,
    text,
    dirty,
});

export const buildSaveTabPayload = (path: string | null, text: string, saveAs: boolean): SaveTabPayload => ({
    path,
    text,
    saveAs,
});

export const isConfirmedSaveResult = (result: unknown): result is SaveTabSuccessResult => {
    if (typeof result !== 'object' || result === null) {
        return false;
    }

    const { path, basePath } = result as Partial<SaveTabSuccessResult>;
    return typeof path === 'string' && path.length > 0 && typeof basePath === 'string' && basePath.length > 0;
}

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
    activateTab: remoteFunction<ActivateTabPayload, undefined>('ActivateTab'),
    saveTab: remoteFunction<SaveTabPayload, SaveTabSuccessResult | null>('SaveTab'),
    closeWindow: remoteFunction<undefined, undefined>('CloseWindow'),
    openNewWindow: remoteFunction<string, undefined>('OpenNewWindow'),
    unhandledException: remoteFunction<string, undefined>('UnhandledException'),
}

export default remote;
