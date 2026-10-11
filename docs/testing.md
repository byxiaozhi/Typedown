# 测试

Typedown 的测试分几层，越往下越接近真实使用，也越慢、越依赖环境。改动落在哪一层，就至少跑那一层；发布前按 [Windows 真机验证](windows-verification.md) 走一遍。

| 层 | 在哪里 | 测什么 | 运行环境 | CI |
| --- | --- | --- | --- | --- |
| 可靠性单元测试 | `Tests/Typedown.ReliabilityTests` | 设置文件、SQLite 迁移、原子写入与备份、安装清单和架构 | Windows（.NET 9） | 每次构建 |
| 自动化单元测试 | `Dev/Typedown.Automation.Tests` | 自动化协议、方法表、文档编辑与冲突、CLI、MCP、示例客户端、屏障、应用与测试宿主分离、S3 签名、上传记录 | Windows 或 Linux（.NET 9） | 否，本地跑 |
| 编辑器单元测试 | `Dev/Typedown.Editor/src/**/*.test.*` | 编辑器前端的纯逻辑 | Node 20（jest） | 每次构建 |
| 编辑器集成检查 | `Tools/EditorBench/*-check.js` | 构建好的编辑器在无头 Chrome 里加载、切换模式、编辑后源码是否被改写 | Node + Chrome | 下面列出的几项 |
| Windows 端到端测试 | `Tools/AutomationE2E` | 真实窗口、真实按键、WebView2、多窗口、保存、主题、图片上传等，通过自动化接口和 UI Automation 驱动 | Windows 桌面会话 | 否，本地跑 |
| 发布候选检查 | `Tools/AutomationE2E/windows-rc-check.ps1` | 已安装的程序：直接客户端、`typedownctl`、`typedownctl mcp`、安装目录里的示例 | 装好待发布版本的 Windows | 否 |
| 人工验证 | [windows-verification.md](windows-verification.md) | 自动测试覆盖不到的：安装升级、文件锁、输入法、真实文件 | Windows | 否 |

`Tests/Typedown.Test`、`Tests/Typedown.UITest`、`Tests/Typedown.Universal.Test` 是上游留下的 .NET Core 3.1 项目，目前不维护，也不在 CI 里运行。

Uno 版（Typedown-Uno 仓库）有自己的检查脚本，见那边的 `docs/testing.md`；两边共用的自动化等价场景在 `Tools/AutomationE2E/equivalence`。

## 单元测试

```bash
dotnet test Tests/Typedown.ReliabilityTests/Typedown.ReliabilityTests.csproj -c Release
dotnet test Dev/Typedown.Automation.Tests/Typedown.Automation.Tests.csproj
```

```bash
cd Dev/Typedown.Editor
yarn install --frozen-lockfile
CI=true yarn test --watchAll=false --runInBand
```

## 编辑器集成检查

先构建编辑器（`Dev/Typedown.Editor` 里 `node scripts/copy-mermaid.js`，再 `yarn build`），产物在 `Dev/Typedown/Resources/Statics`。然后：

```bash
cd Tools/EditorBench
npm ci
export CHROME=/opt/google/chrome/chrome      # 或者 Chrome 的其他位置
NODE_PATH=../../Dev/Typedown.Editor/node_modules node handshake-check.js
npm run check:source                          # 自动化写入依赖的源码映射检查
```

每个检查脚本开头的注释说明它防的是哪一个问题；`STATICS=<目录>` 可以换一份编辑器构建。性能基准和其余检查见 [Tools/EditorBench/README.md](../Tools/EditorBench/README.md)。

CI 的“Test editor integration”一步运行这些：

