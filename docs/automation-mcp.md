# MCP 服务器：typedownctl mcp

`typedownctl mcp` 是一个 MCP 服务器（stdio），让支持 MCP 的 AI 工具（例如 Claude Code、Claude Desktop）读写你在 Typedown 里打开的文档。它走的是本机自动化接口（见 [automation.md](automation.md)），所以先在 **设置 → 通用 → 允许本机自动化** 里打开开关。有程序连着时，窗口标题会显示；AI 每改一次，标题会写明是哪个 AI 改了哪个文档，改动的文字会短暂高亮。

## 注册

Windows 安装版把 `typedownctl.exe` 装在 Typedown 的安装目录里（默认 `C:\Program Files\Typedown\typedownctl.exe`）；MSIX 包（含 Microsoft Store 版）装好后，在终端里直接输入 `typedownctl` 即可，下面的命令把路径换成 `typedownctl` 就行（`claude mcp add typedown -- typedownctl mcp`）<!-- linux -->；Linux 版（deb 包）的命令是 `/usr/bin/typedownctl`<!-- /linux -->。

Claude Code：

```
claude mcp add typedown -- "C:\Program Files\Typedown\typedownctl.exe" mcp
```

<!-- linux -->
Linux：

```
claude mcp add typedown -- typedownctl mcp
```

<!-- /linux -->
其他 MCP 客户端（它们 JSON 配置里的 `mcpServers`<!-- linux -->，Linux 上 `command` 写 `typedownctl`<!-- /linux -->）：

```json
{
  "mcpServers": {
    "typedown": { "command": "C:\\Program Files\\Typedown\\typedownctl.exe", "args": ["mcp"] }
  }
}
```

从源码运行（开发时）：`dotnet publish Tools/Typedown.Cli -f net8.0 -o <dir>`，然后命令是 `dotnet <dir>/typedownctl.dll mcp`。

`--endpoint NAME` 连接另一个端点（自动化测试宿主把它的端点名写在 `automation-endpoint.txt`）。

## 工具

| 工具 | 作用 |
| --- | --- |
| `typedown_list_documents` | 打开的文档：id、窗口、路径、标题、版本号、是否已保存、是否正在显示 |
| `typedown_read_document` | 当前正文（包括还没上报的输入）、版本号、内容哈希；可选标题结构 |
| `typedown_replace_text` | 把 `find` 换成 `replacement`，只在它在 `baseRevision` 时正好出现 `expectedCount` 次（默认 1）才执行 |
| `typedown_replace_document` | 替换整篇正文，只在文档仍是 `baseRevision` 时执行 |
| `typedown_save_document` | 保存到文档原有的文件（未命名文档不行） |
| `typedown_close_document` | 关闭一个已保存的文档；有未保存更改时拒绝，什么都不丢 |
| `typedown_get_view` | 窗口的模式、侧栏、状态栏、专注/打字机模式和屏幕位置大小 |
| `typedown_set_view` | 改变上面几项；模式等是你自己的设置，工具说明要求 AI 只在你要求时改、用完改回去 |

写入可以带 `save`（顺便保存）、`reveal`（把文档切到前台）、`scrollToChange`（切到前台，并把改动处滚进视野，光标不动；你看着它改稿时用）和 `allowFormattingChanges`（接受可视编辑器首次编辑时可能改写格式的正文）。

失败时返回 `isError: true`，`structuredContent` 是 `{ "error": <接口错误，和 typedownctl --json 输出的完全一样>, "next": "<下一步该怎么做>" }`。遇到 `revision_conflict` 或 `match_count_mismatch` 时什么都没写，AI 必须重新读取；服务器不会拿旧内容重试。
