# Release-candidate check of the installed Typedown on this PC: the direct clients, typedownctl and typedownctl mcp,
# all as installed. The user's Typedown data (Documents\Typedown for the installed app: settings, session, recent
# files, cursors, crash backups) is copied aside first and put back at the end, file for file, so the check leaves
# nothing behind.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
# The edition's names - its install folder, exe, CLI and data folder - from Branding.props beside this script (where the E2E package copies it) or in the repository it belongs to.
$brandProps = @((Join-Path $PSScriptRoot 'Branding.props'), (Join-Path $PSScriptRoot '..\..\Branding.props')) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $brandProps) { throw "no Branding.props beside $PSScriptRoot or in its repository: copy it next to this script" }
$brand = ([xml](Get-Content $brandProps)).Project.PropertyGroup
$app = Join-Path $env:ProgramFiles $brand.BrandName
$exe = Join-Path $app ($brand.BrandExeName + '.exe')
$ctl = Join-Path $app ($brand.BrandCliName + '.exe')
$examples = Join-Path $app 'docs\automation-examples'
$data = Join-Path ([Environment]::GetFolderPath('MyDocuments')) $brand.BrandName
$work = 'E:\src\rc'
$saved = 'E:\src\rc-saved-data'
$results = New-Object System.Collections.Generic.List[string]
function Check($ok, $label) { $results.Add(($(if ($ok) { 'PASS ' } else { 'FAIL ' }) + $label)) }

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $work | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText("$work\rc-a.md", "# RC`n`nThe quick brown fox.`n", $utf8)

# 1. Keep the user's data.
if (Test-Path $saved) { throw "a previous check's saved copy is still at $saved - restore it first" }
if (Get-Process $brand.BrandExeName -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) { throw "$($brand.BrandName) is running - quit it first" }
# -Force: Typedown 1.2.27-1.2.30 left the files it saves hidden (SafeFile's temp attributes survive the rename).
function Snapshot($root) { Get-ChildItem $root -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($root.Length) + ' ' + (Get-FileHash $_.FullName).Hash } | Sort-Object }
$before = Snapshot $data
robocopy $data $saved /MIR /NFL /NDL /NJH /NJS | Out-Null
$settingsFile = Get-ChildItem $data -Filter 'Settings.json' -Force | Select-Object -First 1
if (-not $settingsFile) { throw "no Settings.json in $data" }
$settings = Get-Content $settingsFile.FullName -Raw | ConvertFrom-Json
$settings | Add-Member -NotePropertyName AllowLocalAutomation -NotePropertyValue $true -Force
$attributes = $settingsFile.Attributes
[IO.File]::SetAttributes($settingsFile.FullName, 'Normal')
[IO.File]::WriteAllText($settingsFile.FullName, ($settings | ConvertTo-Json -Depth 20), $utf8)
[IO.File]::SetAttributes($settingsFile.FullName, $attributes)

