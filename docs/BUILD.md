# Build

From the repository root, the preferred portable build is:

```powershell
.\scripts\Install-FLocalDotNet.ps1
.\scripts\Build-Portable.ps1
.\scripts\Test-PortableDependencyBoundary.ps1
.\scripts\New-PortablePackage.ps1 -OutputPath .\artifacts\RescueClone-portable.zip -Force
.\scripts\Test-PortableInstall.ps1 -InstallRoot F:\Tools\RescueClone-Smoke
.\scripts\Install-RescueClone.ps1 -InstallRoot F:\Tools\RescueClone -Quiet -NoRestart -Force
.\scripts\Uninstall-RescueClone.ps1 -InstallRoot F:\Tools\RescueClone -Quiet -NoRestart
```

The module layout and developer-facing design rules are documented in `docs\ARCHITECTURE.md`.

That command keeps NuGet packages and .NET CLI home under the project folder:

- `.dotnet-sdk`
- `.nuget-packages`
- `.dotnet-home`

It publishes normal-use self-contained directories here:

- `publish\cli\rc.exe`
- `publish\gui\RescueClone.App.exe`
- `artifacts\RescueClone-portable.zip.sha256` after `scripts\New-PortablePackage.ps1` runs

It also builds the native C++ engine boundary here:

- `native\bin\RescueClone.Native.dll`

`scripts\Build-Native.ps1` auto-detects an F-local MinGW `g++.exe`; pass `-CompilerPath` to use a different compiler path.
`scripts\Build-Portable.ps1` also accepts `-NativeCompilerPath` and forwards it to the native build, which is how CI uses the hosted MSYS2 MinGW compiler.

Direct SDK build commands are still available for development:

```powershell
dotnet restore RescueClone.sln --packages .\.nuget-packages
dotnet build RescueClone.sln -c Release
dotnet test RescueClone.sln -c Release
```

CI:

- `.github\workflows\ci.yml` runs on `windows-latest`.
- It installs .NET SDK `10.0.301` from `global.json`, installs MSYS2 MinGW, runs `scripts\Build-Portable.ps1 -AllowSystemDotNetFallback -NativeCompilerPath C:\msys64\mingw64\bin\g++.exe`, runs `scripts\Test-Portable.ps1`, runs `scripts\Test-PortableInstall.ps1`, builds the portable ZIP, and uploads it as an artifact.
- The portable package step writes both `RescueClone-portable.zip` and `RescueClone-portable.zip.sha256`; CI verifies both exist and uploads both.

CLI examples:

