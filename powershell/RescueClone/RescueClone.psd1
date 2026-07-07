@{
    RootModule = 'RescueClone.psm1'
    ModuleVersion = '0.1.0'
    GUID = '4dfcf59d-2ad4-4c8a-81e4-72f6e8f812f0'
    Author = 'RescueClone'
    CompanyName = 'RescueClone'
    Copyright = '(c) RescueClone'
    PowerShellVersion = '5.1'
    FunctionsToExport = @('Get-RCFeature','Get-RCVolume','Get-RCDisk','Get-RCDiskSafety','Get-RCNativeStatus','New-RCImage','Test-RCImage','Get-RCImageContent','Get-RCImage','Test-RCImageRepository','Compare-RCImage','Export-RCImageFile','Mount-RCImage','Get-RCImageMount','Dismount-RCImage','Restore-RCImage','New-RCBackupJob','Set-RCBackupJob','Remove-RCBackupJob','Export-RCBackupJob','Import-RCBackupJob','Get-RCBackupJobStatus','Test-RCBackupJob','Start-RCBackupJob','Get-RCRetentionPlan','Invoke-RCRetention','Get-RCGfsRetentionPlan','Invoke-RCGfsRetention','Get-RCSchedulePlan','Register-RCSchedule','Get-RCScheduleStatus','Start-RCSchedule','Unregister-RCSchedule','Get-RCRestorePlan','New-RCRescueAnswer','Test-RCRescueAnswer','Start-RCOperation','Start-RCServiceOperation','Get-RCServiceInstallPlan','Install-RCService','Get-RCServiceStatus','Start-RCService','Stop-RCService','Set-RCServiceRecovery','Get-RCServiceRecovery','Uninstall-RCService','Get-RCLog')
    CmdletsToExport = @()
    VariablesToExport = '*'
    AliasesToExport = @()
}
