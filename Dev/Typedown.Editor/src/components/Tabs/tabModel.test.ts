import {
  canonicalizePath,
  closeTab,
  createUntitledTab,
  markTabSaved,
  openDocument,
  reorderTabs,
  switchTab,
  updateTabText,
} from './tabModel';

const createState = (...tabs: ReturnType<typeof createUntitledTab>[]) => ({
  tabs,
  activeTabId: tabs[0]?.id ?? null,
});

const loadRemoteCommon = () => {
  jest.resetModules();
  Object.defineProperty(window, 'chrome', {
    configurable: true,
    value: {
      webview: {
        addEventListener: jest.fn(),
        postMessage: jest.fn(),
      },
    },
  });
  return require('../../services/remote/common') as typeof import('../../services/remote/common');
};

test('canonicalizePath normalizes Windows casing and redundant segments', () => {
  expect(canonicalizePath('C:\\Docs\\Folder\\..\\Draft.md')).toBe('c:\\docs\\draft.md');
});

test('canonicalizePath preserves UNC roots while normalizing redundant segments', () => {
  expect(canonicalizePath('\\\\Server\\Share\\Folder\\..\\File.md')).toBe('\\\\server\\share\\file.md');
});

test('createUntitledTab creates unique untitled tabs with clean state', () => {
  const first = createUntitledTab();
  const second = createUntitledTab();

  expect(first.id).not.toBe(second.id);
  expect(first.path).toBeNull();
  expect(first.displayName).toBe('\u672a\u547d\u540d');
  expect(first.dirty).toBe(false);
});

test('buildActivateTabPayload keeps the nullable path current text and dirty flag', () => {
  const { buildActivateTabPayload } = loadRemoteCommon();

  expect(buildActivateTabPayload(null, 'draft', true)).toEqual({
    path: null,
    text: 'draft',
    dirty: true,
  });
});

test('buildSaveTabPayload keeps the nullable path current text and saveAs flag', () => {
  const { buildSaveTabPayload } = loadRemoteCommon();

  expect(buildSaveTabPayload('C:\\Docs\\Draft.md', 'draft', false)).toEqual({
    path: 'C:\\Docs\\Draft.md',
    text: 'draft',
    saveAs: false,
  });
});

test('unconfirmed save results are rejected so React keeps the tab dirty', () => {
  const { isConfirmedSaveResult } = loadRemoteCommon();
  const tab = {
    ...createUntitledTab(),
    text: 'draft',
    dirty: true,
    path: null,
    displayName: '未命名',
    basePath: null,
  };
  const state = createState(tab);
  const unconfirmedResult = { basePath: 'C:\\Docs' };

  const nextState = isConfirmedSaveResult(unconfirmedResult)
    ? markTabSaved(state, tab.id, { success: true, path: unconfirmedResult.path })
    : state;

  expect(isConfirmedSaveResult(unconfirmedResult)).toBe(false);
  expect(nextState).toEqual(state);
});

test('openDocument reuses the existing tab when the path only differs by case or segments', () => {
  const firstOpen = openDocument(createState(), {
    path: 'C:\\Docs\\Folder\\..\\Draft.md',
    text: 'first',
    cursor: { line: 1, ch: 2 },
    basePath: 'C:\\Docs',
  });

  const reopened = openDocument(firstOpen, {
    path: 'c:\\docs\\.\\draft.md',
    text: 'second',
    cursor: { line: 8, ch: 9 },
    basePath: 'C:\\Docs',
  });

  expect(reopened.tabs).toHaveLength(1);
  expect(reopened.activeTabId).toBe(firstOpen.activeTabId);
  expect(reopened.tabs[0].path).toBe('c:\\docs\\draft.md');
  expect(reopened.tabs[0].text).toBe('first');
  expect(reopened.tabs[0].cursor).toEqual({ line: 1, ch: 2 });
  expect(reopened.tabs[0].basePath).toBe('c:\\docs');
});

test('updateTabText changes only the selected tab and marks it dirty', () => {
  const first = {
    ...createUntitledTab(),
    text: 'first',
    cursor: { line: 1, ch: 2 },
    basePath: 'c:\\docs',
  };
  const second = {
    ...createUntitledTab(),
    text: 'second',
    cursor: { line: 3, ch: 4 },
    basePath: 'c:\\docs',
  };
  const state = createState(first, second);

  const next = updateTabText(state, first.id, 'updated');

  expect(next.tabs[0].text).toBe('updated');
  expect(next.tabs[0].dirty).toBe(true);
  expect(next.tabs[1].text).toBe('second');
  expect(next.tabs[1].dirty).toBe(false);
});

test('switchTab keeps each tab text cursor and base path unchanged', () => {
  const first = {
    ...createUntitledTab(),
    text: 'first',
    cursor: { line: 1, ch: 2 },
    basePath: 'c:\\docs\\one',
  };
  const second = {
    ...createUntitledTab(),
    text: 'second',
    cursor: { line: 3, ch: 4 },
    basePath: 'c:\\docs\\two',
  };
  const state = createState(first, second);

  const next = switchTab(state, second.id);

  expect(next.activeTabId).toBe(second.id);
  expect(next.tabs).toEqual(state.tabs);
});

