namespace RescueClone.Core.Retention;

public sealed record RetentionOptions(
    string RepositoryPath,
    string Pattern,
    int? KeepCount,
    int? MaxAgeDays,
    long? MinFreeBytes);

public sealed record RetentionCandidate(
    string Path,
    long Bytes,
    DateTimeOffset LastWriteUtc,
    IReadOnlyList<string> Reasons);

public sealed record RetentionPlan(
    string RepositoryPath,
    string Pattern,
    int TotalFileCount,
    long TotalBytes,
    long CurrentFreeBytes,
    long? MinFreeBytes,
    IReadOnlyList<RetentionCandidate> Keep,
    IReadOnlyList<RetentionCandidate> Delete);

public sealed record RetentionApplyReport(
    RetentionPlan Plan,
    int DeletedFileCount,
    long DeletedBytes,
    IReadOnlyList<string> DeletedPaths);
