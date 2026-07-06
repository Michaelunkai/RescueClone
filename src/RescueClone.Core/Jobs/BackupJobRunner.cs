using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RescueClone.Core.Jobs;

public sealed class BackupJobRunner
{
    private const int DefaultScriptHookTimeoutSeconds = 300;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ImageEngine _engine;

    public BackupJobRunner(ImageEngine? engine = null)
    {
        _engine = engine ?? new ImageEngine();
    }

    public BackupJobDefinition Load(string path)
    {
        using var input = File.OpenRead(path);
        return JsonSerializer.Deserialize<BackupJobDefinition>(input, JsonOptions)
            ?? throw new InvalidDataException("Backup job definition is empty.");
    }

    public BackupJobValidationResult Validate(BackupJobDefinition job)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(job.JobId))
            errors.Add("JobId is required.");
        if (string.IsNullOrWhiteSpace(job.Name))
            errors.Add("Name is required.");
        if (!job.Enabled)
            warnings.Add("Job is disabled and will not run unless forced by a caller.");
        if (string.IsNullOrWhiteSpace(job.SourcePath) || !Directory.Exists(job.SourcePath))
            errors.Add($"SourcePath does not exist: {job.SourcePath}");
        if (string.IsNullOrWhiteSpace(job.ImagePath))
            errors.Add("ImagePath is required.");
        else
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(job.ImagePath));
            if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
                warnings.Add($"ImagePath parent will be created: {parent}");
        }
        if (!string.IsNullOrWhiteSpace(job.PreBackupScriptPath) && !File.Exists(job.PreBackupScriptPath))
            errors.Add($"PreBackupScriptPath does not exist: {job.PreBackupScriptPath}");
        if (!string.IsNullOrWhiteSpace(job.PostBackupScriptPath) && !File.Exists(job.PostBackupScriptPath))
            errors.Add($"PostBackupScriptPath does not exist: {job.PostBackupScriptPath}");
        if (job.ScriptHookTimeoutSeconds is <= 0)
            errors.Add("ScriptHookTimeoutSeconds must be greater than zero when set.");
        if (job.LogRetentionCount is <= 0)
            errors.Add("LogRetentionCount must be greater than zero when set.");

        return new BackupJobValidationResult(errors.Count == 0, errors, warnings);
    }

    public BackupJobRunResult Run(BackupJobDefinition job, bool forceDisabled = false)
    {
        if (!job.Enabled && !forceDisabled)
            throw new InvalidOperationException($"Backup job is disabled: {job.JobId}");

        var validation = Validate(job);
        if (!validation.Valid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

        var started = DateTimeOffset.UtcNow;
        var imageParent = Path.GetDirectoryName(Path.GetFullPath(job.ImagePath));
        if (!string.IsNullOrWhiteSpace(imageParent))
            Directory.CreateDirectory(imageParent);

        var hooks = new List<BackupScriptHookResult>();
        if (!string.IsNullOrWhiteSpace(job.PreBackupScriptPath))
            hooks.Add(RunScriptHook("pre-backup", job.PreBackupScriptPath, job));

        var created = _engine.Create(new ImageOptions(job.SourcePath, job.ImagePath, job.Compression, job.Password));
        var verified = false;
        var rootSha = created.RootSha256;
        if (job.VerifyAfterCreate)
        {
            var verify = _engine.Verify(job.ImagePath, job.Password);
            verified = true;
            rootSha = verify.RootSha256;
        }

        if (!string.IsNullOrWhiteSpace(job.PostBackupScriptPath))
            hooks.Add(RunScriptHook("post-backup", job.PostBackupScriptPath, job));

        var finished = DateTimeOffset.UtcNow;
        var reports = WriteRunReports(job, started, finished, created, verified, rootSha, hooks);
        return new BackupJobRunResult(
            job.JobId,
            job.Name,
            job.ImagePath,
            verified,
            rootSha,
            created.FileCount,
            created.OriginalBytes,
            created.StoredBytes,
            reports.JsonPath,
            reports.HtmlPath,
            started,
            finished,
            hooks);
    }

    private static BackupScriptHookResult RunScriptHook(string phase, string scriptPath, BackupJobDefinition job)
    {
        var started = DateTimeOffset.UtcNow;
        var scriptFullPath = Path.GetFullPath(scriptPath);
        var extension = Path.GetExtension(scriptFullPath);
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = Path.GetDirectoryName(scriptFullPath) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(scriptFullPath);
        }
        else if (extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptFullPath);
        }
        else
        {
            startInfo.FileName = scriptFullPath;
        }
        startInfo.Environment["RESCUECLONE_HOOK_PHASE"] = phase;
        startInfo.Environment["RESCUECLONE_JOB_ID"] = job.JobId;
        startInfo.Environment["RESCUECLONE_SOURCE_PATH"] = job.SourcePath;
        startInfo.Environment["RESCUECLONE_IMAGE_PATH"] = job.ImagePath;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {phase} script: {scriptFullPath}");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var timeout = TimeSpan.FromSeconds(job.ScriptHookTimeoutSeconds ?? DefaultScriptHookTimeoutSeconds);
        if (!process.WaitForExit(timeout))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            process.WaitForExit();
            Task.WaitAll(outputTask, errorTask);
            var timeoutFinished = DateTimeOffset.UtcNow;
            var timeoutOutput = CombineOutput(outputTask.Result, errorTask.Result);
            _ = new BackupScriptHookResult(phase, scriptFullPath, -1, TimedOut: true, timeoutOutput, started, timeoutFinished);
            throw new TimeoutException($"{phase} script exceeded {timeout.TotalSeconds:0} second timeout: {scriptFullPath}{Environment.NewLine}{timeoutOutput}");
        }
        Task.WaitAll(outputTask, errorTask);
        var finished = DateTimeOffset.UtcNow;
        var combined = CombineOutput(outputTask.Result, errorTask.Result);
        var result = new BackupScriptHookResult(phase, scriptFullPath, process.ExitCode, TimedOut: false, combined, started, finished);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{phase} script failed with exit code {process.ExitCode}: {scriptFullPath}{Environment.NewLine}{combined}");
        return result;
    }

    private static string CombineOutput(string output, string error)
    {
        return string.IsNullOrWhiteSpace(error) ? output.Trim() : $"{output.Trim()}{Environment.NewLine}{error.Trim()}".Trim();
    }

    private static BackupJobReportPaths WriteRunReports(BackupJobDefinition job, DateTimeOffset started, DateTimeOffset finished, ImageReport report, bool verified, string rootSha, IReadOnlyList<BackupScriptHookResult> hooks)
    {
        var directory = string.IsNullOrWhiteSpace(job.LogDirectory)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(job.ImagePath)) ?? Environment.CurrentDirectory, "logs")
            : job.LogDirectory;
        Directory.CreateDirectory(directory);
        var safeId = string.Join("_", job.JobId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var stamp = started.ToString("yyyyMMdd-HHmmss");
        var logPath = Path.Combine(directory, $"{safeId}-{stamp}.json");
        var htmlPath = Path.Combine(directory, $"{safeId}-{stamp}.html");
        var payload = new BackupJobRunResult(
            job.JobId,
            job.Name,
            job.ImagePath,
            verified,
            rootSha,
            report.FileCount,
            report.OriginalBytes,
            report.StoredBytes,
            logPath,
            htmlPath,
            started,
            finished,
            hooks);
        File.WriteAllText(logPath, JsonSerializer.Serialize(payload, JsonOptions));
        File.WriteAllText(htmlPath, BuildHtmlReport(payload));
        RotateReports(directory, safeId, job.LogRetentionCount);
        return new BackupJobReportPaths(logPath, htmlPath);
    }

    private static string BuildHtmlReport(BackupJobRunResult report)
    {
        var hookRows = report.ScriptHooks is null
            ? string.Empty
            : string.Join(Environment.NewLine, report.ScriptHooks.Select(h => $"<tr><td>{WebUtility.HtmlEncode(h.Phase)}</td><td>{WebUtility.HtmlEncode(h.ScriptPath)}</td><td>{h.ExitCode}</td><td>{h.TimedOut}</td><td><pre>{WebUtility.HtmlEncode(h.Output)}</pre></td></tr>"));

        return $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>RescueClone Backup Report - {{WebUtility.HtmlEncode(report.JobId)}}</title>
          <style>
            body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2937; }
            table { border-collapse: collapse; width: 100%; margin-top: 12px; }
            th, td { border: 1px solid #d1d5db; padding: 8px; text-align: left; vertical-align: top; }
            th { background: #f3f4f6; }
            pre { margin: 0; white-space: pre-wrap; }
          </style>
        </head>
        <body>
          <h1>RescueClone Backup Report</h1>
          <table>
            <tr><th>Job</th><td>{{WebUtility.HtmlEncode(report.Name)}} ({{WebUtility.HtmlEncode(report.JobId)}})</td></tr>
            <tr><th>Image</th><td>{{WebUtility.HtmlEncode(report.ImagePath)}}</td></tr>
            <tr><th>Verified</th><td>{{report.Verified}}</td></tr>
            <tr><th>Files</th><td>{{report.FileCount}}</td></tr>
            <tr><th>Original Bytes</th><td>{{report.OriginalBytes}}</td></tr>
            <tr><th>Stored Bytes</th><td>{{report.StoredBytes}}</td></tr>
            <tr><th>Root SHA-256</th><td>{{WebUtility.HtmlEncode(report.RootSha256)}}</td></tr>
            <tr><th>Started UTC</th><td>{{report.StartedUtc:O}}</td></tr>
            <tr><th>Finished UTC</th><td>{{report.FinishedUtc:O}}</td></tr>
          </table>
          <h2>Script Hooks</h2>
          <table>
            <tr><th>Phase</th><th>Script</th><th>Exit Code</th><th>Timed Out</th><th>Output</th></tr>
            {{hookRows}}
          </table>
        </body>
        </html>
        """;
    }

    private static void RotateReports(string directory, string safeId, int? keepCount)
    {
        if (keepCount is null)
            return;

        foreach (var extension in new[] { ".json", ".html" })
        {
            var files = Directory.EnumerateFiles(directory, $"{safeId}-*{extension}")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.Name, StringComparer.Ordinal)
                .Skip(keepCount.Value);
            foreach (var file in files)
                file.Delete();
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record BackupJobReportPaths(string JsonPath, string HtmlPath);
}
