import os
import re

errors_file = r"d:\source\repos\Typedown\Dev\Scripts\build_errors.txt"

with open(errors_file, 'r', encoding='utf-16') as f:
    lines = f.readlines()

# Dict of file -> set of actions to take
file_actions = {}

for line in lines:
    # Example: d:\source\repos\...ToolTip.xaml.cs(88,48): error CS0246: 未能找到类型或命名空间名“RoutedEventArgs”
    match = re.match(r'^(.*?)\((\d+),(\d+)\):\s*error\s*(CS\d+):\s*(.*)$', line)
    if not match:
        continue
    filepath = match.group(1).strip()
    line_num = int(match.group(2))
    err_code = match.group(4)
    msg = match.group(5)
    
    if filepath not in file_actions:
        file_actions[filepath] = {"add_usings": set(), "fixes": set(), "errors": []}
        
    file_actions[filepath]["errors"].append((line_num, err_code, msg))
    
    if "RoutedEventArgs" in msg:
        file_actions[filepath]["add_usings"].add("Avalonia.Interactivity")
    if "FlyoutPlacementMode" in msg or "FlyoutShowMode" in msg:
        file_actions[filepath]["add_usings"].add("Avalonia.Controls.Primitives")
    if "Dispatcher" in msg or "DispatcherTimer" in msg:
        file_actions[filepath]["add_usings"].add("Avalonia.Threading")
    if "NavigationTransitionInfo" in msg:
        file_actions[filepath]["fixes"].add("Remove_NavigationTransitionInfo")

for filepath, actions in file_actions.items():
    if not os.path.exists(filepath):
        continue
        
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
        
    # Apply global generic fixes
    content = content.replace("public static StyledProperty ", "public static readonly AvaloniaProperty ")
    content = content.replace("public static readonly StyledProperty ", "public static readonly AvaloniaProperty ")
    content = content.replace("public StyledProperty ", "public AvaloniaProperty ")
    content = content.replace("Avalonia.Threading.Avalonia.Threading.", "Avalonia.Threading.")
    content = content.replace("Windows.UI.Xaml.RoutedEventArgs", "RoutedEventArgs")
    content = content.replace("DependencyPropertyChangedEventArgs", "AvaloniaPropertyChangedEventArgs")
    content = content.replace("protected override void OnApplyTemplate()", "protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)")
    
    # Add Usings
    using_additions = ""
    for u in actions["add_usings"]:
        using_statement = f"using {u};"
        if using_statement not in content:
            using_additions += using_statement + "\n"
            
    if using_additions:
        # insert after the first using or top of file if no using
        if "using " in content:
            content = content.replace("using ", using_additions + "using ", 1)
        else:
            content = using_additions + content
            
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)

print(f"Processed {len(file_actions)} files using AI heuristics.")
