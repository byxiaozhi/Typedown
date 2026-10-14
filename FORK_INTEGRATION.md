# Fork 整合记录：上游 PR 与社区 fork 合并

- 整合分支：`integrate-upstream-prs`（基于 `main` = 上游 `main` a1baeae）
- 日期：2026-09-16
- 前端 `Dev/Typedown.Editor` 已在 Linux 上 `yarn install && yarn build` 验证通过；C# 部分由 GitHub Actions（`.github/workflows/build.yml`）在 windows-2022 上编译打包，产物已在 Windows 11 26100 上实测可用。

---

## 一、已合并的上游 PR

| 顺序 | PR | 内容 | 解决 issue | 冲突处理 |
|---|---|---|---|---|
| 1 | #77 | `$` 货币符号不再误判为行内公式 | #75 | 无 |
| 2 | #78 | 打印崩溃修复（WebView2 打印预览替代 WinForms PrintDialog） | #42 #60 #31 | 无 |
| 3 | #79 | Mermaid 8.13 → 11.16 | #52 #53 | 无 |
| 4 | #64 | 葡萄牙语翻译修订 | #22 | 无 |
| 5 | #83 | RTL 文字方向（自动/LTR/RTL）+ 字体族设置（系统字体 AutoSuggestBox） | #47 #10 #34 #62 | 无 |
| 6 | #67 | 自定义字体 + 76 种语言资源 | 同上 | **与 #83 重复定义 `FontFamily`**：删除了 #67 的 `SettingsViewModel.FontFamily` 属性、`EditorSetting.xaml` 的 TextBox 项、`EditorViewModel` 重复字段、en/zh-Hans/ar 三个 resw 里的重复 key；保留 #83 的实现和 #67 的其他语言翻译 |

> ⚠️ 重要：#78 和 #79 的 base 是上游 **`1.2.19` 分支**而不是 `main`，因此合并它们时**把 1.2.19 分支的 4 个提交一并带入**了：
> - ARM64 平台支持
> - 版本号 1.2.19
> - 去除 WPF/WinForms 依赖，剪贴板/文件操作改为 WinRT 异步 API
> - `SelfContained=true`
>
> 这意味着整合分支实际基线是 `1.2.19`（仍是 .NET Core 3.1，**不是** net9.0 分支）。1.2.19 唯一的功能退化（打印 NotImplemented）已被 #78 补上。

## 二、已合并的社区 fork

| 来源 | 内容 | 解决 issue | 处理 |
|---|---|---|---|
| **PixelMay/Typedown** `main`（2026-08-23） | ① 文件被外部修改后自动重新加载（`FileSystemWatcher` + 400ms 去抖 + 忽略自身写入），设置里有"自动重载"和"重载前询问"开关；② **非打包（Unpackaged）运行支持**：WebView2 用户数据目录、`Microsoft.UI.Xaml.dll/.pri` 复制到输出目录、`Tools/Installer/Typedown.iss` Inno Setup 安装脚本 | #80（刷新）、#4 #15（exe/portable 包） | csproj 三处冲突手动解决（保留 1.2.19 的 TargetPlatformVersion 与版本号，加入 `System.Reactive` 和 `GeneratePathProperty`）。**去掉了该 fork 的品牌化改动**：README、About 页 "ziyezm" 声明、版本号 1.0.0、安装包命名 |
| **marksk1/Typedown** `main`（2026-09-06） | `Config.AllowOutboundNetwork = false`：默认禁止向作者服务器 `typedown.ownbox.cn` 发送崩溃报告和反馈，隐藏"发送反馈"按钮 | 隐私；作者服务器状态未知 | cherry-pick，无冲突。如需恢复上报改 `Dev/Typedown.Core/Config.cs` 一行 |
| **shanteshpatil/Typedown** | 仅取 `Dev/Typedown.Editor/.eslintignore`（排除第三方库的 lint） | CI 场景 `CI=true` 构建不被 warning 打断 | 其 GitHub Actions 工作流 **从未构建成功**（5 次全失败），未采用 |

