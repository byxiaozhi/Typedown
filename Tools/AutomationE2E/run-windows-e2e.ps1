<#
.SYNOPSIS
    Runs the automation end-to-end cases (docs/automation-api-analysis-plan.md, 10.6/10.7) in the logged-on desktop
    session. Not for SSH (session 0 cannot show the editor): start-interactive.ps1 runs this through a scheduled task.

    1. A fresh run id and data root %LOCALAPPDATA%\Typedown-AutomationTests\<runId>: the test host keeps everything
       there and uses its own instance and pipe names, so the everyday Typedown is never touched.
    2. Starts the automation test host (build-local.ps1 -AutomationTestHost) on that root.
    3. Runs the driver, which refuses anything but buildType=automationTestHost, and writes result.json.
    4. Closes the test host. On success the data root is deleted; on failure it and debug.log are kept.
    Exit code: 0 all passed, 1 a case failed, 3 environment error (no window, locked desktop, host never listened).
#>
param(
    [Parameter(Mandatory = $true)][string]$TestHost,
    [Parameter(Mandatory = $true)][string]$Driver,
    [Parameter(Mandatory = $true)][string]$Artifacts,
    [string]$Only = ''
)
$ErrorActionPreference = 'Stop'
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
$root = Join-Path $env:LOCALAPPDATA "Typedown-AutomationTests\$runId"
$fixtures = Join-Path $root 'fixtures'
$out = Join-Path $Artifacts $runId
New-Item -ItemType Directory -Force $fixtures, $out | Out-Null
"runId=$runId" | Out-File (Join-Path $out 'run.txt') -Encoding utf8

# Branding.props beside this script (where the E2E package copies it) or in the repository it belongs to.
$brandProps = @((Join-Path $PSScriptRoot 'Branding.props'), (Join-Path $PSScriptRoot '..\..\Branding.props')) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $brandProps) { throw "no Branding.props beside $PSScriptRoot or in its repository: copy it next to this script" }
$brand = ([xml](Get-Content $brandProps)).Project.PropertyGroup
# The app's exe under the edition's name.
$exe = Join-Path $TestHost ($brand.BrandExeName + '.exe')
if (-not (Test-Path (Join-Path $TestHost 'automation-test-host.marker'))) { throw "$TestHost is not an automation test host build" }
$hostProcess = Start-Process -FilePath $exe -ArgumentList @('--automation-test-root', "`"$root`"") -PassThru
$code = 3
try {
    & dotnet (Join-Path $Driver 'Typedown.AutomationE2E.dll') --root $root --pid $hostProcess.Id --exe $exe --fixtures $fixtures --out (Join-Path $out 'result.json') $(if ($Only) { '--only'; $Only }) *> (Join-Path $out 'driver.log')
    $code = $LASTEXITCODE
}
finally {
    # The driver may have restarted the host (R04): stop every test host started from this build.
    # By the test host's own path, whatever its name: an edition's was left running when only processes
    # named Typedown were looked at.
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    $log = Join-Path $root 'logs\debug.log'
    if ($code -ne 0 -and (Test-Path $log)) { Copy-Item $log (Join-Path $out 'debug.log') }
    if ($code -eq 0) { Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue }
    "exit=$code" | Out-File (Join-Path $out 'done.txt') -Encoding utf8
}
exit $code
