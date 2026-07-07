# RescueClone User Guide

RescueClone is a clean-room Windows backup suite foundation for verified directory images. It is not a disk-imaging clone of Macrium Reflect. The commands below describe the implemented and tested surfaces only.

## Entry Points

- GUI: run `RUN-GUI.cmd`.
- CLI: run `RC.cmd`.
- PowerShell: run `Import-Module .\powershell\RescueClone\RescueClone.psd1 -Force`.

All implemented features are listed with matching GUI, CLI, and PowerShell surfaces in `docs\PARITY.md`.

## Create And Verify An Image

CLI:

```powershell
.\RC.cmd image create --source F:\Data --image F:\Backups\data.rcimg --compression High --format V2 --password secret
.\RC.cmd image verify --image F:\Backups\data.rcimg --password secret
.\RC.cmd image compare --image F:\Backups\data.rcimg --source F:\Data --password secret
```

PowerShell:

```powershell
New-RCImage -SourcePath F:\Data -ImagePath F:\Backups\data.rcimg -Compression High -Format V2 -Password secret -Confirm:$false
Test-RCImage -ImagePath F:\Backups\data.rcimg -Password secret
Compare-RCImage -ImagePath F:\Backups\data.rcimg -SourcePath F:\Data -Password secret
```

Created image files are marked read-only after successful creation. Use repository protection controls to audit or reapply the read-only bit across an image folder.

## Browse, Extract, Project, And Restore

CLI:

```powershell
.\RC.cmd image browse --image F:\Backups\data.rcimg --password secret
.\RC.cmd image extract --image F:\Backups\data.rcimg --target F:\Restore\selected --paths docs\report.docx --password secret
.\RC.cmd image project --image F:\Backups\data.rcimg --target F:\Mounted\data --password secret
.\RC.cmd image projections --root F:\Mounted
.\RC.cmd image unproject --target F:\Mounted\data
.\RC.cmd image restore --image F:\Backups\data.rcimg --target F:\Restore\full --password secret --overwrite
```

PowerShell:

```powershell
Get-RCImageContent -ImagePath F:\Backups\data.rcimg -Password secret
Export-RCImageFile -ImagePath F:\Backups\data.rcimg -TargetPath F:\Restore\selected -Path docs\report.docx -Password secret -Confirm:$false
Mount-RCImage -ImagePath F:\Backups\data.rcimg -TargetPath F:\Mounted\data -Password secret -Confirm:$false
Get-RCImageMount -RootPath F:\Mounted
Dismount-RCImage -TargetPath F:\Mounted\data -Confirm:$false
Restore-RCImage -ImagePath F:\Backups\data.rcimg -TargetPath F:\Restore\full -Password secret -Overwrite -Confirm:$false
```

Projection is a managed read-only directory projection, not a kernel image mount driver.

## Repository Maintenance

CLI:

```powershell
.\RC.cmd image list --repository F:\Backups --pattern *.rcimg --verify --password secret
.\RC.cmd image audit --repository F:\Backups --pattern *.rcimg --password secret
.\RC.cmd image protect-audit --repository F:\Backups --pattern *.rcimg
.\RC.cmd image protect --repository F:\Backups --pattern *.rcimg
```

PowerShell:

```powershell
Get-RCImage -RepositoryPath F:\Backups -Pattern *.rcimg -Verify -Password secret
Test-RCImageRepository -RepositoryPath F:\Backups -Pattern *.rcimg -Password secret
Test-RCImageRepositoryProtection -RepositoryPath F:\Backups -Pattern *.rcimg
Set-RCImageRepositoryProtection -RepositoryPath F:\Backups -Pattern *.rcimg -Confirm:$false
```

## Backup Jobs

CLI:

```powershell
.\RC.cmd job create --file F:\Jobs\daily.json --job-id daily --name "Daily Data" --source F:\Data --image F:\Backups\daily.rcimg --compression High --verify-after-create true --log-directory F:\Logs
.\RC.cmd job validate --file F:\Jobs\daily.json
.\RC.cmd job run --file F:\Jobs\daily.json
.\RC.cmd job status --file F:\Jobs\daily.json
.\RC.cmd job history --file F:\Jobs\daily.json
.\RC.cmd job list --directory F:\Jobs --pattern *.json
```

