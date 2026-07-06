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

        var created = _engine.Create(new ImageOptions(job.SourcePath, job.ImagePath, job.Compression, job.Password));
        var verified = false;
        var rootSha = created.RootSha256;
        if (job.VerifyAfterCreate)
        {
            var verify = _engine.Verify(job.ImagePath, job.Password);
            verified = true;
            rootSha = verify.RootSha256;
        }

        var finished = DateTimeOffset.UtcNow;
        var logPath = WriteRunLog(job, started, finished, created, verified, rootSha);
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
            finished);
    }

    private static string WriteRunLog(BackupJobDefinition job, DateTimeOffset started, DateTimeOffset finished, ImageReport report, bool verified, string rootSha)
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
            finished);
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
