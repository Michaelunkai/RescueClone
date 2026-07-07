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
        [Parameter(Mandatory=$true)][string[]]$ArgumentList,
        [switch]$AllowNonZeroExit
    )
    $command = Get-RCCommandPath
    if ($command.Kind -eq 'Exe') {
        $output = & $command.Path @ArgumentList 2>&1
    } else {
        $output = & dotnet $command.Path @ArgumentList 2>&1
    }
    if ($LASTEXITCODE -ne 0 -and -not $AllowNonZeroExit) {
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

function Get-RCDisk {
    [CmdletBinding()]
    param()
    Invoke-RCJson -ArgumentList @('storage','disks')
}

function Get-RCDiskSafety {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][int]$DiskNumber,
        [string]$ExpectedFingerprint,
        [switch]$AllowBootSystem
    )
    $args = @('storage','disk-safety','--disk-number',[string]$DiskNumber)
    if ($PSBoundParameters.ContainsKey('ExpectedFingerprint')) { $args += @('--expected-fingerprint',$ExpectedFingerprint) }
    if ($AllowBootSystem) { $args += '--allow-boot-system' }
    Invoke-RCJson -ArgumentList $args
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

function Get-RCImageContent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$ImagePath,
        [string]$Password
    )
    $args = @('image','browse','--image',$ImagePath)
    if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
    Invoke-RCJson -ArgumentList $args
}

function Get-RCImage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [string]$Pattern = '*.rcimg',
        [switch]$Verify,
        [string]$Password
    )
    $args = @('image','list','--repository',$RepositoryPath,'--pattern',$Pattern)
    if ($Verify) { $args += '--verify' }
    if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
    Invoke-RCJson -ArgumentList $args
}

function Test-RCImageRepository {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [string]$Pattern = '*.rcimg',
        [string]$Password
    )
    $args = @('image','audit','--repository',$RepositoryPath,'--pattern',$Pattern)
    if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
    Invoke-RCJson -ArgumentList $args -AllowNonZeroExit
}

function Test-RCImageRepositoryProtection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [string]$Pattern = '*.rcimg'
    )
    Invoke-RCJson -ArgumentList @('image','protect-audit','--repository',$RepositoryPath,'--pattern',$Pattern)
}

function Set-RCImageRepositoryProtection {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [string]$Pattern = '*.rcimg'
    )
    if ($PSCmdlet.ShouldProcess($RepositoryPath, "Mark matching image files read-only")) {
        Invoke-RCJson -ArgumentList @('image','protect','--repository',$RepositoryPath,'--pattern',$Pattern)
    }
}

function Compare-RCImage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$ImagePath,
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$SourcePath,
        [string]$Password
    )
    $args = @('image','compare','--image',$ImagePath,'--source',$SourcePath)
    if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
    Invoke-RCJson -ArgumentList $args -AllowNonZeroExit
}

function Export-RCImageFile {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$ImagePath,
        [Parameter(Mandatory=$true)][string]$TargetPath,
        [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string[]]$Path,
        [string]$Password,
        [switch]$Overwrite
    )
    if ($PSCmdlet.ShouldProcess($TargetPath, "Extract selected image content from $ImagePath")) {
        $args = @('image','extract','--image',$ImagePath,'--target',$TargetPath,'--paths',($Path -join ';'))
        if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
        if ($Overwrite) { $args += '--overwrite' }
        Invoke-RCJson -ArgumentList $args
    }
}

function Mount-RCImage {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$ImagePath,
        [Parameter(Mandatory=$true)][string]$TargetPath,
        [string]$Password,
        [switch]$Overwrite
    )
    if ($PSCmdlet.ShouldProcess($TargetPath, "Project read-only image content from $ImagePath")) {
        $args = @('image','project','--image',$ImagePath,'--target',$TargetPath)
        if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
        if ($Overwrite) { $args += '--overwrite' }
        Invoke-RCJson -ArgumentList $args
    }
}

function Get-RCImageMount {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RootPath
    )
    Invoke-RCJson -ArgumentList @('image','projections','--root',$RootPath)
}

function Dismount-RCImage {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$TargetPath
    )
    if ($PSCmdlet.ShouldProcess($TargetPath, "Remove RescueClone image projection")) {
        Invoke-RCJson -ArgumentList @('image','unproject','--target',$TargetPath)
    }
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

