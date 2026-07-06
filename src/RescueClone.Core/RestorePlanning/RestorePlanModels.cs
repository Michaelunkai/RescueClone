namespace RescueClone.Core.RestorePlanning;

public enum RestoreBootMode
{
    Unknown,
    Bios,
    Uefi
}

public sealed record RestorePlanOptions(
    string ImagePath,
    string? Password,
    string TargetDiskId,
    long? TargetDiskSizeBytes,
    long? RequiredBytes,
    bool TargetIsCurrentSystemDisk,
    RestoreBootMode BootMode,
    bool HasEfiSystemPartition,
    string? BcdStorePath);

public sealed record RestorePlanReport(
    string ImagePath,
    string TargetDiskId,
    bool CanProceed,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    int ImageFileCount,
    long ImageOriginalBytes,
    long RequiredBytes,
    RestoreBootMode BootMode,
    IReadOnlyList<string> HyperVBootProofSteps);
