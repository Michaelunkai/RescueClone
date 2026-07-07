[CmdletBinding()]
param(
    [string]$InstallRoot
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $Root '.tmp\install-smoke\RescueClone'
}

$fullInstallRoot = [IO.Path]::GetFullPath($InstallRoot)
if (Test-Path -LiteralPath $fullInstallRoot) {
    Remove-Item -LiteralPath $fullInstallRoot -Recurse -Force
}

$install = & (Join-Path $Root 'scripts\Install-RescueClone.ps1') -InstallRoot $fullInstallRoot -Quiet -NoRestart -Force | ConvertFrom-Json
$rc = Join-Path $fullInstallRoot 'RC.cmd'
$import = Join-Path $fullInstallRoot 'Import-RescueClone.ps1'
if (-not (Test-Path -LiteralPath $rc)) {
    throw "Installed CLI launcher is missing: $rc"
}
if (-not (Test-Path -LiteralPath $import)) {
    throw "Installed PowerShell import script is missing: $import"
}

$features = & $rc features | ConvertFrom-Json
if ($features.Count -lt 1) {
    throw 'Installed CLI returned no features.'
}
$operationKinds = & $rc operation kinds | ConvertFrom-Json
if ($operationKinds.Count -lt 1) {
    throw 'Installed CLI returned no operation kinds.'
}
$requestPath = Join-Path $fullInstallRoot 'native-status.operation.json'
@{
    kind = 'native.status'
    operationId = 'installed-native-status'
    parameters = @{}
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $requestPath -Encoding UTF8
$operationValidation = & $rc operation validate --request $requestPath | ConvertFrom-Json
if (-not $operationValidation.valid) {
    throw 'Installed CLI operation validation failed.'
}

. $import
$psFeatures = Get-RCFeature
if ($psFeatures.Count -ne $features.Count) {
    throw "Installed PowerShell feature count $($psFeatures.Count) did not match CLI feature count $($features.Count)."
}
$psOperationValidation = Test-RCOperation -RequestPath $requestPath
if (-not $psOperationValidation.valid) {
    throw 'Installed PowerShell operation validation failed.'
}

$uninstall = & (Join-Path $Root 'scripts\Uninstall-RescueClone.ps1') -InstallRoot $fullInstallRoot -Quiet -NoRestart | ConvertFrom-Json
if (Test-Path -LiteralPath $fullInstallRoot) {
    throw "Install root still exists after uninstall: $fullInstallRoot"
}

[pscustomobject]@{
    InstallRoot = $fullInstallRoot
    Installed = [bool]$install.InstallRoot
    FeatureCount = $features.Count
    PowerShellFeatureCount = $psFeatures.Count
    OperationKindCount = $operationKinds.Count
    OperationValidation = $operationValidation.valid
    PowerShellOperationValidation = $psOperationValidation.valid
    Removed = [bool]$uninstall.Removed
} | ConvertTo-Json
