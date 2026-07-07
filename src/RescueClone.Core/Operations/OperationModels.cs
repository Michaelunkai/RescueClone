using System.Text.Json;

namespace RescueClone.Core.Operations;

public enum OperationState
{
    Succeeded,
    Failed
}

public sealed record OperationRequest(
    string Kind,
    Dictionary<string, JsonElement> Parameters,
    string? OperationId = null);

public sealed record OperationReport(
    string OperationId,
    string Kind,
    OperationState State,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    string? LogPath,
    JsonElement? Result,
    string? Error,
    string? RecoveryStatePath = null,
    IReadOnlyList<OperationAuditEvent>? AuditEvents = null,
    OperationError? ErrorDetail = null);

public sealed record OperationRecoveryState(
    OperationRequest Request,
    OperationReport Report,
    DateTimeOffset WrittenUtc);

public sealed record OperationAuditEvent(
    string EventType,
    DateTimeOffset OccurredUtc,
    string Message);

public sealed record OperationError(
    string Code,
    string Message,
    string ExceptionType);

public sealed record OperationKindSurface(
    string Kind,
    string Description,
    IReadOnlyList<string> RequiredParameters,
    IReadOnlyList<string> OptionalParameters);

public static class OperationKindCatalog
{
    public static IReadOnlyList<OperationKindSurface> All { get; } = new List<OperationKindSurface>
    {
        new("image.create.directory", "Create a directory image.", new[] { "source", "image" }, new[] { "compression", "password", "format" }),
        new("image.verify", "Verify an image checksum manifest.", new[] { "image" }, new[] { "password" }),
        new("image.audit.repository", "Verify all matching images in a repository.", new[] { "repository" }, new[] { "pattern", "password" }),
        new("image.protect.audit", "Audit read-only protection on matching repository images.", new[] { "repository" }, new[] { "pattern" }),
        new("image.protect.apply", "Mark matching repository images read-only.", new[] { "repository" }, new[] { "pattern" }),
        new("image.compare.source", "Compare an image to a source directory.", new[] { "image", "source" }, new[] { "password" }),
        new("image.list.repository", "List matching repository images.", new[] { "repository" }, new[] { "pattern", "verify", "password" }),
        new("image.browse", "Browse verified image contents.", new[] { "image" }, new[] { "password" }),
        new("image.extract.directory", "Extract selected image paths to a directory.", new[] { "image", "target", "paths" }, new[] { "password", "overwrite" }),
        new("image.project.readonly", "Create a managed read-only directory projection.", new[] { "image", "target" }, new[] { "password", "overwrite" }),
        new("image.project.list", "List managed image projections under a root.", new[] { "root" }, Array.Empty<string>()),
        new("image.project.remove", "Remove a managed image projection.", new[] { "target" }, Array.Empty<string>()),
        new("image.restore.directory", "Restore a directory image to a directory target.", new[] { "image", "target" }, new[] { "password", "overwrite" }),
        new("job.backup.directory.create", "Create a directory backup job JSON file.", new[] { "file", "jobId", "name", "source", "image" }, new[] { "enabled", "compression", "password", "verifyAfterCreate", "logDirectory" }),
        new("job.backup.directory.update", "Update a directory backup job JSON file.", new[] { "file" }, new[] { "jobId", "name", "enabled", "source", "image", "compression", "password", "verifyAfterCreate", "logDirectory" }),
        new("job.backup.directory.delete", "Delete a backup job JSON file.", new[] { "file" }, Array.Empty<string>()),
        new("job.backup.directory.export", "Export a backup job JSON file.", new[] { "file", "output" }, Array.Empty<string>()),
        new("job.backup.directory.import", "Import a backup job JSON file.", new[] { "file", "target" }, Array.Empty<string>()),
        new("job.backup.directory.list", "List backup job JSON files in a directory.", new[] { "directory" }, new[] { "pattern" }),
        new("job.backup.directory.status", "Read backup job status.", new[] { "file" }, Array.Empty<string>()),
        new("job.backup.directory.history", "List backup job run history.", new[] { "file" }, new[] { "pattern" }),
        new("job.backup.directory.validate", "Validate a backup job JSON file.", new[] { "file" }, Array.Empty<string>()),
        new("job.backup.directory.run", "Run a backup job.", new[] { "file" }, new[] { "forceDisabled" }),
        new("retention.plan", "Plan flat repository retention.", new[] { "repository" }, new[] { "pattern", "keepCount", "maxAgeDays", "minFreeBytes" }),
        new("retention.apply", "Apply flat repository retention.", new[] { "repository" }, new[] { "pattern", "keepCount", "maxAgeDays", "minFreeBytes" }),
        new("retention.gfs.plan", "Plan GFS-style repository retention.", new[] { "repository" }, new[] { "pattern", "dailyKeep", "weeklyKeep", "monthlyKeep" }),
        new("retention.gfs.apply", "Apply GFS-style repository retention.", new[] { "repository" }, new[] { "pattern", "dailyKeep", "weeklyKeep", "monthlyKeep" }),
        new("schedule.plan", "Generate Windows Task Scheduler XML.", new[] { "taskName", "jobFile", "cliPath" }, new[] { "frequency", "time", "runMissed", "eventLog", "eventId", "eventSource" }),
        new("schedule.register", "Register a Windows scheduled task.", new[] { "taskName", "jobFile", "cliPath" }, new[] { "frequency", "time", "runMissed", "eventLog", "eventId", "eventSource" }),
        new("schedule.status", "Read scheduled task status.", new[] { "taskName" }, Array.Empty<string>()),
        new("schedule.run", "Run a scheduled task now.", new[] { "taskName" }, Array.Empty<string>()),
        new("schedule.unregister", "Unregister a scheduled task.", new[] { "taskName" }, Array.Empty<string>()),
        new("restore.plan.readonly", "Create a read-only restore plan.", new[] { "image", "targetDiskId" }, new[] { "password", "targetDiskSizeBytes", "requiredBytes", "targetIsCurrentSystemDisk", "bootMode", "hasEfiSystemPartition", "bcdStore" }),
        new("rescue.answer.create", "Create an unattended rescue answer file.", new[] { "output", "repository", "image", "targetDiskId" }, new[] { "password", "bootMode", "targetDiskSizeBytes", "requiredBytes", "targetIsCurrentSystemDisk", "hasEfiSystemPartition", "bcdStore", "driverDirectories", "networkShares", "repairBoot", "rebootAfterRestore", "verifyImage" }),
        new("rescue.answer.validate", "Validate an unattended rescue answer file.", new[] { "file" }, new[] { "verifyImage" })
    };

    public static void AssertUniqueKinds()
    {
        var duplicates = All.GroupBy(kind => kind.Kind, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
            throw new InvalidOperationException("Duplicate operation kind catalog entries: " + string.Join(", ", duplicates));
    }
}
