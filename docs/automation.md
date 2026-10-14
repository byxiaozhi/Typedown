# 本机自动化

Typedown 可以让**本机上以你的身份运行的程序**读取和编辑已经打开的文档：脚本批量改一处措辞、AI 助手在你看着的时候改稿、测试工具驱动编辑器。它默认关闭。

## 打开和关闭

**设置 → 通用 → 允许本机自动化**。关闭时接口不存在；打开后才开始监听。再次关闭会立即断开所有已连接的程序。

打开后，只要有程序连着，窗口标题末尾会显示“自动化已连接”；程序每改一次文档，标题会短暂显示“{程序名} 编辑了 {文档}”。被改动的文字会淡出高亮约 2 秒，可以在 **设置 → 通用 → 高亮程序所做的更改** 里关掉高亮，但标题提示不能关，程序也不能替你关。

## 能做什么，不能做什么

程序可以：

- 列出打开的窗口和文档，读取正文（包括你刚输入、还没保存的内容）和标题结构；
- 打开文件、新建文档、切到某个文档，关闭已保存的文档；
- 按文本替换或整篇替换正文，撤销、重做，保存到文档原有的路径；
- 切换窗口的显示方式：可视、源码或阅读模式，侧栏（文件或大纲），状态栏，专注和打字机模式，窗口位置和大小——和你点“视图”菜单一样；
- 读写 30 个设置：主题、字号、行高、文字方向、界面语言、编辑器与 Markdown 输出的选项、拼写检查、图片路径选项，以及（Windows）紧凑模式、Mica、动画和关闭后保持运行。改了立即生效，不用重启。

程序不能：

- 选择保存位置（没有“另存为”），关闭窗口；关闭有未保存更改的文档（会被拒绝，什么都不丢，要关先保存）；
- 读写其他设置，包括密码、自定义 CSS、文件夹路径、这两个自动化开关本身；
- 通过网络访问：接口只在本机，Windows 上是只允许你这个用户打开的命名管道<!-- linux -->，Linux 上是只有你能进入的目录里的 socket（`$XDG_RUNTIME_DIR/typedown/automation.v1.sock`，权限 0600）<!-- /linux -->。

每次写入都要带上程序读到的**版本号**。你在程序读取之后又输入了内容，它的写入会被拒绝，它必须重新读取；不会有旧内容覆盖你刚写的字。可视编辑器可能改写格式的正文（例如 setext 标题）默认也会被拒绝，除非程序明确表示接受；可视编辑会丢失内容的正文（例如部分原始 HTML）一律拒绝。

## 安全边界

这个开关信任的是**你这个系统用户下运行的所有程序**：打开后，任何以你的身份运行的程序都能连上并改你打开的文档。它没有逐个程序的授权，也不是插件沙箱。只在你信任本机上运行的软件时打开；不用时关掉。

程序连接时自报的名字只用于显示，Typedown 无法核实。

## 命令行：typedownctl

Windows 安装版把 `typedownctl.exe` 装在 Typedown 的安装目录里（默认 `C:\Program Files\Typedown\typedownctl.exe`），这些说明文档装在它旁边的 `docs` 文件夹。<!-- linux -->Linux 版（deb 包）的命令是 `typedownctl`（`/usr/bin/typedownctl`），文档在 `/opt/typedown/docs`。<!-- /linux -->

```
typedownctl status
typedownctl documents
typedownctl get <documentId> --latest --text
typedownctl replace-text <documentId> --base-revision 3 --find "旧说法" --replacement "新说法" --expected-count 1 --save
typedownctl replace-text <documentId> --base-revision 4 --find "第二章" --replacement "第 2 章" --expected-count 1 --reveal-change
typedownctl replace <documentId> --base-revision 5 --stdin < new.md
typedownctl view <windowId> --mode source --side-pane outline --size 1280x860
typedownctl close <documentId>
typedownctl settings set ui.language ja --base-revision 12
typedownctl settings set appearance.theme dark --base-revision 13     # 或 custom:sepia；也可写完整 JSON
```

`--reveal` 把文档的窗口和标签切到前台；`--reveal-change` 还会把改动处滚进视野（已经在屏幕上就不滚，光标不动），适合你看着程序改长文档的时候。

`view` 返回时新视图已经画在屏幕上，可以直接截图——做说明书插图时不用重开程序。

加 `--json` 时标准输出只有一个 JSON 值，适合脚本。退出码：0 成功，3 Typedown 没运行或开关没开，5 文档不存在，6 版本或匹配次数冲突（重新读取后再试），7 编辑器未就绪，8 保存失败。完整说明见 `typedownctl help`。

## AI 助手：typedownctl mcp

`typedownctl mcp` 是一个 MCP 服务器，支持 MCP 的 AI 工具（例如 Claude Code）通过它读写你打开的文档。注册方法见 [automation-mcp.md](automation-mcp.md)。遇到冲突时它会让 AI 重新读取，不会拿旧内容重试。

## 自己写程序

协议是 JSON-RPC 2.0，按 Content-Length 分帧，规格见 [automation-api-spec.md](automation-api-spec.md)，参数和结果的 JSON Schema 见 [automation-schema/v1.json](automation-schema/v1.json)。[automation-examples](automation-examples/) 里有 Python 和 PowerShell 的直接客户端示例。

v1 接口已经冻结：v1 内只会新增方法和可选参数，不会删除、改名或改变已有行为，详见规格第 6 节。

## 目前的限制

- 微软商店版（MSIX）不带 `typedownctl`。
- 保存、撤销和重做只作用于窗口当前显示的文档；后台标签先用 `document.focus` 切过去。
