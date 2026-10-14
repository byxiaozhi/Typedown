# Typedown 上游 PR 与分支分析

- 上游仓库：https://github.com/byxiaozhi/Typedown
- 统计时间：2026-09-16
- 上游 **没有任何 tag / release**，所有发布走微软商店。
- `main` 最后一次代码提交是 **2023-04-27**（`131b03c Fix webview init fail`），之后仅 2024-03-19 改过 README。
- 上游 **没有合并过任何一个 PR**（12 个 PR，0 merged）。

---

## 一、上游分支（共 3 个）

```
main (a1baeae, 2024-03-19)
 └── 1.2.19 (c382135, 2025-09-03)          +4 commits
      └── 1.2.19-net9.0 (5e90207, 2025-09-06)  +2 commits
```

三个分支是线性关系，`1.2.19` 基于 `main`，`1.2.19-net9.0` 基于 `1.2.19`。提交者为 zhuzhifang（即作者 byxiaozhi），时间集中在 2025-08-31 ~ 2025-09-06，之后再无动作。这应该就是作者在 #42 中提到的"新版开发中"。

### `1.2.19` 分支（4 commits，18 文件，+246/-246）

| 提交 | 内容 |
|------|------|
| c06170c | 所有项目 TargetPlatformVersion 设为 10.0.19041.0 |
| 66f62a5 | 增加 **ARM64** 平台支持（csproj、wapproj、sln） |
| fa07c87 | 版本号 1.2.18 → **1.2.19** |
| c382135 | **剪贴板和文件操作重构为异步 API**：`IClipboard.GetText/GetImage/GetFileDropList` → `*Async`，`IFileOperation.Cut/CopyToClipboard` → `*Async`；实现从 `System.Windows.Clipboard`（WPF）/ `System.Windows.Forms.Clipboard` 迁移到 WinRT `Windows.ApplicationModel.DataTransfer.Clipboard` |

其他变化：
- 移除 `<UseWPF>`，改为 `SelfContained=true`（自包含发布，可能是为 exe/portable 分发做准备，对应 #4/#15）
- 新增 `System.Drawing.Common 9.0.8` 依赖
- ⚠️ **`PrintHelper.PrintPDF` 被整段注释掉，改为 `throw new NotImplementedException()`** —— 因为去掉了 WPF/WinForms 的 `PrintDialog`。也就是说作者的新分支上打印功能是**彻底不可用**的，比 main 的"崩溃"更退一步。
- `Typedown.sln` 大幅精简（-174 行，删除了大量配置组合）

### `1.2.19-net9.0` 分支（2 commits，14 文件，+159/-2017）

两个提交都只叫 "Update"，实际内容是 **.NET Core 3.1 → .NET 9 迁移**：

- `TargetFramework`: `netcoreapp3.1` → `net9.0-windows10.0.26100.0`，`UseUwp=true`，`Microsoft.Windows.CsWinRT 2.2.0`
- `Typedown.Core.csproj` 从旧式 UWP 类库项目（1780 行、逐文件 `<Compile Include>`）改写为 SDK 风格项目（约 40 行），启用 `Nullable`
- `Microsoft.EntityFrameworkCore.Sqlite` 3.1.32 → 9.0.8
- RuntimeIdentifier `win10-x64` → `win-x64`（同理 x86/arm64）
- 移除 `PdfiumViewer` 包引用（仅保留 native），`PrintHelper` 仍是 `throw new NotImplementedException()`
- 关闭 `PublishTrimmed`
- `MainWindow.EnableMicaEffect` 被硬编码为透明背景（**Mica 效果被禁用**，可能是迁移过程中 `SystemBackdropBrush` 不兼容的临时处理）
- `Program.cs` 删除 `IsExternalInit` 垫片（.NET 9 自带）
- `AppContentDialog.cs` 增加 `using` 别名解决 WinUI/WinForms 类型名冲突
- `nupkgs/Typedown.XamlUI.1.0.1.nupkg` 更新（18.3MB → 19.3MB），并多了一个 `.zip` 备份

**结论**：两个分支都是**基础设施迁移**（ARM64、.NET 9、去 WPF、异步剪贴板），**没有修复任何一个用户报告的 issue**，也没有新功能。net9 分支处于"能编译但功能有退化（打印、Mica）"的半成品状态。

---

## 二、上游 PR（共 12 个，0 合并）

### 开放中、可直接合入 fork 的 PR（全部 MERGEABLE，无冲突）

