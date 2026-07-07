namespace RescueClone.Core.Logs;

public sealed record LogListOptions(
    string DirectoryPath,
    string Pattern = "*.json");

public sealed record BackupLogEntry(
    string Path,
    bool Parsed,
    string? Error,
    string? JobId,
    string? Name,
    string? ImagePath,
    bool? Verified,
    string? RootSha256,
    string? HtmlReportPath,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? FinishedUtc);
