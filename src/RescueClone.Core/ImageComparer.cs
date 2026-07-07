using System.Security.Cryptography;

namespace RescueClone.Core;

public sealed record ImageCompareOptions(
    string ImagePath,
    string SourcePath,
    string? Password);

public sealed record ImageCompareDifference(
    string RelativePath,
    string DifferenceType,
    string? ImageSha256,
    string? SourceSha256,
    long? ImageBytes,
    long? SourceBytes);

public sealed record ImageCompareReport(
    string ImagePath,
    string SourcePath,
    int ImageFileCount,
    int SourceFileCount,
    int MatchedCount,
    int MissingCount,
    int ChangedCount,
    int ExtraCount,
    bool Equivalent,
    IReadOnlyList<ImageCompareDifference> Differences);

public sealed class ImageComparer
{
    private readonly IImageEngine _engine;

    public ImageComparer(IImageEngine? engine = null)
    {
        _engine = engine ?? new ImageEngine();
    }

    public ImageCompareReport Compare(ImageCompareOptions options)
    {
        if (!Directory.Exists(options.SourcePath))
            throw new DirectoryNotFoundException($"Source directory was not found: {options.SourcePath}");

        var sourceRoot = Path.GetFullPath(options.SourcePath);
        var image = _engine.Browse(options.ImagePath, options.Password);
        var imageFiles = image.Files.ToDictionary(file => NormalizeRelativePath(file.RelativePath), StringComparer.OrdinalIgnoreCase);
        var sourceFiles = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Select(path => BuildSourceEntry(sourceRoot, path))
            .ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);
        var differences = new List<ImageCompareDifference>();
        var matched = 0;

        foreach (var imageFile in imageFiles.Values.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (!sourceFiles.TryGetValue(NormalizeRelativePath(imageFile.RelativePath), out var sourceFile))
            {
                differences.Add(new ImageCompareDifference(imageFile.RelativePath, "missing", imageFile.Sha256, null, imageFile.OriginalLength, null));
                continue;
            }

            if (!StringComparer.OrdinalIgnoreCase.Equals(imageFile.Sha256, sourceFile.Sha256) || imageFile.OriginalLength != sourceFile.Length)
            {
                differences.Add(new ImageCompareDifference(imageFile.RelativePath, "changed", imageFile.Sha256, sourceFile.Sha256, imageFile.OriginalLength, sourceFile.Length));
                continue;
            }

            matched++;
        }

        foreach (var sourceFile in sourceFiles.Values.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (!imageFiles.ContainsKey(sourceFile.RelativePath))
                differences.Add(new ImageCompareDifference(sourceFile.RelativePath, "extra", null, sourceFile.Sha256, null, sourceFile.Length));
        }

        var missing = differences.Count(difference => difference.DifferenceType == "missing");
        var changed = differences.Count(difference => difference.DifferenceType == "changed");
        var extra = differences.Count(difference => difference.DifferenceType == "extra");
        return new ImageCompareReport(
            image.ImagePath,
            sourceRoot,
            image.FileCount,
            sourceFiles.Count,
            matched,
            missing,
            changed,
            extra,
            differences.Count == 0,
            differences);
    }

    private static SourceEntry BuildSourceEntry(string sourceRoot, string path)
    {
        using var input = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        var relative = NormalizeRelativePath(Path.GetRelativePath(sourceRoot, path));
        return new SourceEntry(relative, new FileInfo(path).Length, hash);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed record SourceEntry(string RelativePath, long Length, string Sha256);
}
