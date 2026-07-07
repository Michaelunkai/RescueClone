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

& (Join-Path $Root 'scripts\Build-Portable.ps1') | Out-Host
& (Join-Path $Root 'scripts\Test-Portable.ps1') | Out-Host
& (Join-Path $Root 'scripts\Test-PortableOperationService.ps1') | Out-Host
& (Join-Path $Root 'scripts\Test-PortableInstall.ps1') | Out-Host
& (Join-Path $Root 'scripts\New-PortablePackage.ps1') -OutputPath $PackagePath -Force | Out-Host
$package = & (Join-Path $Root 'scripts\Test-PortablePackage.ps1') -PackagePath $PackagePath | ConvertFrom-Json
$dependency = & (Join-Path $Root 'scripts\Test-PortableDependencyBoundary.ps1') | ConvertFrom-Json

Push-Location $Root
try {
    git diff --check
    $markerPattern = (@(('TO' + 'DO'), ('FIX' + 'ME'), ('place' + 'holder'), ('st' + 'ub'), ('future' + ' work')) -join '|')
    $markerMatches = @(rg -n $markerPattern --glob '!**/bin/**' --glob '!**/obj/**' --glob '!**/.dotnet-sdk/**' --glob '!**/.nuget-packages/**' --glob '!**/publish/**' --glob '!proof/**' .)
    if ($LASTEXITCODE -gt 1) {
        throw "Deferred-marker scan failed with exit code $LASTEXITCODE."
    }
    if ($markerMatches.Count -gt 0) {
        throw "Deferred-marker scan found matches:`n$($markerMatches -join [Environment]::NewLine)"
    }
} finally {
    Pop-Location
}

[pscustomobject]@{
    Passed = $true
    PackagePath = $package.PackagePath
    PackageSha256 = $package.Sha256
    PackageEntries = $package.EntryCount
    ExtractedCliFeatureCount = $package.ExtractedCliFeatureCount
    ExtractedPowerShellFeatureCount = $package.ExtractedPowerShellFeatureCount
    DependencyProcesses = $dependency.ProcessCount
    DependencyOutsideBoundaryCount = @($dependency.Results | ForEach-Object { $_.OutsideBoundaryCount } | Measure-Object -Sum).Sum
    DependencyNonWindowsCDriveModuleCount = @($dependency.Results | ForEach-Object { $_.NonWindowsCDriveModuleCount } | Measure-Object -Sum).Sum
} | ConvertTo-Json
