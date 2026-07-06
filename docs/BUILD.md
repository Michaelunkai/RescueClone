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

Direct SDK build commands are still available for development:

```powershell
dotnet restore RescueClone.sln --packages .\.nuget-packages
dotnet build RescueClone.sln -c Release
dotnet test RescueClone.sln -c Release
```

CLI examples:

```powershell
.\RC.cmd image create --source .\sample-source --image .\sample.rcimg --compression High --password secret
.\RC.cmd image verify --image .\sample.rcimg --password secret
.\RC.cmd image restore --image .\sample.rcimg --target .\sample-restore --password secret
```

PowerShell examples:

```powershell
Import-Module .\powershell\RescueClone\RescueClone.psd1 -Force
New-RCImage -SourcePath .\sample-source -ImagePath .\sample.rcimg -Compression High -Password secret -Confirm:$false
Test-RCImage -ImagePath .\sample.rcimg -Password secret
Restore-RCImage -ImagePath .\sample.rcimg -TargetPath .\sample-restore -Password secret -Confirm:$false
```

Dependency note: normal CLI, GUI, and PowerShell use the self-contained binaries in `publish`. After `scripts\Install-FLocalDotNet.ps1`, build commands use `.dotnet-sdk\dotnet.exe` from the project folder. The default seed source is the Codex-local SDK cache under `C:\Users\micha\.codex\tools\dotnet-sdk-10.0.301`; pass `-SourceDotNetRoot` to seed from a different drive.
