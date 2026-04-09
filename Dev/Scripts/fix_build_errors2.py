import os
import re

errors_file = r"d:\source\repos\Typedown\Dev\Scripts\build_errors.txt"

with open(errors_file, 'r', encoding='utf-16') as f:
    lines = f.readlines()

file_actions = {}

for line in lines:
    match = re.match(r'^(.*?)\((\d+),(\d+)\):\s*error\s*(CS\d+):\s*(.*)$', line)
    if not match:
        continue
    filepath = match.group(1).strip()
    msg = match.group(5)
    
    if filepath not in file_actions:
        file_actions[filepath] = {"add_usings": set(), "replaces": []}
        
    if "FlyoutBase" in msg or "Flyout" in msg:
        file_actions[filepath]["add_usings"].add("Avalonia.Controls.Primitives")
    if "KeyRoutedEventArgs" in msg:
        file_actions[filepath]["replaces"].append(("KeyRoutedEventArgs", "KeyEventArgs"))
    if "Visibility" in msg:
        file_actions[filepath]["replaces"].append(("Visibility", "bool"))
    if "MenuFlyoutItemBase" in msg:
        file_actions[filepath]["replaces"].append(("MenuFlyoutItemBase", "MenuItem"))

for filepath, actions in file_actions.items():
    if not os.path.exists(filepath):
        continue
        
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Apply replacements
    for old, new in actions["replaces"]:
        content = content.replace(old, new)
        
    # Standard fixes
    content = content.replace("using Windows.UI.Core;", "using Avalonia.Input;")
    
    using_additions = ""
    for u in actions["add_usings"]:
        using_statement = f"using {u};"
        if using_statement not in content:
            using_additions += using_statement + "\n"
            
    if using_additions:
        if "using " in content:
            content = content.replace("using ", using_additions + "using ", 1)
        else:
            content = using_additions + content
            
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

print(f"Processed {len(file_actions)} files using Pass 2 heuristics.")
