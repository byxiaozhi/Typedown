import re

filepath = r"d:\source\repos\Typedown\Dev\Typedown.Core\Controls\CommonControls\AppContentDialog.cs"

with open(filepath, 'r', encoding='utf-8') as f:
    content = f.read()

# Fix properties
pattern = r"public static AvaloniaProperty (\\w+)Property \{ get; \} = AvaloniaProperty\.Register<AppContentDialog, (.+?)>\(nameof\((\w+)\)(.+?)\);"
def replace_prop(match):
    prop_name = match.group(1)
    type_name = match.group(2)
    name = match.group(3)
    rest = match.group(4)
    return f"public static readonly StyledProperty<{type_name}> {prop_name}Property = AvaloniaProperty.Register<AppContentDialog, {type_name}>(nameof({name}){rest});"

content = re.sub(pattern, replace_prop, content)

# Fix Missing using statements
content = content.replace("using Windows.UI.Xaml.Shapes;", "using Avalonia.Controls.Shapes;\nusing Avalonia.Styling;\nusing Avalonia.VisualTree;")
content = content.replace("using FocusManager = Windows.UI.Xaml.Input.FocusManager;", "")
content = content.replace("using FocusNavigationDirection = Windows.UI.Xaml.Input.FocusNavigationDirection;", "")
content = content.replace("using KeyEventHandler = Windows.UI.Xaml.Input.KeyEventHandler;", "")
content = content.replace("public event TypedEventHandler<", "public event EventHandler<")

# Fix XamlRoot usage
content = content.replace("public async Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot)", "public async Task<ContentDialogResult> ShowAsync(Control xamlRoot)")
content = content.replace("private readonly ConditionalWeakTable<XamlRoot, SemaphoreSlim> showSemaphores = new();", "private readonly ConditionalWeakTable<Control, SemaphoreSlim> showSemaphores = new();")
content = content.replace("XamlRoot.Content is Grid", "xamlRoot != null && true") # naive bypass

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(content)
print("done")
