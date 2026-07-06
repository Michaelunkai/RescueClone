# RescueClone Status

This repository is a clean-room Windows backup suite foundation. It is not a Macrium Reflect clone and does not claim full Macrium Reflect parity.

The implemented, verified feature set is intentionally limited to one complete vertical slice:

- GUI, CLI, and PowerShell surfaces for directory image creation.
- GUI, CLI, and PowerShell surfaces for image verification.
- GUI, CLI, and PowerShell surfaces for directory restore.
- A shared engine so all three interfaces execute the same implementation.
- Compression, optional AES-256 encryption, and SHA-256 verification for every stored file.

The following requested capabilities are not implemented in this pass and are not represented as working: VSS disk imaging, partition restore, CBT kernel filter driver, boot repair, rescue ISO/USB/PXE, Hyper-V wipe/restore tests, image mounting driver, scheduler service, tamper-protection driver, installer, dynamic disk/Storage Spaces support, and Macrium-compatible UX parity.

The local machine has incomplete .NET SDK payloads under `C:\Program Files\dotnet`. This repo is retargeted to .NET 10 and seeds a project-local SDK from the complete Codex-local cache.

Portable dependency status:

- Normal CLI execution uses `publish\cli\rc.exe`.
- Normal GUI execution uses `publish\gui\RescueClone.App.exe`.
- Normal PowerShell module execution calls `publish\cli\rc.exe`.
- `scripts\Install-FLocalDotNet.ps1` can seed a project-local `.dotnet-sdk`.
- NuGet packages and .NET CLI home are redirected to project-local folders during `scripts\Build-Portable.ps1`.
- Temporary files are redirected to `.tmp` during build and portable tests.
- After `.dotnet-sdk` is seeded, normal build and runtime paths are project-local. The default seed source is still C on this host unless a different SDK root is passed.
