# Typedown 使用说明

Typedown 是 Windows 上的所见即所得 Markdown 编辑器：写的时候看到的就是排版后的样子，保存下来的是普通的 `.md` 文本文件，任何编辑器都能打开。

本说明对应 1.3.5 版。菜单和设置的名称按简体中文界面写；界面语言可以在 **设置 → 常规** 里改。

- [1. 快速上手](#1-快速上手)
- [2. 四种模式](#2-四种模式)
- [3. 常用排版与快捷键](#3-常用排版与快捷键)
- [4. 图片](#4-图片)
- [5. 图片上传](#5-图片上传)
- [6. 主题](#6-主题)
- [7. 导出、打印与分享](#7-导出、打印与分享)
- [8. 常见问题](#8-常见问题)

---

## 1. 快速上手

**打开文件**：文件 → 打开（Ctrl+O），或者把 `.md` 文件拖进窗口。每个文档一个标签页，Ctrl+Tab / Ctrl+Shift+Tab 切换，Ctrl+W 关闭。

**打开文件夹**：文件 → 打开文件夹。左侧边栏（Ctrl+Shift+L 显示/隐藏）有两页：

- **文件**：文件夹里的文档。单击是预览（打开另一个文件时替换这个标签页），双击正式打开；侧栏顶部的放大镜在整个文件夹里搜索（Ctrl+Shift+F）
- **大纲**：当前文档的标题，点击跳转；写作时当前所在的标题会高亮

**保存**：Ctrl+S；另存为 Ctrl+Shift+S。没保存的内容会定时备份，程序意外退出后再打开时会恢复。下次启动时，上次打开的标签页会自动恢复。

**设置**：Ctrl+, 或窗口右上角的齿轮。

---

## 2. 四种模式

| 模式 | 打开方式 | 用途 |
|---|---|---|
| 可视编辑 | 默认 | 所见即所得，直接在排版后的页面上写 |
| 源代码模式 | 视图 → 源代码模式，Ctrl+/ | 直接编辑 Markdown 原文 |
| 阅读模式 | 视图 → 阅读模式，Ctrl+Shift+R | 只读，防止误改；可以选中、复制 |
| 专注 / 打字机模式 | F8 / F9 | 专注模式淡化其他段落；打字机模式让当前行停在屏幕中间 |

F11 全屏。

阅读模式下的右键菜单只有复制和全选：

- **复制**：粘到 Word、邮件里保留格式
- **复制为纯文本**：去掉 Markdown 记号，按页面显示的样子复制

编辑时的右键菜单还有 **复制为 Markdown**（Ctrl+Shift+C），复制的是 Markdown 原文。

---

## 3. 常用排版与快捷键

可视编辑时，直接输入 Markdown 语法也会立即变成排版：行首输入 `## ` 变成二级标题，`- ` 变成列表，`> ` 变成引用，三个反引号开始代码块，`$$` 开始公式块。

| 操作 | 快捷键 |
|---|---|
| 标题 1–6 / 正文 | Ctrl+1 … Ctrl+6 / Ctrl+0 |
| 标题升级 / 降级 | Ctrl+= / Ctrl+- |
| 加粗 / 斜体 / 下划线 | Ctrl+B / Ctrl+I / Ctrl+U |
| 删除线 | Alt+Shift+5 |
| 行内代码 | Ctrl+Shift+` |
| 链接 / 图片 | Ctrl+K / Ctrl+Shift+I |
| 清除格式 | Ctrl+\ |
| 有序列表 / 无序列表 / 任务列表 | Ctrl+Shift+[ / Ctrl+Shift+] / Ctrl+Shift+X |
| 引用 / 代码块 / 公式块 / 表格 | Ctrl+Shift+Q / Ctrl+Shift+K / Ctrl+Shift+M / Ctrl+Shift+T |
| 在前面 / 后面插入段落 | Ctrl+Shift+Enter / Ctrl+Enter |
| 查找 / 替换 / 查找下一个 | Ctrl+F / Ctrl+H / F3 |
| 粘贴为纯文本 | Ctrl+Shift+V |
| 打印 | Alt+Shift+P |

所有快捷键都可以在 **设置 → 常规 → 快捷键** 里修改。

**段落** 菜单里还有图表（流程图、时序图、Mermaid、PlantUML、Vega）、脚注、水平线、内容目录、YAML 头等插入项。

---

## 4. 图片

### 4.1 插入图片的几种方式

| 方式 | 说明 |
|---|---|
| 粘贴截图 | 截图工具、浏览器"复制图片"等放在剪贴板里的图片，Ctrl+V 粘贴 |
| 粘贴图片文件 | 在资源管理器里复制图片文件，再 Ctrl+V |
| 拖入 | 把图片文件拖进窗口；一次拖多张，按顺序各占一段，一次撤销全部撤回 |
| 菜单 | 格式 → 图像（Ctrl+Shift+I），在弹出的"编辑图片"里填路径或点"浏览..."，可以多选 |
| 手写 | 输入 `![说明文字](路径)` |

支持的图片格式：`.png .jpg .jpeg .gif .svg .webp .jfif`。

### 4.2 插入时怎么处理图片

打开 **设置 → 图片 → 图片插入操作**。图片分三类，每类分别选一种处理方式：

- **剪贴板图片**：截图等，不包括复制的图片文件和网络图片
- **本地图片**：电脑上的图片文件（粘贴、拖入、选择的文件）
- **网络图片**：`http(s)://` 开头的图片地址

| 处理方式 | 剪贴板图片 | 本地图片 | 网络图片 |
|---|---|---|---|
| **无操作**（默认） | 存为 PNG，放进"默认图片根目录"，文档里写绝对路径（`file:///...`） | 文档里直接写原文件的路径 | 文档里直接写网址 |
| **复制到路径** | 存为 PNG，放进你指定的文件夹 | 复制一份到你指定的文件夹 | 下载到你指定的文件夹 |
| **上传图片** | 上传到图床，文档里写网址 | 上传，文档里写网址 | 下载后上传，文档里写网址 |

选"复制到路径"后，下面会出现一个文件夹输入框，默认是 `./images`。相对路径是相对于**文档所在的文件夹**。可以用这些变量：

| 变量 | 含义 | 例子（文档为 `D:\笔记\周报.md`，日期 2026-10-07） |
|---|---|---|
| `${filename}` | 文档文件名，不含扩展名 | `周报` |
| `${filedir}` | 文档所在文件夹 | `D:\笔记` |
| `${year}` `${month}` `${day}` | 当天的年、月、日 | `2026` `10` `07` |

**例子：**

| 文件夹设置 | 图片会放到 | 文档里写成 |
|---|---|---|
| `./images` | `D:\笔记\images\` | `![](./images/截图.png)` |
| `./${filename}.assets` | `D:\笔记\周报.assets\` | `![](./周报.assets/截图.png)` |
| `./images/${year}-${month}` | `D:\笔记\images\2026-10\` | `![](./images/2026-10/截图.png)` |
| `D:\图库\${year}` | `D:\图库\2026\` | 绝对路径 |

`./${filename}.assets` 和 Typora 的习惯一致：每篇文档一个图片文件夹，移动文档时把两者一起移走即可。

**其他相关设置**（同一页）：

- **默认图片根目录**：文档还没保存时，相对路径以这里为准；剪贴板图片选"无操作"时也放在这里。默认是 `图片\Typedown`。
- **优先使用相对路径**：本地图片选"无操作"时，把绝对路径改写成相对于文档的路径。适合图片和文档在同一个文件夹里、需要整体搬家或用 Git 管理的情况。
- **相对路径前添加 "./"**：在上一项的相对路径前加 `./`。

> 路径里的空格会写成 `%20`，中文保持原样，例如 `![](./images/我的%20截图.png)`。

### 4.3 推荐配置

**笔记放在本地或同步盘（OneDrive、坚果云等）**：图片跟着文档走。

- 剪贴板图片：复制到路径，`./${filename}.assets`
- 本地图片：复制到路径，`./${filename}.assets`
- 网络图片：无操作

**写博客、发到网上**：图片都放图床（见第 5 节）。

- 三类都选"上传图片"
- 或者先按上一种方式写，发布前用 **文件 → 上传本地图片** 一次性上传

### 4.4 图片右键菜单

在图片上右键 → **图像**：

| 菜单项 | 作用 |
|---|---|
| 打开图片位置 | 在资源管理器中打开图片所在文件夹（仅本地图片） |
| 复制图片到... | 复制到你选的位置，**文档里的地址改为新位置** |
| 移动图片到... | 同上，并删除原文件（仅本地图片） |
| 上传图片 | 选一个上传配置，上传这张图片并把地址换成网址（仅本地图片） |
| 图片另存为... | 另存一份，文档不变 |
| 删除图片文件 | 删除图片文件，并从文档中去掉这张图片（仅本地图片） |

点击图片会出现浮动工具栏：编辑、内联 / 左对齐 / 居中 / 右对齐、缩放（25%–200%）、删除。对齐和缩放会把图片写成 `<img>` 标签。

---

## 5. 图片上传

### 5.1 三步配置

1. **设置 → 图片 → 图片上传配置 → 添加**：填一个名称，选上传方式
2. 在配置页里填写参数，然后点 **测试上传 → 选择文件**，选一张图片试一下。成功会显示上传后的网址。（配置页顶部的"启用"开关关掉后，这个配置就不会出现在上传菜单里）
3. 回到 **设置 → 图片**，在 **上传方式** 里选这个配置

之后：

- 插入操作选了"上传图片"的图片，插入时自动上传
- **文件 → 上传本地图片** 一次上传当前文档里的所有本地图片
- 图片右键 → 图像 → 上传图片，单张上传

目前可用的上传方式有两种：**S3 / R2 / OSS / COS**（S3 兼容的对象存储）和 **PowerShell**（自己写脚本，可以对接任何图床）。

### 5.2 S3 / R2 / OSS / COS

适用于 Cloudflare R2、Amazon S3、阿里云 OSS、腾讯云 COS、MinIO 等支持 S3 接口的对象存储。

| 字段 | 说明 |
|---|---|
| Endpoint | 服务地址；不写 `https://` 时自动补上 |
| Region | 区域；留空为 `us-east-1`，R2 填 `auto` |
| Bucket | 存储桶名称 |
| Access Key ID / Secret Access Key | 访问密钥。Secret 用 Windows 账户加密保存，不以明文存放 |
| 路径样式地址 | MinIO 和部分服务商需要勾选 |
| 存储桶中的文件夹 | 例如 `images/${year}/${month}`，变量同 4.2；留空则放在存储桶根目录 |
| 公开 URL | 图片对外访问的域名，例如 `https://img.example.com`；留空则用 Endpoint 的地址 |

上传后的文件名是 `原文件名-8位哈希.扩展名`，例如 `截图-3fa2c91b.png`；粘贴的截图叫 `image-哈希.png`。同一张图片总是同一个名字，不会重复上传成多份。

**例子：Cloudflare R2**

| 字段 | 填写 |
|---|---|
| Endpoint | `https://<账户ID>.r2.cloudflarestorage.com` |
| Region | `auto` |
| Bucket | `blog-images` |
| Access Key ID / Secret | 在 R2 → 管理 API 令牌 中创建，权限选"对象读和写" |
| 存储桶中的文件夹 | `images/${year}/${month}` |
| 公开 URL | 存储桶绑定的自定义域名，或开启公开访问后的 `https://pub-xxxx.r2.dev` |

插入 `截图.png` 后，文档里会是：
`![截图](https://img.example.com/images/2026/10/截图-3fa2c91b.png)`

**例子：腾讯云 COS**

| 字段 | 填写 |
|---|---|
| Endpoint | `https://cos.ap-guangzhou.myqcloud.com`（按存储桶所在地域） |
| Region | `ap-guangzhou` |
| Bucket | 完整的存储桶名称，例如 `blog-1250000000` |
| 公开 URL | 绑定的 CDN 域名，或 `https://blog-1250000000.cos.ap-guangzhou.myqcloud.com` |

**例子：MinIO（自建）**

| 字段 | 填写 |
|---|---|
| Endpoint | `http://192.168.1.10:9000` |
| Region | 留空 |
| 路径样式地址 | 勾选 |
| 公开 URL | 留空，或你的反向代理域名 |

> 存储桶需要允许公开读取，否则文档里的图片别人看不到。各家的 Endpoint 和 Region 写法以服务商的 S3 兼容文档为准。

### 5.3 PowerShell 脚本

脚本里定义一个 `Upload-Image` 函数。Typedown 上传每张图片时调用它，传入图片的完整路径；函数**最后输出的一行**就是图片的网址。

```powershell
# Upload file and return URL
function Upload-Image($FilePath)
{
    return $FilePath
}
```

要点：

- 用的是 Windows 自带的 PowerShell 5.1（`powershell.exe`），不是 PowerShell 7
- 每张图片最多 60 秒，超时算失败
- 只有最后一行非空输出会被当作网址，前面输出的内容会被忽略
- 出错时让脚本抛出异常（`throw "..."`），错误信息会显示在提示框里
- 可以用 **从文件导入 / 保存到文件** 管理脚本

**例子 1：PicGo 命令行版**

需要先安装：[Node.js](https://nodejs.org/)，然后运行 `npm install picgo -g` 安装 PicGo 命令行版（PicGo-Core），再用 `picgo set uploader` 配置图床。PicGo **桌面版**不带 `picgo` 命令，不能直接用这个例子。

```powershell
function Upload-Image($FilePath)
{
    $out = picgo upload "$FilePath" 2>&1 | Out-String
    $url = ($out -split "`r?`n" | Where-Object { $_ -match '^https?://' } | Select-Object -Last 1)
    if (-not $url) { throw "PicGo 没有返回网址：$out" }
    $url.Trim()
}
```

**例子 2：自建图床的 HTTP 接口**（以返回 JSON `{"url": "..."}` 的接口为例）

`curl.exe` 是 Windows 10（1803 及以后）和 Windows 11 自带的，不用安装。注意要写 `curl.exe`：在 PowerShell 里单写 `curl` 是另一个命令。`YOUR_TOKEN` 换成图床给你的令牌。

```powershell
function Upload-Image($FilePath)
{
    $json = curl.exe -sS -F "file=@$FilePath" -H "Authorization: Bearer YOUR_TOKEN" https://img.example.com/api/upload
    if ($LASTEXITCODE -ne 0) { throw "curl 上传失败（退出码 $LASTEXITCODE）" }
    $result = $json | ConvertFrom-Json
    if (-not $result.url) { throw "接口没有返回网址：$json" }
    $result.url
}
```

**例子 3：复制到网站目录或同步文件夹**

只用 PowerShell 自带的命令，不需要安装任何东西。

```powershell
function Upload-Image($FilePath)
{
    $month = Get-Date -Format yyyy-MM
    $dir = "D:\site\static\img\$month"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $name = [IO.Path]::GetFileName($FilePath)
    Copy-Item -LiteralPath $FilePath -Destination $dir -Force
    "https://example.com/img/$month/" + [Uri]::EscapeDataString($name)
}
```

**例子 4：rclone（支持几十种网盘和存储）**

需要先从 [rclone.org](https://rclone.org/downloads/) 下载 rclone，放到 PATH 里，并用 `rclone config` 配置一个名为 `myremote` 的远端。

```powershell
function Upload-Image($FilePath)
{
    $name = [IO.Path]::GetFileName($FilePath)
    rclone copyto "$FilePath" "myremote:images/$name" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "rclone 上传失败" }
    "https://img.example.com/images/$name"
}
```

### 5.4 文件 → 上传本地图片

把当前文档里所有**本地图片**上传，并把文档中的地址换成网址：

- 包括 `![](...)`、引用式图片和 `<img src="...">`；网址、`data:` 图片和代码块里的内容不动
- 同一个文件在文档里出现多次，只上传一次
- 替换是一步完成的，**一次撤销（Ctrl+Z）就能全部换回本地路径**
- 上传失败的图片保持原样，结束时会列出原因

**上传记录**：用同一个配置上传过的同一张图片，不会再上传，直接用之前的网址。如果你在图床上删除了文件，到 **设置 → 图片 → 上传历史** 点"清除"，下次会重新上传。

**分享到 HedgeDoc** 时，如果文档里有本地图片，会先问你要不要上传：笔记的读者看不到你电脑上的文件。

---

## 6. 主题

**视图 → 主题** 切换主题：浅色、深色、纯黑，以及几套内置配色（Sepia、Solarized、Nord、Dracula、Gruvbox）。

**自定义主题**：

- **视图 → 主题 → 自定义主题…** 打开配色工具，调好颜色后导出 CSS
- 把 CSS 文件放进主题文件夹（安装版和便携版是 `%LOCALAPPDATA%\Typedown\themes\`），再点 **视图 → 主题 → 重新加载主题**，新主题就出现在菜单里
- 主题里设置的强调色也会用在大纲和文件树的选中标记上

---

## 7. 导出、打印与分享

- **文件 → 导出**：HTML、PDF（带书签）。导出格式和参数在 **设置 → 导出** 里设置
- **文件 → 打印**：Alt+Shift+P
- **文件 → 分享到 HedgeDoc**：把文档发布到 HedgeDoc 服务器，得到一个链接

---

## 8. 常见问题

**图片显示不出来**

- 本地图片：确认路径相对于文档所在的文件夹是对的；文档还没保存时，相对路径以"默认图片根目录"为准
- 网络图片：确认网址在浏览器里能打开

**图片上传失败**

- 先在配置页用 **测试上传** 试，看提示的错误信息
- 确认配置的"启用"开关是打开的，并在 **设置 → 图片 → 上传方式** 中选中了它
- S3：检查 Endpoint、Region、密钥权限和存储桶的公开读取设置
- PowerShell：确认脚本最后输出的是网址；可以先在 PowerShell 窗口里手动运行 `Upload-Image 'C:\某张图片.png'` 看输出

**反馈问题**

在 **设置 → 关于** 点 **发送反馈**。请写明版本号（同一页上能看到）和复现步骤；点 **打开日志文件夹**，附上里面的 `debug.log`。日志只保存在你的电脑上，不会自动上传。