## 三、评估后**未**合并的 fork（供参考）

97 个 fork 中，实际有独立提交的约 30 个，多数只是同步了上游 `1.2.19` 分支或无意义改动（如 `novembbbbber` 提交了一个贪吃蛇）。有价值但未合并的：

| 来源 | 内容 | 未合并原因 |
|---|---|---|
| **fegyenc/Caret**（14 commits，2026-08-21） | 改名 "Caret" 的 **WinUI 3 移植**：net8.0-windows、新建 `Typedown.WinUI` 项目、**AutoBackup 崩溃恢复**（对应 #56）、窗口位置记忆、Mica、多窗口、MSIX 打包文档 | 全新项目结构，与现有 WinUI 2 代码不兼容，是"重写"而非"补丁"。若未来决定迁 WinUI 3，这是最完整的参考 |
| **awarson2233/Typedown** `winui3-migration`（138 commits，300 文件，3 star） | 另一套 WinUI 3 迁移，含 DI、架构测试、治理文档；另有 `avalonia-migration` 分支（PR #69） | 同上，规模更大，看起来是 AI 辅助生成的大量文档+重构 |
| **yin-hai-bo/Typedown** `custom-style`（10 commits） | 预览/HTML 导出的**文档主题**、"自定义样式"对话框（对应 #30 #12）；但同时改了 App ID、显示名、本地配置路径，并把语言裁剪到只剩 en/zh-Hans | 与品牌/本地化改动纠缠在一起，需要手工拆分 |
| **shanteshpatil/Typedown**（其余部分） | Sepia/Forest/Ocean/Midnight 四套配色主题 + 8 种字体下拉（对应 #12） | 字体通过 **Google Fonts 在线加载**（离线不可用），且与已合入的 FontFamily 设置冲突；主题 CSS 可单独拆出来用 |
| **sharply-x/Typedown** `feature/multi-tab`（PR #82，13 commits） | 多标签编辑器（对应 #30 #73 #85） | 2735 行，作者自行关闭 PR；需要先在 Windows 上验证是否可用 |
| **fenfarm/Typedown**（2026-09-15） | 另一个自动重载实现（窗口激活时检查 mtime）+ 状态栏"复制路径/打开所在文件夹" | 自动重载已由 PixelMay 覆盖（更完整）；状态栏两个小功能可以之后单独移植（约 40 行） |
| **seanGSISG/Typedown** | 把 `PowerShellService`（图床上传，#58/#9/#25 的 NotImplemented）替换为硬编码的 Lensdump 上传 | 硬编码个人相册 ID，不通用；但说明了修复入口在 `Dev/Typedown/Services/PowerShellService.cs` |
| **rpsnoopy/Typedown** | ".NET 8 集成与命令行参数"，300 文件 | 两个大提交无细节说明，风险高 |
| **zhoujianwen** `codex/compile-typedown-to-exe` | 登录/git 历史/评审/锁定的服务桩 | 无实质实现 |
| **Gerammer/Typedown_Gmix** | 改了几个主题 CSS | 个人样式偏好 |
| **DavidBerdik/Typedown** `my-updates` | = 1.2.19 + #78 + #79 | 已通过 PR 合入 |

## 三之二、CI 与运行时修复（2026-09-16 ~ 09-18）

把合并结果放到 GitHub Actions 上编译、在 Win11 上实测后修掉的问题，其中前四个是**上游代码本来就有的**，只是在作者的 VS 环境里被掩盖：

