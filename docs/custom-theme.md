# 自定义主题

一个主题就是**一个 CSS 文件**。把它放进主题文件夹，重启后就能在 设置 → 外观 → 主题 里选到它。

主题管两部分：**编辑区**（文档本身、代码块、表格、浮动工具条）用文件里的 CSS 变量；**窗口的其余部分**（菜单栏、侧边栏、标签栏、状态栏）用元数据里的四个颜色。两边都写，整个界面才是一套配色。

## 可视化配色工具

不想手写 CSS 时，可在 **视图 → 主题 → 自定义主题…** 中打开随安装包提供的离线配色工具。Typedown 会在打开前读取内置主题和用户主题文件夹，并把当前列表交给页面；从“本机主题”列表选择一项即可继续修改。主题文件有增删或修改后，重新从菜单打开页面就会刷新列表。

也可以直接打开[中英双语主题配色工具](https://flintt.github.io/Typedown/)。它支持实时预览、对比度检查、导入现有单文件主题或 editor / CodeMirror / Prism 三文件主题，并导出 Typedown 可直接使用的 CSS。[离线单 HTML 文件](../Tools/Themes/theme-designer.html?raw=1)仍随源码提供。直接打开独立页面时，浏览器无权自行扫描本地主题文件夹，因此不会显示“本机主题”列表；仍可手动导入 CSS。

“保存设计工程（.json）”只用于备份或换设备继续编辑；安装主题时请下载 CSS。“继承基底”决定 CSS 未覆盖区域以及菜单、弹窗等原生界面使用 `light`、`dark` 还是 `black`，不会重置当前调色板。

## 放在哪里

| 平台 | 路径 |
|---|---|
| Windows | `%LOCALAPPDATA%\Typedown\themes\` |
| Linux | `~/.local/share/Typedown.Uno/themes/` |
| macOS | `~/Library/Application Support/Typedown.Uno/themes/` |

文件夹不存在时程序会自动创建，并放一个 `example.css` 作为起点。文件名（不含 `.css`）就是主题的内部标识，改名等于换了一个主题。

## 内置主题

程序自带六个主题，装好就能在 视图 → 主题 和 设置 → 外观 里选到，不用往主题文件夹放任何东西：

| 主题 | 基底 | 说明 |
|---|---|---|
| Solarized Light | light | Solarized 的浅色一版，米黄底 |
| Sepia | light | 暖纸色，长时间阅读用 |
| Nord | dark | 冷灰蓝 |
| Solarized Dark | dark | Solarized 的深色一版 |
| Dracula | dark | 深紫灰配亮色语法高亮 |
| Gruvbox Dark | dark | 暖褐底，对比偏高 |

内置主题的文件随程序安装（Windows 在安装目录的 `Resources\Themes\`，Linux 在 `/opt/typedown/Assets/Themes/`），
不要直接改——升级会覆盖。想在它们的基础上改：把文件复制到上面的主题文件夹，改完即可；**同名文件会顶替掉内置的那一个**，
在列表里仍然占同一个位置。换个文件名则是多出一个主题。

## 文件结构

```css
/* Typedown theme
 * name: Solarized Light
 * base: light
 * accent: #268bd2
 * background: #fdf6e3
 * surface: #f2ead7
 * foreground: #073642
 * border: #e0dbc8
 * author: 你的名字
 */
:root {
  --editorBgColor: #fdf6e3;
  --editorColor: #073642;
}
```

开头第一个注释块是元数据，每行一个 `键: 值`：

| 键 | 必填 | 说明 |
|---|---|---|
| `name` | 否 | 设置里显示的名字。省略则用文件名 |
| `base` | 否 | `light`（默认）/ `dark` / `black`。决定没被覆盖的部分取自哪套内置主题 |
| `accent` | 否 | 强调色（链接、光标、选中态），`#RGB` 或 `#RRGGBB` |
| `author` | 否 | 仅作记录 |
| `background` | 否 | **窗口底色**：编辑区、标签栏；当前活动标签是它略微提亮的卡片 |
| `surface` | 否 | **面板底色**：侧边栏、状态栏等面板。省略则跟随 `background` |
| `foreground` | 否 | 窗口里的文字颜色 |
| `border` | 否 | 面板之间以及标签之间的分隔线 |

后面四个决定**编辑区之外**的配色。不写它们，窗口就保持 `base` 那套内置主题的颜色——但那样编辑区和窗口会是两种色调，通常不好看，所以建议一起写。

标签栏本身不上色，和旁边的侧边栏是同一片底色，所以顶部的色带左右齐平。活动标签是在 `background` 基础上略微提亮的卡片，边框用 `border`；非活动标签不画底色；标签文字用 `foreground`。悬停和按下状态由 `background` 与 `foreground` 自动混合，不需要再写额外的颜色。

> Windows 上如果开启了 Mica 材质，主题指定的 `background` 会覆盖掉编辑区背后的 Mica 效果。

注释之后是普通 CSS，**在内置主题之后加载**，所以你只需要写要改的部分；没写的自动沿用 `base`。用户在 设置 → 外观 → 自定义 CSS 里写的内容加载在主题之后，优先级更高。

## 可用的变量

全部定义在 `:root` 上，颜色可以用任何 CSS 颜色写法。

### 文字与背景

| 变量 | 作用 |
|---|---|
| `--editorBgColor` | 编辑区背景（同时决定页面底色） |
| `--editorColor` | 正文颜色 |
| `--editorColor80` `--editorColor60` `--editorColor50` `--editorColor40` `--editorColor30` `--editorColor10` `--editorColor04` | 正文颜色的不同透明度，用于次要文字、分隔线、占位符、悬停底色等。**建议整套一起改**，否则深浅会对不上 |
| `--selectionColor` | 选中文字的底色 |
| `--highlightColor` | 查找命中的底色 |
| `--themeColor` | 强调色（也可用元数据里的 `accent` 设置） |
| `--focusColor` | 焦点描边，默认 `var(--themeColor)` |
| `--deleteColor` | 危险操作（删除按钮等） |
| `--iconColor` | 小图标线条色 |

### 代码与表格

| 变量 | 作用 |
|---|---|
| `--codeBgColor` | 行内代码底色 |
| `--codeBlockBgColor` | 代码块底色 |
| `--tableBorderColor` | 表格边框 |
| `--footnoteBgColor` | 脚注块底色 |
| `--inputBgColor` | 代码块语言输入框等输入控件底色 |

### 浮动元素

| 变量 | 作用 |
|---|---|
| `--floatBgColor` | 浮动工具条 / 菜单底色 |
| `--floatHoverColor` | 其悬停态 |
| `--floatBorderColor` | 其边框 |
| `--floatShadow` | 其阴影（完整的 `box-shadow` 值） |
| `--itemBgColor` | 列表项底色 |
| `--maskColor` | 遮罩层 |

### 版面

| 变量 | 作用 |
|---|---|
| `--editorAreaWidth` | 编辑区宽度。设置里的"编辑区宽度"会覆盖它 |

## 超出变量的部分

主题就是 CSS，想改什么都可以：

```css
/* 标题用衬线字体 */
#ag-editor-id h1, #ag-editor-id h2 { font-family: Georgia, "Noto Serif CJK SC", serif; }

/* 引用块左边一条粗线 */
#ag-editor-id blockquote { border-left: 4px solid var(--themeColor); padding-left: 1em; }

/* 代码高亮：语法着色用的是 Prism 的类名 */
.token.comment { color: #93a1a1; font-style: italic; }
.token.keyword { color: #859900; }
.token.string  { color: #2aa198; }

/* 源码模式用的是 CodeMirror 的类名 */
.CodeMirror { background: #fdf6e3; }
.cm-header  { color: #b58900; }
```

常用的容器：`#ag-editor-id` 是文档根节点，段落是 `.ag-paragraph`，表格是 `table.ag-paragraph`，代码块是 `pre[data-role$='code']`。

## 导出

导出 HTML / PDF 时主题会一起写进去，导出的文件在任何浏览器里打开都是同样的样子。

## 调试

改完文件后重新选一次主题（或重启程序）即可生效。主题解析出错会记进日志：

- Windows：`%LOCALAPPDATA%\Typedown\logs\debug.log`
- Linux / macOS：应用数据目录下的 `debug.log`

## 完整示例

```css
/* Typedown theme
 * name: Solarized Light
 * base: light
 * accent: #268bd2
 * background: #fdf6e3
 * surface: #f2ead7
 * foreground: #073642
 * border: #e0dbc8
 * author: Typedown
 */
:root {
  --editorBgColor: #fdf6e3;
  --editorColor: #073642;
  --editorColor80: rgba(7, 54, 66, .8);
  --editorColor60: rgba(7, 54, 66, .6);
  --editorColor50: rgba(7, 54, 66, .5);
  --editorColor40: rgba(7, 54, 66, .4);
  --editorColor30: rgba(7, 54, 66, .3);
  --editorColor10: rgba(7, 54, 66, .1);
  --editorColor04: rgba(7, 54, 66, .04);
  --selectionColor: rgba(38, 139, 210, .18);
  --highlightColor: rgba(181, 137, 0, .35);
  --codeBgColor: #eee8d5;
  --codeBlockBgColor: #eee8d5;
  --footnoteBgColor: rgba(7, 54, 66, .04);
  --inputBgColor: rgba(7, 54, 66, .06);
  --tableBorderColor: #e0dbc8;
  --itemBgColor: #eee8d5;
  --floatBgColor: #fdf6e3;
  --floatHoverColor: rgba(7, 54, 66, .06);
  --floatBorderColor: rgba(7, 54, 66, .12);
  --maskColor: rgba(253, 246, 227, .8);
  --iconColor: rgba(101, 123, 131, .8);
  --deleteColor: #dc322f;
}

.token.comment { color: #93a1a1; font-style: italic; }
.token.keyword { color: #859900; }
.token.string  { color: #2aa198; }
.token.number  { color: #d33682; }
```

---

# Custom themes (English)

A theme is a single CSS file dropped into the themes folder (`%LOCALAPPDATA%\Typedown\themes\` on Windows,
`~/.local/share/Typedown.Uno/themes/` on Linux, `~/Library/Application Support/Typedown.Uno/themes/` on macOS).
It then appears under Settings → Appearance → Theme.

Open the packaged offline designer from **View → Theme → Custom theme…**. Before launching it, Typedown reads
the bundled and user theme folders and supplies the current list to the page. Select an item under “Themes on
this device” to continue editing it. Reopen the page from the menu after adding, removing, or changing theme files.

The [bilingual theme designer](https://flintt.github.io/Typedown/) also runs directly in a browser. It provides
live previews, contrast checks, imports for existing single-file themes or separate editor / CodeMirror / Prism
CSS, and exports CSS ready for Typedown. A [standalone offline HTML file](../Tools/Themes/theme-designer.html?raw=1)
is included in the repository. A page opened directly cannot scan a local folder under browser security rules,
so its device theme list stays hidden; CSS files can still be imported manually.
Its JSON project files are only for backup and continued editing; install the exported CSS in Typedown.

Six themes ship with the app — Solarized Light, Sepia, Nord, Solarized Dark, Dracula and Gruvbox Dark — and are
listed under View → Theme without installing anything. Their files sit next to the program (`Resources\Themes\`
on Windows, `/opt/typedown/Assets/Themes/` on Linux) and are replaced on upgrade, so edit a copy instead: put it
in the themes folder above, and a copy under the same file name takes the place of the bundled theme it came
from. Under a different name it becomes a theme of its own.

The first comment block carries the metadata — `name`, `base` (`light`, `dark` or `black`; which built-in theme
fills in what you leave out), `accent`, `author`, and `background`, `surface`, `foreground` and `border`, which
colour the window around the editor: the menu bar, the side pane, the tab bar and the status bar. Everything
after the comment is plain CSS applied on top of the built-in theme, so a theme only states what it changes.
The user's own custom CSS from the settings is applied after the theme and still wins.

The tab bar itself is not painted: it shows the same background as the side pane next to it, so the header band
ends at one height. The active tab is a card slightly lighter than `background`, edged with `border`; inactive tabs
have no fill; labels use `foreground`. Hover and pressed colours are derived automatically.

The variables are listed in the tables above; they live on `:root`. Beyond them a theme can style anything:
`#ag-editor-id` is the document root, Prism class names (`.token.keyword`) colour code, CodeMirror class names
(`.CodeMirror`, `.cm-header`) cover source mode. Themes are included in exported HTML and PDF.
