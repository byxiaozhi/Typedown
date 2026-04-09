$targetDirs = @(
    "D:\source\repos\Typedown\Dev\Typedown.Core\Controls\FloatControls",
    "D:\source\repos\Typedown\Dev\Typedown.Core\Controls\CommonControls"
)
$rootControl = "D:\source\repos\Typedown\Dev\Typedown.Core\Controls\RootControl.xaml"
$rootControlCs = "D:\source\repos\Typedown\Dev\Typedown.Core\Controls\RootControl.xaml.cs"

# Rename xaml to axaml
If (Test-Path $rootControl) {
    Rename-Item $rootControl "RootControl.axaml"
}

foreach ($dir in $targetDirs) {
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -Filter "*.xaml" -Recurse | Rename-Item -NewName { $_.Name -replace '\.xaml$','.axaml' }
    }
}

# Perform text replace on all .axaml and .cs files in these directories + RootControl
$allFiles = @("D:\source\repos\Typedown\Dev\Typedown.Core\Controls\RootControl.axaml", "D:\source\repos\Typedown\Dev\Typedown.Core\Controls\RootControl.xaml.cs")

foreach ($dir in $targetDirs) {
    if (Test-Path $dir) {
        $files = Get-ChildItem -Path $dir -Include *.axaml, *.cs -Recurse
        $allFiles += $files.FullName
    }
}

foreach ($file in $allFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        $content = $content -replace "using Windows\.UI\.Xaml\.Controls;", "using Avalonia.Controls;"
        $content = $content -replace "using Windows\.UI\.Xaml;", "using Avalonia;"
        $content = $content -replace "using Windows\.UI\.Xaml\.Media;", "using Avalonia.Media;"
        $content = $content -replace "using Windows\.UI\.Xaml\.Input;", "using Avalonia.Input;"
        $content = $content -replace "using Windows\.UI\.Xaml\.Markup;", "using Avalonia.Markup.Xaml;"
        $content = $content -replace "using Windows\.Foundation;", "using Avalonia.Utilities;"
        # Xaml namespaces
        $content = $content -replace 'xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"', 'xmlns="https://github.com/avaloniaui"'
        $content = $content -replace "Windows\.UI\.Xaml\.Visibility", "bool" # Visibility to IsVisible mapping roughly
        
        Set-Content -Path $file -Value $content
    }
}

Write-Host "Migration script done."
