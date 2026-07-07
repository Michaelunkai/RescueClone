[CmdletBinding()]
param(
    [string]$PackagePath
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $Root 'artifacts\RescueClone-portable.zip'
}

$packageFull = [IO.Path]::GetFullPath($PackagePath)
$hashPath = "$packageFull.sha256"
if (-not (Test-Path -LiteralPath $packageFull -PathType Leaf)) {
    throw "Missing portable package: $packageFull"
}
if (-not (Test-Path -LiteralPath $hashPath -PathType Leaf)) {
    throw "Missing portable package checksum: $hashPath"
}

$actualHash = (Get-FileHash -LiteralPath $packageFull -Algorithm SHA256).Hash.ToLowerInvariant()
$sidecar = (Get-Content -LiteralPath $hashPath -Raw).Trim()
$sidecarHash = ($sidecar -split '\s+')[0].ToLowerInvariant()
if ($actualHash -ne $sidecarHash) {
    throw "Portable package checksum mismatch. Expected $sidecarHash, actual $actualHash."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($packageFull)
try {
    $entryNames = @($zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
} finally {
    $zip.Dispose()
}

$requiredEntries = @(
    'publish/cli/rc.exe',
    'publish/gui/RescueClone.App.exe',
    'powershell/RescueClone/RescueClone.psd1',
    'docs/BUILD.md',
    'docs/USER_GUIDE.md',
    'docs/ARCHITECTURE.md',
    'scripts/Install-RescueClone.ps1',
    'scripts/Uninstall-RescueClone.ps1',
    'scripts/Test-Portable.ps1',
    'scripts/Test-PortableInstall.ps1',
    'scripts/Test-PortableOperationService.ps1',
    'scripts/Test-PortableDependencyBoundary.ps1',
    'scripts/Test-PortablePackage.ps1',
    'scripts/Test-All.ps1',
    'README.md',
    'RC.cmd',
    'RUN-GUI.cmd'
)
$missingEntries = @($requiredEntries | Where-Object { $entryNames -notcontains $_ })
if ($missingEntries.Count -gt 0) {
    throw "Portable package is missing required entries: $($missingEntries -join ', ')"
}

$extractRoot = Join-Path $Root '.tmp\package-smoke'
if (Test-Path -LiteralPath $extractRoot) {
    Remove-Item -LiteralPath $extractRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
Expand-Archive -LiteralPath $packageFull -DestinationPath $extractRoot -Force

$extractedCli = Join-Path $extractRoot 'publish\cli\rc.exe'
$extractedModule = Join-Path $extractRoot 'powershell\RescueClone\RescueClone.psd1'
if (-not (Test-Path -LiteralPath $extractedCli -PathType Leaf)) {
    throw "Extracted package is missing CLI: $extractedCli"
}
if (-not (Test-Path -LiteralPath $extractedModule -PathType Leaf)) {
    throw "Extracted package is missing PowerShell module: $extractedModule"
}

$features = @(& $extractedCli features | ConvertFrom-Json)
Import-Module $extractedModule -Force
$psFeatures = @(Get-RCFeature)
if ($features.Count -eq 0 -or $features.Count -ne $psFeatures.Count) {
    throw "Extracted package feature catalog mismatch. CLI=$($features.Count), PowerShell=$($psFeatures.Count)."
}

[pscustomobject]@{
    PackagePath = $packageFull
    Sha256Path = $hashPath
    Sha256 = $actualHash
    EntryCount = $entryNames.Count
    RequiredEntryCount = $requiredEntries.Count
    SidecarMatches = $true
    ExtractRoot = $extractRoot
    ExtractedCliFeatureCount = $features.Count
    ExtractedPowerShellFeatureCount = $psFeatures.Count
} | ConvertTo-Json
