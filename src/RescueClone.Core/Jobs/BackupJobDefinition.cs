using System.Text.Json.Serialization;

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
    bool NotifyWindowsEventLog = false)
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
    BackupNotificationResult? WindowsEventLogNotification = null);

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