test('reorderTabs moves one tab without changing the active tab or contents', () => {
  const first = { ...createUntitledTab(), text: 'first', cursor: { line: 1, ch: 2 }, basePath: 'c:\\docs' };
  const second = { ...createUntitledTab(), text: 'second', cursor: { line: 3, ch: 4 }, basePath: 'c:\\docs' };
  const third = { ...createUntitledTab(), text: 'third', cursor: { line: 5, ch: 6 }, basePath: 'c:\\docs' };
  const state = { tabs: [first, second, third], activeTabId: second.id };

  const next = reorderTabs(state, 0, 2);

  expect(next.activeTabId).toBe(second.id);
  expect(next.tabs.map((tab) => tab.id)).toEqual([second.id, third.id, first.id]);
  expect(next.tabs.map((tab) => tab.text)).toEqual(['second', 'third', 'first']);
});

test('markTabSaved updates path name and base path only after a successful save result', () => {
  const tab = {
    ...createUntitledTab(),
    text: 'draft',
    dirty: true,
    path: 'c:\\docs\\draft.md',
    displayName: 'Draft',
    basePath: 'c:\\docs',
    cursor: { line: 1, ch: 2 },
  };
  const state = createState(tab);

  const failed = markTabSaved(state, tab.id, {
    success: false,
    path: 'C:\\Docs\\Saved.md',
  });

  expect(failed).toEqual(state);

  const saved = markTabSaved(state, tab.id, {
    success: true,
    path: 'C:\\Docs\\Saved.md',
  });

  expect(saved.tabs[0]).toMatchObject({
    path: 'c:\\docs\\saved.md',
    displayName: 'saved.md',
    basePath: 'c:\\docs',
    dirty: false,
  });
});

test('markTabSaved keeps drive root as the base path for files at the drive root', () => {
  const tab = {
    ...createUntitledTab(),
    dirty: true,
  };
  const state = createState(tab);

  const saved = markTabSaved(state, tab.id, {
    success: true,
    path: 'C:\\File.md',
  });

  expect(saved.tabs[0]).toMatchObject({
    path: 'c:\\file.md',
    displayName: 'file.md',
    basePath: 'c:\\',
    dirty: false,
  });
});

test('markTabSaved keeps the UNC share root as the base path for files at the share root', () => {
  const tab = {
    ...createUntitledTab(),
    dirty: true,
  };
  const state = createState(tab);

  const saved = markTabSaved(state, tab.id, {
    success: true,
    path: '\\\\Server\\Share\\File.md',
  });

  expect(saved.tabs[0]).toMatchObject({
    path: '\\\\server\\share\\file.md',
    displayName: 'file.md',
    basePath: '\\\\server\\share',
    dirty: false,
  });
});

test('closeTab returns the expected state for save dont-save and cancel', () => {
  const first = {
    ...createUntitledTab(),
    text: 'first',
    dirty: true,
    path: 'c:\\docs\\first.md',
    displayName: 'first.md',
    basePath: 'c:\\docs',
  };
  const second = {
    ...createUntitledTab(),
    text: 'second',
    dirty: false,
    path: 'c:\\docs\\second.md',
    displayName: 'second.md',
    basePath: 'c:\\docs',
  };
  const state = { tabs: [first, second], activeTabId: first.id };

  const cancel = closeTab(state, first.id, 'cancel');
  expect(cancel.decision).toBe('cancel');
  expect(cancel.shouldCloseWindow).toBe(false);
  expect(cancel.nextState).toBe(state);

  const dontSave = closeTab(state, first.id, 'dont-save');
  expect(dontSave.decision).toBe('dont-save');
  expect(dontSave.shouldCloseWindow).toBe(false);
  expect(dontSave.nextState.tabs.map((tab) => tab.id)).toEqual([second.id]);
  expect(dontSave.nextState.activeTabId).toBe(second.id);

  const save = closeTab(state, first.id, 'save', {
    success: true,
    path: 'C:\\Docs\\Saved.md',
  });
  expect(save.decision).toBe('save');
  expect(save.shouldCloseWindow).toBe(false);
  expect(save.nextState.tabs.map((tab) => tab.id)).toEqual([second.id]);
  expect(save.nextState.activeTabId).toBe(second.id);
  expect(save.nextState.tabs[0]).toMatchObject({
    path: 'c:\\docs\\second.md',
    displayName: 'second.md',
    basePath: 'c:\\docs',
    dirty: false,
  });
});

test('closeTab reports shouldCloseWindow when closing the last tab', () => {
  const tab = {
    ...createUntitledTab(),
    text: 'only',
    dirty: false,
    path: 'c:\\docs\\only.md',
    displayName: 'only.md',
    basePath: 'c:\\docs',
  };
  const state = { tabs: [tab], activeTabId: tab.id };

  const decision = closeTab(state, tab.id, 'dont-save');

  expect(decision.shouldCloseWindow).toBe(true);
  expect(decision.nextState.tabs).toHaveLength(0);
  expect(decision.nextState.activeTabId).toBeNull();
});
