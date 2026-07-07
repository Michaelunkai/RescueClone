# Interface Parity

The source of truth for implemented parity is `RescueClone.Core.FeatureCatalog`.

| Feature | GUI | CLI | PowerShell |
| --- | --- | --- | --- |
| Directory image create with v1/v2 format selection | Create Image tab | `rc image create` | `New-RCImage` |
| Image verify | Verify Image tab | `rc image verify` | `Test-RCImage` |
| Directory restore | Restore Image tab | `rc image restore` | `Restore-RCImage` |
| Directory backup job create | Backup Job tab | `rc job create` | `New-RCBackupJob` |
| Directory backup job delete | Backup Job tab | `rc job delete` | `Remove-RCBackupJob` |
| Directory backup job validate | Backup Job tab | `rc job validate` | `Test-RCBackupJob` |
| Directory backup job run | Backup Job tab | `rc job run` | `Start-RCBackupJob` |
| Retention plan | Retention tab | `rc retention plan` | `Get-RCRetentionPlan` |
| Retention apply | Retention tab | `rc retention apply` | `Invoke-RCRetention` |
| Schedule plan | Scheduler tab | `rc schedule plan` | `Get-RCSchedulePlan` |
| Schedule register | Scheduler tab | `rc schedule register` | `Register-RCSchedule` |
| Schedule unregister | Scheduler tab | `rc schedule unregister` | `Unregister-RCSchedule` |
| Read-only restore plan | Restore Plan tab | `rc restore plan` | `Get-RCRestorePlan` |
| Durable local operation run | Operations tab | `rc operation run` | `Start-RCOperation` |
| Centralized backup log listing | Logs tab | `rc logs list` | `Get-RCLog` |
| Read-only volume inventory | Volumes tab | `rc storage volumes` | `Get-RCVolume` |
| Read-only disk inventory | Disks tab | `rc storage disks` | `Get-RCDisk` |
| Native engine status | Native Engine tab | `rc native status` | `Get-RCNativeStatus` |

The test suite asserts that every implemented feature has GUI, CLI, and PowerShell entries in the catalog. New functionality must be added to all three surfaces before it is marked implemented.
