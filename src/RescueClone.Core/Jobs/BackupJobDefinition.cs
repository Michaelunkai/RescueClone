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
    string? LogDirectory)
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
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc);
