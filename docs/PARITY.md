# Interface Parity

The source of truth for implemented parity is `RescueClone.Core.FeatureCatalog`.

| Feature | GUI | CLI | PowerShell |
| --- | --- | --- | --- |
| Directory image create | Create Image tab | `rc image create` | `New-RCImage` |
| Image verify | Verify Image tab | `rc image verify` | `Test-RCImage` |
| Directory restore | Restore Image tab | `rc image restore` | `Restore-RCImage` |
| Directory backup job validate | Backup Job tab | `rc job validate` | `Test-RCBackupJob` |
| Directory backup job run | Backup Job tab | `rc job run` | `Start-RCBackupJob` |
| Read-only restore plan | Restore Plan tab | `rc restore plan` | `Get-RCRestorePlan` |
| Durable local operation run | Operations tab | `rc operation run` | `Start-RCOperation` |

The test suite asserts that every implemented feature has GUI, CLI, and PowerShell entries in the catalog. New functionality must be added to all three surfaces before it is marked implemented.