| PR | 作者 | 日期 | 规模 | 解决的 issue | 内容 |
|----|------|------|------|-------------|------|
| **#78** Fix Typedown Closing when Attempting to Print | DavidBerdik | 2026-06-29 | +159/-33, 7 文件 | **#42 #60 #31** | 删除基于 WinForms `PrintDialog` 的 `PrintHelper`，新增 `PrintPreviewControl`（WebView2 打印预览）+ `UIViewModel` 打印命令 + `RootControl.xaml` 承载控件。思路与作者 1.2.19 分支"去 WinForms"方向一致，且真正实现了功能。 |
| **#77** Fix inline math wrongly triggered by currency dollar signs | LuckyLife007 | 2026-06-24 | +2/-2, 2 文件 | **#75** | 修改 `rules.js` 与 `inlineRules.js` 的行内公式正则：开 `$` 后不能是空白，闭 `$` 前不能是空白且后面不能紧跟数字（Pandoc/KaTeX 规则）。极小改动，低风险。 |
| **#79** Update Mermaid to 11.16.0 | DavidBerdik | 2026-07-02 | +566/-327, 9 文件 | **#52 #53** | Mermaid 8.13.10 → 11.16.0，重构 `renderers/index.js` 与 `parser/render/index.js` 适配新 API，新增 `scripts/copy-mermaid.js` 拷贝资源，`exportHtml.js` 同步更新。 |
| **#67** Add support for custom fonts | neuronalism | 2026-04-04 | +618/-1, 80 文件 | **#10 #34 #62 #65 #36(部分)** | 设置 → 编辑器 → 字体 文本框，作用于 Live Preview 和源码编辑器。80 个文件中 76 个是各语言 `.resw`（AI 翻译）。 |
| **#83** Add Arabic/RTL text direction support and editor font family setting | abduosmanaj | 2026-07-25 | +284/-9, 15 文件 | **#47**, 同时覆盖 #10/#34/#62 | 按块自动检测文字方向（first-strong-character），设置项 Auto/LTR/RTL；另有 **字体族设置**（`AutoSuggestBox` 列出系统已安装字体，用 `System.Drawing.Text.InstalledFontCollection`），比 #67 的纯文本框体验更好。仅 en/ar/zh-Hans 三种语言资源。**与 #67 功能重叠，二选一或合并。** |
| **#64** Refine Portuguese translations | renpv | 2026-01-31 | +50/-108, 1 文件 | **#22** | 葡萄牙语 `CommonResources.resw` 修订。 |

### 已关闭、未合并的 PR

| PR | 作者 | 日期 | 状态 | 说明 |
|----|------|------|------|------|
| #82 feat: add multi-tab document editor | sharply-x | 2026-07-24 → 07-26 关闭 | +2735/-64, 20 文件 | 多标签编辑器（对应 #30 #73 #85）。有设计文档、实现计划、状态模型测试、tab-aware 文件操作消息、UI。作者自行关闭，原因未说明。代码仍可从 `sharply-x:feature/multi-tab` 拉取参考。 |
| #76 Fix: Dollar signs… | LuckyLife007 | 2026-06-24 当天关闭 | +32/-7, 10 文件 | #77 的前身，误带入本地打包配置、发布者身份、二进制 DLL，被 #77 取代。 |
| #72 / #71 Claude/fix failing build | shanteshpatil | 2026-05-13/14 关闭 | +3756/-1351, 24 文件 | 无描述，AI 生成的构建修复，重复提交两次后关闭。可能与 #37（yarn build 缺 script）相关，但无法确认。 |
| #69 feat: Avalonia migration | awarson2233 | 2026-04-09 当天关闭 | +50630/-3947, 545 文件 | 整体迁移到 Avalonia（跨平台，对应 #84）。规模巨大，当天自行关闭。 |
| #18 Allow to associate more Markdown file extensions | manfromarce | 2024-08-18 → 10-28 关闭 | +4/-0, 1 文件 | 在 `.appxmanifest` 中注册更多 Markdown 扩展名以便"打开方式"关联（部分对应 #3）。4 行改动，未合并被关闭。 |

---

## 三、Issue 解决状态对照

| Issue | 有对应 PR？ | 状态 |
|-------|-----------|------|
| #42 #60 #31 打印崩溃 | #78 | 开放，可合并。**注意作者 1.2.19 分支反而把打印改成 NotImplemented。** |
| #75 `$` 误判公式 | #77 | 开放，可合并 |
| #52 #53 Mermaid | #79 | 开放，可合并 |
| #10 #34 #62 #65 #36 字体 | #67、#83 | 两个开放 PR，功能重叠 |
| #47 RTL | #83 | 开放，可合并 |
| #22 葡语翻译 | #64 | 开放，可合并 |
| #30 #73 #85 标签页 | #82 | 已关闭，代码可参考 |
| #84 Linux/macOS | #69 | 已关闭（Avalonia），代码可参考 |
| #3 .txt 关联 | #18（部分） | 已关闭 |
| #4 #15 exe/portable | 无 PR；1.2.19 分支改 SelfContained=true 可能是铺垫；社区 zhaokuohaha 自行打包过 1.2.18 | — |
| #68 韩语翻译 | 无（用户表示愿提 PR） | — |
| #56 断电丢文件、#1/#24/#20 改写源码、#63 撤销、#26 `$$`、#48 导出、#81 锁目录、#16/#44 崩溃等 | **无 PR** | 未解决 |

---

## 四、对 fork 的建议

1. **基线选择**：`main` 是唯一功能完整的分支（.NET Core 3.1 + WPF/WinForms）。`1.2.19` / `1.2.19-net9.0` 是未完成的迁移，打印被砍、Mica 被禁，不建议直接作为基线；但 ARM64 支持、异步剪贴板、SDK 风格 csproj 可以之后按需 cherry-pick。
2. **低成本、高收益的合入顺序**：#77（2 行）→ #78（打印）→ #79（Mermaid）→ #64（葡语）→ #83 或 #67（字体，建议取 #83 的 `AutoSuggestBox` 实现 + #67 的多语言资源）。
3. 六个开放 PR 目前都对 `main` MERGEABLE，但彼此之间可能有冲突（#67 与 #83 都改 `EditorSetting.xaml`、`SettingsViewModel.cs`、`EditorViewModel.cs`、`Muya/index.tsx`、`default.css`），需要手动处理。
4. 拉取 PR 分支的命令：`git fetch upstream pull/<N>/head:pr-<N>`。
