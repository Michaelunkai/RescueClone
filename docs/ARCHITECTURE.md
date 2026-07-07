# Architecture

RescueClone is organized around one shared implementation core and three user surfaces:

- GUI: `src\RescueClone.App`
- CLI: `src\RescueClone.Cli`
- PowerShell: `powershell\RescueClone`

The current product slice is a clean-room, directory-image backup and recovery suite foundation. It deliberately documents implemented behavior only. It is not a Macrium Reflect clone and it does not currently include VSS disk imaging, kernel drivers, boot repair, WinPE media creation, PXE boot, or bare-metal restore.

## Source Layout

| Path | Responsibility |
| --- | --- |
| `src\RescueClone.Core` | Shared business logic, image format, operation dispatch, scheduling, retention, restore planning, storage inventory, logs, and service helpers. |
| `src\RescueClone.Cli` | Console command parser and JSON/text output for all implemented features. |
| `src\RescueClone.App` | WPF application with tabs mapped to the same implemented features as CLI and PowerShell. |
| `powershell\RescueClone` | PowerShell module exposing structured cmdlets over the same core behavior. |
| `native\RescueClone.Native` | Clean C++ native boundary used by the v2 container block planner and native diagnostics. |
| `tests\RescueClone.Tests` | Unit and integration-style tests for core behavior, operation dispatch, service helpers, image round trips, scheduling, retention, restore planning, and storage safety. |
| `scripts` | Portable build, package, install, uninstall, smoke, and dependency-boundary verification scripts. |
| `docs` | Operator, developer, parity, build, status, and remaining engineering contract documentation. |

## Shared Core Rule

All meaningful behavior must live in `RescueClone.Core` first. GUI, CLI, and PowerShell are adapters over that core, not separate implementations.

This rule keeps interface parity testable:

1. Add or change the core behavior.
2. Add the CLI command that calls the core behavior.
3. Add the PowerShell cmdlet that calls the same behavior and returns structured objects.
4. Add the GUI control path that calls the same behavior.
5. Add the feature to `FeatureCatalog`.
6. Add or update tests that prove the feature is present across all three user surfaces.
7. Update `docs\PARITY.md`, `docs\BUILD.md`, and `docs\USER_GUIDE.md` when command names or usage change.

No implemented feature should be reachable from only one interface.

## Feature Catalog

`src\RescueClone.Core\FeatureCatalog.cs` is the source of truth for implemented parity. Each `FeatureSurface` entry contains:

- stable feature id
- GUI location
- CLI command
- PowerShell command
- implemented flag

The PowerShell module exposes this catalog through `Get-RCFeature`. The GUI displays the same catalog on the dashboard. The portable smoke test imports the module and checks the published catalog count after CLI image round-trip verification.

## Image Engine

`ImageEngine` owns the RescueClone image container behavior.

Version 1 containers use:

- `RCIMG1` magic header
- JSON header records
- per-file payload records
- per-file SHA-256 validation

Version 2 containers use:

- `RCIMG2` magic header
- fixed-size block planning
- per-block SHA-256 validation
- manifest and footer records
- native C++ block planner boundary through `NativeBlockPlanner`

Both container versions support selectable compression and password-based payload protection. Image creation calls `PrepareImageForWrite` before writing and `ProtectImage` after writing so generated images are read-only by default. Repository protection commands can audit and apply that read-only state across existing image files.

## Backup Jobs

Backup jobs are JSON definitions handled by `Jobs\BackupJobRunner`.

The runner supports:

- create, update, delete, export, and import
- validation and status inspection
- directory listing
- per-job history from structured log files
- execution with optional pre/post script hooks
- retry policy
- verify-after-create
- restore-test-after-create to a directory target
- retention enforcement after backup
- Windows Event Log and email pickup or SMTP notification fields

Job execution still uses the directory-image engine. It does not capture live system volumes or disks.

## Retention

`Retention\RetentionManager` handles repository pruning plans and apply operations.

Supported modes:

- keep by newest count
- keep by maximum age
- prune until minimum free space is available
- GFS-style daily, weekly, and monthly bucket selection

Apply operations delete only files selected by the generated plan. GFS retention is flat repository pruning for standalone image files, not incremental-chain consolidation.

## Scheduling

`Scheduling\ScheduleManager` builds and registers Windows Task Scheduler XML for backup job execution.

Supported scheduling surfaces include:

- plan XML without registering
- register task
- status
- run now
- unregister
- daily, weekly, monthly, and event-triggered definitions
- run-missed flag in generated task settings

Task registration uses the operating system scheduler. The repository also has a service IPC foundation, but the scheduler path does not require the RescueClone service to be installed.

## Restore Planning

`RestorePlanning\RestorePlanner` is a read-only planner. It evaluates a requested image and target description and returns blockers or warnings before destructive work exists.

The current planner validates:

- image verification
- target disk id presence
- requested target size
- required byte count
- current-system-disk flag
- boot mode and boot configuration inputs
- EFI expectations

It does not repartition disks, write boot files, inject drivers, or perform bare-metal restore.

## Rescue Answer Files

`Rescue\RescueAnswerManager` creates and validates versioned unattended restore answer JSON.

The answer file records:

