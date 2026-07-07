[CmdletBinding()]
param(
    [string]$InstallRoot,
    [switch]$Quiet,
    [switch]$NoRestart,
    [switch]$Force
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $Root 'installed\RescueClone'
}

$required = @(
    (Join-Path $Root 'publish\cli\rc.exe'),
    (Join-Path $Root 'publish\gui\RescueClone.App.exe'),
    (Join-Path $Root 'powershell\RescueClone\RescueClone.psd1')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required build output: $path. Run scripts\Build-Portable.ps1 first."
    }
}

if (Test-Path -LiteralPath $InstallRoot) {
    if (-not $Force) {
        throw "Install root already exists: $InstallRoot. Pass -Force to replace it."
    }
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
}

$cliTarget = Join-Path $InstallRoot 'publish\cli'
$guiTarget = Join-Path $InstallRoot 'publish\gui'
$moduleTarget = Join-Path $InstallRoot 'PowerShell\RescueClone'
$docsTarget = Join-Path $InstallRoot 'docs'
New-Item -ItemType Directory -Path $cliTarget, $guiTarget, $moduleTarget, $docsTarget -Force | Out-Null

Copy-Item -Path (Join-Path $Root 'publish\cli\*') -Destination $cliTarget -Recurse -Force
Copy-Item -Path (Join-Path $Root 'publish\gui\*') -Destination $guiTarget -Recurse -Force
Copy-Item -Path (Join-Path $Root 'powershell\RescueClone\*') -Destination $moduleTarget -Recurse -Force
Copy-Item -Path (Join-Path $Root 'docs\*') -Destination $docsTarget -Recurse -Force
Copy-Item -LiteralPath (Join-Path $Root 'README.md') -Destination (Join-Path $InstallRoot 'README.md') -Force

$rcCmd = @"
@echo off
setlocal
"%~dp0publish\cli\rc.exe" %*
"@
Set-Content -LiteralPath (Join-Path $InstallRoot 'RC.cmd') -Value $rcCmd -Encoding ASCII

$guiCmd = @"
@echo off
setlocal
start "" "%~dp0publish\gui\RescueClone.App.exe"
"@
Set-Content -LiteralPath (Join-Path $InstallRoot 'RUN-GUI.cmd') -Value $guiCmd -Encoding ASCII

$moduleImport = @"
`$env:PSModulePath = "$InstallRoot\PowerShell;" + `$env:PSModulePath
Import-Module "$InstallRoot\PowerShell\RescueClone\RescueClone.psd1" -Force
"@
Set-Content -LiteralPath (Join-Path $InstallRoot 'Import-RescueClone.ps1') -Value $moduleImport -Encoding UTF8

$report = [pscustomobject]@{
    InstallRoot = [IO.Path]::GetFullPath($InstallRoot)
    Cli = [IO.Path]::GetFullPath((Join-Path $InstallRoot 'RC.cmd'))
    Gui = [IO.Path]::GetFullPath((Join-Path $InstallRoot 'RUN-GUI.cmd'))
    PowerShellImport = [IO.Path]::GetFullPath((Join-Path $InstallRoot 'Import-RescueClone.ps1'))
    Quiet = [bool]$Quiet
    NoRestart = [bool]$NoRestart
    InstalledUtc = [DateTimeOffset]::UtcNow
}

if (-not $Quiet) {
    $report | Format-List | Out-Host
}
$report | ConvertTo-Json
