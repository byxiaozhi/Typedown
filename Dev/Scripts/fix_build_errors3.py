import os
import re

dirs = [
    r"d:\source\repos\Typedown\Dev\Typedown.Core\Controls\CommonControls",
    r"d:\source\repos\Typedown\Dev\Typedown.Core\Controls\FloatControls",
    r"d:\source\repos\Typedown\Dev\Typedown.Core\Controls"
]

def process_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Fix readonly AvaloniaProperty property
    content = content.replace("public static readonly AvaloniaProperty ", "public static AvaloniaProperty ")
    
    # Fix remaining Windows.UI imports
    content = content.replace("using Windows.UI;", "using Avalonia.Media;")
    content = content.replace("using Windows.UI.Text;", "using Avalonia.Media;")
    
    # RootControl Pages exclusion temporary fix
    if "RootControl.xaml.cs" in path:
        content = content.replace("using Typedown.Core.Pages;", "// using Typedown.Core.Pages;")

    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)

for d in dirs:
    if os.path.exists(d):
        for root, _, files in os.walk(d):
             for file in files:
                filepath = os.path.join(root, file)
                if filepath.endswith(".cs") or filepath.endswith(".axaml"):
                     process_file(filepath)

print("Pass 3 completed.")
