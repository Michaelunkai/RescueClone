using System.Text.Json;
using RescueClone.Core.Jobs;

namespace RescueClone.Core.Logs;

public sealed class BackupLogCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<BackupLogEntry> List(LogListOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
            throw new ArgumentException("DirectoryPath is required.");
        if (!Directory.Exists(options.DirectoryPath))
            throw new DirectoryNotFoundException(options.DirectoryPath);

        var pattern = string.IsNullOrWhiteSpace(options.Pattern) ? "*.json" : options.Pattern;
        return Directory.EnumerateFiles(options.DirectoryPath, pattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ReadEntry)
            .ToArray();
    }

    private static BackupLogEntry ReadEntry(string path)
    {
        try
        {
            var report = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("Log file did not contain a backup job run result.");
            return new BackupLogEntry(
                path,
                Parsed: true,
                Error: null,
                report.JobId,
                report.Name,
                report.ImagePath,
                report.Verified,
                report.RootSha256,
                report.HtmlReportPath,
                report.StartedUtc,
                report.FinishedUtc);
        }
        catch (Exception ex)
        {
            return new BackupLogEntry(
                path,
                Parsed: false,
                Error: ex.Message,
                JobId: null,
                Name: null,
                ImagePath: null,
                Verified: null,
                RootSha256: null,
                HtmlReportPath: null,
                StartedUtc: null,
                FinishedUtc: null);
        }
    }
}
