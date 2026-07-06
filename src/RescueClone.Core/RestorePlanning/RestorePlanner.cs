namespace RescueClone.Core.RestorePlanning;

public sealed class RestorePlanner
{
    private readonly ImageEngine _engine;

    public RestorePlanner(ImageEngine? engine = null)
    {
        _engine = engine ?? new ImageEngine();
    }

    public RestorePlanReport Plan(RestorePlanOptions options)
    {
        var blockers = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(options.TargetDiskId))
            blockers.Add("TargetDiskId is required.");
        if (options.TargetIsCurrentSystemDisk)
            blockers.Add("Target disk is marked as the current running system disk.");
        if (options.BootMode == RestoreBootMode.Unknown)
            blockers.Add("BootMode must be Bios or Uefi.");
        if (options.BootMode == RestoreBootMode.Uefi && !options.HasEfiSystemPartition)
            blockers.Add("UEFI restore requires an EFI System Partition.");
        if (string.IsNullOrWhiteSpace(options.BcdStorePath))
            blockers.Add("BcdStorePath is required for boot configuration validation.");
        else if (!File.Exists(options.BcdStorePath))
            warnings.Add($"BcdStorePath does not exist on this host: {options.BcdStorePath}");

        var imageReport = _engine.Verify(options.ImagePath, options.Password);
        var requiredBytes = options.RequiredBytes ?? imageReport.OriginalBytes;
        if (options.TargetDiskSizeBytes is null)
            warnings.Add("TargetDiskSizeBytes was not supplied; size fit cannot be proven.");
        else if (options.TargetDiskSizeBytes.Value < requiredBytes)
            blockers.Add($"Target disk is too small. RequiredBytes={requiredBytes}; TargetDiskSizeBytes={options.TargetDiskSizeBytes.Value}.");

        var steps = new[]
        {
            "Create disposable Hyper-V VM with a blank target disk matching TargetDiskId.",
            "Attach or copy the selected RescueClone image to the VM recovery environment.",
            "Run the restore plan against the disposable target disk.",
            "Validate BCD/EFI configuration after restore.",
            "Boot the VM once and record the boot result."
        };

        return new RestorePlanReport(
            options.ImagePath,
            options.TargetDiskId,
            blockers.Count == 0,
            blockers,
            warnings,
            imageReport.FileCount,
            imageReport.OriginalBytes,
            requiredBytes,
            options.BootMode,
            steps);
    }
}
