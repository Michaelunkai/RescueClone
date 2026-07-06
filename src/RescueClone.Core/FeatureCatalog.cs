namespace RescueClone.Core;

public sealed record FeatureSurface(string FeatureId, string Gui, string Cli, string PowerShell, bool Implemented);

public static class FeatureCatalog
{
    public static IReadOnlyList<FeatureSurface> All { get; } = new List<FeatureSurface>
    {
        new("image.create.directory", "Create Image", "rc image create", "New-RCImage", true),
        new("image.verify", "Verify Image", "rc image verify", "Test-RCImage", true),
        new("image.restore.directory", "Restore Image", "rc image restore", "Restore-RCImage", true),
        new("job.backup.directory.validate", "Backup Job", "rc job validate", "Test-RCBackupJob", true),
        new("job.backup.directory.run", "Backup Job", "rc job run", "Start-RCBackupJob", true),
        new("restore.plan.readonly", "Restore Plan", "rc restore plan", "Get-RCRestorePlan", true)
    };

    public static void AssertImplementedParity()
    {
        var missing = All.Where(f => string.IsNullOrWhiteSpace(f.Gui) || string.IsNullOrWhiteSpace(f.Cli) || string.IsNullOrWhiteSpace(f.PowerShell)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Feature parity catalog contains incomplete surfaces.");
    }
}
