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
const ref = { pos: 0 };


const remoteFunction =
  <T, TResult>(name: string) =>
    (args?: T) =>
      new Promise<TResult>((resolve, reject) => {
        const id = `invoke_${ref.pos++}`;
        transport.addListener(id, (e) => {
          if (e.code == 0) {
            resolve(e.data);
          } else {
            reject(new Error(e.msg));
          }
          transport.removeAllListeners(id);
        });
        postMessage({ type: "invoke", id, name, args });
      });

const postMessageDiff = (name: string, arg: unknown) => {
  const oldArg = prevMap.get(name);
  const newArg = JSON.stringify(arg);
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
  prevMap.set(name, newArg);
  const diffArgs = newArg.slice(start, newEnd);
  postMessage({
    type: "diffmsg",
    diff: true,
    name,
    args: diffArgs,
    start,
    end: oldEnd,
  });
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

export { remoteFunction };
