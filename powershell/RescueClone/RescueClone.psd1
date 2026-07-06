@{
    RootModule = 'RescueClone.psm1'
    ModuleVersion = '0.1.0'
    GUID = '4dfcf59d-2ad4-4c8a-81e4-72f6e8f812f0'
    Author = 'RescueClone'
    CompanyName = 'RescueClone'
    Copyright = '(c) RescueClone'
    PowerShellVersion = '5.1'
    FunctionsToExport = @('Get-RCFeature','Get-RCVolume','Get-RCDisk','Get-RCNativeStatus','New-RCImage','Test-RCImage','Restore-RCImage','Test-RCBackupJob','Start-RCBackupJob','Get-RCRetentionPlan','Invoke-RCRetention','Get-RCRestorePlan','Start-RCOperation')
    CmdletsToExport = @()
    VariablesToExport = '*'
    AliasesToExport = @()
}
