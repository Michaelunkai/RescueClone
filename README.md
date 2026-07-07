# RescueClone

RescueClone is a clean-room Windows backup/recovery suite foundation. The current verified slice supports directory image creation with selectable v1/v2 containers, verification, image-to-source comparison, repository image listing and integrity audit with optional verification, image content browsing, selected file/folder extraction, managed read-only user-mode image projection/listing/unprojection, restore, JSON-defined directory backup job creation/update/deletion/export/import/status/runs, count/age/free-space and GFS-style retention planning/enforcement for directory-image repositories, Windows Task Scheduler XML planning/register/status/run/unregister for calendar and event-triggered backup jobs, read-only restore planning, unattended rescue answer-file creation/validation, durable local operation execution, named-pipe service IPC operation execution, Windows Service registration and recovery policy for the operation IPC host, centralized backup log listing, read-only volume and disk inventory, read-only disk target safety checks, and a native C++ v2 block-planning boundary through the GUI, CLI, and PowerShell module.

This is not a Macrium Reflect clone. It does not currently implement VSS disk imaging, boot repair, kernel drivers, image mounting, rescue media, scheduler services, or bare-metal restore.

The full remaining engineering contract is tracked in `docs\REMAINING_CONTRACT.md`.

## Portable Build

```powershell
.\scripts\Build-Portable.ps1
.\scripts\Test-Portable.ps1
```

Published runtime entry points:

- CLI: `.\RC.cmd`
- GUI: `.\RUN-GUI.cmd`
- PowerShell: `Import-Module .\powershell\RescueClone\RescueClone.psd1 -Force`
- Portable ZIP package: `.\scripts\New-PortablePackage.ps1 -OutputPath .\artifacts\RescueClone-portable.zip -Force`
- Quiet portable install: `.\scripts\Install-RescueClone.ps1 -InstallRoot F:\Tools\RescueClone -Quiet -NoRestart -Force`
- Quiet portable uninstall: `.\scripts\Uninstall-RescueClone.ps1 -InstallRoot F:\Tools\RescueClone -Quiet -NoRestart`

The portable build writes dependency/cache material under this project folder instead of the user profile:

- `.dotnet-sdk` when `scripts\Install-FLocalDotNet.ps1` is run
- `.nuget-packages`
- `.dotnet-home`
- `.tmp`
- `native\bin`
- `publish\cli`
- `publish\gui`

Build-time note: run `scripts\Install-FLocalDotNet.ps1` once to seed `.dotnet-sdk` from the complete Codex-local .NET 10 SDK cache. After that, `scripts\Build-Portable.ps1` uses `.dotnet-sdk\dotnet.exe` and avoids `C:\Program Files\dotnet` for builds too.
The build fails if `.dotnet-sdk\dotnet.exe` is missing unless `-AllowSystemDotNetFallback` is passed explicitly. Use `scripts\Test-PortableDependencyBoundary.ps1` after publishing to prove the normal CLI service and GUI load project-local RescueClone modules plus Windows system DLLs only.
