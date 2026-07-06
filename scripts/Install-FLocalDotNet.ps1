[CmdletBinding()]
param(
    [string]$SourceDotNetRoot = 'C:\Users\micha\.codex\tools\dotnet-sdk-10.0.301'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$Target = Join-Path $Root '.dotnet-sdk'
New-Item -ItemType Directory -Path $Target -Force | Out-Null

$required = @(
    'dotnet.exe',
    'host',
    'sdk\10.0.301',
    'shared\Microsoft.NETCore.App\10.0.9',
    'shared\Microsoft.WindowsDesktop.App\10.0.9',
    'packs\Microsoft.NETCore.App.Ref\10.0.9',
    'packs\Microsoft.WindowsDesktop.App.Ref\10.0.9'
)

foreach ($relative in $required) {
    $src = Join-Path $SourceDotNetRoot $relative
    $dst = Join-Path $Target $relative
    if (-not (Test-Path -LiteralPath $src)) {
        throw "Missing required .NET component: $src"
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
}

$dotnet = Join-Path $Target 'dotnet.exe'
$info = & $dotnet --info

[pscustomobject]@{
    Target = $Target
    DotNetExe = $dotnet
    Source = $SourceDotNetRoot
    InfoFirstLine = ($info | Select-Object -First 1)
} | ConvertTo-Json
