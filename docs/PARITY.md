# Interface Parity

The source of truth for implemented parity is `RescueClone.Core.FeatureCatalog`.

| Feature | GUI | CLI | PowerShell |
| --- | --- | --- | --- |
| Directory image create with v1/v2 format selection | Create Image tab | `rc image create` | `New-RCImage` |
| Image verify | Verify Image tab | `rc image verify` | `Test-RCImage` |
| Repository integrity audit | Verify Image tab | `rc image audit` | `Test-RCImageRepository` |
| Image-to-source compare | Verify Image tab | `rc image compare` | `Compare-RCImage` |
| Image content browse | Restore Image tab | `rc image browse` | `Get-RCImageContent` |
| Repository image list | Restore Image tab | `rc image list` | `Get-RCImage` |
| Selected file/folder extract | Restore Image tab | `rc image extract` | `Export-RCImageFile` |
| Read-only image projection | Restore Image tab | `rc image project` | `Mount-RCImage` |
| List image projections | Restore Image tab | `rc image projections` | `Get-RCImageMount` |
| Remove image projection | Restore Image tab | `rc image unproject` | `Dismount-RCImage` |
| Directory restore | Restore Image tab | `rc image restore` | `Restore-RCImage` |
| Directory backup job create | Backup Job tab | `rc job create` | `New-RCBackupJob` |
| Directory backup job update | Backup Job tab | `rc job update` | `Set-RCBackupJob` |
| Directory backup job delete | Backup Job tab | `rc job delete` | `Remove-RCBackupJob` |
| Directory backup job export | Backup Job tab | `rc job export` | `Export-RCBackupJob` |
| Directory backup job import | Backup Job tab | `rc job import` | `Import-RCBackupJob` |
| Directory backup job status | Backup Job tab | `rc job status` | `Get-RCBackupJobStatus` |
| Directory backup job validate | Backup Job tab | `rc job validate` | `Test-RCBackupJob` |
| Directory backup job run | Backup Job tab | `rc job run` | `Start-RCBackupJob` |
| Retention plan | Retention tab | `rc retention plan` | `Get-RCRetentionPlan` |
| Retention apply | Retention tab | `rc retention apply` | `Invoke-RCRetention` |
| Schedule plan | Scheduler tab | `rc schedule plan` | `Get-RCSchedulePlan` |
| Schedule register | Scheduler tab | `rc schedule register` | `Register-RCSchedule` |
| Schedule unregister | Scheduler tab | `rc schedule unregister` | `Unregister-RCSchedule` |
| Read-only restore plan | Restore Plan tab | `rc restore plan` | `Get-RCRestorePlan` |
| Unattended rescue answer create | Rescue tab | `rc rescue answer-create` | `New-RCRescueAnswer` |
| Unattended rescue answer validate | Rescue tab | `rc rescue answer-validate` | `Test-RCRescueAnswer` |
| Durable local operation run | Operations tab | `rc operation run` | `Start-RCOperation` |
| Service IPC operation run | Operations tab | `rc service run-operation` | `Start-RCServiceOperation` |
| Windows Service install plan | Operations tab | `rc service plan-install` | `Get-RCServiceInstallPlan` |
| Windows Service install | Operations tab | `rc service install` | `Install-RCService` |
| Windows Service status | Operations tab | `rc service status` | `Get-RCServiceStatus` |
| Windows Service start | Operations tab | `rc service start` | `Start-RCService` |
| Windows Service stop | Operations tab | `rc service stop` | `Stop-RCService` |
| Windows Service uninstall | Operations tab | `rc service uninstall` | `Uninstall-RCService` |
| Centralized backup log listing | Logs tab | `rc logs list` | `Get-RCLog` |
| Read-only volume inventory | Volumes tab | `rc storage volumes` | `Get-RCVolume` |
| Read-only disk inventory | Disks tab | `rc storage disks` | `Get-RCDisk` |
| Disk target safety check | Disks tab | `rc storage disk-safety` | `Get-RCDiskSafety` |
| Native engine status | Native Engine tab | `rc native status` | `Get-RCNativeStatus` |

The test suite asserts that every implemented feature has GUI, CLI, and PowerShell entries in the catalog. New functionality must be added to all three surfaces before it is marked implemented.