<!-- BEGIN generated: editor-checks -->
- `handshake-check.js`: Verifies the LoadFile -> FileLoaded handshake: FileLoaded must carry the original source text and the load's id, nothing for a superseded load may leak through, and no MarkdownChange precedes FileLoaded.
- `reading-source-check.js`: Regression (source-text mapping stability): viewing a document must never rewrite its source.
- `mermaid-lazy-check.js`: mermaid.min.js (3.5 MB) loads only when a document draws a diagram (public/lazy-mermaid.js): a document without one never requests it, a document with one requests it exactly once and the diagram is drawn.
- `mermaid-edit-check.js`: Rapid Mermaid edits used to start overlapping renders against one shared queue.
- `mermaid-typed-check.js`: A diagram written by hand in visual mode: typing ```mermaid and Enter must give the diagram block with its preview (not a plain code block that only became a diagram after a tab or mode switch reloaded the document), and the caret must be able to leave it - by clicking the paragraph below, which the floating preview used to cover, by clicking above, or with the arrow keys - after which the new diagram is shown.
- `security-check.js`: Raw HTML and rich renderers belong to Markdown, but they must never become script in the trusted editor page.
- `reading-copy-check.js`: Copying in reading mode, and copy as plain text.
- `paste-web-check.js`: HTML pasted from web pages and online editors, as Windows hands it to the page (Paste: text and HTML; HTML may carry the clipboard's CF_HTML header with the page's SourceURL), through the real paste path: tidied, sanitized, turned into Markdown in the editor's own style.
- `export-mermaid-error-check.js`: A document with a Mermaid diagram whose text Mermaid cannot read, rendered as the exports render it (RenderXhtml, the path of the PDF, HTML and other exports).
- `renderer-failure-check.js`: What the page does when a part of it fails: a renderer that cannot be loaded (Mermaid's script refused), and an error nothing catches.
- `spec-check.js`: Drives every CommonMark 0.31.2 example through the editor and locks what comes back out.
<!-- END generated: editor-checks -->

## Windows 端到端测试

端到端测试启动一个**自动化测试宿主**，用一个驱动程序通过本机自动化接口和 UI Automation 操作它，逐个用例检查结果。

### 测试宿主

测试宿主是带测试接口的应用构建：

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Installer\build-local.ps1 -AutomationTestHost
```

它输出到 `Dev\Typedown\bin\AutomationTestHost`，旁边有 `automation-test-host.marker`。和日常使用的程序相比：

- 多了下面列出的 `test.*` 方法和写入路径上的屏障（`Dev/Typedown.Automation.TestHost`，由 `AUTOMATION_TEST_HOST` 条件编译接入）。正式构建不引用这个程序集，方法表也拒绝 `test.` 开头的名字；`Tools/Installer/assert-application-build.ps1` 拒绝打包任何含有它的输出，`BuildSeparationTests` 在源码层面检查两者分开。
- 用 `--automation-test-root <目录>` 启动，所有数据（设置、会话、备份、单实例和管道名）都在这个目录里，不会碰到本机正在用的 Typedown。
- 永远不打安装包。

### 运行

驱动程序是 `Tools/AutomationE2E/Driver`（`dotnet build -c Release`）。端到端测试要在**已登录的桌面会话**里运行（编辑器要真的画出来、能拿到焦点）：

```powershell
# 在桌面会话里
powershell -ExecutionPolicy Bypass -File Tools\AutomationE2E\run-windows-e2e.ps1 `
  -TestHost Dev\Typedown\bin\AutomationTestHost -Driver Tools\AutomationE2E\Driver\bin\Release\net8.0-windows `
  -Artifacts <结果目录> [-Only S0,IU01]

# 从 SSH：通过“仅用户登录时运行”的计划任务在桌面会话里跑同一个脚本，等它结束并打印 result.json
powershell -ExecutionPolicy Bypass -File Tools\AutomationE2E\start-interactive.ps1 -TestHost ... -Driver ... -Artifacts ... [-Only ...]
```

- `-Only` 按用例编号挑选，逗号分隔；不写就按下表顺序全跑。`Q01` 会让测试宿主退出，所以排在最后。
- 每次运行有自己的 runId 和数据目录 `%LOCALAPPDATA%\Typedown-AutomationTests\<runId>`；结果在 `<结果目录>\<runId>\result.json` 和 `driver.log`。全部通过时数据目录删除，失败时保留，连同 `debug.log` 一起看。
- 退出码：0 全部通过，1 有用例失败，3 环境问题（没有桌面会话、屏幕锁定、测试宿主没开始监听）。
- 运行期间别动这台机器的键盘鼠标：用例会发真实按键、点击和拖放。
- `IU02` 需要一个 S3 服务：设了 `E2E_S3_ENDPOINT`（和 `E2E_S3_ACCESS_KEY`、`E2E_S3_SECRET_KEY`）就用它，否则用 `rclone serve s3` 在本机临时起一个（`E2E_RCLONE` 指向 `rclone.exe`）；都没有就失败并说明原因。

