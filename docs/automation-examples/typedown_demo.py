#!/usr/bin/env python3
"""A guided tour of Typedown driven through the local automation API: watch the window while it runs.

    python typedown_demo.py                 # pauses 2.5 s between steps
    python typedown_demo.py --pause 4       # slower
    python typedown_demo.py --step          # wait for Enter before each step
    python typedown_demo.py --keep          # leave the demo file open and on disk afterwards

Turn the API on first: Settings > General > Allow local automation. The script writes a new file in the temp folder,
opens it, writes it section by section, edits, undoes and redoes, switches the views, changes a few settings, then
saves and closes the file. The view and the settings it touched are put back as they were, also after Ctrl+C or an
error. Uses typedown_client.py from this folder; Windows connects to the current user's named pipe, Linux to
$XDG_RUNTIME_DIR/typedown/automation.v1.sock
(or /tmp/typedown-<uid>/ without a runtime directory, or --socket PATH).
"""
import argparse
import os
import sys
import tempfile
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from typedown_client import Client, TypedownError  # noqa: E402

SCOPES = ["app.read", "document.read", "document.write", "document.save", "window.focus", "window.view",
          "settings.read", "settings.write"]
MORE = "（待续）"


def table(rows):
    """A Markdown table as the visual editor writes it back: every column padded to its longest cell (counted in
    characters, at least 3) with a row of dashes as wide. Any other layout is a formatting change the editor would
    make, and a write under the default normalizationPolicy (requireKnownSafe) refuses it."""
    widths = [max(3, max(len(row[i]) for row in rows)) for i in range(len(rows[0]))]
    lines = ["| " + " | ".join(cell.ljust(w) for cell, w in zip(row, widths)) + " |" for row in rows]
    lines.insert(1, "| " + " | ".join("-" * w for w in widths) + " |")
    return "\n".join(lines)


START = f"# Typedown 自动化演示\n\n这份文档由脚本通过本机自动化接口一步步写成。\n\n{MORE}\n"
SECTIONS = [
    "## 列表\n\n- 读取和改写已经打开的文档\n- 切换可视、源码和阅读模式\n- 读写主题、字号等设置",
    "## 任务\n\n- [x] 每次写入都带上读到的版本号\n- [ ] 冲突时重新读取，不拿旧内容重试",
    "## 表格\n\n" + table([("方法", "作用"), ("document.get", "读取正文和标题"),
                             ("document.replaceText", "替换固定字符串"), ("window.setView", "切换显示方式")]),
    "## 代码\n\n```python\nclient.call(\"document.replaceText\", {\"find\": \"旧\", \"replacement\": \"新\"})\n```",
    "## 引用\n\n> 程序每改一次文档，标题会短暂显示是谁改的，改动的文字会淡出高亮。",
]


class Demo:
    def __init__(self, client, pause, step):
        self.c = client
        self.pause = pause
        self.step = step
        self.n = 0
        # "change" also scrolls each change into view; versions before it know only "document".
        self.reveal = "change"

    def say(self, text):
        self.n += 1
        print(f"\n[{self.n}] {text}", flush=True)
        if self.step:
            input("    按 Enter 继续……")

    def wait(self, factor=1.0):
        if not self.step:
            time.sleep(self.pause * factor)

    def get(self, doc_id, *include):
        return self.c.call("document.get", {"documentId": doc_id, "consistency": "latest", "include": list(include)})

    def replace_text(self, doc_id, find, replacement):
        # Read, write against the revision read; on a conflict read again and decide again (typedown_client.py).
        for _ in range(3):
            doc = self.get(doc_id, "text")
            count = doc["text"].count(find)
            try:
                return self.c.call("document.replaceText", {
                    "documentId": doc_id, "baseRevision": doc["revision"], "find": find, "replacement": replacement,
                    "expectedCount": count, "reveal": self.reveal})
            except TypedownError as e:
                if e.kind == "invalid_params" and e.data.get("field") == "reveal" and self.reveal == "change":
                    print("    这个版本还不能滚动到改动处，改用 reveal: document")
                    self.reveal = "document"
                    continue
                if e.kind not in ("revision_conflict", "match_count_mismatch"):
                    raise
                print(f"    文档在这期间变了（{e.kind}），重新读取")
        raise SystemExit("文档一直在变，放弃")

    def view(self, window_id, **changes):
        return self.c.call("window.setView", {"windowId": window_id, **changes})

    def set_setting(self, key, value):
        # The settings revision moves when anyone changes an exposed setting; read it right before writing.
        revision = self.c.call("settings.get", {"keys": [key]})["settingsRevision"]
        return self.c.call("settings.set", {"key": key, "value": value, "baseSettingsRevision": revision})


def default_socket():
    # Where Typedown listens: $XDG_RUNTIME_DIR/typedown/, else /tmp/typedown-<uid>/ (UnixSocketListener.DefaultPath).
    fallback = f"/tmp/typedown-{os.geteuid()}/automation.v1.sock"
    runtime = os.environ.get("XDG_RUNTIME_DIR")
    if not runtime or not os.path.isabs(runtime):
        return fallback
    path = os.path.join(runtime, "typedown", "automation.v1.sock")
    return path if len(path.encode("utf-8")) <= 103 else fallback


