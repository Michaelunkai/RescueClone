using System.Text.Json;
using System.Text.Json.Serialization;
using RescueClone.Core;
using RescueClone.Core.Jobs;
using RescueClone.Core.Logs;
using RescueClone.Core.Native;
using RescueClone.Core.Operations;
using RescueClone.Core.Retention;
using RescueClone.Core.RestorePlanning;
using RescueClone.Core.Scheduling;
using RescueClone.Core.Storage;

var exitCode = Run(args);
return exitCode;

static int Run(string[] args)
{
    try
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] == "features")
        {
            FeatureCatalog.AssertImplementedParity();
            WriteJson(FeatureCatalog.All);
            return 0;
        }

        if (args.Length >= 2 && args[0] == "job")
            return RunJob(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "restore")
            return RunRestore(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "retention")
            return RunRetention(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "schedule")
            return RunSchedule(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "operation")
            return RunOperation(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "logs")
            return RunLogs(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "storage")
            return RunStorage(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "native")
            return RunNative(args[1]);

        if (args.Length < 2 || args[0] != "image")
            throw new ArgumentException("Expected: rc image <create|verify|browse|extract|restore>, rc job <validate|run>, rc retention <plan|apply>, rc schedule <plan|register|unregister>, rc restore <plan>, rc operation <run>, rc logs <list>, rc storage <volumes>, or rc native <status>.");

        var command = args[1];
        var values = ParseOptions(args.Skip(2).ToArray());
        var engine = new ImageEngine();

        switch (command)
        {
            case "create":
                var create = new ImageOptions(
                    Required(values, "source"),
                    Required(values, "image"),
                    Enum.Parse<CompressionMode>(values.GetValueOrDefault("compression", "Medium"), ignoreCase: true),
                    values.GetValueOrDefault("password"),
                    Enum.Parse<ImageContainerFormat>(values.GetValueOrDefault("format", "V2"), ignoreCase: true));
                WriteJson(engine.Create(create));
                return 0;

            case "verify":
                WriteJson(engine.Verify(Required(values, "image"), values.GetValueOrDefault("password")));
                return 0;

            case "browse":
                WriteJson(engine.Browse(Required(values, "image"), values.GetValueOrDefault("password")));
                return 0;

            case "extract":
                var extract = new ExtractOptions(
                    Required(values, "image"),
                    Required(values, "target"),
                    SplitPaths(Required(values, "paths")),
                    values.GetValueOrDefault("password"),
                    values.ContainsKey("overwrite"));
                WriteJson(engine.Extract(extract));
                return 0;

            case "restore":
                var restore = new RestoreOptions(
                    Required(values, "image"),
                    Required(values, "target"),
                    values.GetValueOrDefault("password"),
                    values.ContainsKey("overwrite"));
                WriteJson(engine.Restore(restore));
                return 0;

            default:
                throw new ArgumentException($"Unknown image command: {command}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 2;
    }
}

static int RunSchedule(string command, Dictionary<string, string> values)
{
    var manager = new ScheduleManager();
    if (command == "unregister")
    {
        var report = manager.Unregister(Required(values, "task-name"));
        WriteJson(report);
        return report.Succeeded ? 0 : 3;
    }

    var definition = new ScheduleDefinition(
        Required(values, "task-name"),
        Required(values, "job-file"),
        values.GetValueOrDefault("cli-path", Environment.ProcessPath ?? "rc.exe"),
        Enum.Parse<ScheduleFrequency>(values.GetValueOrDefault("frequency", "Daily"), ignoreCase: true),
        TimeOnly.Parse(values.GetValueOrDefault("time", "02:00")),
        values.ContainsKey("run-missed"),
        values.GetValueOrDefault("event-log"),
        TryParseInt(values, "event-id"),
        values.GetValueOrDefault("event-source"));

    switch (command)
    {
        case "plan":
            WriteJson(manager.Plan(definition));
            return 0;
        case "register":
            var report = manager.Register(definition);
            WriteJson(report);
            return report.Succeeded ? 0 : 3;
        default:
            throw new ArgumentException($"Unknown schedule command: {command}");
    }
}

static int RunRetention(string command, Dictionary<string, string> values)
{
    var manager = new RetentionManager();
    var options = new RetentionOptions(
        Required(values, "repository"),
        values.GetValueOrDefault("pattern", "*.rcimg"),
        TryParseInt(values, "keep-count"),
        TryParseInt(values, "max-age-days"),
        TryParseLong(values, "min-free-bytes"));

    switch (command)
    {
        case "plan":
            WriteJson(manager.Plan(options));
            return 0;
        case "apply":
            WriteJson(manager.Apply(options));
            return 0;
        default:
            throw new ArgumentException($"Unknown retention command: {command}");
    }
}

