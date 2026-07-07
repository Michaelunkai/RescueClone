using System.Text.Json.Serialization;
using RescueClone.Core.Logs;
using RescueClone.Core.Retention;

namespace RescueClone.Core.Jobs;

public sealed record BackupJobDefinition(
    string JobId,
    string Name,
    bool Enabled,
    string SourcePath,
    string ImagePath,
    CompressionMode Compression,
    string? Password,
    bool VerifyAfterCreate,
    string? LogDirectory,
    string? PreBackupScriptPath = null,
    string? PostBackupScriptPath = null,
    int? ScriptHookTimeoutSeconds = null,
    int? LogRetentionCount = null,
    bool NotifyWindowsEventLog = false,
    bool NotifyEmail = false,
    string? EmailFrom = null,
    string? EmailTo = null,
    string? EmailPickupDirectory = null,
    string? EmailSmtpHost = null,
    int? EmailSmtpPort = null,
    bool EmailEnableSsl = false,
    string? EmailUsername = null,
    string? EmailPassword = null,
    int? RetryCount = null,
    int? RetryDelaySeconds = null,
    bool RestoreTestAfterCreate = false,
    string? RestoreTestTargetPath = null,
    bool ApplyRetentionAfterCreate = false,
    string? RetentionPattern = null,
    int? RetentionKeepCount = null,
    int? RetentionMaxAgeDays = null,
    long? RetentionMinFreeBytes = null)
{
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? JobId : Name;
}

public sealed record BackupJobValidationResult(
    bool Valid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record BackupJobDeleteReport(
    string Path,
    bool Deleted,
    DateTimeOffset DeletedUtc);

public sealed record BackupJobUpdateOptions(
    string? JobId = null,
    string? Name = null,
    bool? Enabled = null,
    string? SourcePath = null,
    string? ImagePath = null,
    CompressionMode? Compression = null,
    string? Password = null,
    bool? VerifyAfterCreate = null,
    string? LogDirectory = null);

public sealed record BackupJobUpdateReport(
    string Path,
    BackupJobDefinition Before,
    BackupJobDefinition After,
    DateTimeOffset UpdatedUtc);

public sealed record BackupJobTransferReport(
    string Operation,
    string SourcePath,
    string DestinationPath,
    string JobId,
    DateTimeOffset CompletedUtc);

public sealed record BackupJobListEntry(
    string Path,
    bool Loaded,
    BackupJobDefinition? Job,
    BackupJobValidationResult? Validation,
    string? Error);

public sealed record BackupJobListReport(
    string DirectoryPath,
    string Pattern,
    int FileCount,
    int LoadedCount,
    int InvalidCount,
    IReadOnlyList<BackupJobListEntry> Jobs);

public sealed record BackupJobStatusReport(
    string Path,
    BackupJobDefinition Job,
    BackupJobValidationResult Validation,
    string LogDirectory,
    BackupLogEntry? LastRun,
    ImageRepositoryAuditReport? RepositoryAudit = null);

public sealed record BackupJobHistoryReport(
    string Path,
    string JobId,
    string LogDirectory,
    int EntryCount,
    int ParseErrorCount,
    IReadOnlyList<BackupLogEntry> Entries,
    IReadOnlyList<BackupLogEntry> ParseErrors);

public sealed record BackupJobRunResult(
    string JobId,
    string Name,
    string ImagePath,
    bool Verified,
    string RootSha256,
    int FileCount,
    long OriginalBytes,
    long StoredBytes,
    string LogPath,
    string HtmlReportPath,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    IReadOnlyList<BackupScriptHookResult>? ScriptHooks = null,
    BackupNotificationResult? WindowsEventLogNotification = null,
    BackupNotificationResult? EmailNotification = null,
    IReadOnlyList<BackupRetryAttempt>? RetryAttempts = null,
    RestoreReport? RestoreTest = null,
    RetentionApplyReport? Retention = null,
    ImageCompareReport? SourceCompare = null);

public sealed record BackupScriptHookResult(
    string Phase,
    string ScriptPath,
    int ExitCode,
    bool TimedOut,
    string Output,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc);

public sealed record BackupNotificationResult(
    string Channel,
    bool Requested,
    bool Succeeded,
    string Message);

public sealed record BackupRetryAttempt(
    int Attempt,
    bool Succeeded,
    string? Error,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc);