各用例的设计和运行环境要求见 [自动化方案](automation-api-analysis-plan.md) 第 10.6、10.7 节。

### 用例

<!-- BEGIN generated: e2e-cases -->
| Case | What it checks |
| --- | --- |
| S0 | open, write, read back exactly, save bytes, stale write refused |
| S1 | a real keystroke is an edit of the reader: revision advances, the text is in |
| R01 | a write held after its flush while the reader switches tabs never writes the other document |
| R02 | undo and redo stay in their own document |
| R03 | a keystroke while a saving write is held: the saved file is the written revision, the keystroke is kept |
| B01 | settings: one window's change reaches the other window; window modes stay with their window |
| S2 | settings.set reaches every window and the settings file; a stale revision is refused |
| S3 | the newer settings: language, tab size, compact mode and word count reach the window and go back |
| V01 | window.setView: source mode with the outline in a restored 1100x720 window, then reading; a mode switch waits for a held write |
| N01 | back from the settings page (the editor page loads again), a file opened at once is read and written without a timeout |
| K01 | a font size changed through the API while the reader types: the keys still reach the page |
| B02 | an open settings page shows a font size changed from elsewhere |
| F01 | a written document with protected payloads keeps every one of them through a real first keystroke |
| M01 | a written text survives visual, reading and source mode switches byte for byte, then saves exactly |
| P01 | awaitPresentation: a revealed write in a background tab of a minimized window reports it drawn; it needs reveal |
| W01 | the editor page reloads after it applied a write and before the commit: nothing is committed, host and page agree |
| W02 | a mode switch while a write is held after the page applied it: the write commits and every mode shows it |
| W03 | the tab switched away and back while a write is held: refused and restored; switched away only: committed into the tab |
| MC01 | typedownctl mcp as its own process: read, replace text with reveal, a stale revision is a conflict that says to read again |
| EX01 | the PowerShell and Python client examples edit through the real pipe |
| EQ01 | the equivalence scenario (compared with the Uno edition's answers offline) |
| R04 | untitled and background documents come back from their backups after a kill, each its own |
| C01 | document.close: a saved document closes, an unsaved one (a fresh keystroke too) is refused, the last one leaves an empty document |
| Q02 | a window closed at once after it opened (its web view still being created): the process lives on |
| D01 | the window's UI thread runs every callback posted to it, from many threads at once |
| K02 | Ctrl+, twice opens and closes the settings: the caret and the keyboard are where they were |
| K04 | a window just opened takes the keys at once: a letter typed without a click reaches the document |
| K05 | a new tab (the + button, Ctrl+N) and a tab clicked in the strip take the keys at once, without a click in the text |
| K06 | after closing a tab, a menu command, a mode switch, an outline jump, closing the find bar or a dialog, and Ctrl+Tab, the keys go to the document without a click |
| K03 | the same with an untitled document (no per-file caret memory) |
| FS01 | in full screen the main page starts at the top edge of the screen |
| FS02 | out of full screen the window is dragged by its title bar again at once (with the title row, and in compact mode by the menu row) |
| PU01 | PlantUML is not drawn by default (nothing goes to plantuml.com, the block says so); turned on it is, by the server set if one is, off again it is not |
| RV01 | reveal: "change" scrolls a change off screen into view, the caret where it was; "document" leaves the page |
| IU01 | File > Upload local images (PowerShell): each file uploaded once, every use replaced in one undo step, web, missing and code left alone |
| IU03 | a window that has not used the upload service yet finds the configuration at once (it said none until the database was read) |
| IU02 | File > Upload local images to an S3 bucket (rclone serve s3): signed PUT, the object reads back, a wrong secret changes nothing, the secret is not stored in plain text |
| IU04 | pictures saved and uploaded under the right names: a pasted screenshot goes up as image.png, a second different picture of the same name is copied as "name (2)" and the document says so, a new upload configuration starts enabled |
| IN01 | several image files dropped at once: each in a paragraph of its own, in the drop's order, one undo step |
| VI01 | Vim keys in source mode: real keys edit (dd, A, Esc), Ctrl+V reaches Vim as block visual, u undoes, :w saves |
| VI02 | Vim keys in reading mode: G, gg, ]] and Ctrl+D move the page |
| TC01 | a tab closed with its x button: the outline shows the headings of the tab shown next |
| TC02 | a tab closed while its neighbour is not the tab used last: the outline is the shown tab's, with long documents |
| SE01 | the startup action chosen on the settings page is in the settings file |
| WP02 | pasted web pictures: one behind hotlink protection is fetched with the page as referrer, a page that is not a picture is kept on the web and said, and an untitled document saved into a folder takes its pictures along |
| EX02 | an export says where it was written, with buttons to open it, and goes when closed; with no action after export, nothing shows |
| TB01 | the table toolbar's Resize table opens with the table's own size; Cancel leaves the table as it was; OK resizes it |
| TH01 | a custom theme colours the page in visual, reading and source mode, and a change shows at once in each |
| RD01 | reading mode: the context menu offers copying and selecting only, Copy works on a selection, copy as plain text leaves the Markdown out, Ctrl+Z changes nothing |
| CP01 | Copy pasted into Word: pictures at absolute file:/// addresses, a name and an alt text with brackets, an SVG sized in pt, a JPEG |
| TH03 | the side pane marks what is chosen (the bar under Files/Outline, the outline's and the folder tree's row pill) in a custom theme's accent, and in the system accent again without one |
| TH06 | a custom theme switched back to Light or Dark: the editor page, the line beside the side pane, the side pane and the status bar are drawn as before, and the editor is dark under Dark |
| TH02 | View > Theme > Reload themes finds a new theme file and a renamed one; the window draws in the custom theme's base whatever the built-in setting says |
| WP01 | a web page pasted: headings with #, code fenced with its language and without line numbers, Google Docs bold and italic, late-loaded and relative pictures by their real addresses; with Insert web image copying, its pictures are copied beside the document and their addresses replaced |
| ER01 | a page message handler that throws stays subscribed and the message still reaches the other handlers; an exception out of an event handler XAML calls is logged and the app goes on |
| DG01 | a dialog wears the custom theme (its surface and text colours), and the system's colours again without it |
| PM01 | the Paragraph menu's checks follow the caret: Table stays unchecked after its dialog is cancelled, Quote and Table are checked with the caret in a quote and a table |
| OL02 | two long documents scrolled to their middle, switched between: the outline does not mark another heading of the document being left |
| LD01 | a load the editor gives back rewritten is loaded once more and keeps the file's text; one rewritten again is taken after that one retry, not retried without end |
| Q01 | two windows closed one after the other: the process exits (it stayed, headless) |
<!-- END generated: e2e-cases -->

