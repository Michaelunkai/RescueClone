[CmdletBinding()]
param(
    [string]$PipeName = "rescueclone-dependency-" + [Guid]::NewGuid().ToString("N")
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$CliExe = Join-Path $Root 'publish\cli\rc.exe'
if (-not (Test-Path -LiteralPath $CliExe)) {
    throw "Missing published CLI: $CliExe"
}

$process = Start-Process -FilePath $CliExe -ArgumentList @('service', 'serve', '--pipe', $PipeName) -PassThru -WindowStyle Hidden
try {
    Start-Sleep -Milliseconds 800
    if ($process.HasExited) {
        throw "Published CLI service exited early with code $($process.ExitCode)."
    }

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $windowsFull = [IO.Path]::GetFullPath($env:WINDIR).TrimEnd('\') + '\'
    $modules = @(Get-Process -Id $process.Id -Module | Select-Object ModuleName, FileName)
    $outsideBoundary = @($modules | Where-Object {
        $path = [IO.Path]::GetFullPath($_.FileName)
        -not $path.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase) -and
        -not $path.StartsWith($windowsFull, [StringComparison]::OrdinalIgnoreCase)
    })
    $nonWindowsCDrive = @($modules | Where-Object {
        $_.FileName -like 'C:\*' -and $_.FileName -notlike "$windowsFull*"
    })

    if ($outsideBoundary.Count -gt 0) {
        throw "Published CLI loaded modules outside the project root or Windows directory: $($outsideBoundary.FileName -join '; ')"
    }
    if ($nonWindowsCDrive.Count -gt 0) {
        throw "Published CLI loaded non-Windows modules from C drive: $($nonWindowsCDrive.FileName -join '; ')"
    }

    [pscustomobject]@{
        CliExe = $CliExe
        ProcessId = $process.Id
        ModuleCount = $modules.Count
        ProjectRoot = $rootFull
        WindowsRoot = $windowsFull
        OutsideBoundaryCount = $outsideBoundary.Count
        NonWindowsCDriveModuleCount = $nonWindowsCDrive.Count
    } | ConvertTo-Json
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
