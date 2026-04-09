import os
import re

dirs = [
    r"d:\source\repos\Typedown\Dev\Typedown.Core\Controls\CommonControls",
    r"d:\source\repos\Typedown\Dev\Typedown.Core\Controls\FloatControls",
    r"d:\source\repos\Typedown\Dev\Typedown.Core\Controls" # For RootControl
]

def process_cs_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    # namespaces
    content = content.replace("using Windows.UI.Xaml.Controls;", "using Avalonia.Controls;\nusing Avalonia.Controls.Primitives;")
    content = content.replace("using Windows.UI.Xaml;", "using Avalonia;\nusing Avalonia.Interactivity;")
    content = content.replace("using Windows.UI.Xaml.Media;", "using Avalonia.Media;")
    content = content.replace("using Windows.UI.Xaml.Input;", "using Avalonia.Input;")
    content = content.replace("using Windows.UI.Xaml.Markup;", "using Avalonia.Markup.Xaml;")
    content = content.replace("using Windows.Foundation;", "using Avalonia.Utilities;")
    content = content.replace("using Windows.System;", "using Avalonia.Input;")
    
    # Specific type mappings
    content = content.replace("DependencyProperty.Register", "AvaloniaProperty.Register")
    content = content.replace("DependencyProperty", "StyledProperty")
    content = content.replace("DependencyObject", "AvaloniaObject")
    content = content.replace("DispatcherTimer", "Avalonia.Threading.DispatcherTimer")
    content = content.replace("PointerRoutedEventArgs", "PointerEventArgs")
    content = content.replace("Visibility.Collapsed", "false")
    content = content.replace("Visibility.Visible", "true")

    # Fix property accessors for StyledProperty
    # public ___ ___Property = AvaloniaProperty.Register<MyClass, ___>(...) -> wait we can't regex everything perfectly, but we try.
    # The get => (double)GetValue(FlyoutOpacityProperty) -> GetValue(FlyoutOpacityProperty)
    content = re.sub(r'\(double\)\s*GetValue\(', 'GetValue(', content)
    content = re.sub(r'\(int\)\s*GetValue\(', 'GetValue(', content)
    content = re.sub(r'\(string\)\s*GetValue\(', 'GetValue(', content)
    content = re.sub(r'\(bool\)\s*GetValue\(', 'GetValue(', content)

    # Convert generic Register format if possible
    # DependencyProperty.Register(nameof(FlyoutOpacity), typeof(double), typeof(ToolTip), new(0d)); ->
    # AvaloniaProperty.Register<ToolTip, double>(nameof(FlyoutOpacity), 0d);
    def replace_register(m):
        prop_name = m.group(1)
        prop_type = m.group(2)
        owner_type = m.group(3)
        default_val = m.group(4)
        if "PropertyMetadata" in default_val:
            # try to extract just the value from PropertyMetadata(val)
            match = re.search(r'PropertyMetadata\(([^)]+)\)', default_val)
            if match:
                default_val = match.group(1)
        if "new(" in default_val:
             match = re.search(r'new\(([^)]+)\)', default_val)
             if match:
                 default_val = match.group(1)
        if "typeof" in prop_type:
            # Cannot infer directly easily, guess standard types
            ptype = prop_type.replace("typeof(", "").replace(")", "")
        else:
            ptype = "object"
            
        otype = owner_type.replace("typeof(", "").replace(")", "")
        return f'AvaloniaProperty.Register<{otype}, {ptype}>({prop_name}, {default_val})'

    content = re.sub(r'AvaloniaProperty\.Register\((nameof\([^)]+\)|\"[^\"]+\")\s*,\s*(typeof\([^)]+\))\s*,\s*(typeof\([^)]+\))\s*,?\s*([^;)]+)?\)', replace_register, content)

    # Save
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)

def process_axaml_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    content = content.replace("Visibility=\"Collapsed\"", "IsVisible=\"False\"")
    content = content.replace("Visibility=\"Visible\"", "IsVisible=\"True\"")
    # Bindings
    content = re.sub(r'x:Bind ([^,}]+)', r'Binding \1', content)
    
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)

for d in dirs:
    if os.path.exists(d):
        for root, _, files in os.walk(d):
             for file in files:
                filepath = os.path.join(root, file)
                if filepath.endswith(".cs"):
                     process_cs_file(filepath)
                if filepath.endswith(".axaml"):
                     process_axaml_file(filepath)

print("Migration script completed.")
