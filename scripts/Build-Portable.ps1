[CmdletBinding()]
param(
    [ValidateSet('Release','Debug')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$env:NUGET_PACKAGES = Join-Path $Root '.nuget-packages'
$env:DOTNET_CLI_HOME = Join-Path $Root '.dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:TEMP = Join-Path $Root '.tmp'
$env:TMP = $env:TEMP

New-Item -ItemType Directory -Path $env:NUGET_PACKAGES, $env:DOTNET_CLI_HOME, $env:TEMP, (Join-Path $Root 'publish') -Force | Out-Null
$DotNet = Join-Path $Root '.dotnet-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $DotNet)) {
    $DotNet = 'dotnet'
}

function Invoke-Native {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(Mandatory=$true)][string[]]$ArgumentList
    )
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
    }
}

Invoke-Native $DotNet @('restore', (Join-Path $Root 'RescueClone.sln'), '--packages', $env:NUGET_PACKAGES, '-r', 'win-x64')
Invoke-Native $DotNet @('test', (Join-Path $Root 'RescueClone.sln'), '-c', $Configuration, '--no-restore')

$cliOut = Join-Path $Root 'publish\cli'
$guiOut = Join-Path $Root 'publish\gui'
if (Test-Path -LiteralPath $cliOut) { Remove-Item -LiteralPath $cliOut -Recurse -Force }
if (Test-Path -LiteralPath $guiOut) { Remove-Item -LiteralPath $guiOut -Recurse -Force }

Invoke-Native $DotNet @('publish', (Join-Path $Root 'src\RescueClone.Cli\rc.csproj'), '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', $cliOut, '--no-restore')
Invoke-Native $DotNet @('publish', (Join-Path $Root 'src\RescueClone.App\RescueClone.App.csproj'), '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', $guiOut, '--no-restore')

$cliExe = Join-Path $cliOut 'rc.exe'
$guiExe = Join-Path $guiOut 'RescueClone.App.exe'
if (-not (Test-Path -LiteralPath $cliExe)) { throw "Missing CLI publish output: $cliExe" }
if (-not (Test-Path -LiteralPath $guiExe)) { throw "Missing GUI publish output: $guiExe" }

[pscustomobject]@{
    Root = $Root
    CliExe = $cliExe
    GuiExe = $guiExe
    DotNet = $DotNet
    NuGetPackages = $env:NUGET_PACKAGES
    DotNetCliHome = $env:DOTNET_CLI_HOME
} | ConvertTo-Json
