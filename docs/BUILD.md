# Build

From the repository root, the preferred portable build is:

```powershell
.\scripts\Install-FLocalDotNet.ps1
.\scripts\Build-Portable.ps1
```

That command keeps NuGet packages and .NET CLI home under the project folder:

- `.dotnet-sdk`
- `.nuget-packages`
- `.dotnet-home`

It publishes normal-use executables here:

- `publish\cli\rc.exe`
- `publish\gui\RescueClone.App.exe`

It also builds the native C++ engine boundary here:

- `native\bin\RescueClone.Native.dll`

`scripts\Build-Native.ps1` auto-detects an F-local MinGW `g++.exe`; pass `-CompilerPath` to use a different compiler path.

Direct SDK build commands are still available for development:

```powershell
dotnet restore RescueClone.sln --packages .\.nuget-packages
dotnet build RescueClone.sln -c Release
dotnet test RescueClone.sln -c Release
```

CLI examples:

```powershell
.\RC.cmd image create --source .\sample-source --image .\sample.rcimg --compression High --password secret --format V2
.\RC.cmd image verify --image .\sample.rcimg --password secret
.\RC.cmd image restore --image .\sample.rcimg --target .\sample-restore --password secret
.\RC.cmd job create --file .\backup-job.json --job-id daily-docs --name "Daily Docs" --source .\sample-source --image .\images\daily-docs.rcimg --compression High --verify-after-create true --log-directory .\backup-logs
.\RC.cmd job update --file .\backup-job.json --name "Daily Docs Updated" --enabled true --compression Medium
.\RC.cmd job export --file .\backup-job.json --output .\backup-job.export.json
.\RC.cmd job import --file .\backup-job.export.json --target .\backup-job.imported.json
.\RC.cmd job status --file .\backup-job.json
.\RC.cmd job validate --file .\backup-job.json
.\RC.cmd job run --file .\backup-job.json
.\RC.cmd job delete --file .\backup-job.json
.\RC.cmd retention plan --repository .\images --pattern *.rcimg --keep-count 3 --max-age-days 30
.\RC.cmd retention apply --repository .\images --pattern *.rcimg --keep-count 3 --max-age-days 30
.\RC.cmd schedule plan --task-name nightly-docs --job-file .\backup-job.json --cli-path .\publish\cli\rc.exe --frequency Daily --time 02:00 --run-missed
.\RC.cmd schedule register --task-name nightly-docs --job-file .\backup-job.json --cli-path .\publish\cli\rc.exe --frequency Daily --time 02:00 --run-missed
.\RC.cmd schedule plan --task-name event-docs --job-file .\backup-job.json --cli-path .\publish\cli\rc.exe --frequency Event --event-log Application --event-id 1000 --event-source RescueClone
.\RC.cmd schedule unregister --task-name nightly-docs
.\RC.cmd restore plan --image .\sample.rcimg --target-disk-id disk-fixture-1 --boot-mode Bios --bcd-store .\BCD --target-disk-size-bytes 1048576
.\RC.cmd operation run --request .\operation.json --log-directory .\operation-logs
.\RC.cmd logs list --directory .\backup-logs
.\RC.cmd storage volumes
.\RC.cmd storage disks
.\RC.cmd native status
```

Backup job JSON example:

```json
{
  "jobId": "daily-docs",
  "name": "Daily Docs",
  "enabled": true,
  "sourcePath": "F:\\Backups\\Source",
  "imagePath": "F:\\Backups\\Images\\daily-docs.rcimg",
  "compression": "High",
  "password": null,
  "verifyAfterCreate": true,
  "logDirectory": "F:\\Backups\\Logs",
  "preBackupScriptPath": "F:\\Backups\\Scripts\\before-backup.cmd",
  "postBackupScriptPath": "F:\\Backups\\Scripts\\after-backup.cmd",
  "scriptHookTimeoutSeconds": 300,
  "logRetentionCount": 20,
  "notifyWindowsEventLog": true,
  "notifyEmail": true,
  "emailFrom": "rescueclone@example.invalid",
  "emailTo": "operator@example.invalid",
  "emailPickupDirectory": "F:\\Backups\\EmailPickup",
  "emailSmtpHost": null,
  "emailSmtpPort": 25,
  "emailEnableSsl": false,
  "emailUsername": null,
  "emailPassword": null,
  "retryCount": 2,
  "retryDelaySeconds": 5,
  "restoreTestAfterCreate": true,
  "restoreTestTargetPath": "F:\\Backups\\RestoreTests\\daily-docs",
  "applyRetentionAfterCreate": true,
  "retentionPattern": "*.rcimg",
  "retentionKeepCount": 7,
  "retentionMaxAgeDays": 30,
  "retentionMinFreeBytes": 10737418240
}
```

