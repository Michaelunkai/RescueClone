[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$env:TEMP = Join-Path $Root '.tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
$Work = Join-Path $Root 'proof\portable-roundtrip'
if (Test-Path -LiteralPath $Work) { Remove-Item -LiteralPath $Work -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $Work 'source\nested') -Force | Out-Null
'alpha' | Set-Content -LiteralPath (Join-Path $Work 'source\alpha.txt') -Encoding ASCII
'beta' | Set-Content -LiteralPath (Join-Path $Work 'source\nested\beta.txt') -Encoding ASCII

$Cli = Join-Path $Root 'publish\cli\rc.exe'
if (-not (Test-Path -LiteralPath $Cli)) { throw "Missing CLI exe. Run .\scripts\Build-Portable.ps1 first." }

$Image = Join-Path $Work 'backup.rcimg'
$Restore = Join-Path $Work 'restore'
& $Cli image create --source (Join-Path $Work 'source') --image $Image --compression High --password secret | Out-Null
& $Cli image verify --image $Image --password secret | Out-Null
& $Cli image restore --image $Image --target $Restore --password secret | Out-Null
$OperationRequest = Join-Path $Work 'native-status.operation.json'
@{
    kind = 'native.status'
    operationId = 'portable-native-status'
    parameters = @{}
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OperationRequest -Encoding UTF8
$OperationKinds = & $Cli operation kinds | ConvertFrom-Json
$OperationValidation = & $Cli operation validate --request $OperationRequest | ConvertFrom-Json
if (-not $OperationValidation.valid) { throw 'operation validation failed for native.status fixture' }

Import-Module (Join-Path $Root 'powershell\RescueClone\RescueClone.psd1') -Force
$features = Get-RCFeature
$PowerShellOperationValidation = Test-RCOperation -RequestPath $OperationRequest
if (-not $PowerShellOperationValidation.valid) { throw 'PowerShell operation validation failed for native.status fixture' }

$alpha = Get-Content -LiteralPath (Join-Path $Restore 'alpha.txt') -Raw
$beta = Get-Content -LiteralPath (Join-Path $Restore 'nested\beta.txt') -Raw
if ($alpha.Trim() -ne 'alpha') { throw 'alpha restore mismatch' }
if ($beta.Trim() -ne 'beta') { throw 'beta restore mismatch' }

[pscustomobject]@{
    CliExe = $Cli
    Image = $Image
    Restore = $Restore
    FeatureCount = @($features).Count
    OperationKindCount = @($OperationKinds).Count
    OperationValidation = $OperationValidation.valid
    PowerShellOperationValidation = $PowerShellOperationValidation.valid
    Alpha = $alpha.Trim()
    Beta = $beta.Trim()
} | ConvertTo-Json
