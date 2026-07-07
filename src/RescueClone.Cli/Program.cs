using System.Text.Json;
using System.Text.Json.Serialization;
using RescueClone.Core;
using RescueClone.Core.Cloning;
using RescueClone.Core.Jobs;
using RescueClone.Core.Logs;
using RescueClone.Core.Native;
using RescueClone.Core.Operations;
using RescueClone.Core.Retention;
using RescueClone.Core.Rescue;
using RescueClone.Core.RestorePlanning;
using RescueClone.Core.Scheduling;
using RescueClone.Core.Services;
using RescueClone.Core.Storage;
using System.Runtime.Versioning;
using System.ServiceProcess;

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

        if (args.Length >= 2 && args[0] == "clone")
            return RunClone(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "restore")
            return RunRestore(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "rescue")
            return RunRescue(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "retention")
            return RunRetention(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "schedule")
            return RunSchedule(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "operation")
            return RunOperation(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "service")
            return RunService(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "logs")
            return RunLogs(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "storage")
            return RunStorage(args[1], ParseOptions(args.Skip(2).ToArray()));

        if (args.Length >= 2 && args[0] == "native")
            return RunNative(args[1]);

        if (args.Length < 2 || args[0] != "image")
            throw new ArgumentException("Expected: rc image <create|verify|browse|extract|restore>, rc clone <directory>, rc job <validate|run>, rc retention <plan|apply>, rc schedule <plan|register|unregister>, rc restore <plan>, rc rescue <answer-create|answer-validate|answer-execute>, rc operation <kinds|validate|run>, rc service <serve|host|run-operation|plan-install|install|uninstall|start|stop|status|recovery|recovery-status>, rc logs <list>, rc storage <volumes>, or rc native <status>.");

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

            case "list":
                WriteJson(new ImageRepositoryCatalog(engine).List(new ImageRepositoryListOptions(
                    Required(values, "repository"),
                    values.GetValueOrDefault("pattern", "*.rcimg"),
                    values.ContainsKey("verify"),
                    values.GetValueOrDefault("password"))));
                return 0;

            case "audit":
                var audit = new ImageRepositoryCatalog(engine).Audit(new ImageRepositoryAuditOptions(
                    Required(values, "repository"),
                    values.GetValueOrDefault("pattern", "*.rcimg"),
                    values.GetValueOrDefault("password")));
                WriteJson(audit);
                return audit.FailedCount == 0 ? 0 : 3;

            case "protect-audit":
                WriteJson(new ImageRepositoryCatalog(engine).AuditProtection(new ImageRepositoryProtectionOptions(
                    Required(values, "repository"),
                    values.GetValueOrDefault("pattern", "*.rcimg"))));
                return 0;

            case "protect":
                WriteJson(new ImageRepositoryCatalog(engine).ApplyProtection(new ImageRepositoryProtectionOptions(
                    Required(values, "repository"),
                    values.GetValueOrDefault("pattern", "*.rcimg"))));
                return 0;

            case "compare":
                var compare = new ImageComparer(engine).Compare(new ImageCompareOptions(
                    Required(values, "image"),
                    Required(values, "source"),
                    values.GetValueOrDefault("password")));
                WriteJson(compare);
                return compare.Equivalent ? 0 : 4;

            case "extract":
                var extract = new ExtractOptions(
                    Required(values, "image"),
                    Required(values, "target"),
                    SplitPaths(Required(values, "paths")),
                    values.GetValueOrDefault("password"),
                    values.ContainsKey("overwrite"));
                WriteJson(engine.Extract(extract));
                return 0;

            case "project":
                WriteJson(new ImageProjectionManager(engine).Project(new ImageProjectionOptions(
                    Required(values, "image"),
                    Required(values, "target"),
                    values.GetValueOrDefault("password"),
                    values.ContainsKey("overwrite"))));
                return 0;

            case "projections":
                WriteJson(new ImageProjectionManager(engine).List(new ImageProjectionListOptions(
                    Required(values, "root"))));
                return 0;

            case "unproject":
                WriteJson(new ImageProjectionManager(engine).Unproject(new ImageUnprojectionOptions(
                    Required(values, "target"))));
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
        if (command == "status")
        {
            var report = manager.Status(Required(values, "task-name"));
            WriteJson(report);
            return report.Succeeded ? 0 : 3;
        }
        if (command == "run")
        {
            var report = manager.RunNow(Required(values, "task-name"));
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

static int RunClone(string command, Dictionary<string, string> values)
{
    if (command != "directory")
        throw new ArgumentException($"Unknown clone command: {command}");

    WriteJson(new DirectoryCloneManager().Clone(new DirectoryCloneOptions(
        Required(values, "source"),
        Required(values, "target"),
        values.ContainsKey("overwrite"))));
    return 0;
}

static int RunRetention(string command, Dictionary<string, string> values)
{
    var manager = new RetentionManager();
    if (command is "gfs-plan" or "gfs-apply")
    {
        var gfsOptions = new GfsRetentionOptions(
            Required(values, "repository"),
            values.GetValueOrDefault("pattern", "*.rcimg"),
            TryParseInt(values, "daily-keep"),
            TryParseInt(values, "weekly-keep"),
            TryParseInt(values, "monthly-keep"));
        if (command == "gfs-plan")
        {
            WriteJson(manager.PlanGfs(gfsOptions));
            return 0;
        }
        WriteJson(manager.ApplyGfs(gfsOptions));
        return 0;
    }

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

static int RunRescue(string command, Dictionary<string, string> values)
{
    var manager = new RescueAnswerManager();
    switch (command)
    {
        case "answer-create":
            WriteJson(manager.Create(new RescueAnswerOptions(
                Required(values, "output"),
                Required(values, "repository"),
                Required(values, "image"),
                values.GetValueOrDefault("password"),
                Required(values, "target-disk-id"),
                Enum.Parse<RestoreBootMode>(values.GetValueOrDefault("boot-mode", "Unknown"), ignoreCase: true),
                TryParseLong(values, "target-disk-size-bytes"),
                TryParseLong(values, "required-bytes"),
                values.ContainsKey("target-is-current-system-disk"),
                values.ContainsKey("has-efi-system-partition"),
                values.GetValueOrDefault("bcd-store"),
                SplitPaths(values.GetValueOrDefault("driver-directories", string.Empty)),
                SplitPaths(values.GetValueOrDefault("network-shares", string.Empty)),
                ParseBool(values.GetValueOrDefault("repair-boot", "true"), "repair-boot"),
                values.ContainsKey("reboot-after-restore"),
                values.ContainsKey("verify-image"),
                values.GetValueOrDefault("directory-restore-target"))));
            return 0;
        case "answer-validate":
            var report = manager.Validate(Required(values, "file"), values.ContainsKey("verify-image"));
            WriteJson(report);
            return report.Valid ? 0 : 3;
        case "answer-execute":
            var execution = manager.Execute(
                Required(values, "file"),
                values.ContainsKey("verify-image"),
                values.ContainsKey("overwrite"));
            WriteJson(execution);
            return execution.Valid ? 0 : 3;
        default:
            throw new ArgumentException($"Unknown rescue command: {command}");
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
    if (command == "kinds")
    {
        OperationKindCatalog.AssertUniqueKinds();
        WriteJson(OperationKindCatalog.All);
        return 0;
    }

    if (command == "validate")
    {
        var validator = new OperationRunner();
        var report = validator.Validate(validator.LoadRequest(Required(values, "request")));
        WriteJson(report);
        return report.Valid ? 0 : 3;
    }

    if (command != "run")
        throw new ArgumentException($"Unknown operation command: {command}");

    var runner = new OperationRunner();
    var request = runner.LoadRequest(Required(values, "request"));
    WriteJson(runner.Run(request, values.GetValueOrDefault("log-directory")));
    return 0;
}

static int RunService(string command, Dictionary<string, string> values)
{
    var manager = new WindowsServiceManager();
    switch (command)
    {
        case "serve":
            using (var cancellation = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };
                new OperationPipeServer().RunAsync(
                    Required(values, "pipe"),
                    values.GetValueOrDefault("log-directory"),
                    cancellation.Token).GetAwaiter().GetResult();
            }
            return 0;
        case "host":
            if (Environment.UserInteractive)
            {
                using var cancellation = new CancellationTokenSource();
                Console.CancelKeyPress += (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };
                new OperationPipeServer().RunAsync(
                    Required(values, "pipe"),
                    values.GetValueOrDefault("log-directory"),
                    cancellation.Token).GetAwaiter().GetResult();
                return 0;
            }
            ServiceBase.Run(new OperationWindowsService(
                Required(values, "pipe"),
                values.GetValueOrDefault("log-directory")));
            return 0;
        case "run-operation":
            var runner = new OperationRunner();
            var request = runner.LoadRequest(Required(values, "request"));
            var response = new OperationPipeClient().RunOperationAsync(
                Required(values, "pipe"),
                new OperationServiceRequest(request, values.GetValueOrDefault("log-directory")),
                TimeSpan.FromMilliseconds(TryParseInt(values, "timeout-ms") ?? 30000),
                CancellationToken.None).GetAwaiter().GetResult();
            if (!response.Succeeded)
                throw new InvalidOperationException(response.Error ?? "Operation service request failed.");
            if (response.Report is null)
                throw new InvalidDataException("Operation service returned no report.");
            WriteJson(response.Report);
            return response.Report.State == OperationState.Succeeded ? 0 : 3;
        case "plan-install":
            WriteJson(manager.Plan(ReadServiceInstallDefinition(values)));
            return 0;
        case "install":
            var install = manager.Install(ReadServiceInstallDefinition(values));
            WriteJson(install);
            return install.Succeeded ? 0 : 3;
        case "uninstall":
            var uninstall = manager.Uninstall(Required(values, "name"));
            WriteJson(uninstall);
            return uninstall.Succeeded ? 0 : 3;
        case "start":
            var start = manager.Start(Required(values, "name"));
            WriteJson(start);
            return start.Succeeded ? 0 : 3;
        case "stop":
            var stop = manager.Stop(Required(values, "name"));
            WriteJson(stop);
            return stop.Succeeded ? 0 : 3;
        case "status":
            WriteJson(manager.Status(Required(values, "name")));
            return 0;
        case "recovery":
            var recovery = manager.ConfigureRecovery(new WindowsServiceRecoveryOptions(
                Required(values, "name"),
                TryParseInt(values, "reset-period-seconds") ?? 86400,
                TryParseInt(values, "restart-delay-ms") ?? 60000,
                ParseBool(values.GetValueOrDefault("restart-on-failure", "true"), "restart-on-failure")));
            WriteJson(recovery);
            return recovery.Succeeded ? 0 : 3;
        case "recovery-status":
            WriteJson(manager.GetRecovery(Required(values, "name")));
            return 0;
        default:
            throw new ArgumentException($"Unknown service command: {command}");
    }
}

static WindowsServiceInstallDefinition ReadServiceInstallDefinition(Dictionary<string, string> values)
{
    return new WindowsServiceInstallDefinition(
        Required(values, "name"),
        values.GetValueOrDefault("cli-path", Environment.ProcessPath ?? "rc.exe"),
        Required(values, "pipe"),
        values.GetValueOrDefault("log-directory"),
        values.GetValueOrDefault("display-name"),
        values.GetValueOrDefault("start-mode", "auto"));
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
            definition = ApplyAdvancedJobOptionsFromValues(runner, definition, values);
            WriteJson(runner.Save(Required(values, "file"), definition));
            return 0;
        case "delete":
            WriteJson(runner.Delete(Required(values, "file")));
            return 0;
        case "update":
            var jobFile = Required(values, "file");
            var updated = runner.Update(jobFile, new BackupJobUpdateOptions(
                values.GetValueOrDefault("job-id"),
                values.GetValueOrDefault("name"),
                values.ContainsKey("enabled") ? ParseBool(values["enabled"], "enabled") : null,
                values.GetValueOrDefault("source"),
                values.GetValueOrDefault("image"),
                values.ContainsKey("compression") ? Enum.Parse<CompressionMode>(values["compression"], ignoreCase: true) : null,
                values.GetValueOrDefault("password"),
                values.ContainsKey("verify-after-create") ? ParseBool(values["verify-after-create"], "verify-after-create") : null,
                values.GetValueOrDefault("log-directory")));
            if (values.TryGetValue("advanced-json-file", out var advancedPath))
            {
                var after = runner.ApplyAdvancedOptions(updated.After, runner.LoadAdvancedOptions(advancedPath));
                runner.Save(jobFile, after);
                updated = new BackupJobUpdateReport(Path.GetFullPath(jobFile), updated.Before, after, DateTimeOffset.UtcNow);
            }
            WriteJson(updated);
            return 0;
        case "export":
            WriteJson(runner.Export(Required(values, "file"), Required(values, "output")));
            return 0;
        case "import":
            WriteJson(runner.Import(Required(values, "file"), Required(values, "target")));
            return 0;
        case "list":
            WriteJson(runner.List(Required(values, "directory"), values.GetValueOrDefault("pattern")));
            return 0;
        case "status":
            WriteJson(runner.Status(Required(values, "file")));
            return 0;
        case "history":
            WriteJson(runner.History(Required(values, "file"), values.GetValueOrDefault("pattern")));
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

static BackupJobDefinition ApplyAdvancedJobOptionsFromValues(BackupJobRunner runner, BackupJobDefinition definition, Dictionary<string, string> values)
{
    return values.TryGetValue("advanced-json-file", out var advancedPath)
        ? runner.ApplyAdvancedOptions(definition, runner.LoadAdvancedOptions(advancedPath))
        : definition;
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
        if (name is "overwrite" or "verify" or "force-disabled" or "target-is-current-system-disk" or "has-efi-system-partition" or "run-missed" or "allow-boot-system" or "verify-image" or "reboot-after-restore")
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
    rc image list --repository <dir> [--pattern *.rcimg] [--verify] [--password <secret>]
    rc image audit --repository <dir> [--pattern *.rcimg] [--password <secret>]
    rc image protect-audit --repository <dir> [--pattern *.rcimg]
    rc image protect --repository <dir> [--pattern *.rcimg]
    rc image compare --image <file.rcimg> --source <dir> [--password <secret>]
    rc image extract --image <file.rcimg> --target <dir> --paths <relative-paths> [--password <secret>] [--overwrite]
    rc image project --image <file.rcimg> --target <dir> [--password <secret>] [--overwrite]
    rc image projections --root <dir>
    rc image unproject --target <dir>
    rc image restore --image <file.rcimg> --target <dir> [--password <secret>] [--overwrite]
    rc clone directory --source <dir> --target <dir> [--overwrite]
    rc job create --file <job.json> --job-id <id> --name <name> --source <dir> --image <file.rcimg> [--compression None|Medium|High] [--password <secret>] [--verify-after-create true|false] [--log-directory <dir>] [--advanced-json-file <advanced.json>]
    rc job update --file <job.json> [--job-id <id>] [--name <name>] [--enabled true|false] [--source <dir>] [--image <file.rcimg>] [--compression None|Medium|High] [--password <secret>] [--verify-after-create true|false] [--log-directory <dir>] [--advanced-json-file <advanced.json>]
    rc job delete --file <job.json>
    rc job export --file <job.json> --output <exported-job.json>
    rc job import --file <exported-job.json> --target <job.json>
    rc job list --directory <dir> [--pattern *.json]
    rc job status --file <job.json>
    rc job history --file <job.json> [--pattern *.json]
    rc job validate --file <job.json>
    rc job run --file <job.json> [--force-disabled]
    rc retention plan --repository <dir> [--pattern *.rcimg] [--keep-count <n>] [--max-age-days <n>] [--min-free-bytes <n>]
    rc retention apply --repository <dir> [--pattern *.rcimg] [--keep-count <n>] [--max-age-days <n>] [--min-free-bytes <n>]
    rc retention gfs-plan --repository <dir> [--pattern *.rcimg] [--daily-keep <n>] [--weekly-keep <n>] [--monthly-keep <n>]
    rc retention gfs-apply --repository <dir> [--pattern *.rcimg] [--daily-keep <n>] [--weekly-keep <n>] [--monthly-keep <n>]
    rc schedule plan --task-name <name> --job-file <job.json> [--cli-path <rc.exe>] [--frequency Daily|Weekly|Monthly|Event] [--time HH:mm] [--run-missed] [--event-log <log>] [--event-id <id>] [--event-source <source>]
    rc schedule register --task-name <name> --job-file <job.json> [--cli-path <rc.exe>] [--frequency Daily|Weekly|Monthly|Event] [--time HH:mm] [--run-missed] [--event-log <log>] [--event-id <id>] [--event-source <source>]
    rc schedule status --task-name <name>
    rc schedule run --task-name <name>
    rc schedule unregister --task-name <name>
    rc restore plan --image <file.rcimg> --target-disk-id <id> --boot-mode Bios|Uefi --bcd-store <path> [--password <secret>] [--target-disk-size-bytes <n>] [--required-bytes <n>] [--target-is-current-system-disk] [--has-efi-system-partition]
    rc rescue answer-create --output <answer.json> --repository <dir> --image <file.rcimg> --target-disk-id <id> [--password <secret>] [--boot-mode Bios|Uefi|Unknown] [--target-disk-size-bytes <n>] [--required-bytes <n>] [--target-is-current-system-disk] [--has-efi-system-partition] [--bcd-store <path>] [--driver-directories <paths>] [--network-shares <shares>] [--repair-boot true|false] [--reboot-after-restore] [--verify-image] [--directory-restore-target <dir>]
    rc rescue answer-validate --file <answer.json> [--verify-image]
    rc rescue answer-execute --file <answer.json> [--verify-image] [--overwrite]
    rc operation kinds
    rc operation validate --request <operation.json>
    rc operation run --request <operation.json> [--log-directory <dir>]
    rc service serve --pipe <name> [--log-directory <dir>]
    rc service host --pipe <name> [--log-directory <dir>]
    rc service run-operation --pipe <name> --request <operation.json> [--log-directory <dir>] [--timeout-ms <n>]
    rc service plan-install --name <service> --pipe <pipe> [--cli-path <rc.exe>] [--log-directory <dir>] [--display-name <name>] [--start-mode auto|delayed-auto|demand|disabled]
    rc service install --name <service> --pipe <pipe> [--cli-path <rc.exe>] [--log-directory <dir>] [--display-name <name>] [--start-mode auto|delayed-auto|demand|disabled]
    rc service status --name <service>
    rc service start --name <service>
    rc service stop --name <service>
    rc service recovery --name <service> [--reset-period-seconds <n>] [--restart-delay-ms <n>] [--restart-on-failure true|false]
    rc service recovery-status --name <service>
    rc service uninstall --name <service>
    rc logs list --directory <dir> [--pattern *.json]
    rc storage volumes
    rc storage disks
    rc storage disk-safety --disk-number <n> [--expected-fingerprint <sha256>] [--allow-boot-system]
    rc native status
    """);
}

[SupportedOSPlatform("windows")]
internal sealed class OperationWindowsService : ServiceBase
{
    private readonly string _pipeName;
    private readonly string? _logDirectory;
    private CancellationTokenSource? _cancellation;
    private Task? _serverTask;

    public OperationWindowsService(string pipeName, string? logDirectory)
    {
        ServiceName = "RescueClone";
        _pipeName = pipeName;
        _logDirectory = logDirectory;
        CanStop = true;
        CanShutdown = true;
    }

    protected override void OnStart(string[] args)
    {
        _cancellation = new CancellationTokenSource();
        _serverTask = Task.Run(() => new OperationPipeServer().RunAsync(_pipeName, _logDirectory, _cancellation.Token));
    }

    protected override void OnStop()
    {
        _cancellation?.Cancel();
        try
        {
            _serverTask?.Wait(TimeSpan.FromSeconds(20));
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    protected override void OnShutdown()
    {
        OnStop();
        base.OnShutdown();
    }
}
