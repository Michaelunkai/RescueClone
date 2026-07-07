using System.Text.Json;
using System.Text.Json.Serialization;
using RescueClone.Core.Jobs;
using RescueClone.Core.Retention;
using RescueClone.Core.RestorePlanning;

namespace RescueClone.Core.Operations;

public sealed class OperationRunner
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ImageEngine _imageEngine;
    private readonly BackupJobRunner _jobRunner;
    private readonly RestorePlanner _restorePlanner;

    public OperationRunner(ImageEngine? imageEngine = null, BackupJobRunner? jobRunner = null, RestorePlanner? restorePlanner = null)
    {
        _imageEngine = imageEngine ?? new ImageEngine();
        _jobRunner = jobRunner ?? new BackupJobRunner(_imageEngine);
        _restorePlanner = restorePlanner ?? new RestorePlanner(_imageEngine);
    }

    public OperationRequest LoadRequest(string path)
    {
        using var input = File.OpenRead(path);
        return JsonSerializer.Deserialize<OperationRequest>(input, JsonOptions)
            ?? throw new InvalidDataException("Operation request is empty.");
    }

    public OperationReport Run(OperationRequest request, string? logDirectory)
    {
        var operationId = string.IsNullOrWhiteSpace(request.OperationId) ? Guid.NewGuid().ToString("N") : request.OperationId;
        var started = DateTimeOffset.UtcNow;
        JsonElement? result = null;
        string? error = null;
        OperationError? errorDetail = null;
        var state = OperationState.Succeeded;
        var auditEvents = new List<OperationAuditEvent>
        {
            new("operation.started", started, $"Operation {operationId} started: {request.Kind}")
        };

        try
        {
            result = ToJsonElement(Dispatch(request));
            auditEvents.Add(new OperationAuditEvent("operation.succeeded", DateTimeOffset.UtcNow, $"Operation {operationId} succeeded: {request.Kind}"));
        }
        catch (Exception ex)
        {
            state = OperationState.Failed;
            error = ex.Message;
            errorDetail = ClassifyError(ex);
            auditEvents.Add(new OperationAuditEvent("operation.failed", DateTimeOffset.UtcNow, $"Operation {operationId} failed: {ex.Message}"));
        }

        var finished = DateTimeOffset.UtcNow;
        var report = new OperationReport(operationId, request.Kind, state, started, finished, null, result, error, AuditEvents: auditEvents, ErrorDetail: errorDetail);
        var logPath = WriteReport(report, logDirectory);
        var reportWithLog = report with { LogPath = logPath };
        var recoveryStatePath = WriteRecoveryState(request, reportWithLog, logDirectory);
        var finalReport = reportWithLog with { RecoveryStatePath = recoveryStatePath };
        if (!string.IsNullOrWhiteSpace(logPath))
            File.WriteAllText(logPath, JsonSerializer.Serialize(finalReport, JsonOptions));
        return finalReport;
    }

    private object Dispatch(OperationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Kind))
            throw new ArgumentException("Operation kind is required.");
        if (request.Parameters is null)
            throw new ArgumentException("Operation parameters are required.");

        return request.Kind switch
        {
            "image.create.directory" => _imageEngine.Create(new ImageOptions(
                RequiredString(request, "source"),
                RequiredString(request, "image"),
                EnumValue<CompressionMode>(request, "compression", CompressionMode.Medium),
                OptionalString(request, "password"),
                EnumValue<ImageContainerFormat>(request, "format", ImageContainerFormat.V2))),
            "image.verify" => _imageEngine.Verify(
                RequiredString(request, "image"),
                OptionalString(request, "password")),
            "image.browse" => _imageEngine.Browse(
                RequiredString(request, "image"),
                OptionalString(request, "password")),
            "image.extract.directory" => _imageEngine.Extract(new ExtractOptions(
                RequiredString(request, "image"),
                RequiredString(request, "target"),
                StringListValue(request, "paths"),
                OptionalString(request, "password"),
                BoolValue(request, "overwrite"))),
            "image.project.readonly" => new ImageProjectionManager(_imageEngine).Project(new ImageProjectionOptions(
                RequiredString(request, "image"),
                RequiredString(request, "target"),
                OptionalString(request, "password"),
                BoolValue(request, "overwrite"))),
            "image.project.list" => new ImageProjectionManager(_imageEngine).List(new ImageProjectionListOptions(
                RequiredString(request, "root"))),
            "image.project.remove" => new ImageProjectionManager(_imageEngine).Unproject(new ImageUnprojectionOptions(
                RequiredString(request, "target"))),
            "image.restore.directory" => _imageEngine.Restore(new RestoreOptions(
                RequiredString(request, "image"),
                RequiredString(request, "target"),
                OptionalString(request, "password"),
                BoolValue(request, "overwrite"))),
            "job.backup.directory.create" => _jobRunner.Save(
                RequiredString(request, "file"),
                ReadBackupJobDefinition(request)),
            "job.backup.directory.update" => _jobRunner.Update(
                RequiredString(request, "file"),
                new BackupJobUpdateOptions(
                    OptionalString(request, "jobId"),
                    OptionalString(request, "name"),
                    NullableBoolValue(request, "enabled"),
                    OptionalString(request, "source"),
                    OptionalString(request, "image"),
                    NullableEnumValue<CompressionMode>(request, "compression"),
                    OptionalString(request, "password"),
                    NullableBoolValue(request, "verifyAfterCreate"),
                    OptionalString(request, "logDirectory"))),
            "job.backup.directory.delete" => _jobRunner.Delete(RequiredString(request, "file")),
            "job.backup.directory.export" => _jobRunner.Export(
                RequiredString(request, "file"),
                RequiredString(request, "output")),
            "job.backup.directory.import" => _jobRunner.Import(
                RequiredString(request, "file"),
                RequiredString(request, "target")),
            "job.backup.directory.status" => _jobRunner.Status(RequiredString(request, "file")),
            "job.backup.directory.validate" => _jobRunner.Validate(_jobRunner.Load(RequiredString(request, "file"))),
            "job.backup.directory.run" => _jobRunner.Run(_jobRunner.Load(RequiredString(request, "file")), BoolValue(request, "forceDisabled")),
            "retention.plan" => new RetentionManager().Plan(new RetentionOptions(
                RequiredString(request, "repository"),
                OptionalString(request, "pattern") ?? "*.rcimg",
                IntValue(request, "keepCount"),
                IntValue(request, "maxAgeDays"),
                LongValue(request, "minFreeBytes"))),
            "retention.apply" => new RetentionManager().Apply(new RetentionOptions(
                RequiredString(request, "repository"),
                OptionalString(request, "pattern") ?? "*.rcimg",
                IntValue(request, "keepCount"),
                IntValue(request, "maxAgeDays"),
                LongValue(request, "minFreeBytes"))),
            "restore.plan.readonly" => _restorePlanner.Plan(new RestorePlanOptions(
                RequiredString(request, "image"),
                OptionalString(request, "password"),
                RequiredString(request, "targetDiskId"),
                LongValue(request, "targetDiskSizeBytes"),
                LongValue(request, "requiredBytes"),
                BoolValue(request, "targetIsCurrentSystemDisk"),
                EnumValue<RestoreBootMode>(request, "bootMode", RestoreBootMode.Unknown),
                BoolValue(request, "hasEfiSystemPartition"),
                OptionalString(request, "bcdStore"))),
            _ => throw new ArgumentException($"Unknown operation kind: {request.Kind}")
        };
    }

    private static BackupJobDefinition ReadBackupJobDefinition(OperationRequest request)
    {
        return new BackupJobDefinition(
            RequiredString(request, "jobId"),
            RequiredString(request, "name"),
            BoolValue(request, "enabled", defaultValue: true),
            RequiredString(request, "source"),
            RequiredString(request, "image"),
            EnumValue<CompressionMode>(request, "compression", CompressionMode.Medium),
            OptionalString(request, "password"),
            BoolValue(request, "verifyAfterCreate", defaultValue: true),
            OptionalString(request, "logDirectory"));
    }

    private static string? WriteReport(OperationReport report, string? logDirectory)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
            return null;

        Directory.CreateDirectory(logDirectory);
        var safeId = string.Join("_", report.OperationId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = Path.Combine(logDirectory, $"{safeId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report with { LogPath = path }, JsonOptions));
        return path;
    }

    private static string? WriteRecoveryState(OperationRequest request, OperationReport report, string? logDirectory)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
            return null;

        Directory.CreateDirectory(logDirectory);
        var safeId = SafeFileName(report.OperationId);
        var path = Path.Combine(logDirectory, $"{safeId}.state.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new OperationRecoveryState(request, report, DateTimeOffset.UtcNow), JsonOptions));
        return path;
    }

    private static JsonElement ToJsonElement(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        return document.RootElement.Clone();
    }

    private static OperationError ClassifyError(Exception ex)
    {
        var code = ex switch
        {
            FileNotFoundException or DirectoryNotFoundException => "not_found",
            ArgumentException => "invalid_request",
            InvalidOperationException => "operation_failed",
            InvalidDataException => "invalid_data",
            UnauthorizedAccessException => "access_denied",
            IOException => "io_error",
            _ => "unexpected_error"
        };
        return new OperationError(code, ex.Message, ex.GetType().Name);
    }

    private static string RequiredString(OperationRequest request, string name)
    {
        var value = OptionalString(request, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Operation parameter is required: {name}");
        return value;
    }

    private static string? OptionalString(OperationRequest request, string name)
    {
        if (!request.Parameters.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.GetString();
    }

    private static string SafeFileName(string value)
    {
        var safeId = string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safeId) ? "operation" : safeId;
    }

    private static bool BoolValue(OperationRequest request, string name, bool defaultValue = false)
    {
        if (!request.Parameters.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return defaultValue;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.Parse(value.GetString() ?? "false"),
            _ => throw new ArgumentException($"Operation parameter must be a Boolean: {name}")
        };
    }

    private static IReadOnlyList<string> StringListValue(OperationRequest request, string name)
    {
        if (!request.Parameters.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw new ArgumentException($"Operation parameter is required: {name}");

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            JsonValueKind.Array => value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String
                    ? item.GetString()!
                    : throw new ArgumentException($"Operation parameter array must contain strings: {name}"))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray(),
            _ => throw new ArgumentException($"Operation parameter must be a string or string array: {name}")
        };
    }

    private static bool? NullableBoolValue(OperationRequest request, string name)
    {
        if (!request.Parameters.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return BoolValue(request, name);
    }

    private static long? LongValue(OperationRequest request, string name)
    {
        if (!request.Parameters.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt64(),
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
            _ => throw new ArgumentException($"Operation parameter must be a 64-bit integer: {name}")
        };
    }

    private static int? IntValue(OperationRequest request, string name)
    {
        if (!request.Parameters.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => throw new ArgumentException($"Operation parameter must be a 32-bit integer: {name}")
        };
    }

    private static T EnumValue<T>(OperationRequest request, string name, T defaultValue) where T : struct
    {
        var value = OptionalString(request, name);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return Enum.Parse<T>(value, ignoreCase: true);
    }

    private static T? NullableEnumValue<T>(OperationRequest request, string name) where T : struct
    {
        var value = OptionalString(request, name);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Enum.Parse<T>(value, ignoreCase: true);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
