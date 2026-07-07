namespace RescueClone.Core;

public enum CompressionMode
{
    None,
    Medium,
    High
}

public enum ImageContainerFormat
{
    V1 = 1,
    V2 = 2
}

public interface IImageEngine
{
    ImageReport Create(ImageOptions options);
    ImageReport Verify(string imagePath, string? password);
    ImageBrowseReport Browse(string imagePath, string? password);
    RestoreReport Restore(RestoreOptions options);
    RestoreReport Extract(ExtractOptions options);
}

public sealed record ImageOptions(
    string SourcePath,
    string ImagePath,
    CompressionMode Compression,
    string? Password,
    ImageContainerFormat Format = ImageContainerFormat.V2);

public sealed record RestoreOptions(
    string ImagePath,
    string TargetPath,
    string? Password,
    bool Overwrite);

public sealed record ExtractOptions(
    string ImagePath,
    string TargetPath,
    IReadOnlyList<string> RelativePaths,
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
    IReadOnlyList<ImageFileEntry> Files,
    int FormatVersion = 1);

public sealed record ImageBrowseReport(
    string ImagePath,
    int FileCount,
    long OriginalBytes,
    string RootSha256,
    IReadOnlyList<ImageFileEntry> Files,
    int FormatVersion);

public sealed record RestoreReport(
    string ImagePath,
    string TargetPath,
    int FileCount,
    long RestoredBytes);
