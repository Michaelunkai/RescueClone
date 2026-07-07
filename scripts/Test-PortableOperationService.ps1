[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$env:TEMP = Join-Path $Root '.tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null

$Cli = Join-Path $Root 'publish\cli\rc.exe'
if (-not (Test-Path -LiteralPath $Cli)) {
    throw "Missing CLI exe. Run .\scripts\Build-Portable.ps1 first."
}

$Work = Join-Path $Root 'proof\portable-service'
if (Test-Path -LiteralPath $Work) { Remove-Item -LiteralPath $Work -Recurse -Force }
New-Item -ItemType Directory -Path $Work -Force | Out-Null
$Logs = Join-Path $Work 'logs'
$Stdout = Join-Path $Work 'service.out.txt'
$Stderr = Join-Path $Work 'service.err.txt'
$PipeName = 'rescueclone-smoke-' + [Guid]::NewGuid().ToString('N')
$RequestPath = Join-Path $Work 'native-status.operation.json'
@{
    kind = 'native.status'
    operationId = 'service-native-status'
    parameters = @{}
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $RequestPath -Encoding UTF8

$service = $null
try {
    $service = Start-Process -FilePath $Cli -ArgumentList @('service','serve','--pipe',$PipeName,'--log-directory',$Logs) -PassThru -WindowStyle Hidden -RedirectStandardOutput $Stdout -RedirectStandardError $Stderr

    $report = $null
    $lastOutput = $null
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $output = & $Cli service run-operation --pipe $PipeName --request $RequestPath --log-directory $Logs --timeout-ms 2000 2>&1
        $lastOutput = $output -join [Environment]::NewLine
        if ($LASTEXITCODE -eq 0) {
            $report = $lastOutput | ConvertFrom-Json
            break
        }
        if ($service.HasExited) {
            throw "Service process exited before accepting requests. stderr: $(Get-Content -LiteralPath $Stderr -Raw -ErrorAction SilentlyContinue)"
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    if ($null -eq $report) {
        throw "Timed out waiting for operation service pipe. Last output: $lastOutput"
    }
    if ($report.state -ne 'Succeeded') {
        throw "Service operation did not succeed: $($report | ConvertTo-Json -Depth 10)"
    }
    if (-not (Test-Path -LiteralPath $report.logPath)) {
        throw "Missing service operation report log: $($report.logPath)"
    }
    if (-not (Test-Path -LiteralPath $report.recoveryStatePath)) {
        throw "Missing service operation recovery state: $($report.recoveryStatePath)"
    }

    [pscustomobject]@{
        CliExe = $Cli
        PipeName = $PipeName
        ServiceProcessId = $service.Id
        OperationId = $report.operationId
        State = $report.state
        LogPath = $report.logPath
        RecoveryStatePath = $report.recoveryStatePath
    } | ConvertTo-Json
}
finally {
    if ($null -ne $service -and -not $service.HasExited) {
        Stop-Process -Id $service.Id -Force
        $null = $service.WaitForExit(5000)
    }
}
