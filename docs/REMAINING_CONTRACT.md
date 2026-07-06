# Remaining Contract

This document is the remaining acceptance contract for the original Macrium-class request. RescueClone currently implements only the verified directory image create, verify, and restore slice described in `docs/STATUS.md` and `docs/PARITY.md`.

The items below are not claims of current functionality. They are the work required before RescueClone can honestly be described as a production Windows disk imaging, backup, and recovery suite with GUI, CLI, and PowerShell parity.

## Non-Negotiable Product Rules

- Every capability must be implemented in the shared core or service layer first, then exposed through GUI, CLI, and PowerShell.
- A feature is not complete until automated tests prove the GUI, CLI, and PowerShell routes call the same implementation and produce equivalent results.
- Every destructive operation must have explicit target selection, dry-run support where practical, structured logging, and automated post-operation verification.
- Every unattended operation must support zero prompts and deterministic exit codes.
- Normal build and runtime paths must remain F-local or project-local where practical. Any unavoidable Windows system dependency must be documented with the exact reason and minimum scope.
- No copied Macrium source, binaries, UI assets, file formats, branding, or reverse engineered proprietary internals may be used.

## Phase 1: Engine Architecture And Safety Base

Build the service-grade foundation before raw disk writes are enabled.

Required work:

- Define a stable core engine API for disks, volumes, images, jobs, schedules, retention, rescue media, logs, and restore plans.
- Add a privileged Windows service for backup, restore orchestration, scheduler coordination, device enumeration, and operation locking.
- Add service authentication and a local IPC contract used by GUI, CLI, and PowerShell.
- Add a transaction model for backup and restore operations with resumable state files.
- Add a central feature catalog that blocks a feature from reporting implemented unless GUI, CLI, and PowerShell mappings exist.
- Add safety interlocks for destructive disk operations, including target disk fingerprinting and boot/system disk detection.
- Add structured error taxonomy, retry policy, cancellation, progress, and audit events.

Acceptance proof:

- Unit tests cover the shared operation model, feature catalog parity enforcement, retry/cancellation behavior, and disk target safety checks.
- Integration tests prove GUI, CLI, and PowerShell invoke the same service operations for each implemented feature.
- A failed operation leaves a readable recovery state and structured JSON log.

## Phase 2: Image Container Format

Replace the current directory archive format with a disk-image-capable container while keeping backward-compatible handling for the current slice if desired.

Required work:

- Define an original `.rcimg` container specification with header, metadata, block map, stream table, compression/encryption flags, integrity records, and versioning.
- Support full, differential, and incremental chains.
- Support block-level SHA-256 integrity records and whole-image verification.
- Support AES-256 encryption with authenticated metadata and secure key derivation.
- Support compression levels none, medium, and high.
- Support multi-volume spanning and configurable split sizes.
- Support chain consolidation and synthetic full generation.
- Support interrupted write recovery and atomic finalization.

Acceptance proof:

- Format specification is documented.
- Parser rejects corrupt, truncated, tampered, wrong-password, and unsupported-version images.
- Tests verify cross-interface create, verify, restore, split, consolidate, and encrypted restore flows.

## Phase 3: Disk And Volume Imaging

Implement actual Windows disk and partition imaging.

Required work:

- Enumerate physical disks, partitions, GPT/MBR layouts, volume GUIDs, file systems, BitLocker state, dynamic disks, and Storage Spaces.
- Use VSS for live system volume snapshots and open-file-safe backup.
- Capture partition tables, EFI System Partition, MSR, recovery partitions, boot volumes, and data volumes.
- Support NTFS, ReFS, FAT32, and exFAT at the block level.
- Support dynamic disks and Storage Spaces where Windows exposes safe snapshot/read semantics.
- Support full image backup for whole disk, selected partitions, and selected volumes.
- Implement differential and incremental image creation.
- Add robust changed-block tracking using a signed kernel-mode filter driver, or a documented USN/bitmap fallback with clear limitations.

Acceptance proof:

- Hyper-V integration tests create Windows 10 and Windows 11 VMs, back up live system disks, and verify image consistency.
- Tests cover GPT, MBR, NTFS, ReFS, FAT32, exFAT, external USB destinations, and network destinations.
- Incremental tests prove unchanged blocks are not rescanned when CBT is active.

## Phase 4: Retention, Scheduling, And Automation

Implement unattended backup operations.

Required work:

- Add backup job definitions with source selection, destination, compression, encryption, schedule, retention, scripts, notifications, and verification policy.
- Add Windows Task Scheduler integration for daily, weekly, monthly, event-triggered, and missed-on-boot runs.
- Add Grandfather-Father-Son retention.
- Add retention by count, age, and destination free-space thresholds.
- Add automatic chain consolidation.
- Add automatic destination space management for local, external, USB, and network folders.
- Add pre-backup and post-backup script hooks with timeout and exit-code policy.
- Add email notifications and Windows Event Log notifications.
- Add per-job HTML and JSON reports with rotation.

Acceptance proof:

- One CLI command and one PowerShell command can create, schedule, and run a complete unattended backup job.
- The GUI creates the same job model and produces equivalent output.
- Integration tests simulate low destination space and prove retention prunes the correct chains.
- Missed-backup-on-boot tests prove a missed run is detected and executed.

## Phase 5: Restore In Windows

Implement restore flows from within Windows.

Required work:

- Restore entire disks, selected partitions, selected volumes, and selected files/folders.
- Support restoring to same-size, larger, and smaller target disks when the used data fits.
- Recreate GPT/MBR layouts and resize partitions safely.
- Restore EFI, MSR, recovery, boot, and data partitions.
- Repair BCD and EFI boot configuration after system restore.
- Validate boot configuration after restore, not just copied bytes.
- Implement dissimilar hardware restore with storage/HAL/boot-critical driver injection.
- Add restore plan preview and unattended restore plan execution.

Acceptance proof:

- One CLI command and one PowerShell command can perform an unattended restore to a selected target disk with no prompts.
- GUI restore wizard produces the same restore plan and result.
- Hyper-V tests wipe a VM disk, restore from image, and verify the VM boots.
- Driver-injection tests restore to a VM with different storage controller configuration and verify boot.

## Phase 6: Rescue Media

Implement bootable recovery environments.

Required work:

- Build a WinPE-based rescue environment.
- Include a rescue GUI restore wizard, disk management view, clone workflow, image browser, command prompt, and diagnostics launcher.
- Include network restore over SMB and iSCSI.
- Include driver detection and injection for network, storage, NVMe, RAID, USB3, and video drivers.
- Support custom driver folders.
- Output bootable ISO files.
- Write bootable USB drives directly.
- Produce PXE boot artifacts.
- Support unattended restore answer files that specify repository, restore point, target disk, driver policy, boot repair, and reboot policy.
- Support one command that builds rescue media, writes USB, and configures unattended restore.

Acceptance proof:

- Hyper-V tests boot the ISO, run unattended restore from image, reboot, and verify the restored OS boots.
- USB writer tests operate only against disposable test media and verify boot sector/layout.
- PXE artifact tests verify boot files and WinPE startup command configuration.
- GUI, CLI, and PowerShell media creation paths produce equivalent artifacts from the same configuration.

## Phase 7: Cloning

Implement disk-to-disk and partition-to-partition cloning.

Required work:

- Clone entire disks and selected partitions.
- Support resizing to larger or smaller disks where data fits.
- Support live clone with VSS when cloning a running system disk.
- Repair cloned boot configuration.
- Support unattended clone commands.

Acceptance proof:

- Hyper-V tests clone a boot disk to a blank disk and boot from the clone.
- GUI, CLI, and PowerShell clone operations share the same plan and result.

## Phase 8: Mounting And File Browser

Implement browse and mount behavior.

Required work:

- Add read-only image browsing from the GUI, CLI, and PowerShell.
- Add file/folder extraction from images.
- Add a signed Windows image mount driver or safe user-mode projection layer.
- Support mounting selected restore points from full, differential, and incremental chains.
- Add mount manager with unmount, stale mount cleanup, and access control.

Acceptance proof:

- Tests mount or project images, read files, verify hashes, unmount cleanly, and recover after forced process termination.
- PowerShell returns structured objects for mounted images and files.

## Phase 9: Tamper Protection

Implement image protection after backup.

Required work:

- Add ACL enforcement for backup repositories.
- Add optional driver-level write protection for image files.
- Add repository lock/unlock policy for service-only writes.
- Add audit events for unauthorized access attempts.
- Add controlled retention deletion path through the service.

Acceptance proof:

- Tests prove normal user processes cannot alter protected images.
- Service retention can remove only policy-approved image chains.
- Unauthorized tamper attempts are logged and surfaced.

## Phase 10: GUI Application

Expand the WPF shell into a complete desktop product.

Required work:

- Dashboard with disks, jobs, recent backups, health, and alerts.
- Backup wizard for disk, partition, volume, and file selections.
- Restore wizard for files, partitions, disks, and system recovery.
- Clone wizard.
- Rescue media wizard.
- Scheduler view.
- Image mount manager.
- Logs and event viewer.
- Settings for defaults, notifications, security, scripting, retention, and rescue media.
- System tray integration with progress and notifications.
- Accessibility, keyboard navigation, DPI scaling, and localization-ready resources.

Acceptance proof:

- UI automation tests cover every workflow that CLI and PowerShell expose.
- Screens and controls bind to shared service commands, not separate business logic.
- GUI workflow outputs match CLI and PowerShell outputs for identical job definitions.

## Phase 11: CLI

Expand the CLI into a complete scriptable surface.

Required work:

- Commands for job create, edit, delete, run, cancel, pause, resume, export, and import.
- Commands for disk image create, verify, restore, clone, browse, mount, unmount, and extract.
- Commands for rescue media build, ISO output, USB writing, PXE artifacts, and unattended answer files.
- Commands for schedules, retention, logs, status, repository management, notifications, and settings.
- JSON output for automation.
- Stable exit codes.
- Non-interactive mode for all unattended operations.

Acceptance proof:

- CLI contract tests cover all commands, exit codes, JSON schemas, and non-interactive behavior.
- Every implemented GUI feature has a CLI equivalent.

## Phase 12: PowerShell Module

Expand the module into a complete administrative surface.

Required work:

- Cmdlets for every CLI operation using approved verb names where practical.
- Structured objects, not plain text.
- Pipeline support for images, jobs, disks, volumes, restore points, schedules, logs, and mounted images.
- Parameter validation.
- `-WhatIf` and `-Confirm` for destructive operations.
- Comment-based help and examples.

Acceptance proof:

- Pester tests cover every cmdlet and prove object output shape.
- Parity tests compare PowerShell, CLI, and GUI feature catalog coverage.

## Phase 13: Installer And Deployment

Implement production installation.

Required work:

- MSI or EXE installer.
- Silent install with `/quiet /norestart`.
- Service installation and recovery policy.
- PowerShell module installation.
- Driver installation for CBT, mounting, and tamper protection.
- Test-signing flow for development drivers.
- Production signing documentation for drivers and installers.
- Upgrade, repair, and uninstall paths.
- Dependency policy that minimizes C-drive dependencies and documents unavoidable Windows locations.

Acceptance proof:

- Clean Windows 10 and Windows 11 VM install tests pass.
- Silent install and uninstall tests pass.
- Service and drivers are installed, running, and removable.

## Phase 14: Automated Test Lab

Build the proof system required for product claims.

Required work:

- Unit tests for all core components.
- Pester tests for the PowerShell module.
- CLI contract tests.
- WPF UI automation tests.
- Hyper-V VM orchestration.
- Windows Sandbox smoke tests where useful.
- Real backup to wipe to restore to boot verification loops.
- Rescue ISO unattended restore tests.
- Disk matrix tests for GPT, MBR, file systems, dynamic disks, Storage Spaces, smaller/larger target restore, and dissimilar storage controllers.
- Fault injection for dropped network paths, USB removal, corrupted images, low disk space, wrong password, service restart, and cancellation.

Acceptance proof:

- CI runs fast unit and contract tests.
- Local gated test suite runs Hyper-V destructive tests only on disposable VMs and disks.
- Reports include machine-readable JSON results and human-readable summaries.

## Phase 15: Documentation

Document the finished product and the engineering boundaries.

Required work:

- Architecture guide.
- Image format specification.
- Service and IPC specification.
- Driver build, signing, and install guide.
- CLI reference.
- PowerShell reference.
- GUI user guide.
- Rescue media guide.
- Backup and restore runbooks.
- Disaster recovery checklist.
- Security model and threat analysis.
- Dependency and portability guide.

Acceptance proof:

- Documentation examples are executed in tests where possible.
- Each feature page names the GUI, CLI, and PowerShell route.

## Release Gate

RescueClone may only claim production parity after all of the following are true:

- The feature catalog marks every original requested capability implemented.
- GUI, CLI, and PowerShell parity tests pass for every capability.
- Hyper-V backup, wipe, restore, and boot tests pass for Windows 10 and Windows 11.
- Rescue ISO unattended restore tests pass from boot to restored OS.
- Driver signing, installer, service install, silent install, upgrade, repair, and uninstall tests pass.
- Security and tamper-protection tests pass.
- Documentation includes exact build, install, use, restore, rescue, and recovery steps.
- No generated report claims Macrium compatibility or uses Macrium assets or code.

