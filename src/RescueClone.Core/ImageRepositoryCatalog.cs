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

public sealed record ImageRepositoryAuditOptions(
    string RepositoryPath,
    string Pattern,
    string? Password);

public sealed record ImageRepositoryAuditReport(
    string RepositoryPath,
    string Pattern,
    int ImageCount,
    int VerifiedCount,
    int FailedCount,
    IReadOnlyList<ImageRepositoryItem> Images);

public sealed record ImageRepositoryProtectionOptions(
    string RepositoryPath,
    string Pattern);

public sealed record ImageRepositoryProtectionItem(
    string ImagePath,
    long FileSizeBytes,
    bool ReadOnlyBefore,
    bool ReadOnlyAfter,
    bool Changed);

public sealed record ImageRepositoryProtectionReport(
    string RepositoryPath,
    string Pattern,
    int ImageCount,
    int ProtectedCount,
    int ChangedCount,
    bool Applied,
    IReadOnlyList<ImageRepositoryProtectionItem> Images);

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

    public ImageRepositoryAuditReport Audit(ImageRepositoryAuditOptions options)
    {
        var listed = List(new ImageRepositoryListOptions(options.RepositoryPath, options.Pattern, Verify: true, options.Password));
        var failed = listed.Images.Count(image => !image.Verified);
        return new ImageRepositoryAuditReport(
            listed.RepositoryPath,
            listed.Pattern,
            listed.ImageCount,
            listed.Images.Count(image => image.Verified),
            failed,
            listed.Images);
    }

    public ImageRepositoryProtectionReport AuditProtection(ImageRepositoryProtectionOptions options)
    {
        return BuildProtectionReport(options, apply: false);
    }

    public ImageRepositoryProtectionReport ApplyProtection(ImageRepositoryProtectionOptions options)
    {
        return BuildProtectionReport(options, apply: true);
    }

    private static ImageRepositoryProtectionReport BuildProtectionReport(ImageRepositoryProtectionOptions options, bool apply)
    {
        if (!Directory.Exists(options.RepositoryPath))
            throw new DirectoryNotFoundException($"Image repository was not found: {options.RepositoryPath}");

        var root = Path.GetFullPath(options.RepositoryPath);
        var pattern = string.IsNullOrWhiteSpace(options.Pattern) ? "*.rcimg" : options.Pattern;
        var images = Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => BuildProtectionItem(path, apply))
            .ToArray();

        return new ImageRepositoryProtectionReport(
            root,
            pattern,
            images.Length,
            images.Count(image => image.ReadOnlyAfter),
            images.Count(image => image.Changed),
            apply,
            images);
    }

    private static ImageRepositoryProtectionItem BuildProtectionItem(string path, bool apply)
    {
        var info = new FileInfo(path);
        var before = (info.Attributes & FileAttributes.ReadOnly) != 0;
        if (apply && !before)
        {
            File.SetAttributes(info.FullName, info.Attributes | FileAttributes.ReadOnly);
            info.Refresh();
        }

        var after = (info.Attributes & FileAttributes.ReadOnly) != 0;
        return new ImageRepositoryProtectionItem(info.FullName, info.Length, before, after, before != after);
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
