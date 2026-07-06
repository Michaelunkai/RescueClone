using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RescueClone.Core.Storage;

public sealed record DiskInfo(
    int Number,
    string? FriendlyName,
    string? SerialNumber,
    string? PartitionStyle,
    string? BusType,
    long? SizeBytes,
    bool IsBoot,
    bool IsSystem,
    bool IsOffline,
    bool IsReadOnly);

public sealed class DiskEnumerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<DiskInfo> ListDisks()
    {
        var json = TryRunPowerShell("""
        Get-Disk | Sort-Object Number | Select-Object Number,FriendlyName,SerialNumber,PartitionStyle,BusType,Size,IsBoot,IsSystem,IsOffline,IsReadOnly | ConvertTo-Json -Depth 4
        """);
        if (string.IsNullOrWhiteSpace(json))
        {
            json = RunPowerShell("""
            Get-CimInstance Win32_DiskDrive | Sort-Object Index | Select-Object @{Name='Number';Expression={$_.Index}},@{Name='FriendlyName';Expression={$_.Model}},SerialNumber,@{Name='PartitionStyle';Expression={$null}},@{Name='BusType';Expression={$_.InterfaceType}},Size,@{Name='IsBoot';Expression={$false}},@{Name='IsSystem';Expression={$false}},@{Name='IsOffline';Expression={($_.Status -ne 'OK')}},@{Name='IsReadOnly';Expression={$false}} | ConvertTo-Json -Depth 4
            """);
        }
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<DiskInfo>();

        var trimmed = json.TrimStart();
        var rawDisks = trimmed.StartsWith("[", StringComparison.Ordinal)
            ? JsonSerializer.Deserialize<List<RawDiskInfo>>(json, JsonOptions)
            : new List<RawDiskInfo> { JsonSerializer.Deserialize<RawDiskInfo>(json, JsonOptions)! };

        return (rawDisks ?? new List<RawDiskInfo>())
            .Where(d => d is not null)
            .Select(d => new DiskInfo(
                d.Number,
                d.FriendlyName,
                d.SerialNumber,
                d.PartitionStyle,
                d.BusType,
                d.Size,
                d.IsBoot,
                d.IsSystem,
                d.IsOffline,
                d.IsReadOnly))
            .ToArray();
    }

    private static string RunPowerShell(string command)
    {
        var (exitCode, output, error) = ExecutePowerShell(command);
        if (exitCode != 0)
            throw new InvalidOperationException($"Disk inventory failed: {error.Trim()}");
        return output;
    }

    private static string? TryRunPowerShell(string command)
    {
        var (exitCode, output, _) = ExecutePowerShell(command);
        return exitCode == 0 ? output : null;
    }

    private static (int ExitCode, string Output, string Error) ExecutePowerShell(string command)
    {
        var powerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(powerShell))
            powerShell = "powershell.exe";

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = powerShell,
            ArgumentList =
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                command
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start Windows PowerShell for disk inventory.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private sealed class RawDiskInfo
    {
        public int Number { get; set; }
        public string? FriendlyName { get; set; }
        public string? SerialNumber { get; set; }
        public string? PartitionStyle { get; set; }
        public string? BusType { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long? Size { get; set; }
        public bool IsBoot { get; set; }
        public bool IsSystem { get; set; }
        public bool IsOffline { get; set; }
        public bool IsReadOnly { get; set; }
    }
}
