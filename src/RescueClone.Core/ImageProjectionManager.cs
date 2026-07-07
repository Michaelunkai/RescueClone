using System.Text.Json;

namespace RescueClone.Core;

public sealed record ImageProjectionOptions(
    string ImagePath,
    string TargetPath,
    string? Password,
    bool Overwrite);

public sealed record ImageProjectionReport(
    string ImagePath,
    string TargetPath,
    int FileCount,
    long OriginalBytes,
    string ManifestPath,
    DateTimeOffset ProjectedUtc);

public sealed record ImageUnprojectionOptions(
    string TargetPath);

public sealed record ImageUnprojectionReport(
    string TargetPath,
    int RemovedFileCount,
    long RemovedBytes,
    string ManifestPath);

public sealed record ImageProjectionListOptions(
    string RootPath);

public sealed record ImageProjectionListReport(
    string RootPath,
    int ProjectionCount,
    IReadOnlyList<ImageProjectionReport> Projections);

public sealed class ImageProjectionManager
{
    public const string ManifestFileName = ".rescueclone-projection.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ImageEngine _engine;

    public ImageProjectionManager(ImageEngine? engine = null)
    {
        _engine = engine ?? new ImageEngine();
    }

    public ImageProjectionReport Project(ImageProjectionOptions options)
    {
        if (Directory.Exists(options.TargetPath) && Directory.EnumerateFileSystemEntries(options.TargetPath).Any() && !options.Overwrite)
            throw new IOException("Projection target is not empty. Pass overwrite=true to project into it.");

        var restore = _engine.Restore(new RestoreOptions(options.ImagePath, options.TargetPath, options.Password, options.Overwrite));
        var files = Directory.EnumerateFiles(options.TargetPath, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var file in files)
        {
            var attributes = File.GetAttributes(file);
            File.SetAttributes(file, attributes | FileAttributes.ReadOnly);
        }

        var manifestPath = Path.Combine(options.TargetPath, ManifestFileName);
        var report = new ImageProjectionReport(
            options.ImagePath,
            options.TargetPath,
            restore.FileCount,
            restore.RestoredBytes,
            manifestPath,
            DateTimeOffset.UtcNow);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new ProjectionManifest(report, ToRelativePaths(options.TargetPath, files)), JsonOptions));
        File.SetAttributes(manifestPath, File.GetAttributes(manifestPath) | FileAttributes.ReadOnly);
        return report;
    }

    public ImageUnprojectionReport Unproject(ImageUnprojectionOptions options)
    {
        var manifestPath = Path.Combine(options.TargetPath, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Projection manifest was not found. Refusing to unproject an unmanaged directory.", manifestPath);

        ClearReadOnly(manifestPath);
        var manifest = JsonSerializer.Deserialize<ProjectionManifest>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidDataException("Projection manifest is invalid.");
        var root = Path.GetFullPath(options.TargetPath);
        var removedCount = 0;
        long removedBytes = 0;

        foreach (var relativePath in manifest.RelativePaths.OrderByDescending(path => path.Length))
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinRoot(root, fullPath) || !File.Exists(fullPath))
                continue;

            var info = new FileInfo(fullPath);
            removedBytes += info.Length;
            ClearReadOnly(fullPath);
            File.Delete(fullPath);
            removedCount++;
        }

        File.Delete(manifestPath);
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }

        return new ImageUnprojectionReport(options.TargetPath, removedCount, removedBytes, manifestPath);
    }

    public ImageProjectionListReport List(ImageProjectionListOptions options)
    {
        if (!Directory.Exists(options.RootPath))
            throw new DirectoryNotFoundException($"Projection search root was not found: {options.RootPath}");

        var root = Path.GetFullPath(options.RootPath);
        var projections = Directory.EnumerateFiles(root, ManifestFileName, SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var manifest = JsonSerializer.Deserialize<ProjectionManifest>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException($"Projection manifest is invalid: {path}");
                return manifest.Report;
            })
            .OrderBy(report => report.TargetPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ImageProjectionListReport(root, projections.Length, projections);
    }

    private static IReadOnlyList<string> ToRelativePaths(string root, IReadOnlyList<string> files)
    {
        var fullRoot = Path.GetFullPath(root);
        return files.Select(path => Path.GetRelativePath(fullRoot, path).Replace('\\', '/')).ToArray();
    }

    private static void ClearReadOnly(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    private static bool IsWithinRoot(string root, string target)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(target).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ProjectionManifest(
        ImageProjectionReport Report,
        IReadOnlyList<string> RelativePaths);
}