function Copy-RCDirectoryClone {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$SourcePath,
        [Parameter(Mandatory=$true)][string]$TargetPath,
        [switch]$Overwrite
    )
    if ($PSCmdlet.ShouldProcess($TargetPath, "Clone directory $SourcePath")) {
        $args = @('clone','directory','--source',$SourcePath,'--target',$TargetPath)
        if ($Overwrite) { $args += '--overwrite' }
        Invoke-RCJson -ArgumentList $args
    }
}

function New-RCBackupJob {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$JobId,
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$SourcePath,
        [Parameter(Mandatory=$true)][string]$ImagePath,
        [ValidateSet('None','Medium','High')][string]$Compression = 'Medium',
        [string]$Password,
        [bool]$VerifyAfterCreate = $true,
        [string]$LogDirectory,
        [bool]$Enabled = $true
    )
    if ($PSCmdlet.ShouldProcess($Path, "Create backup job definition")) {
        $args = @('job','create','--file',$Path,'--job-id',$JobId,'--name',$Name,'--source',$SourcePath,'--image',$ImagePath,'--compression',$Compression,'--verify-after-create',[string]$VerifyAfterCreate,'--enabled',[string]$Enabled)
        if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
        if ($PSBoundParameters.ContainsKey('LogDirectory')) { $args += @('--log-directory',$LogDirectory) }
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

function Set-RCBackupJob {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
        [string]$JobId,
        [string]$Name,
        [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$SourcePath,
        [string]$ImagePath,
        [ValidateSet('None','Medium','High')][string]$Compression,
        [string]$Password,
        [bool]$VerifyAfterCreate,
        [string]$LogDirectory,
        [bool]$Enabled
    )
    if ($PSCmdlet.ShouldProcess($Path, "Update backup job definition")) {
        $args = @('job','update','--file',$Path)
        if ($PSBoundParameters.ContainsKey('JobId')) { $args += @('--job-id',$JobId) }
        if ($PSBoundParameters.ContainsKey('Name')) { $args += @('--name',$Name) }
        if ($PSBoundParameters.ContainsKey('SourcePath')) { $args += @('--source',$SourcePath) }
        if ($PSBoundParameters.ContainsKey('ImagePath')) { $args += @('--image',$ImagePath) }
        if ($PSBoundParameters.ContainsKey('Compression')) { $args += @('--compression',$Compression) }
        if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
        if ($PSBoundParameters.ContainsKey('VerifyAfterCreate')) { $args += @('--verify-after-create',[string]$VerifyAfterCreate) }
        if ($PSBoundParameters.ContainsKey('LogDirectory')) { $args += @('--log-directory',$LogDirectory) }
        if ($PSBoundParameters.ContainsKey('Enabled')) { $args += @('--enabled',[string]$Enabled) }
        Invoke-RCJson -ArgumentList $args
    }
}

function Remove-RCBackupJob {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path
    )
    if ($PSCmdlet.ShouldProcess($Path, "Delete backup job definition")) {
        Invoke-RCJson -ArgumentList @('job','delete','--file',$Path)
    }
}

function Export-RCBackupJob {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Low')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
        [Parameter(Mandatory=$true)][string]$OutputPath
    )
    if ($PSCmdlet.ShouldProcess($OutputPath, "Export backup job definition from $Path")) {
        Invoke-RCJson -ArgumentList @('job','export','--file',$Path,'--output',$OutputPath)
    }
}

function Import-RCBackupJob {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
        [Parameter(Mandatory=$true)][string]$TargetPath
    )
    if ($PSCmdlet.ShouldProcess($TargetPath, "Import backup job definition from $Path")) {
        Invoke-RCJson -ArgumentList @('job','import','--file',$Path,'--target',$TargetPath)
    }
}

function Get-RCBackupJobStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path
    )
    Invoke-RCJson -ArgumentList @('job','status','--file',$Path)
}

function Get-RCBackupJobHistory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
        [string]$Pattern = '*.json'
    )
    Invoke-RCJson -ArgumentList @('job','history','--file',$Path,'--pattern',$Pattern)
}

function Get-RCBackupJob {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$DirectoryPath,
        [string]$Pattern = '*.json'
    )
    Invoke-RCJson -ArgumentList @('job','list','--directory',$DirectoryPath,'--pattern',$Pattern)
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

