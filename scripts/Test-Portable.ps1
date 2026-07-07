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
$Clone = Join-Path $Work 'clone'
$CliRescueAnswer = Join-Path $Work 'cli-rescue-answer.json'
$CliRescueRestore = Join-Path $Work 'cli-rescue-restore'
$BcdStore = Join-Path $Work 'BCD'
$AdvancedJob = Join-Path $Work 'advanced-job.json'
@{
    retryCount = 2
    retryDelaySeconds = 1
    applyRetentionAfterCreate = $true
    retentionKeepCount = 3
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $AdvancedJob -Encoding UTF8
'fixture' | Set-Content -LiteralPath $BcdStore -Encoding ASCII
& $Cli image create --source (Join-Path $Work 'source') --image $Image --compression High --password secret | Out-Null
& $Cli image verify --image $Image --password secret | Out-Null
& $Cli image restore --image $Image --target $Restore --password secret | Out-Null
& $Cli clone directory --source (Join-Path $Work 'source') --target $Clone | Out-Null
& $Cli rescue answer-create --output $CliRescueAnswer --repository $Work --image (Split-Path -Leaf $Image) --target-disk-id directory-fixture --password secret --boot-mode Bios --bcd-store $BcdStore --target-disk-size-bytes 1048576 --verify-image --directory-restore-target $CliRescueRestore | Out-Null
$CliRescueExecution = & $Cli rescue answer-execute --file $CliRescueAnswer --verify-image | ConvertFrom-Json
$CliJob = Join-Path $Work 'cli-job.json'
$CliJobStatus = & $Cli job create --file $CliJob --job-id cli-job --name 'CLI Job' --source (Join-Path $Work 'source') --image (Join-Path $Work 'cli-job.rcimg') --advanced-json-file $AdvancedJob | ConvertFrom-Json
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
$PowerShellClone = Join-Path $Work 'clone-ps'
Copy-RCDirectoryClone -SourcePath (Join-Path $Work 'source') -TargetPath $PowerShellClone -Confirm:$false | Out-Null
$PowerShellRescueAnswer = Join-Path $Work 'ps-rescue-answer.json'
$PowerShellRescueRestore = Join-Path $Work 'ps-rescue-restore'
New-RCRescueAnswer -OutputPath $PowerShellRescueAnswer -RepositoryPath $Work -ImagePath (Split-Path -Leaf $Image) -TargetDiskId directory-fixture -Password secret -BootMode Bios -BcdStorePath $BcdStore -TargetDiskSizeBytes 1048576 -VerifyImage -DirectoryRestoreTargetPath $PowerShellRescueRestore -Confirm:$false | Out-Null
$PowerShellRescueExecution = Start-RCRescueAnswer -Path $PowerShellRescueAnswer -VerifyImage -Confirm:$false
$PowerShellJob = Join-Path $Work 'ps-job.json'
$PowerShellJobStatus = New-RCBackupJob -Path $PowerShellJob -JobId ps-job -Name 'PowerShell Job' -SourcePath (Join-Path $Work 'source') -ImagePath (Join-Path $Work 'ps-job.rcimg') -AdvancedJsonFile $AdvancedJob -Confirm:$false

$alpha = Get-Content -LiteralPath (Join-Path $Restore 'alpha.txt') -Raw
$beta = Get-Content -LiteralPath (Join-Path $Restore 'nested\beta.txt') -Raw
if ($alpha.Trim() -ne 'alpha') { throw 'alpha restore mismatch' }
if ($beta.Trim() -ne 'beta') { throw 'beta restore mismatch' }
if ((Get-Content -LiteralPath (Join-Path $Clone 'alpha.txt') -Raw).Trim() -ne 'alpha') { throw 'CLI clone alpha mismatch' }
if ((Get-Content -LiteralPath (Join-Path $PowerShellClone 'nested\beta.txt') -Raw).Trim() -ne 'beta') { throw 'PowerShell clone beta mismatch' }
if (-not $CliRescueExecution.directoryRestorePerformed -or (Get-Content -LiteralPath (Join-Path $CliRescueRestore 'alpha.txt') -Raw).Trim() -ne 'alpha') { throw 'CLI rescue answer execution mismatch' }
if (-not $PowerShellRescueExecution.directoryRestorePerformed -or (Get-Content -LiteralPath (Join-Path $PowerShellRescueRestore 'nested\beta.txt') -Raw).Trim() -ne 'beta') { throw 'PowerShell rescue answer execution mismatch' }
if ($CliJobStatus.retryCount -ne 2 -or -not $CliJobStatus.applyRetentionAfterCreate) { throw 'CLI advanced job options mismatch' }
if ($PowerShellJobStatus.retryCount -ne 2 -or -not $PowerShellJobStatus.applyRetentionAfterCreate) { throw 'PowerShell advanced job options mismatch' }

[pscustomobject]@{
    CliExe = $Cli
    Image = $Image
    Restore = $Restore
    FeatureCount = @($features).Count
    OperationKindCount = @($OperationKinds).Count
    OperationValidation = $OperationValidation.valid
    PowerShellOperationValidation = $PowerShellOperationValidation.valid
    Clone = $Clone
    PowerShellClone = $PowerShellClone
    CliRescueRestore = $CliRescueRestore
    PowerShellRescueRestore = $PowerShellRescueRestore
    CliJob = $CliJob
    PowerShellJob = $PowerShellJob
    AdvancedJobRetryCount = $CliJobStatus.retryCount
    PowerShellAdvancedJobRetryCount = $PowerShellJobStatus.retryCount
    Alpha = $alpha.Trim()
    Beta = $beta.Trim()
} | ConvertTo-Json
