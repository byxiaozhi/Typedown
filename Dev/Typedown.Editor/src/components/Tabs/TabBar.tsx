import React, { useCallback, useState } from 'react';
import DirtyCloseDialog from './DirtyCloseDialog';
import type { TabDocument } from './tabModel';
import './TabBar.scss';

interface TabBarProps {
  tabs: TabDocument[];
  activeTabId: string | null;
  onSelectTab: (tabId: string) => void;
  onCloseTab: (tabId: string) => void;
  onDirtyCloseSave: (tabId: string) => void;
  onDirtyCloseDontSave: (tabId: string) => void;
  onNewTab: () => void;
  onReorderTabs: (fromIndex: number, toIndex: number) => void;
}

const TabBar: React.FC<TabBarProps> = ({
  tabs,
  activeTabId,
  onSelectTab,
  onCloseTab,
  onDirtyCloseSave,
  onDirtyCloseDontSave,
  onNewTab,
  onReorderTabs,
}) => {
  const [dirtyCloseTabId, setDirtyCloseTabId] = useState<string | null>(null);
  const [dragIndex, setDragIndex] = useState<number | null>(null);

  const handleClose = useCallback((tabId: string) => {
    const tab = tabs.find((t) => t.id === tabId);
    if (tab?.dirty) {
      setDirtyCloseTabId(tabId);
      return;
    }
    onCloseTab(tabId);
  }, [tabs, onCloseTab]);

  const handleDirtySave = useCallback(() => {
    if (dirtyCloseTabId !== null) {
      onDirtyCloseSave(dirtyCloseTabId);
      setDirtyCloseTabId(null);
    }
  }, [dirtyCloseTabId, onDirtyCloseSave]);

  const handleDirtyDontSave = useCallback(() => {
    if (dirtyCloseTabId !== null) {
      onDirtyCloseDontSave(dirtyCloseTabId);
      setDirtyCloseTabId(null);
    }
  }, [dirtyCloseTabId, onDirtyCloseDontSave]);

  const handleDirtyCancel = useCallback(() => {
    setDirtyCloseTabId(null);
  }, []);

  const handleDragStart = useCallback((e: React.DragEvent, index: number) => {
    setDragIndex(index);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', String(index));
  }, []);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    if (e.dataTransfer) {
      e.dataTransfer.dropEffect = 'move';
    }
  }, []);

  const handleDrop = useCallback((e: React.DragEvent, toIndex: number) => {
    e.preventDefault();
    if (dragIndex !== null && dragIndex !== toIndex) {
      onReorderTabs(dragIndex, toIndex);
    }
    setDragIndex(null);
  }, [dragIndex, onReorderTabs]);

  const handleDragEnd = useCallback(() => {
    setDragIndex(null);
  }, []);

  const dirtyTab = dirtyCloseTabId !== null
    ? tabs.find((tab) => tab.id === dirtyCloseTabId)
    : null;

  return (
    <div className="tab-bar">
      <div className="tab-bar__tabs" role="tablist" aria-label="Tabs">
        {tabs.map((tab, index) => {
          const isActive = tab.id === activeTabId;
          const label = tab.dirty ? `* ${tab.displayName}` : tab.displayName;

          return (
            <div
              key={tab.id}
              className={`tab-bar__tab${isActive ? ' tab-bar__tab--active' : ''}`}
              draggable
              onDragStart={(e) => handleDragStart(e, index)}
              onDragOver={handleDragOver}
              onDrop={(e) => handleDrop(e, index)}
              onDragEnd={handleDragEnd}
            >
              <button
                aria-selected={isActive}
                className="tab-bar__tab-button"
                onClick={() => onSelectTab(tab.id)}
                role="tab"
                title={tab.path ?? tab.displayName}
                type="button"
              >
                {label}
              </button>
              <button
                aria-label={`close ${tab.displayName}`}
                className="tab-bar__close-button"
                onClick={() => handleClose(tab.id)}
                type="button"
              >
                ×
              </button>
            </div>
          );
        })}
      </div>
      <button
        aria-label="new tab"
        className="tab-bar__new-button"
        onClick={onNewTab}
        type="button"
      >
        +
      </button>
      {dirtyTab && (
        <DirtyCloseDialog
          tabName={dirtyTab.displayName}
          onSave={handleDirtySave}
          onDontSave={handleDirtyDontSave}
          onCancel={handleDirtyCancel}
        />
      )}
    </div>
  );
};

export default TabBar;
