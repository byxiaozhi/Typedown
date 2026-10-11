# Typedown 打包与发布

本文记录当前 Windows 构建和发布流程。发布产物由 GitHub Actions 从同一提交生成；本地脚本用于真机验证和临时安装包。

## 产物

| 产物 | 用途 |
| --- | --- |
| `Typedown-windows-x64-v*.exe` / `-arm64-*.exe` | Inno Setup 安装包 |
| `Typedown-portable-x64-v*.zip` / `-arm64-*.zip` | 解压运行的便携版，仅 tag 构建生成 |
| `Typedown_*_x64.msix` / `_ARM64.msix` | 旁加载 MSIX（文件名取 `Branding.props` 的 `BrandName`） |
| `Typedown-signing-certificate.cer` | 安装同批 MSIX 所需的公钥证书 |

CI 的普通分支构建只保留小型 installer artifact；`v*` tag 构建才收集完整产物并创建 GitHub Release。

## 本地依赖

- Visual Studio 2022 或 Build Tools，安装 UWP 和 .NET 桌面构建组件；
- Windows 10/11 SDK，项目当前目标 SDK 见 `Dev/Typedown/Typedown.csproj`；
- Node.js 20 和 Yarn；
- Inno Setup 6，仅在生成 `.exe` 安装包时需要；
- .NET 9 SDK，用于独立可靠性测试。

先构建和测试编辑器：

```powershell
Set-Location Dev\Typedown.Editor
yarn install --frozen-lockfile
$env:CI = 'true'
yarn test --watchAll=false --runInBand
$env:CI = 'false'
yarn build
Set-Location ..\..
```

运行持久化、迁移、安装清单和架构守卫：

```powershell
dotnet test Tests\Typedown.ReliabilityTests\Typedown.ReliabilityTests.csproj -c Release
```

## 本地构建

`Tools/Installer/build-local.ps1` 会定位 MSBuild 和 Windows SDK，检查编辑器 bundle 是否比源码新，然后构建自包含应用。只编译应用、不生成安装包：

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Installer\build-local.ps1 -Platform x64 -BuildEditor -NoInstaller
```

生成 x64 安装包：

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Installer\build-local.ps1 -Platform x64 -BuildEditor
```

ARM64 把 `-Platform x64` 改为 `-Platform ARM64`。`-SkipBuild` 只适用于已经确认输出与当前源码一致的情况。脚本会临时写入 `local.<commit>` 测试标签并在结束后恢复源文件。

## 版本号

发布前以下四处必须一致：

| 文件 | 格式 |
| --- | --- |
| `Dev/Typedown/Typedown.csproj` | `1.3.8` |
| `Dev/Typedown.Core/Properties/AssemblyInfo.cs` | `1.3.8.0` |
| `Tools/Typedown.Package/Package.appxmanifest` | `1.3.8.0` |
| `Tools/Installer/Typedown.iss` | `1.3.8` |

可靠性测试在所有 CI 构建中检查这四处；tag 构建还会检查 tag 与项目版本是否一致。

## 发布步骤

1. 确认工作区干净，完成 [Windows 真机验证](docs/windows-verification.md)。
2. 同步四处版本号，提交版本变更。
3. 创建带说明的 annotated tag，例如 `v1.2.30`；带后缀的 tag（如 `v1.2.30-test.1`）会生成 prerelease。
4. 推送提交和 tag。`Build` workflow 会依次检查翻译、运行可靠性测试、测试并构建编辑器、构建 x64/ARM64 应用，再发布 Release。
5. 下载 Release 产物，在干净 Windows 环境检查 x64；有 ARM64 设备时执行 ARM64 冒烟测试。

不要把 PFX、密码或 Base64 私钥写入仓库、日志或 Release。CI 可从 `TYPEDOWN_PFX_BASE64` 和 `TYPEDOWN_PFX_PASSWORD` secrets 读取签名证书；没有 secrets 时会生成本次运行专用的临时自签名证书。`.cer` 只有公钥，可以随 MSIX 发布。

同一 MSIX identity 和版本已经安装时，Windows 可能拒绝重复安装。测试重打包应递增版本，或先卸载旧包。换签名证书后，需要卸载旧签名的同 identity 包，并信任新发布的 `.cer`。

## 微软商店上传包

`Branding.props` 的 `BrandStoreUpload` 为 `true` 的版本（Typeleaf），CI 每个平台在旁加载 MSIX 之后再按 `StoreUpload` 模式打一次包：不签名（商店会重新签名），得到 `<BrandName>_<版本>_<平台>.msixupload`（内含 `.msix` 和符号 `.appxsym`），作为 `store-upload-x64` / `store-upload-ARM64` 产物交给发布任务，打标签的构建把它们放进 GitHub Release；产物本身只保留 1 天（各产物都是，私有仓库的产物占账户的 Actions 存储额度），手动运行、不打标签的构建要在一天内从运行页面下载。在合作伙伴中心的提交里把两个平台的 `.msixupload` 都上传。包身份（`Identity` 的 `Name`、`Publisher`，以及 `PublisherDisplayName`）取自 `Tools/Typedown.Package/Package.appxmanifest`，必须与合作伙伴中心为该名称保留的值一致；版本号的第四段保持 0（商店保留它）。Typedown 本身没有商店身份，`BrandStoreUpload` 为 `false`。

本机生成（需要 Visual Studio Build Tools 的 MSIX 打包组件）：

```powershell
msbuild Tools\Typedown.Package\Typedown.Package.wapproj /t:Restore /p:Configuration=Release /p:Platform=x64
msbuild Tools\Typedown.Package\Typedown.Package.wapproj /p:Configuration=Release /p:Platform=x64 `
    /p:AppxBundle=Never /p:UapAppxPackageBuildMode=StoreUpload /p:AppxPackageSigningEnabled=false /p:AppxPackageDir=StoreUpload\
```

SDK 不在 `Program Files` 时，像 `Tools\Installer\build-local.ps1` 那样另传 `/p:ManifestTool=<SDK>\x86\mt.exe /p:MakePri=<SDK>\x64\makepri.exe`。

## 发布前产物检查

- 安装包、便携版和 MSIX 都包含 `Typedown.exe`、WebView2 loader、SQLite native 库和 `Resources/Statics/index.html`；
- 文件名、应用“关于”页、程序集、MSIX 和安装程序显示同一版本；
- 安装器能升级上一稳定版，卸载不会删除用户文档；
- `.md` 关联正常，`.txt` 和 `.text` 只出现在“打开方式”中；
- 安装包和 `Typedown.exe` 的签名可检查，MSIX 使用同批 `.cer`；
- Release 说明和附件属于同一个 tag/commit。

若构建能完成而应用无法保存到受保护目录，检查 Windows 安全中心的“受控文件夹访问”记录；这属于运行权限问题，不能用关闭原子写入来规避。
