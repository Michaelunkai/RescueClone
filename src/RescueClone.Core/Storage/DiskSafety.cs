using System.Security.Cryptography;
using System.Text;

namespace RescueClone.Core.Storage;

public sealed record DiskTargetSafetyOptions(
    int DiskNumber,
    string? ExpectedFingerprint = null,
    bool AllowBootOrSystemDisk = false);

public sealed record DiskTargetSafetyReport(
    int DiskNumber,
    string? Fingerprint,
    bool CanProceed,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    DiskInfo? Disk);

public sealed class DiskTargetSafetyEvaluator
{
    public DiskTargetSafetyReport Evaluate(IReadOnlyList<DiskInfo> disks, DiskTargetSafetyOptions options)
    {
        var blockers = new List<string>();
        var warnings = new List<string>();
        var disk = disks.SingleOrDefault(d => d.Number == options.DiskNumber);
        if (disk is null)
        {
            blockers.Add($"Target disk was not found: {options.DiskNumber}.");
            return new DiskTargetSafetyReport(options.DiskNumber, null, CanProceed: false, blockers, warnings, null);
        }

        var fingerprint = Fingerprint(disk);
        if (!string.IsNullOrWhiteSpace(options.ExpectedFingerprint) &&
            !string.Equals(fingerprint, options.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("Target disk fingerprint does not match the expected fingerprint.");
        }

        if (!options.AllowBootOrSystemDisk && (disk.IsBoot || disk.IsSystem))
            blockers.Add("Target disk is marked as a boot or system disk.");
        if (disk.IsOffline)
            blockers.Add("Target disk is offline.");
        if (disk.IsReadOnly)
            blockers.Add("Target disk is read-only.");
        if (string.IsNullOrWhiteSpace(disk.SerialNumber))
            warnings.Add("Target disk has no serial number; fingerprint stability may be reduced.");
        if (disk.SizeBytes is null or <= 0)
            warnings.Add("Target disk size is unavailable; capacity fit cannot be proven from inventory.");

        return new DiskTargetSafetyReport(
            options.DiskNumber,
            fingerprint,
            blockers.Count == 0,
            blockers,
            warnings,
            disk);
    }

    public static string Fingerprint(DiskInfo disk)
    {
        var payload = string.Join("|", new[]
        {
            disk.Number.ToString(),
            disk.FriendlyName ?? string.Empty,
            disk.SerialNumber ?? string.Empty,
            disk.PartitionStyle ?? string.Empty,
            disk.BusType ?? string.Empty,
            disk.SizeBytes?.ToString() ?? string.Empty
        });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