- repository path
- image path and password state
- target disk id and size
- required bytes
- boot mode
- BCD store path
- driver directories
- network shares
- image verification choice
- boot repair and reboot policy flags

Validation reuses image verification and restore-planner blockers. The current answer-file feature is a contract artifact for unattended restore orchestration; it is not ISO, USB, PXE, or WinPE media creation.

## Operations And Service IPC

`Operations\OperationRunner` dispatches durable JSON operation requests by kind. It writes structured operation reports and recovery state sidecars when a log directory is supplied.

`OperationKindCatalog` lists the supported durable request kinds, descriptions, required parameters, and optional parameters. It is exposed through the GUI Operations tab, CLI `rc operation kinds`, and PowerShell `Get-RCOperationKind`.
The same catalog validates operation request JSON before execution through the GUI `Validate Request` action, CLI `rc operation validate`, and PowerShell `Test-RCOperation`.
`OperationRunner` also validates requests before dispatch and records malformed requests as structured failed operation reports.

Operation reports include:

- operation id
- operation kind
- state
- started and finished timestamps
- result payload
- error message
- classified error detail
- audit events
- report path
- recovery state path

`Operations\OperationPipeService` hosts the same runner over a Windows named pipe. CLI, PowerShell, and GUI service-operation paths send the same JSON request shape to the pipe and receive the same structured report.

`Services\WindowsServiceManager` registers the CLI service host with the Windows Service Control Manager and manages status, start, stop, recovery policy, and uninstall operations. This is a user-mode service host foundation, not a privileged kernel driver service.

## Storage And Safety

Storage inventory is read-only.

- `Storage\VolumeInventory` lists logical volumes.
- `Storage\DiskInventory` uses the built-in Windows storage cmdlets in read-only mode.
- `Storage\DiskSafety` fingerprints disk identity from disk number, friendly name, serial number, partition style, bus type, and size.

Disk safety blocks unsafe target use when the disk is missing, fingerprint-mismatched, boot/system, offline, or read-only. Current restore execution targets directories only; disk safety exists to support explicit planning and later destructive-disk gates.

## Logging

Backup and operation logging use structured JSON artifacts.

- Backup logs are listed through `Logs\BackupLogCatalog`.
- Operation logs include report JSON plus recovery state JSON.
- Job history reads matching backup logs for the selected job.

The GUI log view, CLI `rc logs list`, and PowerShell `Get-RCLog` all read the same log catalog.

## Portable Runtime Boundary

Normal use is designed to run from the project or installed F-drive location with no non-Windows dependencies loaded from `C:\`.

Portable build and runtime material is contained under the repository:

- `.dotnet-sdk`
- `.nuget-packages`
- `.dotnet-home`
- `.tmp`
- `native\bin`
- `publish\cli`
- `publish\gui`

`scripts\Test-PortableDependencyBoundary.ps1` launches the published CLI service and GUI, then fails if either loads modules outside the project root and `%WINDIR%`, or if either loads non-Windows modules from `C:\`.

The only expected `C:\` runtime modules are Windows operating system DLLs from `%WINDIR%`.

## Build And Package Flow

Preferred local flow:

```powershell
.\scripts\Install-FLocalDotNet.ps1
.\scripts\Build-Portable.ps1
.\scripts\Test-Portable.ps1
.\scripts\Test-PortableInstall.ps1
.\scripts\Test-PortableDependencyBoundary.ps1
.\scripts\New-PortablePackage.ps1 -OutputPath .\artifacts\RescueClone-portable.zip -Force
```

CI runs the same portable build shape on Windows with an explicit hosted MinGW compiler path for the native boundary. The package script writes both ZIP and SHA-256 sidecar files.

## Verification Layers

The project uses layered verification:

| Layer | Command or location | Purpose |
| --- | --- | --- |
| Core tests | `dotnet test RescueClone.sln -c Release` | Validates core behavior and dispatch paths. |
| Portable smoke | `scripts\Test-Portable.ps1` | Uses the published CLI and PowerShell module for image create, verify, restore, and feature catalog proof. |
| Install smoke | `scripts\Test-PortableInstall.ps1` | Installs to a disposable root, verifies installed CLI and PowerShell feature catalog access, then uninstalls. |
| Dependency boundary | `scripts\Test-PortableDependencyBoundary.ps1` | Proves normal CLI service and GUI module loads stay inside project root and `%WINDIR%`. |
| Package verification | `scripts\New-PortablePackage.ps1` | Builds ZIP, SHA-256 sidecar, and validates required entries. |
| Parity catalog | `FeatureCatalog` and `docs\PARITY.md` | Ensures documented implemented features expose GUI, CLI, and PowerShell surfaces. |

## Current Boundaries

The following capabilities remain outside the implemented product slice:

- VSS disk or partition imaging
- changed-block tracking driver
- kernel image mount driver
- driver-level tamper protection
- signed Windows kernel drivers
- live system-volume restore
- partition resizing
- boot repair
- dissimilar hardware redeploy
- WinPE rescue environment
- bootable ISO, USB, or PXE image creation
- Hyper-V wipe and bare-metal restore round trip
- MSI or EXE installer

Those boundaries are intentional engineering limits of the current repository state. The detailed remaining contract is maintained in `docs\REMAINING_CONTRACT.md`.