def run(d, keep):
    c = d.c
    init = c.initialize(SCOPES, name="Typedown 演示")
    server = init["server"]
    print(f"已连接 {server['name']} {server['version']}（{server['platform']}），获得权限：{', '.join(init['grantedScopes'])}")
    missing = sorted(set(SCOPES) - set(init["grantedScopes"]))
    if missing:
        raise SystemExit(f"缺少权限：{', '.join(missing)}")

    state = c.call("app.getState")
    windows = c.call("window.list")["windows"]
    print(f"{state['windowCount']} 个窗口，"
          f"{sum(w['documentCount'] for w in windows)} 个打开的文档")

    path = os.path.join(tempfile.gettempdir(), "typedown-automation-demo.md")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(START)

    d.say(f"打开演示文件 {path}")
    opened = c.call("document.open", {"path": path, "reveal": "document"})
    doc_id, window_id = opened["documentId"], opened["windowId"]
    original_view = c.call("window.getView", {"windowId": window_id})
    keys = ["appearance.theme", "editor.fontSize", "editor.lineHeight"]
    original_settings = c.call("settings.get", {"keys": keys})["values"]
    closed = False
    try:
        # In front of the terminal the demo runs from, or it plays behind it.
        c.call("window.focus", {"windowId": window_id})
        d.view(window_id, mode="visual")
        d.wait()

        d.say("一节一节写入正文：标题栏会显示“Typedown 演示 编辑了 …”，新文字淡出高亮，写到屏幕外时画面跟过去")
        for section in SECTIONS:
            print("    + " + section.split("\n", 1)[0])
            d.replace_text(doc_id, MORE, section + "\n\n" + MORE)
            d.wait(0.6)
        d.replace_text(doc_id, "\n\n" + MORE, "")
        d.wait()

        d.say("替换措辞：把“脚本”都改成“演示脚本”，先数出现次数，次数对上才改")
        result = d.replace_text(doc_id, "脚本", "演示脚本")
        print(f"    替换后版本 {result['revision']}")
        d.wait()

        d.say("拿旧版本号写入会被拒绝：你在程序读取之后输入的字不会被覆盖")
        stale = result["revision"] - 1
        try:
            c.call("document.replaceText", {"documentId": doc_id, "baseRevision": stale, "find": "演示",
                                            "replacement": "示范", "expectedCount": 1})
            print("    意外：旧版本号的写入被接受了")
        except TypedownError as e:
            print(f"    被拒绝：{e.kind}（拿的是版本 {stale}，文档已在版本 {result['revision']}）")
        d.wait()

        d.say("撤销刚才的替换，再重做")
        doc = d.get(doc_id)
        undone = c.call("document.undo", {"documentId": doc_id, "baseRevision": doc["revision"], "reveal": "document"})
        print("    已撤销")
        d.wait()
        c.call("document.redo", {"documentId": doc_id, "baseRevision": undone["revision"], "reveal": "document"})
        print("    已重做")
        d.wait()

        d.say("读取标题结构，并打开侧栏的大纲")
        for h in d.get(doc_id, "headings")["headings"]:
            print("    " + "  " * (h["level"] - 1) + h["text"])
        d.view(window_id, sidePane={"open": True, "page": "outline"})
        d.wait()

        d.say("切到源码模式")
        d.view(window_id, mode="source")
        d.wait()
        d.say("切到阅读模式")
        d.view(window_id, mode="reading")
        d.wait()
        d.say("回到可视模式，打开专注模式和打字机模式")
        d.view(window_id, mode="visual", focusMode=True, typewriter=True)
        d.wait()
        d.view(window_id, focusMode=False, typewriter=False)

        if not original_view["maximized"]:
            d.say("改变窗口大小")
            b = original_view["bounds"]
            d.view(window_id, bounds={"x": b["x"], "y": b["y"], "width": max(800, b["width"] * 3 // 4),
                                      "height": max(600, b["height"] * 3 // 4)})
            d.wait()
            d.view(window_id, bounds=b)

        d.say("改设置，立即生效：深色主题、字号 20、行高 2.0")
        d.set_setting("appearance.theme", {"kind": "builtIn", "id": "dark"})
        d.wait(0.6)
        d.set_setting("editor.fontSize", 20)
        d.wait(0.6)
        d.set_setting("editor.lineHeight", 2.0)
        d.wait(1.5)

        d.say("保存并关闭演示文件" if not keep else "保存演示文件")
        saved = c.call("document.save", {"documentId": doc_id})
        print(f"    已保存，版本 {saved['revision']}")
        if not keep:
            c.call("document.close", {"documentId": doc_id})
            closed = True
    finally:
        # Put back what the demo changed, also after an error or Ctrl+C.
        for key in keys:
            try:
                d.set_setting(key, original_settings[key])
            except TypedownError as e:
                print(f"恢复设置 {key} 失败：{e}", file=sys.stderr)
        try:
            restore = {k: original_view[k] for k in ("mode", "sidePane", "statusBar", "focusMode", "typewriter")}
            d.view(window_id, **restore)
        except TypedownError as e:
            print(f"恢复显示方式失败：{e}", file=sys.stderr)
    if closed:
        os.remove(path)
    print("\n演示结束，设置和显示方式已恢复" + ("" if closed else f"；演示文件留在 {path}"))


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--pause", type=float, default=2.5, help="seconds between steps (default 2.5)")
    parser.add_argument("--step", action="store_true", help="wait for Enter before each step")
    parser.add_argument("--keep", action="store_true", help="leave the demo file open and on disk")
    parser.add_argument("--endpoint", help="named pipe name (Windows)")
    parser.add_argument("--socket", help="Unix domain socket path (default: the current user's)")
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    try:
        socket_path = None if os.name == "nt" else (args.socket or default_socket())
        client = Client.connect(args.endpoint, socket_path)
    except OSError as e:
        print(f"连不上 Typedown（{e}）：确认它在运行，并在 设置 → 通用 里打开了“允许本机自动化”", file=sys.stderr)
        return 3
    try:
        run(Demo(client, args.pause, args.step), args.keep)
    except TypedownError as e:
        print(f"typedown: {e}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("\n已中断", file=sys.stderr)
        return 130
    finally:
        client.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
