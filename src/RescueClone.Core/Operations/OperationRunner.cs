using System.Text.Json;
using RescueClone.Core.Jobs;
using RescueClone.Core.RestorePlanning;

namespace RescueClone.Core.Operations;

public sealed class OperationRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
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
        var state = OperationState.Succeeded;

        try
        {
            result = ToJsonElement(Dispatch(request));
        }
        catch (Exception ex)
        {
            state = OperationState.Failed;
            error = ex.Message;
        }

        var finished = DateTimeOffset.UtcNow;
        var report = new OperationReport(operationId, request.Kind, state, started, finished, null, result, error);
        var logPath = WriteReport(report, logDirectory);
        return report with { LogPath = logPath };
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
                OptionalString(request, "password"))),
            "image.verify" => _imageEngine.Verify(
                RequiredString(request, "image"),
                OptionalString(request, "password")),
            "image.restore.directory" => _imageEngine.Restore(new RestoreOptions(
                RequiredString(request, "image"),
                RequiredString(request, "target"),
                OptionalString(request, "password"),
                BoolValue(request, "overwrite"))),
            "job.backup.directory.validate" => _jobRunner.Validate(_jobRunner.Load(RequiredString(request, "file"))),
            "job.backup.directory.run" => _jobRunner.Run(_jobRunner.Load(RequiredString(request, "file")), BoolValue(request, "forceDisabled")),
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

    private static JsonElement ToJsonElement(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        return document.RootElement.Clone();
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

    private static bool BoolValue(OperationRequest request, string name)
    {
        if (!request.Parameters.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.Parse(value.GetString() ?? "false"),
            _ => throw new ArgumentException($"Operation parameter must be a Boolean: {name}")
        };
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

    private static T EnumValue<T>(OperationRequest request, string name, T defaultValue) where T : struct
    {
        var value = OptionalString(request, name);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return Enum.Parse<T>(value, ignoreCase: true);
    }
}
