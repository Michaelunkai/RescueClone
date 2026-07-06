using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RescueClone.Core.Jobs;

public sealed class BackupJobRunner
{
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
        var logPath = WriteRunLog(job, started, finished, created, verified, rootSha, hooks);
        return new BackupJobRunResult(
            job.JobId,
            job.Name,
            job.ImagePath,
            verified,
            rootSha,
            created.FileCount,
            created.OriginalBytes,
            created.StoredBytes,
            logPath,
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
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        var finished = DateTimeOffset.UtcNow;
        var combined = string.IsNullOrWhiteSpace(error) ? output.Trim() : $"{output.Trim()}{Environment.NewLine}{error.Trim()}".Trim();
        var result = new BackupScriptHookResult(phase, scriptFullPath, process.ExitCode, combined, started, finished);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{phase} script failed with exit code {process.ExitCode}: {scriptFullPath}{Environment.NewLine}{combined}");
        return result;
    }

    private static string WriteRunLog(BackupJobDefinition job, DateTimeOffset started, DateTimeOffset finished, ImageReport report, bool verified, string rootSha, IReadOnlyList<BackupScriptHookResult> hooks)
    {
        var directory = string.IsNullOrWhiteSpace(job.LogDirectory)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(job.ImagePath)) ?? Environment.CurrentDirectory, "logs")
            : job.LogDirectory;
        Directory.CreateDirectory(directory);
        var safeId = string.Join("_", job.JobId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var logPath = Path.Combine(directory, $"{safeId}-{started:yyyyMMdd-HHmmss}.json");
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
            started,
            finished,
            hooks);
        File.WriteAllText(logPath, JsonSerializer.Serialize(payload, JsonOptions));
        return logPath;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
