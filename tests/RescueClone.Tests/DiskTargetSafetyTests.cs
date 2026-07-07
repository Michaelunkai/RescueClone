using RescueClone.Core;
using RescueClone.Core.Storage;

namespace RescueClone.Tests;

[TestClass]
public sealed class DiskTargetSafetyTests
{
    [TestMethod]
    public void EvaluateAllowsMatchingNonSystemWritableDisk()
    {
        var disk = DataDisk();
        var fingerprint = DiskTargetSafetyEvaluator.Fingerprint(disk);

        var report = new DiskTargetSafetyEvaluator().Evaluate(
            new[] { disk },
            new DiskTargetSafetyOptions(disk.Number, fingerprint));

        Assert.IsTrue(report.CanProceed, string.Join(Environment.NewLine, report.Blockers));
        Assert.AreEqual(fingerprint, report.Fingerprint);
        Assert.AreEqual(disk.Number, report.DiskNumber);
    }

    [TestMethod]
    public void EvaluateBlocksBootSystemOfflineReadOnlyAndFingerprintMismatch()
    {
        var disk = DataDisk() with
        {
            IsBoot = true,
            IsSystem = true,
            IsOffline = true,
            IsReadOnly = true
        };

        var report = new DiskTargetSafetyEvaluator().Evaluate(
            new[] { disk },
            new DiskTargetSafetyOptions(disk.Number, "not-the-current-fingerprint"));

        Assert.IsFalse(report.CanProceed);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("fingerprint", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("boot or system", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("offline", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("read-only", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void EvaluateBlocksMissingDisk()
    {
        var report = new DiskTargetSafetyEvaluator().Evaluate(
            Array.Empty<DiskInfo>(),
            new DiskTargetSafetyOptions(99));

        Assert.IsFalse(report.CanProceed);
        Assert.IsNull(report.Disk);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("not found", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void FeatureCatalogIncludesDiskSafetyParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "storage.disk.safety");

        Assert.AreEqual("Disks", feature.Gui);
        Assert.AreEqual("rc storage disk-safety", feature.Cli);
        Assert.AreEqual("Get-RCDiskSafety", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }

    private static DiskInfo DataDisk()
    {
        return new DiskInfo(
            Number: 7,
            FriendlyName: "Fixture Disk",
            SerialNumber: "ABC123",
            PartitionStyle: "GPT",
            BusType: "SATA",
            SizeBytes: 1024 * 1024,
            IsBoot: false,
            IsSystem: false,
            IsOffline: false,
            IsReadOnly: false);
    }
}
