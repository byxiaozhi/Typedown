<#
.SYNOPSIS
    Adds typedownctl (the automation CLI, and "typedownctl mcp", the MCP server) and the automation documents to an
    app folder about to be packaged. Called by build-local.ps1 and the CI packaging step.

.DESCRIPTION
    typedownctl is built for netcoreapp3.1, self-contained, for the app's own runtime identifier: its runtime files
    are the ones the app already carries, so the installer grows by a few hundred KB instead of a second runtime.
    Every file the CLI brings is checked against the app folder:
      - not there yet: copied;
      - there and byte for byte the same: left alone;
      - there and different: the build stops. A different runtime file would replace the app's own. The only
        exceptions are the libraries both are compiled against (Typedown.Automation, Newtonsoft.Json): the app's
        copy stays, provided the assembly version is the same.
    Then typedownctl.exe is run from the app folder ("help") to prove it starts on the app's runtime (not for an
    ARM64 build on an x64 machine, which cannot start it), and the
    documents go to <app>\docs with their relative links intact.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools\Installer\add-cli.ps1 -AppFolder Tools\Installer\publish -RuntimeIdentifier win10-x64
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AppFolder,
    [Parameter(Mandatory = $true)][string]$RuntimeIdentifier,
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$AppFolder = (Resolve-Path $AppFolder).Path
# The edition's names (Branding.props): the app's exe, the CLI.
$brand = ([xml](Get-Content (Join-Path $repo 'Branding.props'))).Project.PropertyGroup
$appExe = $brand.BrandExeName + '.exe'
$cli = $brand.BrandCliName
if (-not (Test-Path (Join-Path $AppFolder $appExe))) { throw "add-cli: no $appExe in $AppFolder" }

$out = Join-Path ([IO.Path]::GetTempPath()) ("$cli-" + [Guid]::NewGuid().ToString('N'))
# The portable identifier (win-x64 for the app's win10-x64): the project also targets net8.0, which no longer knows
# the version-specific ones, and both name the same runtime pack - the file comparison below proves it.
$rid = $RuntimeIdentifier -replace '^win10-', 'win-'
Write-Host "add-cli: publishing $cli for netcoreapp3.1 / $rid"
& dotnet publish (Join-Path $repo 'Tools\Typedown.Cli\Typedown.Cli.csproj') -f netcoreapp3.1 -r $rid `
    --self-contained true -c $Configuration -o $out -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "add-cli: dotnet publish failed" }

function Get-AssemblyVersion([string]$file) {
    try { [Reflection.AssemblyName]::GetAssemblyName($file).Version.ToString() } catch { $null }
}

$shared = @('Typedown.Automation.dll', 'Newtonsoft.Json.dll')
$added = 0; $same = 0; $kept = 0
try {
    foreach ($file in Get-ChildItem $out -Recurse -File) {
        $relative = $file.FullName.Substring($out.Length).TrimStart('\', '/')
        if ($relative -like '*.pdb') { continue }
        $target = Join-Path $AppFolder $relative
        if (-not (Test-Path $target)) {
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            Copy-Item $file.FullName $target
            $added++
            continue
        }
        if ((Get-FileHash $file.FullName).Hash -eq (Get-FileHash $target).Hash) { $same++; continue }
        if ($shared -contains $file.Name) {
            $mine = Get-AssemblyVersion $file.FullName
            $theirs = Get-AssemblyVersion $target
            if ($mine -and $mine -eq $theirs) { $kept++; continue }
            throw "add-cli: $relative is version $mine for $cli but $theirs in the app"
        }
        throw "add-cli: $cli would replace the app's $relative with a different file - the runtime $cli was built for is not the app's. Build both with the same .NET SDK."
    }
}
finally {
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "add-cli: $added file(s) added, $same already there and identical, $kept shared librar(ies) kept from the app"

# It has to start on the app's runtime, from the app folder - where this machine can run it: an ARM64 build made on an
# x64 machine (CI builds both there) cannot start, and the release build stopped here. The file checks above hold for
# it all the same.
$hostArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
if ($rid -like '*-arm64' -and $hostArch -ne 'arm64') {
    Write-Host "add-cli: $cli.exe not started: $rid does not run on this $hostArch machine"
}
else {
    $help = & (Join-Path $AppFolder "$cli.exe") help 2>&1
    if ($LASTEXITCODE -ne 0 -or -not ($help -join "`n").Contains($cli)) {
        throw "add-cli: $cli.exe does not run from the app folder (exit $LASTEXITCODE): $help"
    }
    Write-Host "add-cli: $cli.exe runs from the app folder"
}

# The documents, keeping their relative links: the user guide, and automation.md, which links the spec, the schema, the
# examples and the MCP page.
$docs = Join-Path $AppFolder 'docs'
New-Item -ItemType Directory -Force $docs, (Join-Path $docs 'automation-schema'), (Join-Path $docs 'automation-examples') | Out-Null
# Written for Typedown: under another name they get the edition's names - the product in prose, the CLI, the install
# folder and the automation endpoint, not file or function names (typedown_client.py, Connect-Typedown) - and an
# edition without a Linux build drops what sits between <!-- linux --> and <!-- /linux -->.
function Copy-Document([string]$from, [string]$to) {
    $bytes = [IO.File]::ReadAllBytes($from)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [IO.File]::ReadAllText($from)
    if ($brand.BrandHasLinux -ne 'true') { $text = [regex]::Replace($text, '(?s)<!-- linux -->.*?<!-- /linux -->', '') }
    if ($brand.BrandName -ne 'Typedown') {
        $text = $text.Replace('typedownctl', $cli).Replace('Typedown.Automation.v1', $brand.BrandName + '.Automation.v1')  # brand-ok: from Typedown's names
        $text = $text.Replace('Program Files\Typedown', 'Program Files\' + $brand.BrandName).Replace('Program Files\\Typedown', 'Program Files\\' + $brand.BrandName)
        $text = $text.Replace('claude mcp add typedown', 'claude mcp add ' + $brand.BrandName.ToLowerInvariant()).Replace('"typedown": {', '"' + $brand.BrandName.ToLowerInvariant() + '": {')
        $text = [regex]::Replace($text, '(?<![\w/\\.-])Typedown(?![-_.]\w|\w)', $brand.BrandName)
        # A folder named after the product inside a path (%LOCALAPPDATA%\Typedown\themes, Pictures\Typedown): the rule
        # above leaves a name after a backslash alone.
        $text = [regex]::Replace($text, '(?<=\\)Typedown(?=\\|`)', $brand.BrandName)
    }
    # Windows PowerShell 5.1 reads a script without a BOM as ANSI: keep the source's.
    [IO.File]::WriteAllText($to, $text, (New-Object Text.UTF8Encoding($hasBom)))
}
foreach ($name in 'user-guide.md', 'automation.md', 'automation-mcp.md', 'automation-api-spec.md') {
    Copy-Document (Join-Path $repo "docs\$name") (Join-Path $docs $name)
}
Copy-Item (Join-Path $repo 'docs\automation-schema\v1.json') (Join-Path $docs 'automation-schema') -Force
Get-ChildItem (Join-Path $repo 'docs\automation-examples') -File -Include *.py, *.ps1, *.md -Recurse |
    ForEach-Object { Copy-Document $_.FullName (Join-Path (Join-Path $docs 'automation-examples') $_.Name) }
Write-Host "add-cli: documents in $docs"
