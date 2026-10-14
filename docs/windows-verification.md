# Windows 真机验证

这份清单用于发布候选版本。自动测试能覆盖协议、设置文件、SQLite、原子写入和安装清单，但不能替代 WebView2、文件锁、安装升级和真实输入法行为。

## 准备

- 使用即将发布的 commit，记录 commit、版本、Windows build、WebView2 Runtime 和 CPU 架构。
- 保持 Git 工作区干净。测试前备份现有 `%LOCALAPPDATA%\Typedown`，或使用专门的测试 Windows 账户。
- 只结束本次测试启动的 Typedown 进程；不要批量终止 WebView2 进程。
- 准备 UTF-8、UTF-8 BOM、UTF-16、CRLF/LF、大表格、Mermaid 错误、长文档和包含本地图片的样本。

## 自动检查

在仓库根目录执行：

```powershell
python Tools\Translations\check.py --strict
dotnet test Tests\Typedown.ReliabilityTests\Typedown.ReliabilityTests.csproj -c Release
Set-Location Dev\Typedown.Editor
yarn install --frozen-lockfile
$env:CI = 'true'
yarn test --watchAll=false --runInBand
$env:CI = 'false'
yarn build
Set-Location ..\..
powershell -ExecutionPolicy Bypass -File Tools\Installer\build-local.ps1 -Platform x64 -NoInstaller
```

必须保存命令退出码。`SafeFile` 的 Windows 文件锁用例在 Windows 上应实际通过；在其他系统被跳过不算 Windows 验证。

## 应用冒烟

1. 首次启动，确认窗口和编辑器加载完成，测试版本字符串可区分本次构建。
2. 只打开一个 Markdown 文件。左侧文件页应明确显示未打开文件夹，大纲页仍能显示当前文档标题树。
3. 使用“打开文件夹”，展开多级目录，切换文件页、大纲页和搜索页。
4. 同时打开多个标签，快速切换、关闭、恢复会话；每个标签的正文、光标、滚动位置和撤销历史不能串到其他文档。
5. 分别在所见即所得、源码和阅读模式编辑或切换。模式切换后磁盘源码不能被格式化或改写。
6. 测试紧贴段落的表格、列表、围栏代码块和 Mermaid。正文内可显示 Mermaid 错误提示，正文底部不能累积重复诊断；删除错误块和切换标签后提示必须消失。
7. 输入后立即保存、立即切换标签、立即关闭窗口。重开文件确认最后输入已经落盘，或应用明确阻止关闭。
8. 测试另存为、外部编辑后重载、文件被其他程序独占时保存、只读文件和无权限目录。失败时原文件必须完整。
9. 修改语言、主题、字号等设置后立即退出并重开。确认最新设置生效，`Settings.json` 是有效 JSON，未知字段没有被删除。
10. 触发自动备份后强制结束本次应用进程，再启动并检查恢复内容。

## 安装与升级

- 用上一稳定版安装包安装，保留一份设置和文档，再运行候选安装包升级。
- 从开始菜单、桌面快捷方式、`.md` 双击和“打开方式”分别启动。
- 验证安装器处理后台运行实例，并能完整卸载。
- 便携版在新目录解压运行；确认不会依赖构建机路径。
- MSIX 先导入同批 `.cer`，再安装；验证启动、文件关联和卸载。

## 架构覆盖和记录

x64 是每次发布的完整验证目标。ARM64 至少验证安装、启动、打开/编辑/保存、模式切换和卸载；没有实体 ARM64 验证时，在发布记录中明确写出未覆盖，不用 x64 结果代替。

验证记录至少包含：

```text
Commit/tag:
App version/build label:
Windows/WebView2:
Architecture:
Installer/portable/MSIX:
Automated tests:
Smoke result:
Known limitations:
Tester/date:
```
