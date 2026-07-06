namespace RescueClone.Core;

public enum CompressionMode
{
    None,
    Medium,
    High
}

public sealed record ImageOptions(
    string SourcePath,
    string ImagePath,
    CompressionMode Compression,
    string? Password);

public sealed record RestoreOptions(
    string ImagePath,
    string TargetPath,
    string? Password,
    bool Overwrite);

public sealed record ImageFileEntry(
    string RelativePath,
    long OriginalLength,
    long StoredLength,
    string Sha256);

public sealed record ImageReport(
    string ImagePath,
    int FileCount,
    long OriginalBytes,
    long StoredBytes,
    string RootSha256,
    IReadOnlyList<ImageFileEntry> Files);

public sealed record RestoreReport(
    string ImagePath,
    string TargetPath,
    int FileCount,
    long RestoredBytes);

