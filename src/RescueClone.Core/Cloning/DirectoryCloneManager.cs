using System.Security.Cryptography;

namespace RescueClone.Core.Cloning;

public sealed record DirectoryCloneOptions(
    string SourcePath,
    string TargetPath,
    bool Overwrite = false);

public sealed record DirectoryCloneFileResult(
    string RelativePath,
    long Length,
    string Sha256);

public sealed record DirectoryCloneReport(
    string SourcePath,
    string TargetPath,
    int FileCount,
    long TotalBytes,
    bool Verified,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    IReadOnlyList<DirectoryCloneFileResult> Files);

public sealed class DirectoryCloneManager
{
    public DirectoryCloneReport Clone(DirectoryCloneOptions options)
    {
        if (!Directory.Exists(options.SourcePath))
            throw new DirectoryNotFoundException(options.SourcePath);

        var sourceRoot = EnsureTrailingSeparator(Path.GetFullPath(options.SourcePath));
        var targetRoot = EnsureTrailingSeparator(Path.GetFullPath(options.TargetPath));
        if (string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Clone source and target must be different directories.");
        if (IsWithinRoot(sourceRoot, targetRoot))
            throw new InvalidOperationException("Clone target cannot be inside the source directory.");
        if (Directory.Exists(targetRoot) && Directory.EnumerateFileSystemEntries(targetRoot).Any() && !options.Overwrite)
            throw new IOException("Clone target is not empty. Pass overwrite=true to replace matching files.");

        var started = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(targetRoot);
        var results = new List<DirectoryCloneFileResult>();
        long totalBytes = 0;

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(sourceRoot, sourceFile).Replace('\\', '/');
            var targetFile = Path.GetFullPath(Path.Combine(targetRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinRoot(targetRoot, targetFile))
                throw new InvalidDataException($"Unsafe relative path during clone: {relative}");
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? targetRoot);
            if (File.Exists(targetFile) && !options.Overwrite)
                throw new IOException($"Clone target file exists: {targetFile}");

            File.Copy(sourceFile, targetFile, overwrite: options.Overwrite);
            File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
            var sourceHash = HashFile(sourceFile);
            var targetHash = HashFile(targetFile);
            if (!StringComparer.OrdinalIgnoreCase.Equals(sourceHash, targetHash))
                throw new InvalidDataException($"Clone verification failed for {relative}.");

            var length = new FileInfo(sourceFile).Length;
            results.Add(new DirectoryCloneFileResult(relative, length, sourceHash));
            totalBytes += length;
        }

        return new DirectoryCloneReport(
            sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            targetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            results.Count,
            totalBytes,
            Verified: true,
            started,
            DateTimeOffset.UtcNow,
            results);
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static bool IsWithinRoot(string root, string candidate)
    {
        var rootFull = EnsureTrailingSeparator(Path.GetFullPath(root));
        var candidateFull = Path.GetFullPath(candidate);
        return candidateFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }
}
