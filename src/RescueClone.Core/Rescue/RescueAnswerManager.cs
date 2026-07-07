using System.Text.Json;
using System.Text.Json.Serialization;
using RescueClone.Core.RestorePlanning;

namespace RescueClone.Core.Rescue;

public sealed record RescueAnswerOptions(
    string OutputPath,
    string RepositoryPath,
    string ImagePath,
    string? Password,
    string TargetDiskId,
    RestoreBootMode BootMode,
    long? TargetDiskSizeBytes,
    long? RequiredBytes,
    bool TargetIsCurrentSystemDisk,
    bool HasEfiSystemPartition,
    string? BcdStore,
    IReadOnlyList<string> DriverDirectories,
    IReadOnlyList<string> NetworkShares,
    bool RepairBoot,
    bool RebootAfterRestore,
    bool VerifyImage);

public sealed record RescueAnswerFile(
    int Version,
    string RepositoryPath,
    string ImagePath,
    string? Password,
    string TargetDiskId,
    RestoreBootMode BootMode,
    long? TargetDiskSizeBytes,
    long? RequiredBytes,
    bool TargetIsCurrentSystemDisk,
    bool HasEfiSystemPartition,
    string? BcdStore,
    IReadOnlyList<string> DriverDirectories,
    IReadOnlyList<string> NetworkShares,
    bool RepairBoot,
    bool RebootAfterRestore);

public sealed record RescueAnswerReport(
    string AnswerPath,
    bool Valid,
    IReadOnlyList<string> Blockers,
    RestorePlanReport? RestorePlan,
    bool ImageVerified);

public sealed class RescueAnswerManager
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ImageEngine _imageEngine;
    private readonly RestorePlanner _restorePlanner;

    public RescueAnswerManager(ImageEngine? imageEngine = null, RestorePlanner? restorePlanner = null)
    {
        _imageEngine = imageEngine ?? new ImageEngine();
        _restorePlanner = restorePlanner ?? new RestorePlanner(_imageEngine);
    }

    public RescueAnswerReport Create(RescueAnswerOptions options)
    {
        var answer = BuildAnswer(options);
        var validation = ValidateAnswer(options.OutputPath, answer, options.VerifyImage);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.OutputPath)) ?? ".");
        File.WriteAllText(options.OutputPath, JsonSerializer.Serialize(answer, JsonOptions));
        return validation with { AnswerPath = Path.GetFullPath(options.OutputPath) };
    }

    public RescueAnswerReport Validate(string path, bool verifyImage)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Rescue answer file was not found.", path);
        var answer = JsonSerializer.Deserialize<RescueAnswerFile>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Rescue answer file is empty.");
        return ValidateAnswer(path, answer, verifyImage);
    }

    private RescueAnswerFile BuildAnswer(RescueAnswerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
            throw new ArgumentException("OutputPath is required.");
        if (string.IsNullOrWhiteSpace(options.RepositoryPath))
            throw new ArgumentException("RepositoryPath is required.");
        if (string.IsNullOrWhiteSpace(options.ImagePath))
            throw new ArgumentException("ImagePath is required.");
        if (string.IsNullOrWhiteSpace(options.TargetDiskId))
            throw new ArgumentException("TargetDiskId is required.");

        return new RescueAnswerFile(
            1,
            Path.GetFullPath(options.RepositoryPath),
            ResolveImagePath(options.RepositoryPath, options.ImagePath),
            options.Password,
            options.TargetDiskId.Trim(),
            options.BootMode,
            options.TargetDiskSizeBytes,
            options.RequiredBytes,
            options.TargetIsCurrentSystemDisk,
            options.HasEfiSystemPartition,
            string.IsNullOrWhiteSpace(options.BcdStore) ? null : Path.GetFullPath(options.BcdStore),
            options.DriverDirectories.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath).ToArray(),
            options.NetworkShares.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToArray(),
            options.RepairBoot,
            options.RebootAfterRestore);
    }

    private RescueAnswerReport ValidateAnswer(string path, RescueAnswerFile answer, bool verifyImage)
    {
        var blockers = new List<string>();
        if (answer.Version != 1)
            blockers.Add($"Unsupported answer file version: {answer.Version}.");
        if (!Directory.Exists(answer.RepositoryPath))
            blockers.Add($"Repository does not exist: {answer.RepositoryPath}");
        if (!File.Exists(answer.ImagePath))
            blockers.Add($"Image does not exist: {answer.ImagePath}");
        if (!string.IsNullOrWhiteSpace(answer.RepositoryPath) &&
            !string.IsNullOrWhiteSpace(answer.ImagePath) &&
            Path.GetFullPath(answer.ImagePath).StartsWith(Path.GetFullPath(answer.RepositoryPath), StringComparison.OrdinalIgnoreCase) == false)
            blockers.Add("Image path is outside the configured repository.");
        foreach (var driverDirectory in answer.DriverDirectories)
        {
            if (!Directory.Exists(driverDirectory))
                blockers.Add($"Driver directory does not exist: {driverDirectory}");
        }
        if (string.IsNullOrWhiteSpace(answer.TargetDiskId))
            blockers.Add("Target disk ID is required.");

        RestorePlanReport? restorePlan = null;
        var imageVerified = false;
        if (File.Exists(answer.ImagePath))
        {
            if (verifyImage)
            {
                _imageEngine.Verify(answer.ImagePath, answer.Password);
                imageVerified = true;
            }

            restorePlan = _restorePlanner.Plan(new RestorePlanOptions(
                answer.ImagePath,
                answer.Password,
                answer.TargetDiskId,
                answer.TargetDiskSizeBytes,
                answer.RequiredBytes,
                answer.TargetIsCurrentSystemDisk,
                answer.BootMode,
                answer.HasEfiSystemPartition,
                answer.BcdStore));
            blockers.AddRange(restorePlan.Blockers);
        }

        return new RescueAnswerReport(
            Path.GetFullPath(path),
            blockers.Count == 0,
            blockers,
            restorePlan,
            imageVerified);
    }

    private static string ResolveImagePath(string repositoryPath, string imagePath)
    {
        return Path.IsPathFullyQualified(imagePath)
            ? Path.GetFullPath(imagePath)
            : Path.GetFullPath(Path.Combine(repositoryPath, imagePath));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
