using System.Text.Json.Serialization;
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
    RetentionApplyReport? Retention = null);

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
