using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RescueClone.Core.Services;

public sealed record WindowsServiceInstallDefinition(
    string ServiceName,
    string CliPath,
    string PipeName,
    string? LogDirectory,
    string? DisplayName,
    string StartMode);

public sealed record WindowsServiceInstallPlan(
    string ServiceName,
    string DisplayName,
    string CliPath,
    string PipeName,
    string? LogDirectory,
    string StartMode,
    string BinaryPath);

public sealed record WindowsServiceCommandReport(
    string Action,
    string ServiceName,
    bool Succeeded,
    int ExitCode,
    string StandardOutput,
    string StandardError);

public sealed record WindowsServiceStatusReport(
    string ServiceName,
    bool Exists,
    string? State,
    int ExitCode,
    string StandardOutput,
    string StandardError);

public sealed class WindowsServiceManager
{
    public WindowsServiceInstallPlan Plan(WindowsServiceInstallDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ServiceName))
            throw new ArgumentException("ServiceName is required.");
        if (string.IsNullOrWhiteSpace(definition.CliPath))
            throw new ArgumentException("CliPath is required.");
        if (string.IsNullOrWhiteSpace(definition.PipeName))
            throw new ArgumentException("PipeName is required.");
        if (!File.Exists(definition.CliPath))
            throw new FileNotFoundException("CLI executable was not found.", definition.CliPath);

        var displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.ServiceName
            : definition.DisplayName.Trim();
        var startMode = string.IsNullOrWhiteSpace(definition.StartMode)
            ? "auto"
            : definition.StartMode.Trim().ToLowerInvariant();
        if (startMode is not ("auto" or "demand" or "disabled" or "delayed-auto"))
            throw new ArgumentException("StartMode must be auto, delayed-auto, demand, or disabled.");

        var binaryPath = $"{Quote(Path.GetFullPath(definition.CliPath))} service host --pipe {Quote(definition.PipeName.Trim())}";
        if (!string.IsNullOrWhiteSpace(definition.LogDirectory))
            binaryPath += $" --log-directory {Quote(Path.GetFullPath(definition.LogDirectory))}";

        return new WindowsServiceInstallPlan(
            definition.ServiceName.Trim(),
            displayName,
            Path.GetFullPath(definition.CliPath),
            definition.PipeName.Trim(),
            string.IsNullOrWhiteSpace(definition.LogDirectory) ? null : Path.GetFullPath(definition.LogDirectory),
            startMode,
            binaryPath);
    }

    public WindowsServiceCommandReport Install(WindowsServiceInstallDefinition definition)
    {
        var plan = Plan(definition);
        var result = RunSc("create", plan.ServiceName, "binPath=", plan.BinaryPath, "DisplayName=", plan.DisplayName, "start=", plan.StartMode);
        return new WindowsServiceCommandReport("install", plan.ServiceName, result.ExitCode == 0, result.ExitCode, result.StandardOutput, result.StandardError);
    }

    public WindowsServiceCommandReport Uninstall(string serviceName)
    {
        var result = RunSc("delete", RequiredServiceName(serviceName));
        return new WindowsServiceCommandReport("uninstall", serviceName, result.ExitCode == 0, result.ExitCode, result.StandardOutput, result.StandardError);
    }

    public WindowsServiceCommandReport Start(string serviceName)
    {
        var result = RunSc("start", RequiredServiceName(serviceName));
        return new WindowsServiceCommandReport("start", serviceName, result.ExitCode == 0, result.ExitCode, result.StandardOutput, result.StandardError);
    }

    public WindowsServiceCommandReport Stop(string serviceName)
    {
        var result = RunSc("stop", RequiredServiceName(serviceName));
        return new WindowsServiceCommandReport("stop", serviceName, result.ExitCode == 0, result.ExitCode, result.StandardOutput, result.StandardError);
    }

    public WindowsServiceStatusReport Status(string serviceName)
    {
        serviceName = RequiredServiceName(serviceName);
        var result = RunSc("query", serviceName);
        var exists = result.ExitCode == 0;
        var state = exists ? ParseState(result.StandardOutput) : null;
        return new WindowsServiceStatusReport(serviceName, exists, state, result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private static string RequiredServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name is required.");
        return serviceName.Trim();
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string? ParseState(string output)
    {
        var match = Regex.Match(output, @"STATE\s+:\s+\d+\s+([A-Z_]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunSc(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start sc.exe.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Trim(), error.Trim());
    }
}
