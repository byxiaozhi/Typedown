<#
.SYNOPSIS
    Builds the Windows app and packs it into the Inno Setup installer, the same way CI does, on a machine with
    the Build Tools installed.

.DESCRIPTION
    Three steps: MSBuild publishes Dev\Typedown (self-contained, per architecture, precompiled with ReadyToRun
    as in CI), the output is copied to Tools\Installer\publish, and ISCC compiles Tools\Installer\Typedown.iss
    over it. -AutomationTestHost only builds (the E2E scripts run the test host from its bin folder). The result is
    Tools\Installer\Output\Typedown-windows-<arch>-v<version>.exe.

    Unlike CI it does not sign anything — CI signs with a self-signed certificate the system does not trust
    either, so for testing on your own machine the difference is one more SmartScreen prompt.

    What it needs, and where it looks:
      MSBuild        Visual Studio 2022 Build Tools with the "UWP" and ".NET desktop" build tool workloads,
                     found through vswhere.
      Windows SDK    read from the KitsRoot10 registry value, so an SDK installed outside Program Files is
                     found too; mt.exe and makepri.exe from the newest version present are passed to MSBuild,
                     which otherwise looks for them under Program Files only and fails with MSB3073.
      Inno Setup 6   ISCC.exe, either the per-user install (%LOCALAPPDATA%\Programs\Inno Setup 6, which needs
                     no administrator) or the machine-wide one.
      Editor bundle  Dev\Typedown\Resources\Statics, which is not in the repository: build it with
                     "npm ci && npm run build" in Dev\Typedown.Editor, or copy it from another machine.
                     The script refuses to package a bundle older than the editor sources, so a change to the
                     editor cannot quietly stay out of the installer; -BuildEditor rebuilds it here instead.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\Installer\build-local.ps1
    powershell -ExecutionPolicy Bypass -File Tools\Installer\build-local.ps1 -Platform ARM64 -SkipBuild
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    # Packs whatever was built last instead of building again.
    [switch]$SkipBuild,
    # Stops after the build, without making an installer.
    [switch]$NoInstaller,
    # What to show in About instead of "local.<commit>"; empty keeps whatever the file already says.
    [string]$Label,
    # Rebuilds the editor bundle first (needs node and Dev\Typedown.Editor\node_modules).
    [switch]$BuildEditor,
    # Builds the automation test host instead (the app with test.* methods and edit barriers, its own data root) into
    # Dev\Typedown\bin\AutomationTestHost. Implies -NoInstaller: a test host is never packaged.
    [switch]$AutomationTestHost
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Set-Location $repo

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) { throw "vswhere not found - install Visual Studio 2022 Build Tools (see the comment at the top of this script)" }
    $install = & $vswhere -products * -requires Microsoft.Component.MSBuild -property installationPath | Select-Object -First 1
    if (-not $install) { throw "no Visual Studio installation with MSBuild found" }
    $msbuild = Join-Path $install 'MSBuild\Current\Bin\MSBuild.exe'
    if (-not (Test-Path $msbuild)) { throw "MSBuild not found under $install" }
    $msbuild
}

function Find-SdkTools {
    $root = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots' -ErrorAction SilentlyContinue).KitsRoot10
    if (-not $root) { $root = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10' }
    $bin = Join-Path $root 'bin'
    if (-not (Test-Path $bin)) { throw "no Windows SDK under $root" }
    $version = Get-ChildItem $bin -Directory |
        Where-Object { $_.Name -match '^10\.' -and (Test-Path (Join-Path $_.FullName 'x86\mt.exe')) } |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (-not $version) { throw "no Windows SDK with mt.exe under $bin" }
    @{
        ManifestTool = Join-Path $version.FullName 'x86\mt.exe'
        MakePri      = Join-Path $version.FullName 'x86\makepri.exe'
        Version      = $version.Name
    }
}

function Find-ISCC {
    foreach ($p in @(
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'))) {
        if (Test-Path $p) { return $p }
    }
    throw "ISCC.exe not found - install Inno Setup 6 (innosetup-6.x.exe /VERYSILENT /CURRENTUSER needs no administrator)"
}

$editorDir = Join-Path $repo 'Dev\Typedown.Editor'
$statics = Join-Path $repo 'Dev\Typedown\Resources\Statics\index.html'

if ($BuildEditor) {
    if (-not (Test-Path (Join-Path $editorDir 'node_modules'))) {
        throw "the editor has no dependencies installed - run 'npm ci' (or 'yarn install') in Dev\Typedown.Editor first"
    }
    Write-Host 'Building the editor bundle'
    Push-Location $editorDir
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "the editor build failed" }
    } finally { Pop-Location }
}

if (-not (Test-Path $statics)) {
    throw "the editor bundle is missing (Dev\Typedown\Resources\Statics) - build it in Dev\Typedown.Editor with npm run build, or copy it from a machine that has it"
}

