namespace RescueClone.Core;

public sealed record ImageRepositoryListOptions(
    string RepositoryPath,
    string Pattern,
    bool Verify,
    string? Password);

public sealed record ImageRepositoryItem(
    string ImagePath,
    long FileSizeBytes,
    DateTimeOffset CreatedUtc,
    DateTimeOffset LastWriteUtc,
    bool Verified,
    int? FormatVersion,
    int? FileCount,
    long? OriginalBytes,
    string? RootSha256,
    string? Error);

public sealed record ImageRepositoryListReport(
    string RepositoryPath,
    string Pattern,
    bool Verify,
    int ImageCount,
    IReadOnlyList<ImageRepositoryItem> Images);

public sealed class ImageRepositoryCatalog
{
    private readonly ImageEngine _engine;

    public ImageRepositoryCatalog(ImageEngine? engine = null)
    {
        _engine = engine ?? new ImageEngine();
    }

    public ImageRepositoryListReport List(ImageRepositoryListOptions options)
    {
        if (!Directory.Exists(options.RepositoryPath))
            throw new DirectoryNotFoundException($"Image repository was not found: {options.RepositoryPath}");

        var root = Path.GetFullPath(options.RepositoryPath);
        var pattern = string.IsNullOrWhiteSpace(options.Pattern) ? "*.rcimg" : options.Pattern;
        var images = Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => BuildItem(path, options))
            .ToArray();

        return new ImageRepositoryListReport(root, pattern, options.Verify, images.Length, images);
    }

    private ImageRepositoryItem BuildItem(string path, ImageRepositoryListOptions options)
    {
        var info = new FileInfo(path);
        if (!options.Verify)
        {
            return new ImageRepositoryItem(
                info.FullName,
                info.Length,
                info.CreationTimeUtc,
                info.LastWriteTimeUtc,
                Verified: false,
                FormatVersion: null,
                FileCount: null,
                OriginalBytes: null,
                RootSha256: null,
                Error: null);
        }

        try
        {
            var verified = _engine.Verify(info.FullName, options.Password);
            return new ImageRepositoryItem(
                info.FullName,
                info.Length,
                info.CreationTimeUtc,
                info.LastWriteTimeUtc,
                Verified: true,
                verified.FormatVersion,
                verified.FileCount,
                verified.OriginalBytes,
                verified.RootSha256,
                Error: null);
        }
        catch (Exception ex)
        {
            return new ImageRepositoryItem(
                info.FullName,
                info.Length,
                info.CreationTimeUtc,
                info.LastWriteTimeUtc,
                Verified: false,
                FormatVersion: null,
                FileCount: null,
                OriginalBytes: null,
                RootSha256: null,
                ex.Message);
        }
    }
}
