[CmdletBinding()]
param(
    [string]$CompilerPath
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$Source = Join-Path $Root 'native\RescueClone.Native\rc_native.cpp'
$OutDir = Join-Path $Root 'native\bin'
$Dll = Join-Path $OutDir 'RescueClone.Native.dll'

if (-not $CompilerPath) {
    $candidates = @(
        (Join-Path $Root 'tools\mingw64\bin\g++.exe'),
        'F:\study\Dev_Toolchain\dev-toolchain\programming\C++\mingw-complete\mingw64\bin\g++.exe',
        'F:\study\Dev_Toolchain\programming\python\apps\systemTools\sizes\tools\winlibs\extract\mingw64\bin\g++.exe',
        'F:\study\repos\programming\C++\mingw-complete\mingw64\bin\g++.exe'
    )
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'Missing g++.exe for native RescueClone build. Pass -CompilerPath or seed an F-local MinGW toolchain.'
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
& $CompilerPath -std=c++17 -O2 -Wall -Wextra -shared -static-libgcc -static-libstdc++ -o $Dll $Source
if ($LASTEXITCODE -ne 0) {
    throw "Native build failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $Dll)) {
    throw "Missing native build output: $Dll"
}

[pscustomobject]@{
    Compiler = $CompilerPath
    Output = $Dll
} | ConvertTo-Json
