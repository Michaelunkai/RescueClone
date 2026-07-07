# RescueClone

RescueClone is a clean-room Windows backup/recovery suite foundation. The current verified slice supports directory image creation with selectable v1/v2 containers, verification, restore, JSON-defined directory backup job creation/update/deletion/export/import/status/runs, retention planning/enforcement for directory-image repositories, Windows Task Scheduler XML planning/register/unregister for calendar and event-triggered backup jobs, read-only restore planning, durable local operation execution, centralized backup log listing, read-only volume and disk inventory, read-only disk target safety checks, and a native C++ v2 block-planning boundary through the GUI, CLI, and PowerShell module.

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

The portable build writes dependency/cache material under this project folder instead of the user profile:

- `.dotnet-sdk` when `scripts\Install-FLocalDotNet.ps1` is run
- `.nuget-packages`
- `.dotnet-home`
- `.tmp`
- `native\bin`
- `publish\cli`
- `publish\gui`

Build-time note: run `scripts\Install-FLocalDotNet.ps1` once to seed `.dotnet-sdk` from the complete Codex-local .NET 10 SDK cache. After that, `scripts\Build-Portable.ps1` uses `.dotnet-sdk\dotnet.exe` and avoids `C:\Program Files\dotnet` for builds too.
