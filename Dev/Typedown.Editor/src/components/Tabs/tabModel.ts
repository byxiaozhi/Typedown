export type CloseDecision = 'save' | 'dont-save' | 'cancel';

export interface TabDocument {
  id: string;
  path: string | null;
  displayName: string;
  text: string;
  cursor: unknown;
  basePath: string | null;
  dirty: boolean;
}

export interface TabState {
  tabs: TabDocument[];
  activeTabId: string | null;
}

interface OpenDocumentArgs {
  path: string;
  text: string;
  cursor?: unknown;
  basePath?: string | null;
}

interface SaveResult {
  success: boolean;
  path?: string;
}

interface CloseResult {
  decision: CloseDecision;
  nextState: TabState;
  shouldCloseWindow: boolean;
}

const UNTITLED_NAME = '\u672a\u547d\u540d';

export const canonicalizePath = (path: string): string => {
  const normalized = path.replace(/\//g, '\\');
  const isUnc = normalized.startsWith('\\\\');
  if (isUnc) {
    return `\\\\${normalizeParts(normalized.slice(2).split('\\'), 2).join('\\')}`;
  }

  const driveMatch = normalized.match(/^([a-zA-Z]:)(.*)$/);
  const prefix = driveMatch ? driveMatch[1].toLowerCase() : '';
  const rest = driveMatch ? driveMatch[2] : normalized;
  const absolute = rest.startsWith('\\');
  const parts = normalizeParts(rest.split('\\'), 0);

  const separator = prefix || absolute ? '\\' : '';
  return `${prefix}${separator}${parts.join('\\')}`;
};

export const createUntitledTab = (): TabDocument => ({
  id: `tab-${Date.now()}-${Math.random().toString(36).slice(2)}`,
  path: null,
  displayName: UNTITLED_NAME,
  text: '',
  cursor: null,
  basePath: null,
  dirty: false,
});

export const openDocument = (state: TabState, document: OpenDocumentArgs): TabState => {
  const path = canonicalizePath(document.path);
  const existing = state.tabs.find((tab) => tab.path === path);

  if (existing) {
    return { ...state, activeTabId: existing.id };
  }

  const tab: TabDocument = {
    id: `tab-${path}`,
    path,
    displayName: getFileName(path),
    text: document.text,
    cursor: document.cursor ?? null,
    basePath: document.basePath ? canonicalizePath(document.basePath) : null,
    dirty: false,
  };

  return {
    tabs: [...state.tabs, tab],
    activeTabId: tab.id,
  };
};

export const updateTabText = (state: TabState, tabId: string, text: string): TabState => ({
  ...state,
  tabs: state.tabs.map((tab) => (
    tab.id === tabId ? { ...tab, text, dirty: true } : tab
  )),
});

export const switchTab = (state: TabState, tabId: string): TabState => (
  state.tabs.some((tab) => tab.id === tabId) ? { ...state, activeTabId: tabId } : state
);

export const reorderTabs = (state: TabState, fromIndex: number, toIndex: number): TabState => {
  if (
    fromIndex < 0
    || toIndex < 0
    || fromIndex >= state.tabs.length
    || toIndex >= state.tabs.length
    || fromIndex === toIndex
  ) {
    return state;
  }

  const tabs = [...state.tabs];
  const [moved] = tabs.splice(fromIndex, 1);
  tabs.splice(toIndex, 0, moved);

  return { ...state, tabs };
};

export const markTabSaved = (state: TabState, tabId: string, result: SaveResult): TabState => {
  if (!result.success || !result.path) {
    return state;
  }

  const path = canonicalizePath(result.path);
  const basePath = getBasePath(path);

  return {
    ...state,
    tabs: state.tabs.map((tab) => (
      tab.id === tabId
        ? {
          ...tab,
          path,
          displayName: getFileName(path),
          basePath,
          dirty: false,
        }
        : tab
    )),
  };
};

export const closeTab = (
  state: TabState,
  tabId: string,
  decision: CloseDecision,
  saveResult?: SaveResult,
): CloseResult => {
  if (decision === 'cancel') {
    return { decision, nextState: state, shouldCloseWindow: false };
  }

  if (decision === 'save' && (!saveResult || !saveResult.success)) {
    return { decision: 'cancel', nextState: state, shouldCloseWindow: false };
  }

  const tabIndex = state.tabs.findIndex((tab) => tab.id === tabId);
  if (tabIndex < 0) {
    return { decision: 'cancel', nextState: state, shouldCloseWindow: false };
  }

  const tabs = state.tabs.filter((tab) => tab.id !== tabId);
  const nextActiveTab = state.activeTabId === tabId
    ? tabs[Math.min(tabIndex, tabs.length - 1)]
    : state.tabs.find((tab) => tab.id === state.activeTabId);
  const nextState = {
    tabs,
    activeTabId: nextActiveTab?.id ?? null,
  };

  return {
    decision,
    nextState,
    shouldCloseWindow: tabs.length === 0,
  };
};

const getFileName = (path: string): string => path.split('\\').pop() ?? path;

const getBasePath = (path: string): string | null => {
  if (path.startsWith('\\\\')) {
    const parts = path.slice(2).split('\\');
    parts.pop();
    return parts.length >= 2 ? `\\\\${parts.join('\\')}` : null;
  }

  if (/^[a-z]:\\[^\\]+$/i.test(path)) {
    return `${path.slice(0, 2)}\\`;
  }

  const parts = path.split('\\');
  parts.pop();
  return parts.length > 0 ? parts.join('\\') : null;
};

const normalizeParts = (parts: string[], rootLength: number): string[] => {
  const normalized: string[] = [];

  parts.forEach((part) => {
    if (!part || part === '.') return;
    if (part === '..') {
      if (normalized.length > rootLength) {
        normalized.pop();
      }
      return;
    }
    normalized.push(part.toLowerCase());
  });

  return normalized;
};
