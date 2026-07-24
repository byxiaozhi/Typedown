// jsdom does not ship DataTransfer; provide a minimal polyfill for drag tests.
class MockDataTransfer {
  effectAllowed = 'none';
  dropEffect = 'none';
  private _data: Record<string, string> = {};

  getData(format: string): string {
    return this._data[format] ?? '';
  }

  setData(format: string, data: string): void {
    this._data[format] = data;
  }

  clearData(): void {
    this._data = {};
  }
}

(globalThis as any).DataTransfer = MockDataTransfer;

import React from 'react';
import { fireEvent, render, screen, within } from '@testing-library/react';
import TabBar from './TabBar';
import type { TabDocument } from './tabModel';

let tabIdCounter = 0;
const makeTab = (overrides: Partial<TabDocument> = {}): TabDocument => ({
  id: `tab-${++tabIdCounter}`,
  path: null,
  displayName: '\u672a\u547d\u540d',
  text: '',
  cursor: null,
  basePath: null,
  dirty: false,
  ...overrides,
});

const getTabButtons = () =>
  screen.queryAllByRole('tab') as HTMLButtonElement[];

beforeEach(() => {
  tabIdCounter = 0;
});

test('filename is visible and full path is exposed through the tab tooltip', () => {
  const tabs = [
    makeTab({ displayName: 'readme.md', path: 'c:\\projects\\docs\\readme.md' }),
  ];
  render(
    <TabBar
      tabs={tabs}
      activeTabId={tabs[0].id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  const tabButton = getTabButtons()[0];
  expect(tabButton).toHaveTextContent('readme.md');
  expect(tabButton).toHaveAttribute('title', 'c:\\projects\\docs\\readme.md');
});

test('untitled documents show \u672a\u547d\u540d', () => {
  const tab = makeTab();
  render(
    <TabBar
      tabs={[tab]}
      activeTabId={tab.id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  expect(screen.getByRole('tab')).toHaveTextContent('\u672a\u547d\u540d');
});

test('clicking a tab selects it', () => {
  const onSelectTab = jest.fn();
  const tabs = [makeTab({ displayName: 'a.md' }), makeTab({ displayName: 'b.md' })];
  render(
    <TabBar
      tabs={tabs}
      activeTabId={tabs[0].id}
      onSelectTab={onSelectTab}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  fireEvent.click(screen.getByRole('tab', { name: 'b.md' }));
  expect(onSelectTab).toHaveBeenCalledWith(tabs[1].id);
});

test('dragging a tab requests reorder', () => {
  const onReorderTabs = jest.fn();
  const tabs = [makeTab({ displayName: 'a.md' }), makeTab({ displayName: 'b.md' }), makeTab({ displayName: 'c.md' })];
  render(
    <TabBar
      tabs={tabs}
      activeTabId={tabs[0].id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={onReorderTabs}
    />
  );

  const tabElements = getTabButtons();
  const dragTab = tabElements[0];
  const targetTab = tabElements[2];

  const dt = new (globalThis as any).DataTransfer();
  fireEvent.dragStart(dragTab, { dataTransfer: dt });
  fireEvent.dragOver(targetTab, { dataTransfer: dt });
  fireEvent.drop(targetTab, { dataTransfer: dt });

  expect(onReorderTabs).toHaveBeenCalledWith(0, 2);
});

test('plus button requests a new untitled tab', () => {
  const onNewTab = jest.fn();
  render(
    <TabBar
      tabs={[makeTab()]}
      activeTabId="tab-1"
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={onNewTab}
      onReorderTabs={jest.fn()}
    />
  );

  fireEvent.click(screen.getByRole('button', { name: 'new tab' }));
  expect(onNewTab).toHaveBeenCalled();
});

test('dirty close dialog shows exactly Save, Don\'t Save, and Cancel', () => {
  const dirtyTab = makeTab({ displayName: 'dirty.md', dirty: true });
  render(
    <TabBar
      tabs={[dirtyTab]}
      activeTabId={dirtyTab.id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  const closeButton = screen.getByRole('button', { name: 'close dirty.md' });
  fireEvent.click(closeButton);

  const dialog = screen.getByRole('dialog');
  expect(within(dialog).getByRole('button', { name: 'Save' })).toBeInTheDocument();
  expect(within(dialog).getByRole('button', { name: "Don't Save" })).toBeInTheDocument();
  expect(within(dialog).getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
});

test('Cancel leaves the dialog state clean without invoking any close callback', () => {
  const onDirtyCloseSave = jest.fn();
  const onDirtyCloseDontSave = jest.fn();
  const dirtyTab = makeTab({ displayName: 'dirty.md', dirty: true });
  render(
    <TabBar
      tabs={[dirtyTab]}
      activeTabId={dirtyTab.id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={onDirtyCloseSave}
      onDirtyCloseDontSave={onDirtyCloseDontSave}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  fireEvent.click(screen.getByRole('button', { name: 'close dirty.md' }));
  const dialog = screen.getByRole('dialog');
  fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));

  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  expect(onDirtyCloseSave).not.toHaveBeenCalled();
  expect(onDirtyCloseDontSave).not.toHaveBeenCalled();
});

test('closing a clean tab invokes onCloseTab', () => {
  const onCloseTab = jest.fn();
  const cleanTab = makeTab({ displayName: 'clean.md', path: 'c:\\docs\\clean.md', dirty: false });
  render(
    <TabBar
      tabs={[cleanTab]}
      activeTabId={cleanTab.id}
      onSelectTab={jest.fn()}
      onCloseTab={onCloseTab}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  fireEvent.click(screen.getByRole('button', { name: 'close clean.md' }));
  expect(onCloseTab).toHaveBeenCalledWith(cleanTab.id);
});

test('dirty indicator is shown on dirty tabs', () => {
  const cleanTab = makeTab({ displayName: 'clean.md', dirty: false });
  const dirtyTab = makeTab({ displayName: 'dirty.md', dirty: true });
  render(
    <TabBar
      tabs={[cleanTab, dirtyTab]}
      activeTabId={cleanTab.id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  const tabButtons = getTabButtons();
  const dirtyBtn = tabButtons.find((btn) => btn.textContent === '* dirty.md');
  const cleanBtn = tabButtons.find((btn) => btn.textContent === 'clean.md');
  expect(dirtyBtn).toBeDefined();
  expect(cleanBtn).toBeDefined();
});

test('active tab has aria-selected true', () => {
  const tabs = [makeTab({ displayName: 'a.md' }), makeTab({ displayName: 'b.md' })];
  render(
    <TabBar
      tabs={tabs}
      activeTabId={tabs[0].id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  expect(screen.getByRole('tab', { name: 'a.md' })).toHaveAttribute('aria-selected', 'true');
  expect(screen.getByRole('tab', { name: 'b.md' })).toHaveAttribute('aria-selected', 'false');
});

test('Save button in dirty dialog invokes onDirtyCloseSave', () => {
  const onDirtyCloseSave = jest.fn();
  const dirtyTab = makeTab({ displayName: 'dirty.md', dirty: true });
  render(
    <TabBar
      tabs={[dirtyTab]}
      activeTabId={dirtyTab.id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={onDirtyCloseSave}
      onDirtyCloseDontSave={jest.fn()}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  fireEvent.click(screen.getByRole('button', { name: 'close dirty.md' }));
  fireEvent.click(screen.getByRole('button', { name: 'Save' }));

  expect(onDirtyCloseSave).toHaveBeenCalledWith(dirtyTab.id);
});

test('Don\'t Save button in dirty dialog invokes onDirtyCloseDontSave', () => {
  const onDirtyCloseDontSave = jest.fn();
  const dirtyTab = makeTab({ displayName: 'dirty.md', dirty: true });
  render(
    <TabBar
      tabs={[dirtyTab]}
      activeTabId={dirtyTab.id}
      onSelectTab={jest.fn()}
      onCloseTab={jest.fn()}
      onDirtyCloseSave={jest.fn()}
      onDirtyCloseDontSave={onDirtyCloseDontSave}
      onNewTab={jest.fn()}
      onReorderTabs={jest.fn()}
    />
  );

  fireEvent.click(screen.getByRole('button', { name: 'close dirty.md' }));
  fireEvent.click(screen.getByRole('button', { name: "Don't Save" }));

  expect(onDirtyCloseDontSave).toHaveBeenCalledWith(dirtyTab.id);
});
