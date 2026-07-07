namespace RescueClone.Core;

public sealed record FeatureSurface(string FeatureId, string Gui, string Cli, string PowerShell, bool Implemented);

public static class FeatureCatalog
{
    public static IReadOnlyList<FeatureSurface> All { get; } = new List<FeatureSurface>
    {
        new("image.create.directory", "Create Image", "rc image create", "New-RCImage", true),
        new("image.verify", "Verify Image", "rc image verify", "Test-RCImage", true),
        new("image.browse", "Restore Image", "rc image browse", "Get-RCImageContent", true),
        new("image.list.repository", "Restore Image", "rc image list", "Get-RCImage", true),
        new("image.extract.directory", "Restore Image", "rc image extract", "Export-RCImageFile", true),
        new("image.project.readonly", "Restore Image", "rc image project", "Mount-RCImage", true),
        new("image.project.list", "Restore Image", "rc image projections", "Get-RCImageMount", true),
        new("image.project.remove", "Restore Image", "rc image unproject", "Dismount-RCImage", true),
        new("image.restore.directory", "Restore Image", "rc image restore", "Restore-RCImage", true),
        new("job.backup.directory.create", "Backup Job", "rc job create", "New-RCBackupJob", true),
        new("job.backup.directory.update", "Backup Job", "rc job update", "Set-RCBackupJob", true),
        new("job.backup.directory.delete", "Backup Job", "rc job delete", "Remove-RCBackupJob", true),
        new("job.backup.directory.export", "Backup Job", "rc job export", "Export-RCBackupJob", true),
        new("job.backup.directory.import", "Backup Job", "rc job import", "Import-RCBackupJob", true),
        new("job.backup.directory.status", "Backup Job", "rc job status", "Get-RCBackupJobStatus", true),
        new("job.backup.directory.validate", "Backup Job", "rc job validate", "Test-RCBackupJob", true),
        new("job.backup.directory.run", "Backup Job", "rc job run", "Start-RCBackupJob", true),
        new("retention.plan", "Retention", "rc retention plan", "Get-RCRetentionPlan", true),
        new("retention.apply", "Retention", "rc retention apply", "Invoke-RCRetention", true),
        new("schedule.plan", "Scheduler", "rc schedule plan", "Get-RCSchedulePlan", true),
        new("schedule.register", "Scheduler", "rc schedule register", "Register-RCSchedule", true),
        new("schedule.unregister", "Scheduler", "rc schedule unregister", "Unregister-RCSchedule", true),
        new("restore.plan.readonly", "Restore Plan", "rc restore plan", "Get-RCRestorePlan", true),
        new("operation.run.local", "Operations", "rc operation run", "Start-RCOperation", true),
        new("operation.run.service", "Operations", "rc service run-operation", "Start-RCServiceOperation", true),
        new("logs.backup.list", "Logs", "rc logs list", "Get-RCLog", true),
        new("storage.volume.list", "Volumes", "rc storage volumes", "Get-RCVolume", true),
        new("storage.disk.list", "Disks", "rc storage disks", "Get-RCDisk", true),
        new("storage.disk.safety", "Disks", "rc storage disk-safety", "Get-RCDiskSafety", true),
        new("native.status", "Native Engine", "rc native status", "Get-RCNativeStatus", true)
    };

    public static void AssertImplementedParity()
    {
        var missing = All.Where(f => string.IsNullOrWhiteSpace(f.Gui) || string.IsNullOrWhiteSpace(f.Cli) || string.IsNullOrWhiteSpace(f.PowerShell)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Feature parity catalog contains incomplete surfaces.");
    }
}