static int RunStorage(string command, Dictionary<string, string> values)
{
    switch (command)
    {
        case "volumes":
            WriteJson(new VolumeEnumerator().ListVolumes());
            return 0;
        case "disks":
            WriteJson(new DiskEnumerator().ListDisks());
            return 0;
        case "disk-safety":
            WriteJson(new DiskTargetSafetyEvaluator().Evaluate(
                new DiskEnumerator().ListDisks(),
                new DiskTargetSafetyOptions(
                    int.Parse(Required(values, "disk-number")),
                    values.GetValueOrDefault("expected-fingerprint"),
                    values.ContainsKey("allow-boot-system"))));
            return 0;
        default:
            throw new ArgumentException($"Unknown storage command: {command}");
    }
}

static int RunNative(string command)
{
    if (command != "status")
        throw new ArgumentException($"Unknown native command: {command}");
    WriteJson(NativeDiagnostics.GetStatus());
    return 0;
}

static int RunOperation(string command, Dictionary<string, string> values)
{
    if (command != "run")
        throw new ArgumentException($"Unknown operation command: {command}");

    var runner = new OperationRunner();
    var request = runner.LoadRequest(Required(values, "request"));
    WriteJson(runner.Run(request, values.GetValueOrDefault("log-directory")));
    return 0;
}

static int RunLogs(string command, Dictionary<string, string> values)
{
    if (command != "list")
        throw new ArgumentException($"Unknown logs command: {command}");

    WriteJson(new BackupLogCatalog().List(new LogListOptions(
        Required(values, "directory"),
        values.GetValueOrDefault("pattern", "*.json"))));
    return 0;
}

static int RunRestore(string command, Dictionary<string, string> values)
{
    if (command != "plan")
        throw new ArgumentException($"Unknown restore command: {command}");

    var options = new RestorePlanOptions(
        Required(values, "image"),
        values.GetValueOrDefault("password"),
        Required(values, "target-disk-id"),
        TryParseLong(values, "target-disk-size-bytes"),
        TryParseLong(values, "required-bytes"),
        values.ContainsKey("target-is-current-system-disk"),
        Enum.Parse<RestoreBootMode>(values.GetValueOrDefault("boot-mode", "Unknown"), ignoreCase: true),
        values.ContainsKey("has-efi-system-partition"),
        values.GetValueOrDefault("bcd-store"));
    WriteJson(new RestorePlanner().Plan(options));
    return 0;
}

static int RunJob(string command, Dictionary<string, string> values)
{
    var runner = new BackupJobRunner();
    switch (command)
    {
        case "create":
            var definition = new BackupJobDefinition(
                Required(values, "job-id"),
                Required(values, "name"),
                ParseBool(values.GetValueOrDefault("enabled", "true"), "enabled"),
                Required(values, "source"),
                Required(values, "image"),
                Enum.Parse<CompressionMode>(values.GetValueOrDefault("compression", "Medium"), ignoreCase: true),
                values.GetValueOrDefault("password"),
                ParseBool(values.GetValueOrDefault("verify-after-create", "true"), "verify-after-create"),
                values.GetValueOrDefault("log-directory"));
            WriteJson(runner.Save(Required(values, "file"), definition));
            return 0;
        case "delete":
            WriteJson(runner.Delete(Required(values, "file")));
            return 0;
        case "update":
            WriteJson(runner.Update(Required(values, "file"), new BackupJobUpdateOptions(
                values.GetValueOrDefault("job-id"),
                values.GetValueOrDefault("name"),
                values.ContainsKey("enabled") ? ParseBool(values["enabled"], "enabled") : null,
                values.GetValueOrDefault("source"),
                values.GetValueOrDefault("image"),
                values.ContainsKey("compression") ? Enum.Parse<CompressionMode>(values["compression"], ignoreCase: true) : null,
                values.GetValueOrDefault("password"),
                values.ContainsKey("verify-after-create") ? ParseBool(values["verify-after-create"], "verify-after-create") : null,
                values.GetValueOrDefault("log-directory"))));
            return 0;
        case "export":
            WriteJson(runner.Export(Required(values, "file"), Required(values, "output")));
            return 0;
        case "import":
            WriteJson(runner.Import(Required(values, "file"), Required(values, "target")));
            return 0;
        case "status":
            WriteJson(runner.Status(Required(values, "file")));
            return 0;
        case "validate":
            var job = runner.Load(Required(values, "file"));
            WriteJson(runner.Validate(job));
            return 0;
        case "run":
            job = runner.Load(Required(values, "file"));
            WriteJson(runner.Run(job, values.ContainsKey("force-disabled")));
            return 0;
        default:
            throw new ArgumentException($"Unknown job command: {command}");
    }
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Unexpected argument: {arg}");
        var name = arg[2..];
        if (name is "overwrite" or "force-disabled" or "target-is-current-system-disk" or "has-efi-system-partition" or "run-missed" or "allow-boot-system")
        {
            values[name] = "true";
            continue;
        }
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {arg}");
        values[name] = args[++i];
    }
    return values;
}