$started = Get-Date
# From here native programs speak on stderr as part of the check (typedownctl before the app is up, conflicts):
# PowerShell 5 would turn that into a terminating error under 'Stop'.
$ErrorActionPreference = 'Continue'
try {
    # 2. Start the installed app in the logged-on session.
    Set-Content "$work\start.cmd" "start `"`" `"$exe`" `"$work\rc-a.md`"" -Encoding ASCII
    schtasks /Create /TN TdRcStart /TR "$work\start.cmd" /SC ONCE /ST 23:59 /IT /F | Out-Null
    schtasks /Run /TN TdRcStart | Out-Null
    $up = $false
    for ($i = 0; $i -lt 60 -and -not $up; $i++) { Start-Sleep 1; & $ctl status *> $null; $up = ($LASTEXITCODE -eq 0) }
    schtasks /Delete /TN TdRcStart /F | Out-Null
    Check $up 'the installed app answers typedownctl status'
    if (-not $up) { return }
    Start-Sleep 3

    # 3. typedownctl.
    $docs = (& $ctl --json documents) | ConvertFrom-Json
    $a = ($docs.documents | Where-Object { $_.path -like '*rc-a.md' } | Select-Object -First 1).documentId
    Check ($null -ne $a) 'typedownctl documents lists the opened file'
    $doc = (& $ctl --json get $a --latest --text) | ConvertFrom-Json
    Check ($doc.text -eq "# RC`n`nThe quick brown fox.`n") 'typedownctl get --latest --text is exact'
    & $ctl --json replace-text $a --base-revision $doc.revision --find brown --replacement red --expected-count 1 --save | Out-Null
    Check ($LASTEXITCODE -eq 0 -and [IO.File]::ReadAllText("$work\rc-a.md") -eq "# RC`n`nThe quick red fox.`n") 'typedownctl replace-text --save writes the file'
    & $ctl --json replace-text $a --base-revision $doc.revision --find red --replacement blue --expected-count 1 *> $null
    Check ($LASTEXITCODE -eq 6) "a stale revision is exit 6 (got $LASTEXITCODE)"

    # 4. typedownctl mcp, as an AI tool starts it.
    $psi = New-Object Diagnostics.ProcessStartInfo $ctl, 'mcp'
    $psi.UseShellExecute = $false; $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = $utf8
    $mcp = [Diagnostics.Process]::Start($psi)
    $script:id = 0
    function Send($method, $params) {
        $script:id++
        $mcp.StandardInput.WriteLine((@{ jsonrpc = '2.0'; id = $script:id; method = $method; params = $params } | ConvertTo-Json -Depth 10 -Compress))
        $mcp.StandardInput.Flush()
        $mcp.StandardOutput.ReadLine() | ConvertFrom-Json
    }
    $init = Send 'initialize' @{ protocolVersion = '2025-06-18'; clientInfo = @{ name = 'RC agent'; version = '1' } }
    Check ($init.result.serverInfo.name -eq ($brand.BrandName.ToLowerInvariant() + '-mcp')) 'typedownctl mcp answers initialize'
    $read = (Send 'tools/call' @{ name = 'typedown_read_document'; arguments = @{ documentId = $a } }).result
    $w = (Send 'tools/call' @{ name = 'typedown_replace_text'; arguments = @{ documentId = $a; baseRevision = $read.structuredContent.revision; find = 'quick'; replacement = 'very quick'; reveal = $true } }).result
    Check (-not $w.isError) 'MCP replace_text with reveal'
    $stale = (Send 'tools/call' @{ name = 'typedown_replace_text'; arguments = @{ documentId = $a; baseRevision = $read.structuredContent.revision; find = 'red'; replacement = 'blue' } }).result
    Check ($stale.isError -and $stale.structuredContent.error.data.kind -eq 'revision_conflict') 'MCP: a stale revision is revision_conflict, and says to read again'
    $mcp.StandardInput.Close()
    Check ($mcp.WaitForExit(10000)) 'typedownctl mcp exits when its input closes'

    # 5. The installed examples.
    $py = & python "$examples\typedown_client.py" replace-text $a fox cat 2>&1
    Check ($LASTEXITCODE -eq 0) "the installed Python example writes ($py)"
    $ps = & powershell -NoProfile -ExecutionPolicy Bypass -Command ". '$examples\Typedown-Client.ps1'; `$c = Connect-Typedown; Initialize-Typedown `$c @('document.read','document.write') | Out-Null; Set-TypedownText `$c '$a' 'cat' 'dog' | Out-Null; `$c.Pipe.Dispose()" 2>&1
    Check ($LASTEXITCODE -eq 0) "the installed PowerShell example writes ($ps)"
    $final = ((& $ctl --json get $a --latest --text) | ConvertFrom-Json).text
    Check ($final -eq "# RC`n`nThe very quick red dog.`n") "every write is in the document ($($final -replace "`n", '|'))"

    # 5b. Settings and view through the installed typedownctl, in the window that is open (no restart). Settings first:
    # a resized window stores its placement a moment later, which advances the settings revision.
    $got = (& $ctl --json settings get ui.language) | ConvertFrom-Json
    $language = $got.values.'ui.language'
    $set = (& $ctl --json settings set ui.language ja --base-revision $got.settingsRevision) | ConvertFrom-Json
    Check ($set.value -eq 'ja') "typedownctl settings set ui.language ja (was $language)"
    $r = ((& $ctl --json settings get ui.language) | ConvertFrom-Json).settingsRevision
    & $ctl --json settings set ui.language $language --base-revision $r | Out-Null
    Check ($LASTEXITCODE -eq 0) "and back to $language"
    $w = ((& $ctl --json windows) | ConvertFrom-Json).windows[0].windowId
    $revisionBefore = ((& $ctl --json settings get editor.fontSize) | ConvertFrom-Json).settingsRevision
    $v = (& $ctl --json view $w --mode source --side-pane outline --size 1100x700) | ConvertFrom-Json
    Check ($v.mode -eq 'source' -and $v.sidePane.open -and $v.sidePane.page -eq 'outline' -and $v.bounds.width -eq 1100 -and $v.bounds.height -eq 700) "typedownctl view: source mode, the outline, 1100x700 in one call ($($v | ConvertTo-Json -Compress))"
    $v = (& $ctl --json view $w --mode visual --side-pane closed) | ConvertFrom-Json
    Check ($v.mode -eq 'visual' -and -not $v.sidePane.open) 'typedownctl view: back to visual, the pane closed'
    # The window saves its placement a moment after a resize: the app's own state, not a setting.
    Start-Sleep 3
    $revisionAfter = ((& $ctl --json settings get editor.fontSize) | ConvertFrom-Json).settingsRevision
    Check ($null -ne $revisionBefore -and $revisionAfter -eq $revisionBefore) "the view changes and the saved placement leave settingsRevision as it was ($revisionBefore -> $revisionAfter)"

    # 6. The equivalence scenario against the installed app (compared with the recorded Windows answer elsewhere).
    # Through cmd, so the transcript keeps Python's UTF-8 bytes (PowerShell 5 would re-encode it to UTF-16).
    $env:PYTHONIOENCODING = 'utf-8'
    cmd /c "python E:\src\rc-scenario.py --examples `"$examples`" --workdir `"$work\eq`" > `"$work\eq-rc.json`""
    Check ($LASTEXITCODE -eq 0) 'the equivalence scenario ran against the installed app'
}
finally {
    # 7. Stop what this check started, put the user's data back, drop the files it made.
    Get-Process $brand.BrandExeName -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -gt $started } | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process $brand.BrandCliName -ErrorAction SilentlyContinue | Where-Object { $_.StartTime -gt $started } | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep 2
    robocopy $saved $data /MIR /NFL /NDL /NJH /NJS | Out-Null
    $after = Snapshot $data
    $restored = -not (Compare-Object $before $after)
    if ($restored) { Remove-Item $saved -Recurse -Force }
    $results.Add("user data restored: $restored" + $(if (-not $restored) { " (copy kept at $saved)" } else { '' }))
    $results | ForEach-Object { $_ }
}
