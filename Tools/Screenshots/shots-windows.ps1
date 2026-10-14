# README screenshots of the installed Windows app: 3 languages x 4 scenes in ONE running app, only through typedownctl:
# the language, the document (open this language's sample, close the others), the view, the theme, the window size. Runs in the logged-on session (scheduled task). The user's Typedown data (Documents\Typedown) is copied aside first and put back at the end, file for file.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace W -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[W.U]::SetProcessDPIAware() | Out-Null
# typedownctl writes UTF-8; PowerShell 5 would read it in the system code page and break the JSON.
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)

$app = 'C:\Program Files\Typedown'
$ctl = Join-Path $app 'typedownctl.exe'
$data = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Typedown'
$saved = 'E:\src\shots-saved-data'
$out = if ($env:SHOTS_OUT) { $env:SHOTS_OUT } else { 'E:\src\shots-win3' }
$docs = 'C:\Users\Public\Documents\Typedown'
$samples = Join-Path $PSScriptRoot 'samples'
$log = 'E:\src\shots-win3.log'
$names = @{ 'zh-Hans' = '写作手记.md'; 'en' = 'Writing.md'; 'ja' = '執筆メモ.md' }
$utf8 = New-Object Text.UTF8Encoding($false)
function Log($text) { Add-Content $log $text -Encoding UTF8 }
function Snapshot($root) { Get-ChildItem $root -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($root.Length) + ' ' + (Get-FileHash $_.FullName).Hash } | Sort-Object }

Remove-Item $log, $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $out, $docs | Out-Null
if (Test-Path $saved) { Log 'FAIL a saved copy is already there'; return }
if (Get-Process Typedown -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $app 'Typedown.exe') }) { Log 'FAIL Typedown is running'; return }
$before = Snapshot $data
robocopy $data $saved /MIR /NFL /NDL /NJH /NJS | Out-Null
$settingsPath = (Get-ChildItem $data -Filter 'Settings.json' -Force).FullName
$base = Get-Content (Join-Path $saved 'Settings.json') -Raw | ConvertFrom-Json
$ErrorActionPreference = 'Continue'
try {
    # No crash backups while the shots run: a recovery question would cover the window.
    Get-ChildItem (Join-Path $data 'Backup') -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    function Setting($key, $value) {
        $rev = ((& $ctl --json settings get $key) -join "`n" | ConvertFrom-Json).settingsRevision
        # Windows PowerShell 5 drops the inner double quotes of an argument to a native program: escape them.
        $result = (& $ctl --json settings set $key ($value -replace '"', '\"') --base-revision $rev) -join ''
        if ($result -notmatch 'settingsRevision') { Log "setting $key refused: $result" }
    }
    foreach ($lang in 'zh-Hans', 'en', 'ja') { Copy-Item (Join-Path $samples "$lang.md") (Join-Path $docs $names[$lang]) -Force }
    $s = $base | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $set = @{ Language = 'en'; AppTheme = 1; CustomTheme = ''; SourceCode = $false; ReadOnly = $false; SidePaneOpen = $false; SidePaneIndex = 1;
              FileStartupAction = 0; StatusBarOpen = $true; AllowLocalAutomation = $true; Topmost = $false }
    foreach ($k in $set.Keys) { $s | Add-Member -NotePropertyName $k -NotePropertyValue $set[$k] -Force }
    if (Test-Path $settingsPath) { [IO.File]::SetAttributes($settingsPath, 'Normal') }
    [IO.File]::WriteAllText($settingsPath, ($s | ConvertTo-Json -Depth 20), $utf8)
    $p = Start-Process (Join-Path $app 'Typedown.exe') -PassThru
    $up = $false
    for ($i = 0; $i -lt 60 -and -not $up; $i++) { Start-Sleep 1; & $ctl status *> $null; $up = ($LASTEXITCODE -eq 0) }
    $win = ((& $ctl --json windows) -join "`n" | ConvertFrom-Json).windows[0].windowId
    $h = [IntPtr]::Zero
    for ($i = 0; $i -lt 20 -and $h -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 500; $p.Refresh(); $h = $p.MainWindowHandle }
    & $ctl --json view $win --bounds 40,40,1280,860 *> $null
    [W.U]::SetForegroundWindow($h) | Out-Null
    function Shot($name) {
        $r = New-Object W.U+RECT
        [W.U]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) | Out-Null   # DWMWA_EXTENDED_FRAME_BOUNDS: the visible frame
        $bmp = New-Object Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
        $bmp.Save((Join-Path $out "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bmp.Dispose()
        Log ("{0}: {1}x{2}" -f $name, ($r.Right - $r.Left), ($r.Bottom - $r.Top))
    }
    foreach ($lang in 'zh-Hans', 'en', 'ja') {
        Setting 'ui.language' $lang
        $doc = Join-Path $docs $names[$lang]
        $id = ((& $ctl --json open $doc) -join "`n" | ConvertFrom-Json).documentId
        # Only this language's document stays: the startup blank and the previous sample are closed.
        foreach ($other in (((& $ctl --json documents) -join "`n" | ConvertFrom-Json).documents | Where-Object { $_.documentId -ne $id })) {
            $closed = (& $ctl --json close $other.documentId) -join ''
            if ($closed -notmatch '"closed":true') { Log "could not close $($other.title): $closed" }
        }
        for ($i = 0; $i -lt 5; $i++) { & $ctl --json get $id --latest *> $null; if ($LASTEXITCODE -eq 0) { break }; Start-Sleep 2 }
        & $ctl --json view $win --mode visual --side-pane closed *> $null
        Start-Sleep 5   # maths and diagrams, and no connection marker left in the title
        Shot "$lang-visual"
        & $ctl --json view $win --mode source *> $null; Start-Sleep 1; Shot "$lang-source"
        & $ctl --json view $win --mode visual --side-pane outline *> $null; Start-Sleep 1; Shot "$lang-outline"
        & $ctl --json view $win --side-pane closed *> $null; Setting 'appearance.theme' '{"kind":"builtIn","id":"dark"}'; Start-Sleep 3; Shot "$lang-dark"
        Setting 'appearance.theme' '{"kind":"builtIn","id":"light"}'; Start-Sleep 1
    }
    $p.Refresh()
    Log "one process for all: pid $($p.Id), still running: $(-not $p.HasExited)"
    foreach ($lang in 'zh-Hans', 'en', 'ja') { Remove-Item (Join-Path $docs $names[$lang]) -Force -ErrorAction SilentlyContinue }
}
catch { Log ("FAIL " + $_) }
finally {
    Get-Process Typedown -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $app 'Typedown.exe') } | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep 2
    robocopy $saved $data /MIR /NFL /NDL /NJH /NJS | Out-Null
    $restored = -not (Compare-Object $before (Snapshot $data))
    if ($restored) { Remove-Item $saved -Recurse -Force }
    Log "user data restored: $restored"
    Log 'DONE'
}
