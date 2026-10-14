import EventEmitter from "events";

const postMessage = (msg: unknown) => window.chrome.webview.postMessage(JSON.stringify(msg))

type Listener<T> = (arg: T) => void;

const transport = new EventEmitter();

interface IMessage {
  name: string;
  args: unknown;
}

window.chrome.webview.addEventListener<string>("message", ({ data }) => {
  const { name, args } = JSON.parse(data) as IMessage
  transport.emit(name, args);
});

const prevMap = new Map<string, string>();
// Only idempotent state notifications may be skipped. Commands such as OpenURI
// and lifecycle messages such as FileLoaded must still run when repeated.
const deduplicatedStates = new Set(['OnScroll', 'SelectionFormats', 'SelectionChange']);
const ref = { pos: 0 };


// A pending invoke whose host reply never arrives would otherwise keep its listener (and its Promise) forever.
// Every path — reply, send failure, timeout — removes the listener exactly once. The default deadline suits the
// fast metadata calls; a value of 0 disables it for the few host operations that can legitimately run long
// (export, print), which are always answered and so cannot pile up.
const DEFAULT_INVOKE_TIMEOUT_MS = 30000;

const remoteFunction =
  <T, TResult>(name: string, timeoutMs: number = DEFAULT_INVOKE_TIMEOUT_MS) =>
    (args?: T) =>
      new Promise<TResult>((resolve, reject) => {
        const id = `invoke_${ref.pos++}`;
        let timer: ReturnType<typeof setTimeout> | undefined;
        const settle = (finish: () => void) => {
          if (timer !== undefined) clearTimeout(timer);
          transport.removeAllListeners(id);
          finish();
        };
        transport.addListener(id, (e) => settle(() => {
          if (e.code == 0) resolve(e.data);
          else reject(new Error(e.msg));
        }));
        if (timeoutMs > 0) {
          timer = setTimeout(() => settle(() => reject(new Error(`invoke "${name}" timed out after ${timeoutMs} ms`))), timeoutMs);
        }
        try {
          postMessage({ type: "invoke", id, name, args });
        } catch (err) {
          settle(() => reject(err instanceof Error ? err : new Error(String(err))));
        }
      });

const postMessageDiff = (name: string, arg: unknown) => {
  const oldArg = prevMap.get(name);
  const newArg = JSON.stringify(arg);
  if (oldArg === newArg && deduplicatedStates.has(name)) return;
  if (!oldArg) {
    postMessage({
      type: "diffmsg",
      diff: false,
      name,
      args: newArg,
    });
    prevMap.set(name, newArg);
    return;
  }
  const oldCount = oldArg.length;
  const newCount = newArg.length;
  let start = 0;
  let oldEnd = oldCount;
  let newEnd = newCount;
  const min = Math.min(oldCount, newCount);
  for (; start < min; start++) {
    if (oldArg[start] != newArg[start]) {
      break;
    }
  }
  for (; oldEnd > start && newEnd > start; oldEnd--, newEnd--) {
    if (oldArg[oldEnd - 1] != newArg[newEnd - 1]) {
      break;
    }
  }
  const diffArgs = newArg.slice(start, newEnd);
  postMessage({
    type: "diffmsg",
    diff: true,
    name,
    args: diffArgs,
    start,
    end: oldEnd,
  });
  // A failed send must not advance the baseline used by the receiver.
  prevMap.set(name, newArg);
};

export default {
  addListener: <T>(eventName: string, listener: Listener<T>) => {
    transport.addListener(eventName, listener);
    return () => { transport.removeListener(eventName, listener) };
  },
  removeListener: <T>(eventName: string, listener: Listener<T>) => transport.removeListener(eventName, listener),
  removeAllListener: (eventName: string) => transport.removeAllListeners(eventName),
  postMessageNoDiff: (name: string, args: unknown) => postMessage({ type: 'message', name, args }),
  postMessage: postMessageDiff,
};

// Test hook: the number of live listeners for an id, to assert an invoke leaves none behind.
export const _listenerCount = (name: string) => transport.listenerCount(name);

export { remoteFunction };
