# RescueClone Status

This repository is a clean-room Windows backup suite foundation. It is not a Macrium Reflect clone and does not claim full Macrium Reflect parity.

The implemented, verified feature set is intentionally limited to one complete vertical slice:

- GUI, CLI, and PowerShell surfaces for directory image creation.
- GUI, CLI, and PowerShell surfaces for image verification.
- GUI, CLI, and PowerShell surfaces for repository integrity audit.
- GUI, CLI, and PowerShell surfaces for auditing and applying read-only protection to matching repository image files.
- GUI, CLI, and PowerShell surfaces for comparing verified image content against a source directory.
- GUI, CLI, and PowerShell surfaces for repository image listing with optional verification.
- GUI, CLI, and PowerShell surfaces for verified image content browsing.
- GUI, CLI, and PowerShell surfaces for selected file/folder extraction from verified images.
- GUI, CLI, and PowerShell surfaces for managed read-only user-mode image projection, listing, and unprojection.
- GUI, CLI, and PowerShell surfaces for directory restore.
- GUI, CLI, and PowerShell surfaces for creating a validated basic directory backup job JSON definition.
- GUI, CLI, and PowerShell surfaces for updating exposed fields in a directory backup job JSON definition.
- GUI, CLI, and PowerShell surfaces for deleting a backup job JSON definition.
- GUI, CLI, and PowerShell surfaces for exporting and importing a validated backup job JSON definition.
- GUI, CLI, and PowerShell surfaces for listing backup job JSON definitions in a directory, including malformed job files as structured invalid entries.
- GUI, CLI, and PowerShell surfaces for backup job status, including validation, destination repository audit, and the latest parsed run log when present.
- GUI, CLI, and PowerShell surfaces for validating and running a directory backup job JSON definition.
- GUI, CLI, and PowerShell surfaces for count/age/free-space retention planning and enforcement on directory-image repositories.
- GUI, CLI, and PowerShell surfaces for GFS-style daily/weekly/monthly retention planning and enforcement on directory-image repositories.
- GUI, CLI, and PowerShell surfaces for Windows Task Scheduler XML planning, register, status, run-now, and unregister.
- GUI, CLI, and PowerShell surfaces for read-only restore planning with boot/target safety blockers.
- GUI, CLI, and PowerShell surfaces for unattended rescue answer-file creation and validation.
- GUI, CLI, and PowerShell surfaces for durable local operation execution from a JSON request.
- GUI, CLI, and PowerShell surfaces for sending operation requests through a named-pipe service IPC host.
- GUI, CLI, and PowerShell surfaces for Windows Service install planning, install, status, start, stop, and uninstall of the operation IPC host.
- GUI, CLI, and PowerShell surfaces for Windows Service restart-on-failure recovery policy configuration and query.
- GUI, CLI, and PowerShell surfaces for centralized backup log listing from structured JSON reports.
- GUI, CLI, and PowerShell surfaces for read-only volume and disk inventory.
- GUI, CLI, and PowerShell surfaces for native engine status.
- A shared engine so all three interfaces execute the same implementation.
- A native C++ DLL owns v2 block layout planning behind a plain C ABI and is called by the managed image writer.
- Local operation runs record operation ID, kind, state, timestamps, result JSON, error text, and an optional report file path.
- Local operation runs with a log directory also write a readable recovery-state JSON sidecar containing the original request and final report for both success and failure paths.
- Local operation reports include structured audit events for operation start, success, and failure.
- Failed local operation reports include structured `errorDetail` fields with a stable code, original message, and exception type.
- Local operation runs can dispatch implemented image create/verify/repository-audit/repository-protection/source-compare/repository-list/browse/extract/project/list/unproject/restore operations, backup job create/update/delete/export/import/list/status/validate/run operations, count/age/free-space and GFS retention plan/apply operations, read-only restore planning, and rescue answer create/validate operations.
- Named-pipe service IPC can host the operation runner and return structured operation reports to CLI, PowerShell, and GUI clients.
- Windows Service registration installs `rc service host` as the same operation runner behind SCM start/stop/status/uninstall commands.
- Windows Service recovery policy can configure and query SCM restart-on-failure actions for the operation host.
- Compression, optional AES-256 encryption, and SHA-256 verification for every stored file.
- Directory images can be written as legacy v1 sequential containers or v2 indexed block containers; verify, browse, extract, and restore read both formats.
- Repository image listing returns image paths, sizes, timestamps, and optional verified format/file-count/root-hash metadata; repository audit verifies all matching images and returns verified/failed counts.
- Repository protection audit reports read-only state for matching image files; protection apply marks unprotected matching images read-only and reports per-file changes.
- Image-to-source comparison verifies the image, hashes source files, and reports missing, changed, and extra files with structured counts and differences.
- Backup job status includes a repository audit for the configured image destination once that destination directory exists.
- Managed user-mode image projection restores verified image content into a manifest-marked directory, marks projected files read-only, lists projection manifests under a selected root, and unprojects only manifest-listed files.
- V2 directory images include block offsets, per-block SHA-256 hashes, file hashes, a root manifest hash, and a fixed footer that points to the manifest.
- Completed directory image files are marked read-only after creation; managed retention can clear that bit only for files selected by its deletion plan.
- Volume inventory reports drive root, readiness, drive type, file system, label, total/free bytes, and whether the root matches the running Windows system root.
- Disk inventory reports disk number, friendly name, serial number, partition style, bus type, size, boot/system flags, offline state, and read-only state using built-in Windows storage cmdlets in read-only mode.
- Disk target safety checks fingerprint selected disks and block missing, fingerprint-mismatched, boot/system, offline, and read-only targets before destructive restore work is enabled.
- Job runs can perform post-create verification and write a structured JSON run log.
- Job runs can execute configured pre-backup and post-backup script hooks non-interactively, fail the job on nonzero hook exit or timeout, and include hook exit/output records in the structured JSON run log.
- Job runs write paired machine-readable JSON and human-readable HTML reports and can rotate old reports by per-job keep count.
- Backup log listing reads structured job report directories and returns parsed report metadata plus structured parse errors for malformed log files.
- Job runs can publish Windows Application Event Log notifications for success and runtime failure through the built-in Windows event creation tool; success notification results are recorded in JSON and HTML reports.
- Job runs can publish success and runtime-failure email notifications through SMTP or a configured pickup directory; success notification results are recorded in JSON and HTML reports.
- Job runs can retry transient create/verify failures using per-job retry count and retry delay settings, and record retry attempts in JSON and HTML reports.
- Job runs record an automatic image-to-source comparison in JSON and HTML reports after successful image creation.
- Job runs can optionally restore-test the created image to a configured target directory and record the restore-test result in JSON and HTML reports.
- Job runs can apply count, age, and free-space retention after a successful backup while excluding the image just created; retention results are recorded in JSON and HTML reports.
- Retention can select and delete `*.rcimg` files by newest-file keep count, max age, free-space threshold, and GFS-style newest daily/weekly/monthly buckets; it does not yet perform incremental-chain consolidation.
- Scheduler registration writes Windows Task Scheduler tasks that run `rc job run --file <job>`, supports daily, weekly, monthly calendar triggers, event-log triggers, maps missed runs to Task Scheduler `StartWhenAvailable`, and supports status/run-now management.
- Restore planning verifies the selected image and reports blockers without writing disks, repairing boot files, or changing EFI/BCD state.
- Rescue answer files record repository, image, target disk, boot mode, BCD path, driver directories, network shares, boot repair, and reboot policy, then validate using the same restore planner. They are not bootable media.