| 问题 | 现象 | 修复 |
|---|---|---|
| `Typedown.csproj` Statics `Link` 多一个 `\` | MakeAppx 报 `0x8007007B` 拒绝 `Statics\\index.html` | 去掉多余反斜杠 |
| manifest 声明 `uz` 语言但 MakePri 静默跳过 | MSIX 安装报 "UZ 不是有效的语言" | 移除 `uz` 资源 |
| VCRTForwarders `UseDebugCRT` 任务依赖 sln 注入的元数据 | 直接构建 wapproj 报 MSB4044 | Release 下设 `VCRTForwarders-IncludeDebugCRT=false` |
| Win2D 原生 DLL 只有 `runtimes/win10-x64`，wapproj 用 `win-x64` 发布 | Win11 启动即崩（Mica 需要 Win2D，`0x8007007E`） | 直接引用 Win2D.uwp 并显式复制 `Microsoft.Graphics.Canvas.dll`；`EnableMicaEffect` 加 try/catch |
| `PublishTrimmed=true` 在 1.2.19 的 `SelfContained=true` 下真正生效 | 反射相关代码被裁掉 | 关闭裁剪 |
| PR #83 在 UWP 类库里用 `System.Drawing` 枚举字体 | 打开"编辑器"设置页崩溃 | 改用 Win2D `CanvasTextFormat.GetSystemFontFamilies()` |
| 崩溃信息只发到作者服务器 | 无法诊断 | 写本地日志 `%LOCALAPPDATA%\Typedown\logs` |

CI 产物：`Typedown-windows-x64-v*.exe`（Inno Setup 安装包）、`Typedown-portable-x64-v*.zip`、`Typedown.Package_*_x64.msix`、签名证书 `.cer`。推 `v*` tag 自动发布 GitHub Release。签名证书存于仓库 secrets（`TYPEDOWN_PFX_BASE64` / `TYPEDOWN_PFX_PASSWORD`），本地备份 `~/typedown-signing/`。


## 三之三、功能与性能改进（2026-09-18 ~ 09-19，v1.2.20 之后）

用 `Tools/EditorBench`（headless Chrome + stub 宿主）实测并回归验证，C# 部分由 CI 编译。

| 上游 issue | 问题 | 处理 |
|---|---|---|
| #7 | 大文档每键 60–100 ms | 去掉每键 React 提交（避免 React 遍历整个 contenteditable 保存选区）、`getBlock` 加缓存、`SelectionChange` 去重、大文档字数统计节流；每键 ~85 ms → ~40 ms（含 ~20 ms 测试工具开销） |
| — | 打开 80k 文档强制 3 次全文布局，启动后 `ThemeChanged` 再渲染一遍 | 样式 effect 先于内容渲染；主题未变且无 mermaid 时不重渲染 |
| #23 | 撤销历史 100 份全文快照、每键 Trim 拷贝 | 总量上限 32M 字符；无分配比较 |
| #56 | 断电后文件清空 | `SafeFile.WriteAllTextAtomicAsync`：临时文件 + flush + `File.Replace` |
| #1 | 列表被改写成 2 空格缩进、项间加空行 | 设置：列表缩进（1/2/4 空格、4 空格标准）、宽松列表开关 |
| #24 #70 | 表格自动补空格对齐 | 设置：对齐表格列（关闭即紧凑） |
| #20 #61 | 切换源码模式末尾不断加空行 | `addCursorToMarkdown` 空行光标改放到最近内容行，不再追加行 |
| #26 | 单行 `$$...$$` 不渲染 | 新块规则 `multiplemathSingleLine`，导出保留单行形式 |
| #41 | 大纲显示 HTML 标签/Markdown 标记 | `getHeadingPlainText` |
| #59 #2 | 大纲单击不跳转 | `TreeView.ItemInvoked` 总是跳转 |
| #51 | ↑ 键光标移出视口不滚动 | 光标 y < 100 时向上滚动 |
| #17 | `../` 相对路径图片不显示 | `resolveLocalPath` 处理盘符/UNC 前缀（path-browserify 只认 POSIX） |
| #48 | 导出 HTML/PDF 时 `(*"Share"*)` 斜体错乱 | 导出用的 marked em 规则允许 `*"`，与编辑器/CommonMark 一致 |
| #44 | 新建文件空白无法输入 | `LoadFile` 消息改为对象（另一会话修复） |
| — | 保存期间编辑被误标已保存、保存并发 | 保存串行化 + 快照哈希（另一会话修复） |

