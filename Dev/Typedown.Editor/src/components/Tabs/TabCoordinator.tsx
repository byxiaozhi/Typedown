import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Editor from 'components/Editor';
import { remote } from 'services/remote';
import transport from 'services/transport';
import {
  buildActivateTabPayload,
  buildSaveTabPayload,
  isConfirmedSaveResult,
} from 'services/remote/common';
import {
  closeTab,
  createUntitledTab,
  openDocument,
  markTabSaved,
  switchTab,
  type TabDocument,
  type TabState,
} from './tabModel';

interface StartupSettings {
  filePath: string | null;
  markdown: string;
  basePath: string | null;
  [key: string]: unknown;
}

interface DocumentLoadedArgs {
  path: string;
  text: string;
  basePath: string | null;
  dirty: boolean;
}

interface NewTabRequestedArgs {
  text?: string;
  basePath?: string | null;
  dirty?: boolean;
}

interface SaveRequestedArgs {
  saveAs?: boolean;
}

const DEFAULT_NEW_TAB_MARKDOWN = '\n';

const createStartupState = ({ filePath, markdown, basePath }: StartupSettings): TabState => {
  if (filePath) {
    return openDocument({ tabs: [], activeTabId: null }, {
      path: filePath,
      text: markdown,
      basePath,
    });
  }

  const tab = {
    ...createUntitledTab(),
    text: markdown,
    basePath,
  };

  return {
    tabs: [tab],
    activeTabId: tab.id,
  };
};

const getActiveTab = (state: TabState): TabDocument | undefined => (
  state.tabs.find((tab) => tab.id === state.activeTabId)
);

const TabCoordinator: React.FC = () => {
  const [editorOptions, setEditorOptions] = useState<Record<string, unknown>>();
  const [tabState, setTabState] = useState<TabState>({ tabs: [], activeTabId: null });
  const tabStateRef = useRef(tabState);

  useEffect(() => {
    tabStateRef.current = tabState;
  }, [tabState]);

  useEffect(() => {
    let cancelled = false;

    remote.getSettings().then((settings: unknown) => {
      if (cancelled) {
        return;
      }

      const { markdown, filePath, basePath, ...options } = settings as StartupSettings;
      setEditorOptions(options);
      setTabState(createStartupState({
        markdown,
        filePath,
        basePath,
      }));
    });

    return () => {
      cancelled = true;
    };
  }, []);

  const activateAndLoad = useCallback((tab: TabDocument) => {
    void remote.activateTab(buildActivateTabPayload(tab.path, tab.text, tab.dirty));
    transport.postMessage('LoadFile', {
      text: tab.text,
      basePath: tab.basePath,
    });
  }, []);

  const handleTabSwitch = useCallback((tabId: string) => {
    const currentState = tabStateRef.current;
    const targetTab = currentState.tabs.find((tab) => tab.id === tabId);
    if (!targetTab || targetTab.id === currentState.activeTabId) {
      return;
    }

    setTabState((state) => switchTab(state, tabId));
    activateAndLoad(targetTab);
  }, [activateAndLoad]);

  const handleMarkdownChange = useCallback((markdown: string) => {
    setTabState((state) => ({
      ...state,
      tabs: state.tabs.map((tab) => (
        tab.id === state.activeTabId
          ? { ...tab, text: markdown, dirty: true }
          : tab
      )),
    }));
  }, []);

  const handleCursorChange = useCallback((cursor: unknown) => {
    setTabState((state) => ({
      ...state,
      tabs: state.tabs.map((tab) => (
        tab.id === state.activeTabId
          ? { ...tab, cursor }
          : tab
      )),
    }));
  }, []);

  const handleNewTab = useCallback((args?: NewTabRequestedArgs) => {
    const tab = {
      ...createUntitledTab(),
      text: args?.text ?? DEFAULT_NEW_TAB_MARKDOWN,
      basePath: args?.basePath ?? null,
      dirty: args?.dirty ?? false,
    };

    setTabState((state) => ({
      tabs: [...state.tabs, tab],
      activeTabId: tab.id,
    }));

    activateAndLoad(tab);
  }, [activateAndLoad]);

  useEffect(() => transport.addListener<DocumentLoadedArgs>('DocumentLoaded', (document) => {
    const currentState = tabStateRef.current;
    const nextState = openDocument(currentState, {
      path: document.path,
      text: document.text,
      basePath: document.basePath,
      cursor: null,
    });
    const targetTab = getActiveTab(nextState);
    if (!targetTab) {
      return;
    }

    const normalizedState = document.dirty
      ? {
        ...nextState,
        tabs: nextState.tabs.map((tab) => (
          tab.id === targetTab.id ? { ...tab, dirty: true } : tab
        )),
      }
      : nextState;

    setTabState(normalizedState);
    const activeDocument = getActiveTab(normalizedState);
    if (activeDocument) {
      activateAndLoad(activeDocument);
    }
  }), [activateAndLoad]);

  useEffect(() => transport.addListener<NewTabRequestedArgs | undefined>('NewTabRequested', (args) => {
    void handleNewTab(args);
  }), [handleNewTab]);

  useEffect(() => transport.addListener<SaveRequestedArgs | undefined>('SaveRequested', async (args) => {
    const activeTab = getActiveTab(tabStateRef.current);
    if (!activeTab) {
      return;
    }

    const result = await remote.saveTab(buildSaveTabPayload(activeTab.path, activeTab.text, args?.saveAs === true));
    if (!isConfirmedSaveResult(result)) {
      return;
    }

    setTabState((state) => markTabSaved(state, activeTab.id, {
      success: true,
      path: result.path,
    }));
  }), []);

  const handleClose = useCallback(async (tabId: string) => {
    const currentState = tabStateRef.current;
    const targetTab = currentState.tabs.find((tab) => tab.id === tabId);
    if (!targetTab || targetTab.dirty) {
      return;
    }

    const result = closeTab(currentState, tabId, 'dont-save');
    setTabState(result.nextState);

    if (result.shouldCloseWindow) {
      await remote.closeWindow();
      return;
    }

    const nextActiveTab = getActiveTab(result.nextState);
    if (nextActiveTab && nextActiveTab.id !== targetTab.id) {
      activateAndLoad(nextActiveTab);
    }
  }, [activateAndLoad]);

  const activeTab = useMemo(() => getActiveTab(tabState), [tabState]);

  if (!editorOptions || !activeTab) {
    return null;
  }

  return (
    <div>
      <div aria-label="Tabs" role="tablist">
        {tabState.tabs.map((tab) => {
          const label = `${tab.dirty ? '* ' : ''}${tab.displayName}`;

          return (
            <span key={tab.id}>
              <button
                aria-selected={tab.id === tabState.activeTabId}
                onClick={() => {
                  void handleTabSwitch(tab.id);
                }}
                role="tab"
                title={tab.path ?? tab.displayName}
                type="button"
              >
                {label}
              </button>
              <button
                aria-label={`close ${tab.displayName}`}
                onClick={() => {
                  void handleClose(tab.id);
                }}
                type="button"
              >
                ×
              </button>
            </span>
          );
        })}
      </div>
      <button
        aria-label="new tab"
        onClick={() => {
          void handleNewTab();
        }}
        type="button"
      >
        +
      </button>
      <Editor
        basePath={activeTab.basePath}
        cursor={activeTab.cursor}
        markdown={activeTab.text}
        onCursorChange={handleCursorChange}
        onMarkdownChange={handleMarkdownChange}
        options={editorOptions}
      />
    </div>
  );
};

export default TabCoordinator;
