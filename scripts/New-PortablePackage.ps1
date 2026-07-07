[CmdletBinding()]
param(
    [string]$OutputPath,
    [switch]$Force
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Root 'artifacts\RescueClone-portable.zip'
}

$required = @(
    (Join-Path $Root 'publish\cli\rc.exe'),
    (Join-Path $Root 'publish\gui\RescueClone.App.exe'),
    (Join-Path $Root 'powershell\RescueClone\RescueClone.psd1'),
    (Join-Path $Root 'scripts\Install-RescueClone.ps1'),
    (Join-Path $Root 'scripts\Uninstall-RescueClone.ps1')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing package input: $path. Run scripts\Build-Portable.ps1 first."
    }
}

$outputFull = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $outputFull) {
    if (-not $Force) {
        throw "Output package already exists: $outputFull. Pass -Force to replace it."
    }
    Remove-Item -LiteralPath $outputFull -Force
}

$stage = Join-Path $Root '.tmp\portable-package'
Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stage -Force | Out-Null

Copy-Item -Path (Join-Path $Root 'publish') -Destination $stage -Recurse -Force
Copy-Item -Path (Join-Path $Root 'powershell') -Destination $stage -Recurse -Force
Copy-Item -Path (Join-Path $Root 'docs') -Destination $stage -Recurse -Force
Copy-Item -LiteralPath (Join-Path $Root 'README.md') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $Root 'RC.cmd') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $Root 'RUN-GUI.cmd') -Destination $stage -Force
New-Item -ItemType Directory -Path (Join-Path $stage 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $Root 'scripts\Install-RescueClone.ps1') -Destination (Join-Path $stage 'scripts') -Force
Copy-Item -LiteralPath (Join-Path $Root 'scripts\Uninstall-RescueClone.ps1') -Destination (Join-Path $stage 'scripts') -Force

New-Item -ItemType Directory -Path (Split-Path -Parent $outputFull) -Force | Out-Null
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $outputFull -CompressionLevel Optimal -Force
$package = Get-Item -LiteralPath $outputFull

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $entryNames = @($zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
} finally {
    $zip.Dispose()
}

$requiredEntries = @(
    'publish/cli/rc.exe',
    'publish/gui/RescueClone.App.exe',
    'powershell/RescueClone/RescueClone.psd1',
    'scripts/Install-RescueClone.ps1',
    'scripts/Uninstall-RescueClone.ps1'
)
$missingEntries = @($requiredEntries | Where-Object { $entryNames -notcontains $_ })
if ($missingEntries.Count -gt 0) {
    throw "Portable package is missing required entries: $($missingEntries -join ', ')"
}

[pscustomobject]@{
    PackagePath = $package.FullName
    Length = $package.Length
    CreatedUtc = [DateTimeOffset]::UtcNow
    EntryCount = $entryNames.Count
    HasCli = [bool]($entryNames -contains 'publish/cli/rc.exe')
    HasGui = [bool]($entryNames -contains 'publish/gui/RescueClone.App.exe')
    HasPowerShellModule = [bool]($entryNames -contains 'powershell/RescueClone/RescueClone.psd1')
    HasInstallScript = [bool]($entryNames -contains 'scripts/Install-RescueClone.ps1')
    HasUninstallScript = [bool]($entryNames -contains 'scripts/Uninstall-RescueClone.ps1')
} | ConvertTo-Json