static long? TryParseLong(Dictionary<string, string> values, string key)
{
    if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        return null;
    if (!long.TryParse(value, out var parsed))
        throw new ArgumentException($"Invalid --{key}: {value}");
    return parsed;
}

static int? TryParseInt(Dictionary<string, string> values, string key)
{
    if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        return null;
    if (!int.TryParse(value, out var parsed))
        throw new ArgumentException($"Invalid --{key}: {value}");
    return parsed;
}

static bool ParseBool(string value, string key)
{
    if (!bool.TryParse(value, out var parsed))
        throw new ArgumentException($"Invalid --{key}: {value}");
    return parsed;
}

static IReadOnlyList<string> SplitPaths(string value)
{
    return value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static string Required(Dictionary<string, string> values, string key)
{
    if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        throw new ArgumentException($"Missing --{key}.");
    return value;
}

static void WriteJson<T>(T value)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    Console.WriteLine(JsonSerializer.Serialize(value, options));
}

static void PrintHelp()
{
    Console.WriteLine("""
    RescueClone CLI

    rc features
    rc image create --source <dir> --image <file.rcimg> [--compression None|Medium|High] [--password <secret>] [--format V1|V2]
    rc image verify --image <file.rcimg> [--password <secret>]
    rc image browse --image <file.rcimg> [--password <secret>]
    rc image extract --image <file.rcimg> --target <dir> --paths <relative-paths> [--password <secret>] [--overwrite]
    rc image restore --image <file.rcimg> --target <dir> [--password <secret>] [--overwrite]
    rc job create --file <job.json> --job-id <id> --name <name> --source <dir> --image <file.rcimg> [--compression None|Medium|High] [--password <secret>] [--verify-after-create true|false] [--log-directory <dir>]
    rc job update --file <job.json> [--job-id <id>] [--name <name>] [--enabled true|false] [--source <dir>] [--image <file.rcimg>] [--compression None|Medium|High] [--password <secret>] [--verify-after-create true|false] [--log-directory <dir>]
    rc job delete --file <job.json>
    rc job export --file <job.json> --output <exported-job.json>
    rc job import --file <exported-job.json> --target <job.json>
    rc job status --file <job.json>
    rc job validate --file <job.json>
    rc job run --file <job.json> [--force-disabled]
    rc retention plan --repository <dir> [--pattern *.rcimg] [--keep-count <n>] [--max-age-days <n>] [--min-free-bytes <n>]
    rc retention apply --repository <dir> [--pattern *.rcimg] [--keep-count <n>] [--max-age-days <n>] [--min-free-bytes <n>]
    rc schedule plan --task-name <name> --job-file <job.json> [--cli-path <rc.exe>] [--frequency Daily|Weekly|Monthly|Event] [--time HH:mm] [--run-missed] [--event-log <log>] [--event-id <id>] [--event-source <source>]
    rc schedule register --task-name <name> --job-file <job.json> [--cli-path <rc.exe>] [--frequency Daily|Weekly|Monthly|Event] [--time HH:mm] [--run-missed] [--event-log <log>] [--event-id <id>] [--event-source <source>]
    rc schedule unregister --task-name <name>
    rc restore plan --image <file.rcimg> --target-disk-id <id> --boot-mode Bios|Uefi --bcd-store <path> [--password <secret>] [--target-disk-size-bytes <n>] [--required-bytes <n>] [--target-is-current-system-disk] [--has-efi-system-partition]
    rc operation run --request <operation.json> [--log-directory <dir>]
    rc logs list --directory <dir> [--pattern *.json]
    rc storage volumes
    rc storage disks
    rc storage disk-safety --disk-number <n> [--expected-fingerprint <sha256>] [--allow-boot-system]
    rc native status
    """);
}