Operation request JSON example:

```json
{
  "kind": "image.verify",
  "operationId": "verify-sample",
  "parameters": {
    "image": "F:\\Backups\\Images\\daily-docs.rcimg",
    "password": null
  }
}
```

Supported local operation kinds currently include `image.create.directory`, `image.verify`,
`image.restore.directory`, `job.backup.directory.create`, `job.backup.directory.update`,
`job.backup.directory.delete`, `job.backup.directory.export`, `job.backup.directory.import`,
`job.backup.directory.status`, `job.backup.directory.validate`, `job.backup.directory.run`,
`retention.plan`, `retention.apply`, and `restore.plan.readonly`.

When `rc operation run` or `Start-RCOperation` receives `--log-directory` / `-LogDirectory`,
the runner writes `<operation-id>.json` plus `<operation-id>.state.json`. The state sidecar
contains the original request and final report so a failed unattended operation leaves a readable
recovery artifact. Operation reports include structured audit events named `operation.started`,
`operation.succeeded`, and `operation.failed`.

PowerShell examples:

```powershell
Import-Module .\powershell\RescueClone\RescueClone.psd1 -Force
New-RCImage -SourcePath .\sample-source -ImagePath .\sample.rcimg -Compression High -Format V2 -Password secret -Confirm:$false
Test-RCImage -ImagePath .\sample.rcimg -Password secret
Restore-RCImage -ImagePath .\sample.rcimg -TargetPath .\sample-restore -Password secret -Confirm:$false
New-RCBackupJob -Path .\backup-job.json -JobId daily-docs -Name 'Daily Docs' -SourcePath .\sample-source -ImagePath .\images\daily-docs.rcimg -Compression High -VerifyAfterCreate $true -LogDirectory .\backup-logs -Confirm:$false
Set-RCBackupJob -Path .\backup-job.json -Name 'Daily Docs Updated' -Enabled $true -Compression Medium -Confirm:$false
Export-RCBackupJob -Path .\backup-job.json -OutputPath .\backup-job.export.json -Confirm:$false
Import-RCBackupJob -Path .\backup-job.export.json -TargetPath .\backup-job.imported.json -Confirm:$false
Get-RCBackupJobStatus -Path .\backup-job.json
Test-RCBackupJob -Path .\backup-job.json
Start-RCBackupJob -Path .\backup-job.json -Confirm:$false
Remove-RCBackupJob -Path .\backup-job.json -Confirm:$false
Get-RCRetentionPlan -RepositoryPath .\images -Pattern *.rcimg -KeepCount 3 -MaxAgeDays 30
Invoke-RCRetention -RepositoryPath .\images -Pattern *.rcimg -KeepCount 3 -MaxAgeDays 30 -Confirm:$false
Get-RCSchedulePlan -TaskName nightly-docs -JobFilePath .\backup-job.json -CliPath .\publish\cli\rc.exe -Frequency Daily -Time 02:00 -RunMissed
Register-RCSchedule -TaskName nightly-docs -JobFilePath .\backup-job.json -CliPath .\publish\cli\rc.exe -Frequency Daily -Time 02:00 -RunMissed -Confirm:$false
Get-RCSchedulePlan -TaskName event-docs -JobFilePath .\backup-job.json -CliPath .\publish\cli\rc.exe -Frequency Event -EventLog Application -EventId 1000 -EventSource RescueClone
Unregister-RCSchedule -TaskName nightly-docs -Confirm:$false
Get-RCRestorePlan -ImagePath .\sample.rcimg -TargetDiskId disk-fixture-1 -BootMode Bios -BcdStorePath .\BCD -TargetDiskSizeBytes 1048576
Start-RCOperation -RequestPath .\operation.json -LogDirectory .\operation-logs -Confirm:$false
Get-RCLog -DirectoryPath .\backup-logs
Get-RCVolume
Get-RCDisk
Get-RCNativeStatus
```

Dependency note: normal CLI, GUI, and PowerShell use the self-contained binaries in `publish`. After `scripts\Install-FLocalDotNet.ps1`, build commands use `.dotnet-sdk\dotnet.exe` from the project folder. The default seed source is the Codex-local SDK cache under `C:\Users\micha\.codex\tools\dotnet-sdk-10.0.301`; pass `-SourceDotNetRoot` to seed from a different drive. Disk inventory uses the built-in Windows `Get-Disk` storage cmdlet through Windows PowerShell in read-only mode.
