import React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';

type Listener = (args: any) => void;

const mockListeners = new Map<string, Listener>();
const mockEventLog: string[] = [];

const mockGetSettings = jest.fn();
const mockActivateTab = jest.fn();
const mockSaveTab = jest.fn();
const mockCloseWindow = jest.fn();
const mockPostMessage = jest.fn();
const mockPostMessageNoDiff = jest.fn();

jest.mock('services/remote', () => ({
  remote: {
    getSettings: (...args: unknown[]) => mockGetSettings(...args),
    activateTab: (...args: unknown[]) => mockActivateTab(...args),
    saveTab: (...args: unknown[]) => mockSaveTab(...args),
    closeWindow: (...args: unknown[]) => mockCloseWindow(...args),
  },
}));

jest.mock('services/transport', () => ({
  __esModule: true,
  remoteFunction: jest.fn(),
  default: {
    addListener: (eventName: string, listener: Listener) => {
      mockListeners.set(eventName, listener);
      return () => {
        mockListeners.delete(eventName);
      };
    },
    postMessage: (...args: unknown[]) => {
      mockEventLog.push(`post:${String(args[0])}`);
      return mockPostMessage(...args);
    },
    postMessageNoDiff: (...args: unknown[]) => mockPostMessageNoDiff(...args),
  },
}));

jest.mock('components/Editor', () => {
  const MockEditor = ({
    markdown,
    basePath,
    onMarkdownChange,
    onCursorChange,
  }: {
    markdown: string;
    basePath: string | null;
    onMarkdownChange?: (markdown: string) => void;
    onCursorChange?: (cursor: unknown) => void;
  }) => (
    <div>
      <div data-testid="editor-instance">{markdown}</div>
      <div data-testid="editor-base-path">{basePath ?? ''}</div>
      <button onClick={() => onMarkdownChange?.('updated from editor')}>change markdown</button>
      <button onClick={() => onCursorChange?.({ line: 3, ch: 9 })}>change cursor</button>
    </div>
  );

  return {
    __esModule: true,
    default: MockEditor,
  };
});

import TabCoordinator from './TabCoordinator';

const getTabs = () => Array.from(document.querySelectorAll('[role="tab"]')) as HTMLButtonElement[];

const emit = async (eventName: string, args: any) => {
  const listener = mockListeners.get(eventName);
  if (!listener) {
    throw new Error(`Missing listener for ${eventName}`);
  }
  await act(async () => {
    await listener(args);
  });
};

beforeEach(() => {
  mockListeners.clear();
  mockEventLog.length = 0;
  mockGetSettings.mockReset();
  mockActivateTab.mockReset();
  mockSaveTab.mockReset();
  mockCloseWindow.mockReset();
  mockPostMessage.mockClear();
  mockPostMessageNoDiff.mockClear();
  mockGetSettings.mockResolvedValue({
    filePath: 'C:\\Docs\\start.md',
    markdown: 'start markdown',
    basePath: 'C:\\Docs',
    sourceCode: false,
    focusMode: false,
    typewriter: false,
  });
  mockActivateTab.mockImplementation(async () => {
    mockEventLog.push('activateTab');
  });
  mockSaveTab.mockResolvedValue({
    path: 'C:\\Docs\\saved.md',
    basePath: 'C:\\Docs',
  });
  mockCloseWindow.mockResolvedValue(undefined);
});

test('startup settings creates exactly one tab using filePath markdown and basePath', async () => {
  render(<TabCoordinator />);

  expect((await screen.findByText('start.md')).closest('button')).toHaveAttribute('title', 'c:\\docs\\start.md');
  expect(getTabs()).toHaveLength(1);
  expect(screen.getAllByTestId('editor-instance')).toHaveLength(1);
  expect(screen.getByTestId('editor-instance')).toHaveTextContent('start markdown');
  expect(screen.getByTestId('editor-base-path')).toHaveTextContent('c:\\docs');
});

