namespace RescueClone.Core.Retention;

public sealed class RetentionManager
{
    public RetentionPlan Plan(RetentionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RepositoryPath))
            throw new ArgumentException("RepositoryPath is required.");
        if (!Directory.Exists(options.RepositoryPath))
            throw new DirectoryNotFoundException(options.RepositoryPath);
        if (options.KeepCount is < 0)
            throw new ArgumentOutOfRangeException(nameof(options.KeepCount));
        if (options.MaxAgeDays is < 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaxAgeDays));
        if (options.MinFreeBytes is < 0)
            throw new ArgumentOutOfRangeException(nameof(options.MinFreeBytes));

        var pattern = string.IsNullOrWhiteSpace(options.Pattern) ? "*.rcimg" : options.Pattern;
        var excludedPaths = new HashSet<string>(
            options.ExcludedPaths?.Select(path => Path.GetFullPath(path)) ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var files = Directory.EnumerateFiles(options.RepositoryPath, pattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .Where(file => !excludedPaths.Contains(file.FullName))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var deleteReasons = files.ToDictionary(file => file.FullName, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        ApplyCountRule(files, options.KeepCount, deleteReasons);
        ApplyAgeRule(files, options.MaxAgeDays, deleteReasons);
        ApplyFreeSpaceRule(options.RepositoryPath, files, options.MinFreeBytes, deleteReasons);

        var keep = new List<RetentionCandidate>();
        var delete = new List<RetentionCandidate>();
        foreach (var file in files)
        {
            var reasons = deleteReasons[file.FullName];
            var candidate = ToCandidate(file, reasons);
            if (reasons.Count == 0)
                keep.Add(candidate);
            else
                delete.Add(candidate);
        }

        var root = Path.GetPathRoot(Path.GetFullPath(options.RepositoryPath)) ?? options.RepositoryPath;
        var currentFree = new DriveInfo(root).AvailableFreeSpace;
        return new RetentionPlan(
            options.RepositoryPath,
            pattern,
            files.Length,
            files.Sum(file => file.Length),
            currentFree,
            options.MinFreeBytes,
            keep,
            delete);
    }

    public RetentionApplyReport Apply(RetentionOptions options)
    {
        var plan = Plan(options);
        var deletedPaths = new List<string>();
        long deletedBytes = 0;
        foreach (var candidate in plan.Delete)
        {
            var fullPath = Path.GetFullPath(candidate.Path);
            var repoRoot = EnsureTrailingSeparator(Path.GetFullPath(options.RepositoryPath));
            if (!fullPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Retention candidate is outside repository: {candidate.Path}");
            if (!File.Exists(fullPath))
                continue;
            deletedBytes += new FileInfo(fullPath).Length;
            ClearReadOnly(fullPath);
            File.Delete(fullPath);
            deletedPaths.Add(fullPath);
        }

        return new RetentionApplyReport(plan, deletedPaths.Count, deletedBytes, deletedPaths);
    }

    public RetentionPlan PlanGfs(GfsRetentionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RepositoryPath))
            throw new ArgumentException("RepositoryPath is required.");
        if (!Directory.Exists(options.RepositoryPath))
            throw new DirectoryNotFoundException(options.RepositoryPath);
        if (options.DailyKeepCount is < 0)
            throw new ArgumentOutOfRangeException(nameof(options.DailyKeepCount));
        if (options.WeeklyKeepCount is < 0)
            throw new ArgumentOutOfRangeException(nameof(options.WeeklyKeepCount));
        if (options.MonthlyKeepCount is < 0)
            throw new ArgumentOutOfRangeException(nameof(options.MonthlyKeepCount));
        if (options.DailyKeepCount is null && options.WeeklyKeepCount is null && options.MonthlyKeepCount is null)
            throw new ArgumentException("At least one GFS keep count is required.");

        var pattern = string.IsNullOrWhiteSpace(options.Pattern) ? "*.rcimg" : options.Pattern;
        var excludedPaths = new HashSet<string>(
            options.ExcludedPaths?.Select(path => Path.GetFullPath(path)) ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var files = Directory.EnumerateFiles(options.RepositoryPath, pattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .Where(file => !excludedPaths.Contains(file.FullName))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SelectNewestPerBucket(files, options.DailyKeepCount, file => file.LastWriteTimeUtc.Date, protectedPaths);
        SelectNewestPerBucket(files, options.WeeklyKeepCount, file =>
        {
            var date = DateOnly.FromDateTime(file.LastWriteTimeUtc);
            return ISOWeekKey(date);
        }, protectedPaths);
        SelectNewestPerBucket(files, options.MonthlyKeepCount, file => new YearMonth(file.LastWriteTimeUtc.Year, file.LastWriteTimeUtc.Month), protectedPaths);

        var keep = new List<RetentionCandidate>();
        var delete = new List<RetentionCandidate>();
        foreach (var file in files)
        {
            if (protectedPaths.Contains(file.FullName))
                keep.Add(ToCandidate(file, Array.Empty<string>()));
            else
                delete.Add(ToCandidate(file, new[] { "not selected by GFS daily/weekly/monthly policy" }));
        }

        var root = Path.GetPathRoot(Path.GetFullPath(options.RepositoryPath)) ?? options.RepositoryPath;
        var currentFree = new DriveInfo(root).AvailableFreeSpace;
        return new RetentionPlan(
            options.RepositoryPath,
            pattern,
            files.Length,
            files.Sum(file => file.Length),
            currentFree,
            null,
            keep,
            delete);
    }

    public RetentionApplyReport ApplyGfs(GfsRetentionOptions options)
    {
        var plan = PlanGfs(options);
        var deletedPaths = new List<string>();
        long deletedBytes = 0;
        foreach (var candidate in plan.Delete)
        {
            var fullPath = Path.GetFullPath(candidate.Path);
            var repoRoot = EnsureTrailingSeparator(Path.GetFullPath(options.RepositoryPath));
            if (!fullPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Retention candidate is outside repository: {candidate.Path}");
            if (!File.Exists(fullPath))
                continue;
            deletedBytes += new FileInfo(fullPath).Length;
            ClearReadOnly(fullPath);
            File.Delete(fullPath);
            deletedPaths.Add(fullPath);
        }

        return new RetentionApplyReport(plan, deletedPaths.Count, deletedBytes, deletedPaths);
    }

    private static void ApplyCountRule(FileInfo[] files, int? keepCount, Dictionary<string, List<string>> deleteReasons)
    {
        if (keepCount is null)
            return;
        foreach (var file in files.Skip(keepCount.Value))
            deleteReasons[file.FullName].Add($"exceeds keep-count {keepCount.Value}");
    }

    private static void ApplyAgeRule(FileInfo[] files, int? maxAgeDays, Dictionary<string, List<string>> deleteReasons)
    {
        if (maxAgeDays is null)
            return;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-maxAgeDays.Value);
        foreach (var file in files.Where(file => file.LastWriteTimeUtc < cutoff.UtcDateTime))
            deleteReasons[file.FullName].Add($"older than {maxAgeDays.Value} days");
    }

    private static void ApplyFreeSpaceRule(string repositoryPath, FileInfo[] files, long? minFreeBytes, Dictionary<string, List<string>> deleteReasons)
    {
        if (minFreeBytes is null)
            return;

        var root = Path.GetPathRoot(Path.GetFullPath(repositoryPath)) ?? repositoryPath;
        var free = new DriveInfo(root).AvailableFreeSpace;
        if (free >= minFreeBytes.Value)
            return;

        var projectedFree = free;
        foreach (var file in files.OrderBy(file => file.LastWriteTimeUtc).ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (projectedFree >= minFreeBytes.Value)
                break;
            deleteReasons[file.FullName].Add($"required to reach min-free-bytes {minFreeBytes.Value}");
            projectedFree += file.Length;
        }
    }

    private static void SelectNewestPerBucket<TKey>(FileInfo[] files, int? keepCount, Func<FileInfo, TKey> bucketSelector, HashSet<string> protectedPaths)
        where TKey : notnull
    {
        if (keepCount is null or 0)
            return;
        foreach (var file in files
            .GroupBy(bucketSelector)
            .Select(group => group.OrderByDescending(file => file.LastWriteTimeUtc).ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase).First())
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(keepCount.Value))
        {
            protectedPaths.Add(file.FullName);
        }
    }

    private static YearWeek ISOWeekKey(DateOnly date)
    {
        var year = System.Globalization.ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue));
        var week = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        return new YearWeek(year, week);
    }

    private static RetentionCandidate ToCandidate(FileInfo file, IReadOnlyList<string> reasons)
    {
        return new RetentionCandidate(file.FullName, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), reasons.ToArray());
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static void ClearReadOnly(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    private readonly record struct YearWeek(int Year, int Week);

    private readonly record struct YearMonth(int Year, int Month);
}
