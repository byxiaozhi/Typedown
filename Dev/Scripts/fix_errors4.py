import os
import re

dirs = [r'd:\source\repos\Typedown\Dev\Typedown.Core']

def fix_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Generic replaces
    content = content.replace("StyledPropertyChangedEventArgs", "AvaloniaPropertyChangedEventArgs")
    content = content.replace("using Windows.UI.Core;", "")
    content = content.replace("Windows.UI.Core.CoreCursor", "Avalonia.Input.Cursor")
    content = content.replace("Windows.UI.Core.CoreCursorType", "Avalonia.Input.StandardCursorType")
    content = content.replace("Windows.System.VirtualKey", "Avalonia.Input.Key")
    content = content.replace("Windows.System.VirtualKeyModifiers", "Avalonia.Input.KeyModifiers")
    content = content.replace("[ContentProperty(", "[Content(")
    content = content.replace("DependencyObject", "AvaloniaObject")
    content = content.replace("MenuFlyoutItemBase", "MenuItem")
    content = content.replace("MenuFlyoutItem", "MenuItem")
    content = content.replace("MenuFlyoutSeparator", "Separator")
    content = content.replace("MenuFlyoutSubItem", "MenuItem")
    
    # Add usings if missing
    usings = [
        "using Avalonia.Interactivity;", 
        "using Avalonia.Data.Converters;", 
        "using Avalonia.Metadata;", 
        "using Avalonia.Input;", 
        "using Avalonia.Controls.Primitives;", 
        "using System.Collections.ObjectModel;"
    ]
    for using_stmt in usings:
        if using_stmt not in content and "using System" in content and ("class" in content or "struct" in content):
            content = content.replace("using System;", f"using System;\n{using_stmt}")

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

for root, _, files in os.walk(dirs[0]):
    for file in files:
        if file.endswith(".cs") or file.endswith(".axaml.cs"):
            fix_file(os.path.join(root, file))

print("Final heuristics pass completed.")
