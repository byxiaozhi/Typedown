import os, re

dirs = [r'd:\source\repos\Typedown\Dev\Typedown.Core']

def fix_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Clear lingering Windows namespaces
    content = re.sub(r'using Windows\.UI[^;]*;', 'using Avalonia.Controls;', content)
    content = re.sub(r'using Microsoft\.UI[^;]*;', 'using Avalonia.Controls;', content)
    content = re.sub(r'using Windows\.Storage[^;]*;', 'using Avalonia.Platform.Storage;', content)
    content = re.sub(r'using Windows\.Foundation[^;]*;', 'using System.Collections.Generic;', content)
    
    # Fix DataTemplate
    if "DataTemplate" in content and "using Avalonia.Markup.Xaml.Templates;" not in content:
        content = content.replace("using Avalonia.Controls;", "using Avalonia.Controls;\nusing Avalonia.Markup.Xaml.Templates;")

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

for root, _, files in os.walk(dirs[0]):
    for file in files:
        if file.endswith(".cs") or file.endswith(".axaml.cs"):
            fix_file(os.path.join(root, file))

print("Pass 5 Done.")
