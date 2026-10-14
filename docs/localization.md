# 本地化维护规范

Typedown 使用 Windows `.resw` 资源。英文是键集合的基准；其他语言缺键时运行时会回退到英文，因此缺失翻译不会崩溃，却会产生中英混排。CI 用严格检查提前阻止这种情况。

## 资源布局

每个语言目录都必须包含以下四个文件：

```text
Dev/Typedown.Core/Resources/Strings/<language>/
  CommonResources.resw
  DialogResources.resw
  Resources.resw
  SettingsResources.resw
```

语言集合必须在三处完全一致：

1. `Dev/Typedown.Core/Utilities/Locale.cs` 的 `SupportedLangs`；
2. `Dev/Typedown.Core/Resources/Strings/` 下的目录；
3. `Tools/Typedown.Package/Package.appxmanifest` 的 `<Resource Language="…"/>`。

可靠性测试会比较这三处。新增语言时必须同时修改，删除语言时也一样。

## 使用资源键

XAML 使用 `LocaleString`：

```xml
<TextBlock Text="{u:LocaleString Key=FolderPane.NoFolderOpen}" />
```

C# 使用 `Locale.GetString`：

```csharp
Status = Locale.GetString("FolderPane.NoFolderOpen");
```

键里的点会由 `Locale.GetString` 转换为资源路径分隔符 `/`。同一个键只能表达一个稳定语义。即使两个位置目前英文相同，只要上下文或词性不同，也应使用不同键。

## 文案和占位符

- 用完整、简短的界面句子，避免把多个资源片段在代码里拼成一句话。
- 保留 `{0}`、`{1}` 等全部占位符，并保持编号不变。译文可以调整顺序。
- 保留快捷键、文件扩展名、产品名和代码标识的原始拼写。
- 复数、单数和不同语境分别建键，不能依赖英文形式恰好相同。
- 翻译 XML 特殊字符时使用合法实体，保存为 UTF-8。
- 按钮使用动作词；提示说明发生了什么以及用户下一步能做什么。

## 修改流程

1. 先在英文对应的 `.resw` 中添加或修改键。
2. 在全部语言的同名文件中同步该键；不确定译法时不要用空值占位。
3. 检查 XAML 或 C# 的调用源是否选对资源文件和语义。
4. 运行严格检查：

   ```bash
   python3 Tools/Translations/check.py --strict
   ```

5. 在 Windows 上至少检查英文、简体中文和一种长文本语言。验证设置页、对话框、菜单、截断、换行和重启后的语言选择。

查看某个语言缺失的键：

```bash
python3 Tools/Translations/check.py de
```

列出全部缺失项：

```bash
python3 Tools/Translations/check.py --missing
```

语言切换会更新资源上下文，并重建依赖 XAML 标记扩展的部分界面。修改相关代码时，需要同时验证打包版和便携版；两者是否具有包身份不同，语言选择不能只依赖 `ApplicationLanguages.PrimaryLanguageOverride`。
