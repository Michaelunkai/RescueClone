[CmdletBinding()]
param(
    [string]$InstallRoot,
    [switch]$Quiet,
    [switch]$NoRestart
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $Root 'installed\RescueClone'
}

$fullRoot = [IO.Path]::GetFullPath($InstallRoot)
if (-not (Test-Path -LiteralPath $fullRoot)) {
    $report = [pscustomobject]@{
        InstallRoot = $fullRoot
        Removed = $false
        Message = 'Install root was not present.'
        Quiet = [bool]$Quiet
        NoRestart = [bool]$NoRestart
        CompletedUtc = [DateTimeOffset]::UtcNow
    }
    if (-not $Quiet) { $report | Format-List | Out-Host }
    $report | ConvertTo-Json
    return
}

Remove-Item -LiteralPath $fullRoot -Recurse -Force
$report = [pscustomobject]@{
    InstallRoot = $fullRoot
    Removed = $true
    Message = 'Install root removed.'
    Quiet = [bool]$Quiet
    NoRestart = [bool]$NoRestart
    CompletedUtc = [DateTimeOffset]::UtcNow
}
if (-not $Quiet) { $report | Format-List | Out-Host }
$report | ConvertTo-Json