```powershell
.\RC.cmd image create --source .\sample-source --image .\sample.rcimg --compression High --password secret --format V2
.\RC.cmd image verify --image .\sample.rcimg --password secret
.\RC.cmd image audit --repository . --pattern *.rcimg --password secret
.\RC.cmd image protect-audit --repository . --pattern *.rcimg
.\RC.cmd image protect --repository . --pattern *.rcimg
.\RC.cmd image compare --image .\sample.rcimg --source .\sample-source --password secret
.\RC.cmd image list --repository . --pattern *.rcimg --verify --password secret
.\RC.cmd image browse --image .\sample.rcimg --password secret
.\RC.cmd image extract --image .\sample.rcimg --target .\sample-extract --paths nested\report.txt --password secret
.\RC.cmd image project --image .\sample.rcimg --target .\sample-projection --password secret
.\RC.cmd image projections --root .
.\RC.cmd image unproject --target .\sample-projection
.\RC.cmd image restore --image .\sample.rcimg --target .\sample-restore --password secret
.\RC.cmd job create --file .\backup-job.json --job-id daily-docs --name "Daily Docs" --source .\sample-source --image .\images\daily-docs.rcimg --compression High --verify-after-create true --log-directory .\backup-logs
.\RC.cmd job update --file .\backup-job.json --name "Daily Docs Updated" --enabled true --compression Medium
.\RC.cmd job export --file .\backup-job.json --output .\backup-job.export.json
.\RC.cmd job import --file .\backup-job.export.json --target .\backup-job.imported.json
.\RC.cmd job list --directory . --pattern *.json
.\RC.cmd job status --file .\backup-job.json
.\RC.cmd job history --file .\backup-job.json --pattern *.json
.\RC.cmd job validate --file .\backup-job.json
.\RC.cmd job run --file .\backup-job.json
.\RC.cmd job delete --file .\backup-job.json
.\RC.cmd retention plan --repository .\images --pattern *.rcimg --keep-count 3 --max-age-days 30
.\RC.cmd retention apply --repository .\images --pattern *.rcimg --keep-count 3 --max-age-days 30
.\RC.cmd retention gfs-plan --repository .\images --pattern *.rcimg --daily-keep 7 --weekly-keep 4 --monthly-keep 12
.\RC.cmd retention gfs-apply --repository .\images --pattern *.rcimg --daily-keep 7 --weekly-keep 4 --monthly-keep 12
.\RC.cmd schedule plan --task-name nightly-docs --job-file .\backup-job.json --cli-path .\publish\cli\rc.exe --frequency Daily --time 02:00 --run-missed
.\RC.cmd schedule register --task-name nightly-docs --job-file .\backup-job.json --cli-path .\publish\cli\rc.exe --frequency Daily --time 02:00 --run-missed
.\RC.cmd schedule plan --task-name event-docs --job-file .\backup-job.json --cli-path .\publish\cli\rc.exe --frequency Event --event-log Application --event-id 1000 --event-source RescueClone
.\RC.cmd schedule status --task-name nightly-docs
.\RC.cmd schedule run --task-name nightly-docs
.\RC.cmd schedule unregister --task-name nightly-docs
.\RC.cmd restore plan --image .\sample.rcimg --target-disk-id disk-fixture-1 --boot-mode Bios --bcd-store .\BCD --target-disk-size-bytes 1048576
.\RC.cmd rescue answer-create --output .\rescue-answer.json --repository .\images --image daily-docs.rcimg --target-disk-id disk-fixture-1 --boot-mode Bios --bcd-store .\BCD --target-disk-size-bytes 1048576 --driver-directories .\drivers --network-shares \\server\share --reboot-after-restore --verify-image
.\RC.cmd rescue answer-validate --file .\rescue-answer.json --verify-image
.\RC.cmd operation kinds
.\RC.cmd operation run --request .\operation.json --log-directory .\operation-logs
.\RC.cmd service serve --pipe rescueclone-local --log-directory .\operation-logs
.\RC.cmd service run-operation --pipe rescueclone-local --request .\operation.json --log-directory .\operation-logs --timeout-ms 30000
.\RC.cmd service plan-install --name RescueClone --pipe rescueclone-local --cli-path .\publish\cli\rc.exe --log-directory .\operation-logs --display-name "RescueClone Operation Service" --start-mode demand
.\RC.cmd service install --name RescueClone --pipe rescueclone-local --cli-path .\publish\cli\rc.exe --log-directory .\operation-logs --display-name "RescueClone Operation Service" --start-mode demand
.\RC.cmd service status --name RescueClone
.\RC.cmd service start --name RescueClone
.\RC.cmd service stop --name RescueClone
.\RC.cmd service recovery --name RescueClone --reset-period-seconds 86400 --restart-delay-ms 60000 --restart-on-failure true
.\RC.cmd service recovery-status --name RescueClone
.\RC.cmd service uninstall --name RescueClone
.\RC.cmd logs list --directory .\backup-logs
.\RC.cmd storage volumes
.\RC.cmd storage disks
.\RC.cmd storage disk-safety --disk-number 1 --expected-fingerprint <sha256>
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
`image.audit.repository`, `image.protect.audit`, `image.protect.apply`, `image.compare.source`, `image.list.repository`, `image.browse`, `image.extract.directory`, `image.project.readonly`, `image.project.list`, `image.project.remove`,
`image.restore.directory`,
`job.backup.directory.create`, `job.backup.directory.update`,
`job.backup.directory.delete`, `job.backup.directory.export`, `job.backup.directory.import`,
`job.backup.directory.list`, `job.backup.directory.status`, `job.backup.directory.history`, `job.backup.directory.validate`, `job.backup.directory.run`,
`retention.plan`, `retention.apply`, `retention.gfs.plan`, `retention.gfs.apply`,
`schedule.plan`, `schedule.register`, `schedule.status`, `schedule.run`, `schedule.unregister`,
`restore.plan.readonly`, `rescue.answer.create`, and `rescue.answer.validate`.
Use `rc operation kinds` or `Get-RCOperationKind` to retrieve the current operation kind catalog with required and optional parameters.

When `rc operation run` or `Start-RCOperation` receives `--log-directory` / `-LogDirectory`,
the runner writes `<operation-id>.json` plus `<operation-id>.state.json`. The state sidecar
contains the original request and final report so a failed unattended operation leaves a readable
recovery artifact. Operation reports include structured audit events named `operation.started`,
`operation.succeeded`, and `operation.failed`. Failed operation reports include `errorDetail`
with one of the current codes: `not_found`, `invalid_request`, `operation_failed`,
`invalid_data`, `access_denied`, `io_error`, or `unexpected_error`.

PowerShell examples:

```powershell
Import-Module .\powershell\RescueClone\RescueClone.psd1 -Force
New-RCImage -SourcePath .\sample-source -ImagePath .\sample.rcimg -Compression High -Format V2 -Password secret -Confirm:$false
Test-RCImage -ImagePath .\sample.rcimg -Password secret
Test-RCImageRepository -RepositoryPath . -Pattern *.rcimg -Password secret
Test-RCImageRepositoryProtection -RepositoryPath . -Pattern *.rcimg
Set-RCImageRepositoryProtection -RepositoryPath . -Pattern *.rcimg -Confirm:$false
Compare-RCImage -ImagePath .\sample.rcimg -SourcePath .\sample-source -Password secret
Get-RCImage -RepositoryPath . -Pattern *.rcimg -Verify -Password secret
Get-RCImageContent -ImagePath .\sample.rcimg -Password secret
Export-RCImageFile -ImagePath .\sample.rcimg -TargetPath .\sample-extract -Path nested\report.txt -Password secret -Confirm:$false
Mount-RCImage -ImagePath .\sample.rcimg -TargetPath .\sample-projection -Password secret -Confirm:$false
Get-RCImageMount -RootPath .
Dismount-RCImage -TargetPath .\sample-projection -Confirm:$false
Restore-RCImage -ImagePath .\sample.rcimg -TargetPath .\sample-restore -Password secret -Confirm:$false
New-RCBackupJob -Path .\backup-job.json -JobId daily-docs -Name 'Daily Docs' -SourcePath .\sample-source -ImagePath .\images\daily-docs.rcimg -Compression High -VerifyAfterCreate $true -LogDirectory .\backup-logs -Confirm:$false
Set-RCBackupJob -Path .\backup-job.json -Name 'Daily Docs Updated' -Enabled $true -Compression Medium -Confirm:$false
Export-RCBackupJob -Path .\backup-job.json -OutputPath .\backup-job.export.json -Confirm:$false
Import-RCBackupJob -Path .\backup-job.export.json -TargetPath .\backup-job.imported.json -Confirm:$false
Get-RCBackupJob -DirectoryPath . -Pattern *.json
Get-RCBackupJobStatus -Path .\backup-job.json
Get-RCBackupJobHistory -Path .\backup-job.json -Pattern *.json
Test-RCBackupJob -Path .\backup-job.json
Start-RCBackupJob -Path .\backup-job.json -Confirm:$false
Remove-RCBackupJob -Path .\backup-job.json -Confirm:$false
Get-RCRetentionPlan -RepositoryPath .\images -Pattern *.rcimg -KeepCount 3 -MaxAgeDays 30
Invoke-RCRetention -RepositoryPath .\images -Pattern *.rcimg -KeepCount 3 -MaxAgeDays 30 -Confirm:$false
Get-RCGfsRetentionPlan -RepositoryPath .\images -Pattern *.rcimg -DailyKeep 7 -WeeklyKeep 4 -MonthlyKeep 12
Invoke-RCGfsRetention -RepositoryPath .\images -Pattern *.rcimg -DailyKeep 7 -WeeklyKeep 4 -MonthlyKeep 12 -Confirm:$false
Get-RCSchedulePlan -TaskName nightly-docs -JobFilePath .\backup-job.json -CliPath .\publish\cli\rc.exe -Frequency Daily -Time 02:00 -RunMissed
Register-RCSchedule -TaskName nightly-docs -JobFilePath .\backup-job.json -CliPath .\publish\cli\rc.exe -Frequency Daily -Time 02:00 -RunMissed -Confirm:$false
Get-RCSchedulePlan -TaskName event-docs -JobFilePath .\backup-job.json -CliPath .\publish\cli\rc.exe -Frequency Event -EventLog Application -EventId 1000 -EventSource RescueClone
Get-RCScheduleStatus -TaskName nightly-docs
Start-RCSchedule -TaskName nightly-docs -Confirm:$false
Unregister-RCSchedule -TaskName nightly-docs -Confirm:$false
Get-RCRestorePlan -ImagePath .\sample.rcimg -TargetDiskId disk-fixture-1 -BootMode Bios -BcdStorePath .\BCD -TargetDiskSizeBytes 1048576
New-RCRescueAnswer -OutputPath .\rescue-answer.json -RepositoryPath .\images -ImagePath daily-docs.rcimg -TargetDiskId disk-fixture-1 -BootMode Bios -BcdStorePath .\BCD -TargetDiskSizeBytes 1048576 -DriverDirectory .\drivers -NetworkShare \\server\share -RebootAfterRestore -VerifyImage -Confirm:$false
Test-RCRescueAnswer -Path .\rescue-answer.json -VerifyImage
Get-RCOperationKind
Start-RCOperation -RequestPath .\operation.json -LogDirectory .\operation-logs -Confirm:$false
Start-RCServiceOperation -PipeName rescueclone-local -RequestPath .\operation.json -LogDirectory .\operation-logs -TimeoutMilliseconds 30000 -Confirm:$false
Get-RCServiceInstallPlan -Name RescueClone -PipeName rescueclone-local -CliPath .\publish\cli\rc.exe -LogDirectory .\operation-logs -DisplayName 'RescueClone Operation Service' -StartMode demand
Install-RCService -Name RescueClone -PipeName rescueclone-local -CliPath .\publish\cli\rc.exe -LogDirectory .\operation-logs -DisplayName 'RescueClone Operation Service' -StartMode demand -Confirm:$false
Get-RCServiceStatus -Name RescueClone
Start-RCService -Name RescueClone -Confirm:$false
Stop-RCService -Name RescueClone -Confirm:$false
Set-RCServiceRecovery -Name RescueClone -ResetPeriodSeconds 86400 -RestartDelayMilliseconds 60000 -RestartOnFailure $true -Confirm:$false
Get-RCServiceRecovery -Name RescueClone
Uninstall-RCService -Name RescueClone -Confirm:$false
Get-RCLog -DirectoryPath .\backup-logs
Get-RCVolume
Get-RCDisk
Get-RCDiskSafety -DiskNumber 1 -ExpectedFingerprint <sha256>
Get-RCNativeStatus
```

Dependency note: normal CLI, GUI, and PowerShell use the self-contained directories in `publish`. After `scripts\Install-FLocalDotNet.ps1`, build commands use `.dotnet-sdk\dotnet.exe` from the project folder. `scripts\Build-Portable.ps1` now fails if that project-local SDK is missing unless `-AllowSystemDotNetFallback` is passed explicitly. The default seed source is the Codex-local SDK cache under `C:\Users\micha\.codex\tools\dotnet-sdk-10.0.301`; pass `-SourceDotNetRoot` to seed from a different drive. Disk inventory uses the built-in Windows `Get-Disk` storage cmdlet through Windows PowerShell in read-only mode. `scripts\Test-PortableDependencyBoundary.ps1` launches the published CLI service and GUI, then fails if either loads non-Windows modules from `C:\` or any module outside the project root and `%WINDIR%`.

Service IPC note: `rc service serve --pipe <name>` hosts the current operation runner on a Windows named pipe in the foreground. `rc service install` registers `rc service host --pipe <name>` with the Windows Service Control Manager, and `rc service start/status/stop/recovery/recovery-status/uninstall` manage that registration and restart-on-failure policy. `rc service run-operation`, `Start-RCServiceOperation`, and the GUI Operations tab's service button send the same operation request JSON through that pipe and return the structured operation report. This is the current service foundation; it is not an MSI-installed privileged driver service.

Portable install note: `scripts\Install-RescueClone.ps1` copies the published self-contained CLI/GUI directories, PowerShell module, docs, and launcher scripts to `-InstallRoot` and supports unattended `-Quiet -NoRestart`. It is a portable script installer, not an MSI/EXE installer and it does not install a privileged Windows Service or drivers.

Portable package note: `scripts\New-PortablePackage.ps1` creates a ZIP containing `publish`, `powershell`, `docs`, launchers, and portable install/uninstall scripts. It does not build an MSI/EXE.

Projection note: `rc image project`, `Mount-RCImage`, and the GUI Project Image button create a managed read-only directory projection by restoring verified image content, marking projected files read-only, and writing `.rescueclone-projection.json`. `rc image projections`, `Get-RCImageMount`, and the GUI List Projections button enumerate those manifests under a selected root. `rc image unproject`, `Dismount-RCImage`, and the GUI Remove Projection button only remove directories with that manifest. This is a safe user-mode projection layer, not a signed kernel image-mount driver.

Rescue answer note: `rc rescue answer-create`, `New-RCRescueAnswer`, and the GUI Rescue tab write a versioned unattended restore answer JSON with repository, image, target disk, boot mode, driver folders, network shares, boot repair, and reboot policy. Validation verifies the image when requested and reuses the restore planner blockers. This is not WinPE, ISO, USB, or PXE media creation.

GFS retention note: `rc retention gfs-plan`, `Get-RCGfsRetentionPlan`, and the GUI Retention tab keep the newest selected daily, weekly, and monthly buckets from a flat image repository and delete unselected images only when the apply command is used. This is GFS-style pruning for standalone directory images; it is not incremental-chain consolidation.

Disk safety checks are read-only. The evaluator fingerprints the selected disk from number, friendly name, serial number, partition style, bus type, and size, then blocks by default when the disk is missing, fingerprint-mismatched, boot/system, offline, or read-only.
