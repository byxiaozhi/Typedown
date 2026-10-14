<#
.SYNOPSIS
    From SSH: runs run-windows-e2e.ps1 in the logged-on user's desktop session through a scheduled task
    ("run only when the user is logged on"), waits for it, prints result.json and exits with its code.
    Without a logged-on session the task never produces a result: that is reported as an environment error (3).
#>
param(
    [Parameter(Mandatory = $true)][string]$TestHost,
    [Parameter(Mandatory = $true)][string]$Driver,
    [Parameter(Mandatory = $true)][string]$Artifacts,
    [int]$TimeoutSeconds = 600,
    [string]$Only = ''
)
$ErrorActionPreference = 'Stop'
$task = 'TypedownAutomationE2E'
$script = Join-Path $PSScriptRoot 'run-windows-e2e.ps1'
New-Item -ItemType Directory -Force $Artifacts | Out-Null
$before = @(Get-ChildItem $Artifacts -Directory | ForEach-Object { $_.Name })
# The full command goes into a launcher file: schtasks cuts /TR at 261 characters, and a cut command runs with
# broken arguments and never reports. Hidden: a console window would cover the test host and take the foreground.
$launcher = Join-Path $PSScriptRoot 'launch-e2e.cmd'
"@powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$script`" -TestHost `"$TestHost`" -Driver `"$Driver`" -Artifacts `"$Artifacts`" -Only `"$Only`"" |
    Out-File $launcher -Encoding ascii
schtasks /Create /TN $task /TR "`"$launcher`"" /SC ONCE /ST 23:59 /IT /F | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Output "ENVIRONMENT ERROR: could not register the scheduled task"; exit 3 }
schtasks /Run /TN $task | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Output "ENVIRONMENT ERROR: could not start the scheduled task"; exit 3 }
# The task goes when this script does, however it ends: its trigger (once, at 23:59 today - schtasks needs one) stayed
# behind and ran the day's last run again at 23:59, unasked.
try {
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$run = $null
$started = Get-Date
while ((Get-Date) -lt $deadline) {
    $run = Get-ChildItem $Artifacts -Directory | Where-Object { $before -notcontains $_.Name } | Select-Object -First 1
    if ($run -and (Test-Path (Join-Path $run.FullName 'done.txt'))) { break }
    # A task that never got as far as creating its run folder has failed to start: say so now, not at the deadline.
    if (-not $run -and ((Get-Date) - $started).TotalSeconds -gt 30) {
        $info = schtasks /Query /TN $task /V /FO LIST | Out-String
        Write-Output "ENVIRONMENT ERROR: the task started no run within 30 s`n$info"
        exit 3
    }
    Start-Sleep -Seconds 2
}
if (-not $run -or -not (Test-Path (Join-Path $run.FullName 'done.txt'))) {
    Write-Output "ENVIRONMENT ERROR: no result from the interactive task within $TimeoutSeconds s (nobody logged on, or the desktop is locked)"
    exit 3
}
$result = Join-Path $run.FullName 'result.json'
if (Test-Path $result) { Get-Content $result -Raw -Encoding utf8 } else { Get-Content (Join-Path $run.FullName 'driver.log') -Raw -ErrorAction SilentlyContinue }
$exit = [int]((Get-Content (Join-Path $run.FullName 'done.txt')) -replace 'exit=', '')
Write-Output "artifacts: $($run.FullName)"
exit $exit
} finally {
    schtasks /Delete /TN $task /F 2>&1 | Out-Null
}