PowerShell:

```powershell
New-RCBackupJob -Path F:\Jobs\daily.json -JobId daily -Name "Daily Data" -SourcePath F:\Data -ImagePath F:\Backups\daily.rcimg -Compression High -VerifyAfterCreate $true -LogDirectory F:\Logs -Confirm:$false
Test-RCBackupJob -Path F:\Jobs\daily.json
Start-RCBackupJob -Path F:\Jobs\daily.json -Confirm:$false
Get-RCBackupJobStatus -Path F:\Jobs\daily.json
Get-RCBackupJobHistory -Path F:\Jobs\daily.json
Get-RCBackupJob -DirectoryPath F:\Jobs -Pattern *.json
```

Job runs create JSON and HTML reports. History reports entries for the selected job and keeps malformed log files visible as parse errors.

## Retention

Count, age, and free-space retention:

```powershell
.\RC.cmd retention plan --repository F:\Backups --pattern *.rcimg --keep-count 7 --max-age-days 30
.\RC.cmd retention apply --repository F:\Backups --pattern *.rcimg --keep-count 7 --max-age-days 30
```

GFS-style retention:

```powershell
.\RC.cmd retention gfs-plan --repository F:\Backups --pattern *.rcimg --daily-keep 7 --weekly-keep 4 --monthly-keep 12
.\RC.cmd retention gfs-apply --repository F:\Backups --pattern *.rcimg --daily-keep 7 --weekly-keep 4 --monthly-keep 12
```

GFS retention selects standalone directory images by daily, weekly, and monthly buckets. It does not consolidate incremental chains.

## Scheduling

CLI:

```powershell
.\RC.cmd schedule plan --task-name daily --job-file F:\Jobs\daily.json --cli-path F:\Tools\RescueClone\publish\cli\rc.exe --frequency Daily --time 02:00 --run-missed
.\RC.cmd schedule register --task-name daily --job-file F:\Jobs\daily.json --cli-path F:\Tools\RescueClone\publish\cli\rc.exe --frequency Daily --time 02:00 --run-missed
.\RC.cmd schedule status --task-name daily
.\RC.cmd schedule run --task-name daily
.\RC.cmd schedule unregister --task-name daily
```

PowerShell equivalents are `Get-RCSchedulePlan`, `Register-RCSchedule`, `Get-RCScheduleStatus`, `Start-RCSchedule`, and `Unregister-RCSchedule`.

## Rescue Answer Files

```powershell
.\RC.cmd rescue answer-create --output F:\Rescue\answer.json --repository F:\Backups --image daily.rcimg --target-disk-id disk-fixture-1 --boot-mode Bios --target-disk-size-bytes 1048576 --verify-image
.\RC.cmd rescue answer-validate --file F:\Rescue\answer.json --verify-image
```

This creates and validates an unattended restore answer JSON. It does not build WinPE, ISO, USB, PXE, or boot media.

## Operations And Service IPC

Create an operation request JSON and run it locally:

```powershell
.\RC.cmd operation run --request F:\Ops\operation.json --log-directory F:\Ops\logs
```

Run the same request through a named-pipe service host:

```powershell
.\RC.cmd service serve --pipe rescueclone-local --log-directory F:\Ops\logs
.\RC.cmd service run-operation --pipe rescueclone-local --request F:\Ops\operation.json --log-directory F:\Ops\logs --timeout-ms 30000
```

Windows Service management commands can install, start, stop, query, set recovery policy, and uninstall the operation IPC host. The service host is not a privileged driver service.

## Storage And Safety

```powershell
.\RC.cmd storage volumes
.\RC.cmd storage disks
.\RC.cmd storage disk-safety --disk-number 1 --expected-fingerprint <sha256>
```

Disk safety checks are read-only and block risky targets. They do not perform destructive disk restore work.

## Install And Package

Build and verify:

```powershell
.\scripts\Build-Portable.ps1
.\scripts\Test-Portable.ps1
.\scripts\Test-PortableInstall.ps1
.\scripts\Test-PortableDependencyBoundary.ps1
.\scripts\New-PortablePackage.ps1 -OutputPath .\artifacts\RescueClone-portable.zip -Force
```

The package builder writes `RescueClone-portable.zip` and `RescueClone-portable.zip.sha256`.
