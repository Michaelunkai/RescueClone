Set-StrictMode -Version 2.0

function Get-RCCommandPath {
    $root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $publishedExe = Join-Path $root 'publish\cli\rc.exe'
    if (Test-Path -LiteralPath $publishedExe) {
        return @{ Kind = 'Exe'; Path = $publishedExe }
    }
    $debugExe = Join-Path $root 'src\RescueClone.Cli\bin\Release\net10.0\win-x64\rc.exe'
    if (Test-Path -LiteralPath $debugExe) {
        return @{ Kind = 'Exe'; Path = $debugExe }
    }
    $dll = Join-Path $root 'src\RescueClone.Cli\bin\Release\net10.0\rc.dll'
    if (Test-Path -LiteralPath $dll) {
        return @{ Kind = 'Dll'; Path = $dll }
    }
    throw "RescueClone CLI is not built. Run from the project root: .\scripts\Build-Portable.ps1"
}

function Invoke-RCJson {
    param(
        [Parameter(Mandatory=$true)][string[]]$ArgumentList
    )
    $command = Get-RCCommandPath
    if ($command.Kind -eq 'Exe') {
        $output = & $command.Path @ArgumentList 2>&1
    } else {
        $output = & dotnet $command.Path @ArgumentList 2>&1
    }
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join [Environment]::NewLine)
    }
    return ($output -join [Environment]::NewLine) | ConvertFrom-Json
}

function Get-RCFeature {
    [CmdletBinding()]
    param()
    Invoke-RCJson -ArgumentList @('features')
}

function Get-RCVolume {
    [CmdletBinding()]
    param()
    Invoke-RCJson -ArgumentList @('storage','volumes')
}

function Get-RCNativeStatus {
    [CmdletBinding()]
    param()
    Invoke-RCJson -ArgumentList @('native','status')
}

function New-RCImage {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$SourcePath,
        [Parameter(Mandatory=$true)][string]$ImagePath,
        [ValidateSet('None','Medium','High')][string]$Compression = 'Medium',
        [ValidateSet('V1','V2')][string]$Format = 'V2',
        [string]$Password
    )
    if ($PSCmdlet.ShouldProcess($ImagePath, "Create image from $SourcePath")) {
        $args = @('image','create','--source',$SourcePath,'--image',$ImagePath,'--compression',$Compression,'--format',$Format)
        if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
        Invoke-RCJson -ArgumentList $args
    }
}

function Test-RCImage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$ImagePath,
        [string]$Password
    )
    $args = @('image','verify','--image',$ImagePath)
    if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
    Invoke-RCJson -ArgumentList $args
}

function Restore-RCImage {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$ImagePath,
        [Parameter(Mandatory=$true)][string]$TargetPath,
        [string]$Password,
        [switch]$Overwrite
    )
    if ($PSCmdlet.ShouldProcess($TargetPath, "Restore image $ImagePath")) {
        $args = @('image','restore','--image',$ImagePath,'--target',$TargetPath)
        if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
        if ($Overwrite) { $args += '--overwrite' }
        Invoke-RCJson -ArgumentList $args
    }
}

function Test-RCBackupJob {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path
    )
    Invoke-RCJson -ArgumentList @('job','validate','--file',$Path)
}

function Start-RCBackupJob {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
        [switch]$ForceDisabled
    )
    if ($PSCmdlet.ShouldProcess($Path, "Run backup job")) {
        $args = @('job','run','--file',$Path)
        if ($ForceDisabled) { $args += '--force-disabled' }
        Invoke-RCJson -ArgumentList $args
    }
}

function Get-RCRestorePlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$ImagePath,
        [string]$Password,
        [Parameter(Mandatory=$true)][string]$TargetDiskId,
        [long]$TargetDiskSizeBytes,
        [long]$RequiredBytes,
        [switch]$TargetIsCurrentSystemDisk,
        [Parameter(Mandatory=$true)][ValidateSet('Bios','Uefi','Unknown')][string]$BootMode,
        [switch]$HasEfiSystemPartition,
        [string]$BcdStorePath
    )
    $args = @('restore','plan','--image',$ImagePath,'--target-disk-id',$TargetDiskId,'--boot-mode',$BootMode)
    if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
    if ($PSBoundParameters.ContainsKey('TargetDiskSizeBytes')) { $args += @('--target-disk-size-bytes',[string]$TargetDiskSizeBytes) }
    if ($PSBoundParameters.ContainsKey('RequiredBytes')) { $args += @('--required-bytes',[string]$RequiredBytes) }
    if ($TargetIsCurrentSystemDisk) { $args += '--target-is-current-system-disk' }
    if ($HasEfiSystemPartition) { $args += '--has-efi-system-partition' }
    if ($PSBoundParameters.ContainsKey('BcdStorePath')) { $args += @('--bcd-store',$BcdStorePath) }
    Invoke-RCJson -ArgumentList $args
}

function Start-RCOperation {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$RequestPath,
        [string]$LogDirectory
    )
    if ($PSCmdlet.ShouldProcess($RequestPath, "Run RescueClone operation request")) {
        $args = @('operation','run','--request',$RequestPath)
        if ($PSBoundParameters.ContainsKey('LogDirectory')) { $args += @('--log-directory',$LogDirectory) }
        Invoke-RCJson -ArgumentList $args
    }
}
