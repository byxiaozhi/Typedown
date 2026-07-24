import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';

type Listener = (args: any) => void;

const mockListeners = new Map<string, Listener>();
const mockPostMessage = jest.fn();
const mockPrintHtml = jest.fn();
const mockExportCallback = jest.fn();

jest.mock('services/exportHtml', () => {
  return {
    __esModule: true,
    default: class MockExportHtml {
      generate() {
        return Promise.resolve('<html />');
      }
    },
  };
});

jest.mock('services/importHtml', () => ({
  __esModule: true,
  htmlToMarkdown: jest.fn(() => 'imported markdown'),
}));

jest.mock('services/common', () => ({
  __esModule: true,
  getHtmlToc: jest.fn(() => ''),
  getTOC: jest.fn(() => ({ toc: [] })),
}));

jest.mock('services/remote', () => ({
  remote: {
    printHTML: (...args: unknown[]) => mockPrintHtml(...args),
    exportCallback: (...args: unknown[]) => mockExportCallback(...args),
  },
}));

jest.mock('services/transport', () => ({
  __esModule: true,
  default: {
    addListener: (eventName: string, listener: Listener) => {
      mockListeners.set(eventName, listener);
      return () => {
        mockListeners.delete(eventName);
      };
    },
    postMessage: (...args: unknown[]) => mockPostMessage(...args),
  },
}));

jest.mock('components/Muya', () => {
  const MockMuya = ({
    markdown,
    onMarkdownChange,
    onCursorChange,
  }: {
    markdown: string;
    onMarkdownChange: (markdown: string) => void;
    onCursorChange: (cursor: unknown) => void;
  }) => (
    <div>
      <div data-testid="muya-markdown">{markdown}</div>
      <button onClick={() => onMarkdownChange('user markdown')} type="button">emit markdown</button>
      <button onClick={() => onCursorChange({ line: 7, ch: 3 })} type="button">emit cursor</button>
    </div>
  );

  return {
    __esModule: true,
    default: MockMuya,
  };
});

jest.mock('components/CodeMirror', () => {
  const MockCodeMirror = ({ markdown }: { markdown: string }) => (
    <div data-testid="codemirror-markdown">{markdown}</div>
  );

  return {
    __esModule: true,
    default: MockCodeMirror,
  };
});

import Editor from './index';

beforeEach(() => {
  mockListeners.clear();
  mockPostMessage.mockReset();
  mockPrintHtml.mockReset();
  mockExportCallback.mockReset();
});

test('external markdown and cursor sync does not call parent edit callbacks', () => {
  const onMarkdownChange = jest.fn();
  const onCursorChange = jest.fn();
  const options = { sourceCode: false, focusMode: false, typewriter: false };
  const view = render(
    <Editor
      basePath="c:\\docs"
      cursor={{ line: 1, ch: 1 }}
      markdown="first markdown"
      onCursorChange={onCursorChange}
      onMarkdownChange={onMarkdownChange}
      options={options}
    />,
  );

  onMarkdownChange.mockClear();
  onCursorChange.mockClear();

  view.rerender(
    <Editor
      basePath="c:\\next"
      cursor={{ line: 9, ch: 4 }}
      markdown="second markdown"
      onCursorChange={onCursorChange}
      onMarkdownChange={onMarkdownChange}
      options={options}
    />,
  );

  expect(screen.getByTestId('muya-markdown')).toHaveTextContent('second markdown');
  expect(onMarkdownChange).not.toHaveBeenCalled();
  expect(onCursorChange).not.toHaveBeenCalled();
});

test('user edits still call parent callbacks after external sync', () => {
  const onMarkdownChange = jest.fn();
  const onCursorChange = jest.fn();
  const options = { sourceCode: false, focusMode: false, typewriter: false };
  const view = render(
    <Editor
      basePath="c:\\docs"
      cursor={{ line: 1, ch: 1 }}
      markdown="first markdown"
      onCursorChange={onCursorChange}
      onMarkdownChange={onMarkdownChange}
      options={options}
    />,
  );

  onMarkdownChange.mockClear();
  onCursorChange.mockClear();

  view.rerender(
    <Editor
      basePath="c:\\next"
      cursor={{ line: 9, ch: 4 }}
      markdown="second markdown"
      onCursorChange={onCursorChange}
      onMarkdownChange={onMarkdownChange}
      options={options}
    />,
  );

  fireEvent.click(screen.getByRole('button', { name: 'emit markdown' }));
  fireEvent.click(screen.getByRole('button', { name: 'emit cursor' }));

  expect(onMarkdownChange).toHaveBeenCalledWith('user markdown');
  expect(onCursorChange).toHaveBeenCalledWith({ line: 7, ch: 3 });
});