仍未处理：#63 撤销粒度（C# 侧 3 秒/换行提交策略）、#16 表格编辑闪退、#44 偶发启动空白、图床 PowerShell 未实现（#58）、多级引用 #43、#29 大纲跳转含行内代码。


## 三之四、功能请求实现（2026-09-22）

| 上游 issue | 功能 | 位置 |
|---|---|---|
| #11 #65 | 全屏（F11 / 视图菜单），隐藏标题栏和菜单栏 | `UIViewModel.IsFullScreen`、`MainWindow.SetFullScreen` |
| #45 | 隐藏段落左侧的 P/H1 标记 | 设置 → 编辑器 → 段落标记 |
| #38 | 阅读模式（Ctrl+Shift+R / 视图菜单） | `Settings.ReadOnly`，编辑器 contenteditable + 宿主命令门禁 |
| #28 | 开始菜单搜 "markdown" 可找到 | exe FileDescription、快捷方式 Comment |
| #3 | .txt / .text 出现在"打开方式" | MSIX 支持类型、安装包 OpenWithProgids |
| #12 | 纯黑主题 | `AppTheme.Black` + `theme/*/black.theme.css` |
| #33 | 文件夹范围全文搜索 | 侧边栏搜索面板 `SearchPane` |
| #85 | 侧边栏默认页（文件/大纲） | 设置 → 外观 |
| #35 | 大纲全部展开/折叠、定位时自动展开 | `TocPage`、`TocTreeItem.ExpandToSelected` |
| #8 | 图片目录变量 `${filename}` `${filedir}` `${year}`… | `ImageAction.ExpandPathVariables` |
| #43 | 增加/减少引用层级 | 段落菜单 |
| #40 | 输入 `</` 自动补全闭合标签 | `inputCtrl.js` |
| #50 | 记住每个文件的光标位置 | `CursorMemory`（`cursors.json`） |
| #30 | 自定义 CSS（编辑器 + 导出） | 设置 → 外观 |
| #57 | 拼写检查开关（Chromium） | 设置 → 编辑器 |
| #30 #73 #85 | **多标签页** | `TabsViewModel`、`DocumentTab`、`DocumentTabBar`（WinUI TabView） |

多标签设计：单个编辑器实例；切换标签时把编辑器状态（文本、哈希、光标、撤销历史）快照到 `DocumentTab`，再通过常规 `LoadFile` 路径恢复目标标签。已打开的文件再次打开只切换标签；新建/打开会复用干净的"未命名"标签。Ctrl+W 关闭当前标签（最后一个则关闭窗口），Ctrl+Tab / Ctrl+Shift+Tab 切换。已知限制：后台标签不做自动备份和外部修改监视（切回时检查一次）；重启不恢复标签。

未做：图床（#58 #9 #25，按要求跳过）、#66（需求不明确）、#46（源码模式本来就有行号）。

## 四、本地分支与 remote

```
本地分支：main, integrate-upstream-prs, pr-18/64/67/77/78/79/82/83
remotes：origin(flintt) upstream(byxiaozhi) fork-fenfarm fork-PixelMay fork-shanteshpatil fork-marksk1 fork-yin-hai-bo fork-caret
```

## 五、下一步建议

1. 在 Windows 上打开 `Typedown.sln` 编译 `integrate-upstream-prs`，重点验证：打印（#78）、外部修改重载（PixelMay）、字体/RTL 设置页（#83）、ARM64/x64 SelfContained 发布。
2. 验证通过后合到 `main` 并推送到 `origin`。
3. 仍未解决且无现成代码的高优先级问题：断电丢文件 #56（Caret 的 AutoBackup 思路可参考）、打开即改写源码 #1/#24/#20、撤销异常 #63、`$$` 行间公式 #26、图床脚本 NotImplemented #58。