The following requested capabilities are not implemented in this pass and are not represented as working: VSS disk imaging, partition restore, CBT kernel filter driver, boot repair, rescue ISO/USB/PXE, Hyper-V wipe/restore tests, signed kernel image mounting driver, privileged driver service, scheduler service, GFS chain consolidation, tamper-protection driver, MSI/EXE installer, dynamic disk/Storage Spaces support, and Macrium-compatible UX parity.

The local machine has incomplete .NET SDK payloads under `C:\Program Files\dotnet`. This repo is retargeted to .NET 10 and seeds a project-local SDK from the complete Codex-local cache.

Portable dependency status:

- Normal CLI execution uses `publish\cli\rc.exe`.
- Normal GUI execution uses `publish\gui\RescueClone.App.exe`.
- Normal PowerShell module execution calls `publish\cli\rc.exe`.
- `scripts\Install-FLocalDotNet.ps1` can seed a project-local `.dotnet-sdk`.
- NuGet packages and .NET CLI home are redirected to project-local folders during `scripts\Build-Portable.ps1`.
- `scripts\Build-Portable.ps1` requires `.dotnet-sdk\dotnet.exe` by default and only uses machine `dotnet` when `-AllowSystemDotNetFallback` is passed explicitly.
- `scripts\Test-PortableDependencyBoundary.ps1` verifies the published CLI service loads no non-Windows modules from `C:\`.
- `scripts\Install-RescueClone.ps1` and `scripts\Uninstall-RescueClone.ps1` provide quiet/no-restart portable install and uninstall flows for the published CLI, GUI, PowerShell module, and docs.
- `scripts\New-PortablePackage.ps1` builds a portable ZIP package from published outputs and install scripts.
- Temporary files are redirected to `.tmp` during build and portable tests.
- Native C++ output is built to `native\bin\RescueClone.Native.dll` by `scripts\Build-Native.ps1` using an F-local MinGW compiler when available.
- Disk inventory calls built-in Windows PowerShell and `Get-Disk`; this is an operating-system dependency and does not install new C-drive tooling.
- After `.dotnet-sdk` is seeded, normal build and runtime paths are project-local. The default seed source is still C on this host unless a different SDK root is passed.
