[CmdletBinding()]
param(
    [string]$PipeName = "rescueclone-dependency-" + [Guid]::NewGuid().ToString("N")
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$RootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
$WindowsFull = [IO.Path]::GetFullPath($env:WINDIR).TrimEnd('\') + '\'

function Test-PublishedProcessBoundary {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$FilePath,
        [AllowNull()][string[]]$ArgumentList = $null,
        [int]$StartupDelayMilliseconds = 800
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        throw "Missing published ${Name}: $FilePath"
    }

    $startArguments = @{
        FilePath = $FilePath
        PassThru = $true
        WindowStyle = 'Hidden'
    }
    if ($ArgumentList -and $ArgumentList.Count -gt 0) {
        $startArguments.ArgumentList = $ArgumentList
    }
    $process = Start-Process @startArguments
    try {
        Start-Sleep -Milliseconds $StartupDelayMilliseconds
        if ($process.HasExited) {
            throw "Published $Name exited early with code $($process.ExitCode)."
        }

        $modules = @(Get-Process -Id $process.Id -Module | Select-Object ModuleName, FileName)
        $outsideBoundary = @($modules | Where-Object {
            $path = [IO.Path]::GetFullPath($_.FileName)
            -not $path.StartsWith($RootFull, [StringComparison]::OrdinalIgnoreCase) -and
            -not $path.StartsWith($WindowsFull, [StringComparison]::OrdinalIgnoreCase)
        })
        $nonWindowsCDrive = @($modules | Where-Object {
            $_.FileName -like 'C:\*' -and $_.FileName -notlike "$WindowsFull*"
        })

        if ($outsideBoundary.Count -gt 0) {
            throw "Published $Name loaded modules outside the project root or Windows directory: $($outsideBoundary.FileName -join '; ')"
        }
        if ($nonWindowsCDrive.Count -gt 0) {
            throw "Published $Name loaded non-Windows modules from C drive: $($nonWindowsCDrive.FileName -join '; ')"
        }

        [pscustomobject]@{
            Name = $Name
            FilePath = $FilePath
            ProcessId = $process.Id
            ModuleCount = $modules.Count
            OutsideBoundaryCount = $outsideBoundary.Count
            NonWindowsCDriveModuleCount = $nonWindowsCDrive.Count
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
}

$results = @(
    Test-PublishedProcessBoundary -Name 'CLI service' -FilePath (Join-Path $Root 'publish\cli\rc.exe') -ArgumentList @('service', 'serve', '--pipe', $PipeName)
    Test-PublishedProcessBoundary -Name 'GUI' -FilePath (Join-Path $Root 'publish\gui\RescueClone.App.exe') -StartupDelayMilliseconds 2000
)

[pscustomobject]@{
    ProjectRoot = $RootFull
    WindowsRoot = $WindowsFull
    ProcessCount = $results.Count
    Results = $results
} | ConvertTo-Json -Depth 4