test('DocumentLoaded opens a new tab or activates the existing canonical path', async () => {
  render(<TabCoordinator />);
  await screen.findByText('start.md');

  await emit('DocumentLoaded', {
    path: 'C:\\Docs\\Second.md',
    text: 'second markdown',
    basePath: 'C:\\Docs',
    dirty: true,
  });

  expect((await screen.findByText('* second.md')).closest('button')).toHaveAttribute('aria-selected', 'true');
  expect(getTabs()).toHaveLength(2);

  await emit('DocumentLoaded', {
    path: 'c:\\docs\\.\\second.md',
    text: 'should not duplicate',
    basePath: 'C:\\Docs',
    dirty: false,
  });

  expect(getTabs()).toHaveLength(2);
  expect(screen.getByText('* second.md').closest('button')).toHaveAttribute('aria-selected', 'true');
});

test('editor changes update only the active tab', async () => {
  render(<TabCoordinator />);
  await screen.findByText('start.md');

  await emit('DocumentLoaded', {
    path: 'C:\\Docs\\second.md',
    text: 'second markdown',
    basePath: 'C:\\Docs',
    dirty: false,
  });

  await act(async () => {
    fireEvent.click(screen.getByText('start.md'));
  });
  fireEvent.click(screen.getByRole('button', { name: 'change markdown' }));

  fireEvent.click(screen.getByText('second.md'));
  expect(screen.getByTestId('editor-instance')).toHaveTextContent('second markdown');

  fireEvent.click(screen.getByText('* start.md'));
  expect(screen.getByTestId('editor-instance')).toHaveTextContent('updated from editor');
});

test('switching calls activateTab before loading the target document into the editor', async () => {
  render(<TabCoordinator />);
  await screen.findByText('start.md');

  await emit('DocumentLoaded', {
    path: 'C:\\Docs\\second.md',
    text: 'second markdown',
    basePath: 'C:\\Docs',
    dirty: false,
  });
  fireEvent.click(screen.getByRole('button', { name: 'change markdown' }));
  mockEventLog.length = 0;
  mockActivateTab.mockClear();
  mockPostMessage.mockClear();

  fireEvent.click(screen.getByText('start.md'));

  await waitFor(() => expect(mockEventLog).toEqual(['activateTab']));
  expect(mockActivateTab).toHaveBeenCalledWith({
    path: 'c:\\docs\\start.md',
    text: 'start markdown',
    dirty: false,
  });
  expect(mockPostMessage).not.toHaveBeenCalledWith('LoadFile', expect.anything());
});

test('a failed saveTab call leaves the tab dirty and active', async () => {
  mockSaveTab.mockResolvedValueOnce(null);

  render(<TabCoordinator />);
  await screen.findByText('start.md');

  fireEvent.click(screen.getByRole('button', { name: 'change markdown' }));
  await emit('SaveRequested', { saveAs: false });

  await waitFor(() => expect(mockSaveTab).toHaveBeenCalled());
  expect(screen.getByText('* start.md')).toBeInTheDocument();
  expect(screen.getByText('* start.md').closest('button')).toHaveAttribute('aria-selected', 'true');
});

test('NewTabRequested creates an untitled tab without replacing existing tabs', async () => {
  render(<TabCoordinator />);
  await screen.findByText('start.md');

  await emit('NewTabRequested', undefined);

  expect((await screen.findByText('未命名')).closest('button')).toHaveAttribute('aria-selected', 'true');
  expect(getTabs()).toHaveLength(2);
  expect(screen.getByText('start.md')).toBeInTheDocument();
});

test('CloseWindow is called only after the last tab is closed without pending dirty work', async () => {
  render(<TabCoordinator />);
  await screen.findByText('start.md');

  fireEvent.click(screen.getByRole('button', { name: 'close start.md' }));

  await waitFor(() => expect(mockCloseWindow).toHaveBeenCalledTimes(1));
});