function Get-RCRetentionPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [string]$Pattern = '*.rcimg',
        [int]$KeepCount,
        [int]$MaxAgeDays,
        [long]$MinFreeBytes
    )
    $args = @('retention','plan','--repository',$RepositoryPath,'--pattern',$Pattern)
    if ($PSBoundParameters.ContainsKey('KeepCount')) { $args += @('--keep-count',[string]$KeepCount) }
    if ($PSBoundParameters.ContainsKey('MaxAgeDays')) { $args += @('--max-age-days',[string]$MaxAgeDays) }
    if ($PSBoundParameters.ContainsKey('MinFreeBytes')) { $args += @('--min-free-bytes',[string]$MinFreeBytes) }
    Invoke-RCJson -ArgumentList $args
}

function Invoke-RCRetention {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [string]$Pattern = '*.rcimg',
        [int]$KeepCount,
        [int]$MaxAgeDays,
        [long]$MinFreeBytes
    )
    if ($PSCmdlet.ShouldProcess($RepositoryPath, "Apply RescueClone retention policy")) {
        $args = @('retention','apply','--repository',$RepositoryPath,'--pattern',$Pattern)
        if ($PSBoundParameters.ContainsKey('KeepCount')) { $args += @('--keep-count',[string]$KeepCount) }
        if ($PSBoundParameters.ContainsKey('MaxAgeDays')) { $args += @('--max-age-days',[string]$MaxAgeDays) }
        if ($PSBoundParameters.ContainsKey('MinFreeBytes')) { $args += @('--min-free-bytes',[string]$MinFreeBytes) }
        Invoke-RCJson -ArgumentList $args
    }
}

function Get-RCGfsRetentionPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [string]$Pattern = '*.rcimg',
        [int]$DailyKeep,
        [int]$WeeklyKeep,
        [int]$MonthlyKeep
    )
    $args = @('retention','gfs-plan','--repository',$RepositoryPath,'--pattern',$Pattern)
    if ($PSBoundParameters.ContainsKey('DailyKeep')) { $args += @('--daily-keep',[string]$DailyKeep) }
    if ($PSBoundParameters.ContainsKey('WeeklyKeep')) { $args += @('--weekly-keep',[string]$WeeklyKeep) }
    if ($PSBoundParameters.ContainsKey('MonthlyKeep')) { $args += @('--monthly-keep',[string]$MonthlyKeep) }
    Invoke-RCJson -ArgumentList $args
}

function Invoke-RCGfsRetention {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [string]$Pattern = '*.rcimg',
        [int]$DailyKeep,
        [int]$WeeklyKeep,
        [int]$MonthlyKeep
    )
    if ($PSCmdlet.ShouldProcess($RepositoryPath, "Apply RescueClone GFS retention policy")) {
        $args = @('retention','gfs-apply','--repository',$RepositoryPath,'--pattern',$Pattern)
        if ($PSBoundParameters.ContainsKey('DailyKeep')) { $args += @('--daily-keep',[string]$DailyKeep) }
        if ($PSBoundParameters.ContainsKey('WeeklyKeep')) { $args += @('--weekly-keep',[string]$WeeklyKeep) }
        if ($PSBoundParameters.ContainsKey('MonthlyKeep')) { $args += @('--monthly-keep',[string]$MonthlyKeep) }
        Invoke-RCJson -ArgumentList $args
    }
}

function Get-RCSchedulePlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$TaskName,
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$JobFilePath,
        [string]$CliPath,
        [ValidateSet('Daily','Weekly','Monthly','Event')][string]$Frequency = 'Daily',
        [string]$Time = '02:00',
        [switch]$RunMissed,
        [string]$EventLog,
        [int]$EventId,
        [string]$EventSource
    )
    $args = @('schedule','plan','--task-name',$TaskName,'--job-file',$JobFilePath,'--frequency',$Frequency,'--time',$Time)
    if ($PSBoundParameters.ContainsKey('CliPath')) { $args += @('--cli-path',$CliPath) }
    if ($RunMissed) { $args += '--run-missed' }
    if ($PSBoundParameters.ContainsKey('EventLog')) { $args += @('--event-log',$EventLog) }
    if ($PSBoundParameters.ContainsKey('EventId')) { $args += @('--event-id',[string]$EventId) }
    if ($PSBoundParameters.ContainsKey('EventSource')) { $args += @('--event-source',$EventSource) }
    Invoke-RCJson -ArgumentList $args
}

