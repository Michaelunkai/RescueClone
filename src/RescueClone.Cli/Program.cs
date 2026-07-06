using System.Text.Json;
using RescueClone.Core;
using RescueClone.Core.Jobs;
using RescueClone.Core.RestorePlanning;

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

        if (args.Length < 2 || args[0] != "image")
            throw new ArgumentException("Expected: rc image <create|verify|restore>, rc job <validate|run>, or rc restore <plan>.");

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
                    values.GetValueOrDefault("password"));
                WriteJson(engine.Create(create));
                return 0;

            case "verify":
                WriteJson(engine.Verify(Required(values, "image"), values.GetValueOrDefault("password")));
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
    var job = runner.Load(Required(values, "file"));
    switch (command)
    {
        case "validate":
            WriteJson(runner.Validate(job));
            return 0;
        case "run":
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
        if (name is "overwrite" or "force-disabled" or "target-is-current-system-disk" or "has-efi-system-partition")
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

static string Required(Dictionary<string, string> values, string key)
{
    if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        throw new ArgumentException($"Missing --{key}.");
    return value;
}

static void WriteJson<T>(T value)
{
    Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}

static void PrintHelp()
{
    Console.WriteLine("""
    RescueClone CLI

    rc features
    rc image create --source <dir> --image <file.rcimg> [--compression None|Medium|High] [--password <secret>]
    rc image verify --image <file.rcimg> [--password <secret>]
    rc image restore --image <file.rcimg> --target <dir> [--password <secret>] [--overwrite]
    rc job validate --file <job.json>
    rc job run --file <job.json> [--force-disabled]
    rc restore plan --image <file.rcimg> --target-disk-id <id> --boot-mode Bios|Uefi --bcd-store <path> [--password <secret>] [--target-disk-size-bytes <n>] [--required-bytes <n>] [--target-is-current-system-disk] [--has-efi-system-partition]
    """);
}