# A bundle older than the editor sources would mean packaging an app whose editor is a version behind, which
# is silent and very easy to miss: the installer builds, runs, and simply does not contain the change.
$builtAt = (Get-Item $statics).LastWriteTimeUtc
$newestSource = Get-ChildItem (Join-Path $editorDir 'src') -Recurse -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($newestSource -and $newestSource.LastWriteTimeUtc -gt $builtAt) {
    $rel = $newestSource.FullName.Substring($repo.Length).TrimStart('\')
    throw "the editor bundle is older than the editor sources ($rel changed after it was built) - rerun with -BuildEditor, or copy a fresh bundle from the machine that built it"
}

$version = ([xml](Get-Content 'Dev\Typedown\Typedown.csproj')).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
$arch = $Platform.ToLowerInvariant()
Write-Host "Typedown $version  $Platform  $Configuration"

# About shows this next to the version. CI stamps its run and commit here, so a local build says where it came
# from too rather than carrying whatever label the repository was committed with.
function Get-LocalLabel {
    if ($Label) { return $Label }
    try {
        $commit = (git rev-parse --short HEAD 2>$null)
        if ($LASTEXITCODE -ne 0 -or -not $commit) { return "local.$(Get-Date -Format yyyyMMdd.HHmm)" }
        $dirty = (git status --porcelain 2>$null | Measure-Object).Count -gt 0
        return "local.$commit" + $(if ($dirty) { '+dirty' } else { '' })
    }
    catch {
        return "local.$(Get-Date -Format yyyyMMdd.HHmm)"
    }
}

if (-not $SkipBuild) {
    $msbuild = Find-MSBuild
    $sdk = Find-SdkTools
    Write-Host "MSBuild: $msbuild"
    Write-Host "Windows SDK: $($sdk.Version)"

    # The label lives in a source file, so it is put back afterwards: building must not leave the checkout
    # modified, or the next build would call itself dirty and a commit could carry the label by accident.
    $configPath = Join-Path $repo 'Dev\Typedown.Core\Config.cs'
    $configOriginal = Get-Content $configPath -Raw
    $label = Get-LocalLabel
    Write-Host "Label: $label"
    $stamped = [regex]::Replace($configOriginal, 'public const string TestBuild = "[^"]*";', "public const string TestBuild = `"$label`";")
    if ($stamped -eq $configOriginal) { Write-Warning "TestBuild not found in Config.cs; building without a label" }
    try {
        Set-Content $configPath $stamped -Encoding utf8 -NoNewline
        # /restore re-evaluates the project after generating NuGet imports. Running
        # Restore,Build as targets in one evaluation misses those imports on a fresh checkout.
        $hostArgs = @()
        if ($AutomationTestHost) { $hostArgs = @('/p:AutomationTestHost=true') }
        # The application is published, as the packaging project does in CI: a release comes out precompiled
        # (ReadyToRun, Typedown.csproj). The test host is only built - the E2E scripts run it from its bin folder.
        $target = if ($AutomationTestHost) { '/t:Build' } else { '/t:Publish' }
        # An array built up, not "if (...) { @(...) }": PowerShell unwraps a one-element array to its string, and
        # splatting a string passes it character by character.
        $publishArgs = @()
        if (-not $AutomationTestHost) {
            # Emptied first: publish only adds, and a checkout the WinUI 3 build also uses kept that build's files
            # beside these (assert-application-build.ps1 refuses such a folder).
            Remove-Item (Join-Path $repo "Dev\Typedown\bin\$Platform\$Configuration\publish") -Recurse -Force -ErrorAction SilentlyContinue
            $publishArgs += "/p:PublishDir=$(Join-Path $repo "Dev\Typedown\bin\$Platform\$Configuration\publish")\"
        }
        & $msbuild 'Dev\Typedown\Typedown.csproj' /restore $target /m /v:m `
            /p:Configuration=$Configuration /p:Platform=$Platform `
            /p:ManifestTool=$($sdk.ManifestTool) /p:MakePri=$($sdk.MakePri) @hostArgs @publishArgs
        $buildFailed = $LASTEXITCODE -ne 0
    }
    finally {
        Set-Content $configPath $configOriginal -Encoding utf8 -NoNewline
    }
    if ($buildFailed) { throw "build failed" }
}

$rid = if ($Platform -eq 'ARM64') { 'win10-arm64' } else { 'win10-x64' }
if ($AutomationTestHost) {
    $published = Join-Path $repo "Dev\Typedown\bin\AutomationTestHost\$Platform\$Configuration\netcoreapp3.1\$rid"
    if (-not (Test-Path (Join-Path $published 'automation-test-host.marker'))) { throw "no test host marker in $published" }
    Write-Host "Automation test host: $published"
    return
}
$published = Join-Path $repo "Dev\Typedown\bin\$Platform\$Configuration\publish"
$exe = ([xml](Get-Content (Join-Path $repo 'Branding.props'))).Project.PropertyGroup.BrandExeName + '.exe'
if (-not (Test-Path (Join-Path $published $exe))) { throw "$exe not found in $published" }
Write-Host "App: $published"
& (Join-Path $PSScriptRoot 'assert-application-build.ps1') -Path $published
if ($NoInstaller) { return }

$iscc = Find-ISCC
$stage = Join-Path $repo 'Tools\Installer\publish'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
# Not the MSIX project's own build of the app (win-x64\msixpublish, made beside this one when the package is built
# here too): 140 MB the installer has no use for.
Get-ChildItem $published | Where-Object { -not ($_.PSIsContainer -and $_.Name -match '^win-(x64|x86|arm64)$') } |
    Copy-Item -Destination $stage -Recurse -Force
# The CLI (and its MCP server) beside the app, on the app's runtime, with the automation documents.
& (Join-Path $PSScriptRoot 'add-cli.ps1') -AppFolder $stage -RuntimeIdentifier $rid -Configuration $Configuration
& (Join-Path $PSScriptRoot 'assert-application-build.ps1') -Path $stage

& $iscc /DMyAppVersion=$version /DMyArch=$arch 'Tools\Installer\Typedown.iss' | Select-String -Pattern 'error|Successful compile' | ForEach-Object { $_.Line }
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

$setup = Get-ChildItem 'Tools\Installer\Output\*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ("Installer: {0}  {1:N1} MB" -f $setup.FullName, ($setup.Length / 1MB))