function Register-RCSchedule {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][string]$TaskName,
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$JobFilePath,
        [string]$CliPath,
        [ValidateSet('Daily','Weekly','Monthly','Event')][string]$Frequency = 'Daily',
        [string]$Time = '02:00',
        [switch]$RunMissed,
        [string]$EventLog,
        [int]$EventId,
        [string]$EventSource
    )
    if ($PSCmdlet.ShouldProcess($TaskName, "Register RescueClone scheduled task")) {
        $args = @('schedule','register','--task-name',$TaskName,'--job-file',$JobFilePath,'--frequency',$Frequency,'--time',$Time)
        if ($PSBoundParameters.ContainsKey('CliPath')) { $args += @('--cli-path',$CliPath) }
        if ($RunMissed) { $args += '--run-missed' }
        if ($PSBoundParameters.ContainsKey('EventLog')) { $args += @('--event-log',$EventLog) }
        if ($PSBoundParameters.ContainsKey('EventId')) { $args += @('--event-id',[string]$EventId) }
        if ($PSBoundParameters.ContainsKey('EventSource')) { $args += @('--event-source',$EventSource) }
        Invoke-RCJson -ArgumentList $args
    }
}

function Get-RCScheduleStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$TaskName
    )
    Invoke-RCJson -ArgumentList @('schedule','status','--task-name',$TaskName) -AllowNonZeroExit
}

function Start-RCSchedule {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][string]$TaskName
    )
    if ($PSCmdlet.ShouldProcess($TaskName, "Run RescueClone scheduled task now")) {
        Invoke-RCJson -ArgumentList @('schedule','run','--task-name',$TaskName)
    }
}

function Unregister-RCSchedule {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory=$true)][string]$TaskName
    )
    if ($PSCmdlet.ShouldProcess($TaskName, "Unregister RescueClone scheduled task")) {
        Invoke-RCJson -ArgumentList @('schedule','unregister','--task-name',$TaskName)
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

function New-RCRescueAnswer {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][string]$OutputPath,
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$RepositoryPath,
        [Parameter(Mandatory=$true)][string]$ImagePath,
        [string]$Password,
        [Parameter(Mandatory=$true)][string]$TargetDiskId,
        [ValidateSet('Bios','Uefi','Unknown')][string]$BootMode = 'Unknown',
        [long]$TargetDiskSizeBytes,
        [long]$RequiredBytes,
        [switch]$TargetIsCurrentSystemDisk,
        [switch]$HasEfiSystemPartition,
        [string]$BcdStorePath,
        [string[]]$DriverDirectory,
        [string[]]$NetworkShare,
        [bool]$RepairBoot = $true,
        [switch]$RebootAfterRestore,
        [switch]$VerifyImage
    )
    if ($PSCmdlet.ShouldProcess($OutputPath, "Create unattended rescue answer file")) {
        $args = @('rescue','answer-create','--output',$OutputPath,'--repository',$RepositoryPath,'--image',$ImagePath,'--target-disk-id',$TargetDiskId,'--boot-mode',$BootMode,'--repair-boot',[string]$RepairBoot)
        if ($PSBoundParameters.ContainsKey('Password')) { $args += @('--password',$Password) }
        if ($PSBoundParameters.ContainsKey('TargetDiskSizeBytes')) { $args += @('--target-disk-size-bytes',[string]$TargetDiskSizeBytes) }
        if ($PSBoundParameters.ContainsKey('RequiredBytes')) { $args += @('--required-bytes',[string]$RequiredBytes) }
        if ($TargetIsCurrentSystemDisk) { $args += '--target-is-current-system-disk' }
        if ($HasEfiSystemPartition) { $args += '--has-efi-system-partition' }
        if ($PSBoundParameters.ContainsKey('BcdStorePath')) { $args += @('--bcd-store',$BcdStorePath) }
        if ($PSBoundParameters.ContainsKey('DriverDirectory')) { $args += @('--driver-directories',($DriverDirectory -join ';')) }
        if ($PSBoundParameters.ContainsKey('NetworkShare')) { $args += @('--network-shares',($NetworkShare -join ';')) }
        if ($RebootAfterRestore) { $args += '--reboot-after-restore' }
        if ($VerifyImage) { $args += '--verify-image' }
        Invoke-RCJson -ArgumentList $args -AllowNonZeroExit
    }
}

function Test-RCRescueAnswer {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$Path,
        [switch]$VerifyImage
    )
    $args = @('rescue','answer-validate','--file',$Path)
    if ($VerifyImage) { $args += '--verify-image' }
    Invoke-RCJson -ArgumentList $args -AllowNonZeroExit
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

function Get-RCOperationKind {
    [CmdletBinding()]
    param()
    Invoke-RCJson -ArgumentList @('operation','kinds')
}