### 测试接口（`test.*`）

只有测试宿主有这些方法。端到端测试用它们做两类事：一是**观察**用户看不到、正式接口也不暴露的状态（编辑器页面里实际的文字和样式、窗口布局、菜单内容）；二是**制造**真实使用中难以稳定复现的时机（让写入停在某一步，再去切换标签、打字或重新加载页面）。

调用方式和正式方法一样（同一条管道、同一套 JSON-RPC），参数里带 `?` 的可以不写。

<!-- BEGIN generated: test-methods -->
| Method | Parameters | What it is for | Where |
| --- | --- | --- | --- |
| `test.barrier.arm` | `point`, `documentId?` | Arms a one-shot barrier at a point of the write path (optionally for one document); returns its barrierId. | EditBarriers.cs |
| `test.barrier.waitHit` | `barrierId`, `timeoutMs?` | Waits until an edit is held at the barrier (hit) or the timeout passes; the held operation's id. | EditBarriers.cs |
| `test.barrier.release` | `barrierId` | Lets the held edit go on. | EditBarriers.cs |
| `test.editor.pageText` | `documentId` | What the page itself holds right now - lets a test wait for real input to arrive instead of sleeping. | TestHostHooks.cs |
| `test.dispatcher.stress` | `windowId`, `threads?`, `posts?` | The window's UI thread runs every callback posted to it, from any number of threads at once. Its dispatcher numbered posts with a plain increment: two threads could take the same number, and one callback was dropped - an await continuation that never ran (E2E C01 hung a minute with the UI thread idle). | TestHostHooks.cs |
| `test.backup.run` | - | Backups: one pass now in every window, and what the backup folder holds afterwards. | TestHostHooks.cs |
| `test.editor.style` | `windowId` | How a window's editor page is drawn: its font size, line height and text direction, as the page reports them. | TestHostHooks.cs |
| `test.editor.focus` | `windowId` | Keyboard focus into the window's editor page, the way the app itself gives it (the editor control's focus is handed on to the web view) - independent of what a click would land on. | TestHostHooks.cs |
| `test.editor.eval` | `windowId`, `script` | Runs a script in the window's editor page and returns its JSON result: for diagnosing what the page holds (caret, Muya's state) when a check fails in the real window and not in the page harness. | TestHostHooks.cs |
| `test.editor.screenPoint` | `x?`, `y?`, `windowId` | Where a point of the editor page (CSS pixels, as getBoundingClientRect gives them) is on the screen, in physical pixels: the page's coordinates are the editor control's (the floating tools are placed by the same mapping), scaled by the window's rasterization scale from its client area. The web view draws into the window without a window or an automation element of its own, so a test that clicks the page has nothing else to measure. | TestHostHooks.cs |
| `test.edition.set` | `name`, `value?`, `windowId` | A state an edition takes from outside (Edition.TestSet), set for a test; an error when the edition has no such name (Typedown has none). | TestHostHooks.cs |
| `test.edition.get` | `name`, `windowId` | A state of the edition's (Edition.TestGet), as a string; an error when the edition has no such name. | TestHostHooks.cs |
| `test.editor.reload` | `windowId` | Reloads the window's editor page, as the application does after a page error or a crashed web process. | TestHostHooks.cs |
| `test.window.handle` | `windowId` | The window's native handle (HWND), for UI Automation and window messages from the driver. | TestHostHooks.cs |
| `test.window.menuTitles` | `windowId` | The titles of the window's menu bar, as drawn: the menu follows the interface language. | TestHostHooks.cs |
| `test.window.layout` | `windowId` | Where the main page starts in its window, and whether the window is in full screen: in full screen nothing may sit above it (a 4 px row of window background did, along the top of the screen). | TestHostHooks.cs |
| `test.window.keep` | `windowId` | Holds a window in memory from now on, as anything still referring to it does after it closes: XamlWindow.AllWindows lists a window until the garbage collector has finalized it, closed or not. | TestHostHooks.cs |
| `test.window.navigate` | `route`, `windowId` | Moves a window to a page, as its menu would: route "Settings/Editor" opens that settings page, "Main" goes back. | TestHostHooks.cs |
| `test.window.open` | - | Opens another window, as File > New window does, and returns its windowId once it is registered. | TestHostHooks.cs |
| `test.settings.set` | `name`, `windowId`, `value` | Sets a property of a window's settings view model by name, as its settings page would: also the settings the automation API does not expose. | TestHostHooks.cs |
| `test.settings.get` | `name`, `windowId` | Reads a property of a window's settings view model by name. | TestHostHooks.cs |
| `test.images.configure` | `method?`, `config?`, `windowId` | Image upload without its dialogs: an enabled configuration (PowerShell or S3), chosen as Settings > Image > Upload with; returns what was stored, so a test can see that the secret is not there in plain text. | TestHostHooks.cs |
| `test.images.insert` | `paths?`, `windowId` | What a drop, paste or pick of image files does: each through the local image setting, inserted together. | TestHostHooks.cs |
| `test.images.paste` | `path`, `windowId` | A picture file pasted as a bitmap (a screenshot on the clipboard): the clipboard image action's result, the address the editor would insert - without the clipboard itself, which other programs on the machine share. | TestHostHooks.cs |
| `test.theme.apply` | `customTheme?`, `builtIn?`, `windowId` | A theme picked as the View menu picks it (a custom theme brings its base light/dark along), and what the window then draws with. | TestHostHooks.cs |
| `test.theme.menu` | `reload?`, `windowId` | View > Theme: the entries it shows, after clicking its "Reload themes" item when asked (through the item's automation peer, as an assistive tool would). | TestHostHooks.cs |
| `test.editor.rewriteLoads` | `count?`, `windowId` | The next loads of the window's editor come back rewritten, as a load that did not keep the text did once. | TestHostHooks.cs |
| `test.dialog.press` | `button?`, `windowId` | Presses a button of the dialog open in the window (primary, secondary or close), as a click would; the button's text back, with the dialog's title and its message when they are text. A test finding it through UI Automation found the window's own close button first. | TestHostHooks.cs |
| `test.images.webAction` | `action?`, `path?`, `windowId` | Settings > Image > Insert web image: kept on the web ("none"), copied to a folder ("copy", relative to the document) or uploaded ("upload"). | TestHostHooks.cs |
| `test.paste.html` | `html`, `text?`, `windowId` | Text and HTML pasted into the window's active document as from the clipboard (the paste after the clipboard is read: the page converts the HTML, then web pictures go as the setting above says); answers once done. | TestHostHooks.cs |
| `test.events.throwingHandler` | `windowId` | The window's editor messages with a handler that throws on the first one: how many each of two handlers got of two messages (a handler that threw was detached for good, and the message went no further). | TestHostHooks.cs |
| `test.app.throwUnhandled` | `windowId` | An exception out of an event handler XAML calls (a timer's, as a click handler's would be): the process stays (it ended the app), and the exception is in the log. | TestHostHooks.cs |
| `test.window.popups` | `windowId` | The popups open in a window (menus, flyouts, a number box's buttons...), by the type of what they show. | TestHostHooks.cs |
| `test.app.caseLabel` | `label?` | The running E2E case and its purpose, shown in every window's title after the test host's marker (label empty: none). Not tied to a window: a case sets it before it has one. | TestHostHooks.cs |
| `test.app.titleMarker` | `show?`, `windowId` | The test host's title markers ("AUTOMATION TEST HOST ·" and the connected client's note) on (show: true) or off in every window, for screenshots that are to look like the app. | TestHostHooks.cs |
| `test.file.saveAs` | `path`, `windowId` | The window's active document saved as the path, as File > Save as with that file picked (an untitled one's first save too); the path back, or null when it was not saved. | TestHostHooks.cs |
| `test.export.run` | `type?`, `path`, `windowId` | The window's document exported to a file, as File > Export > <type> with that file picked in the save dialog (html or pdf: the first export configuration of that type); answers once the export is asked for, not done. | TestHostHooks.cs |
| `test.export.notice` | `windowId` | The notice an export leaves (Settings > Export > After export): the file it names, or null when none shows. | TestHostHooks.cs |
| `test.export.menu` | `windowId` | The entries of a window's File > Export submenu, as shown. | TestHostHooks.cs |
| `test.dialogs.dismiss` | - | Every dialog left open in any window, closed as its close button would: the driver asks before each case, so one case's dialog (an insert-table dialog an Esc did not reach, an unsaved document's "save?") does not hold the next cases' clicks back. | TestHostHooks.cs |
| `test.dialog.colours` | `windowId` | The open dialog's colours and the custom theme's, to tell whether the dialog wears the theme. | TestHostHooks.cs |
| `test.theme.chrome` | `windowId` | The colours a custom theme changes around the editor, as they are now: the line between the side pane and the editor, the side pane, the menu bar and the status bar. A theme switched off has to put back what the built-in theme draws (TH06). | TestHostHooks.cs |
| `test.pane.accent` | `windowId` | The colours the side pane marks what is chosen with: every selection indicator drawn in it (the bar under Files/Outline, the pill of each outline and folder-tree row), by where it is, with its fill. | TestHostHooks.cs |
| `test.images.uploadAll` | `windowId` | File > Upload local images on the window's active document, without its dialogs; returns the result. | TestHostHooks.cs |
<!-- END generated: test-methods -->

屏障的位置（`test.barrier.arm` 的 `point`）：`beforeFlushReply`、`afterEditorMutationBeforeReport`、`beforeSaveCommit`，定义在 `Dev/Typedown.Automation/DocumentEdit.cs`。一个屏障只拦一次；没人放行的屏障 60 秒后自己放行，所以失败的用例不会把测试宿主卡住。

## 保持本文与代码一致

上面的用例表、测试接口表和 CI 检查列表由 `Tools/Docs/testing-index.py` 从代码生成：

```bash
python3 Tools/Docs/testing-index.py           # 文档落后于代码时失败，并给出差异
python3 Tools/Docs/testing-index.py --write   # 更新文档
```

CI 的“Check brand names”一步也运行它。新加用例时，`Case("编号 说明", 方法)` 里的说明就是表里的说明；新加 `test.*` 方法时，紧挨着它上面写一段注释说明用途，没有注释检查会失败。
