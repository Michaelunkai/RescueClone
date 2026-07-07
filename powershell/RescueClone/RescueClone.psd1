@{
    RootModule = 'RescueClone.psm1'
    ModuleVersion = '0.1.0'
    GUID = '4dfcf59d-2ad4-4c8a-81e4-72f6e8f812f0'
    Author = 'RescueClone'
    CompanyName = 'RescueClone'
    Copyright = '(c) RescueClone'
    PowerShellVersion = '5.1'
    FunctionsToExport = @('Get-RCFeature','Get-RCVolume','Get-RCDisk','Get-RCDiskSafety','Get-RCNativeStatus','New-RCImage','Test-RCImage','Restore-RCImage','New-RCBackupJob','Set-RCBackupJob','Remove-RCBackupJob','Export-RCBackupJob','Import-RCBackupJob','Get-RCBackupJobStatus','Test-RCBackupJob','Start-RCBackupJob','Get-RCRetentionPlan','Invoke-RCRetention','Get-RCSchedulePlan','Register-RCSchedule','Unregister-RCSchedule','Get-RCRestorePlan','Start-RCOperation','Get-RCLog')
    CmdletsToExport = @()
    VariablesToExport = '*'
    AliasesToExport = @()
}