function Test-RCOperation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$RequestPath
    )
    Invoke-RCJson -ArgumentList @('operation','validate','--request',$RequestPath) -AllowNonZeroExit
}

function Start-RCServiceOperation {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][string]$PipeName,
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })][string]$RequestPath,
        [string]$LogDirectory,
        [int]$TimeoutMilliseconds = 30000
    )
    if ($PSCmdlet.ShouldProcess($PipeName, "Run RescueClone operation request through service IPC")) {
        $args = @('service','run-operation','--pipe',$PipeName,'--request',$RequestPath,'--timeout-ms',[string]$TimeoutMilliseconds)
        if ($PSBoundParameters.ContainsKey('LogDirectory')) { $args += @('--log-directory',$LogDirectory) }
        Invoke-RCJson -ArgumentList $args
    }
}

function Get-RCServiceInstallPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$PipeName,
        [string]$CliPath,
        [string]$LogDirectory,
        [string]$DisplayName,
        [ValidateSet('auto','delayed-auto','demand','disabled')][string]$StartMode = 'auto'
    )
    $args = @('service','plan-install','--name',$Name,'--pipe',$PipeName,'--start-mode',$StartMode)
    if ($PSBoundParameters.ContainsKey('CliPath')) { $args += @('--cli-path',$CliPath) }
    if ($PSBoundParameters.ContainsKey('LogDirectory')) { $args += @('--log-directory',$LogDirectory) }
    if ($PSBoundParameters.ContainsKey('DisplayName')) { $args += @('--display-name',$DisplayName) }
    Invoke-RCJson -ArgumentList $args
}

function Install-RCService {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$PipeName,
        [string]$CliPath,
        [string]$LogDirectory,
        [string]$DisplayName,
        [ValidateSet('auto','delayed-auto','demand','disabled')][string]$StartMode = 'auto'
    )
    if ($PSCmdlet.ShouldProcess($Name, "Install RescueClone Windows Service")) {
        $args = @('service','install','--name',$Name,'--pipe',$PipeName,'--start-mode',$StartMode)
        if ($PSBoundParameters.ContainsKey('CliPath')) { $args += @('--cli-path',$CliPath) }
        if ($PSBoundParameters.ContainsKey('LogDirectory')) { $args += @('--log-directory',$LogDirectory) }
        if ($PSBoundParameters.ContainsKey('DisplayName')) { $args += @('--display-name',$DisplayName) }
        Invoke-RCJson -ArgumentList $args
    }
}

function Get-RCServiceStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$Name
    )
    Invoke-RCJson -ArgumentList @('service','status','--name',$Name)
}

function Start-RCService {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][string]$Name
    )
    if ($PSCmdlet.ShouldProcess($Name, "Start RescueClone Windows Service")) {
        Invoke-RCJson -ArgumentList @('service','start','--name',$Name)
    }
}

function Stop-RCService {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][string]$Name
    )
    if ($PSCmdlet.ShouldProcess($Name, "Stop RescueClone Windows Service")) {
        Invoke-RCJson -ArgumentList @('service','stop','--name',$Name) -AllowNonZeroExit
    }
}

function Set-RCServiceRecovery {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='Medium')]
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [int]$ResetPeriodSeconds = 86400,
        [int]$RestartDelayMilliseconds = 60000,
        [bool]$RestartOnFailure = $true
    )
    if ($PSCmdlet.ShouldProcess($Name, "Configure RescueClone Windows Service recovery policy")) {
        Invoke-RCJson -ArgumentList @('service','recovery','--name',$Name,'--reset-period-seconds',[string]$ResetPeriodSeconds,'--restart-delay-ms',[string]$RestartDelayMilliseconds,'--restart-on-failure',[string]$RestartOnFailure)
    }
}

function Get-RCServiceRecovery {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$Name
    )
    Invoke-RCJson -ArgumentList @('service','recovery-status','--name',$Name)
}

function Uninstall-RCService {
    [CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
    param(
        [Parameter(Mandatory=$true)][string]$Name
    )
    if ($PSCmdlet.ShouldProcess($Name, "Uninstall RescueClone Windows Service")) {
        Invoke-RCJson -ArgumentList @('service','uninstall','--name',$Name)
    }
}

function Get-RCLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })][string]$DirectoryPath,
        [string]$Pattern = '*.json'
    )
    Invoke-RCJson -ArgumentList @('logs','list','--directory',$DirectoryPath,'--pattern',$Pattern)
}
