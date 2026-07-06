using RescueClone.Core;
using RescueClone.Core.RestorePlanning;

namespace RescueClone.Tests;

[TestClass]
public sealed class RestorePlannerTests
{
    [TestMethod]
    public void PlanBlocksCurrentSystemDisk()
    {
        var context = NewContext();
        var report = Plan(context, options => options with { TargetIsCurrentSystemDisk = true });

        Assert.IsFalse(report.CanProceed);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("current running system disk", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void PlanBlocksMissingTargetDiskId()
    {
        var context = NewContext();
        var report = Plan(context, options => options with { TargetDiskId = "" });

        Assert.IsFalse(report.CanProceed);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("TargetDiskId", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PlanBlocksUnknownBootMode()
    {
        var context = NewContext();
        var report = Plan(context, options => options with { BootMode = RestoreBootMode.Unknown });

        Assert.IsFalse(report.CanProceed);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("BootMode", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PlanBlocksUefiWithoutEfiSystemPartition()
    {
        var context = NewContext();
        var report = Plan(context, options => options with { BootMode = RestoreBootMode.Uefi, HasEfiSystemPartition = false });

        Assert.IsFalse(report.CanProceed);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("EFI System Partition", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PlanBlocksMissingBcdStorePath()
    {
        var context = NewContext();
        var report = Plan(context, options => options with { BcdStorePath = null });

        Assert.IsFalse(report.CanProceed);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("BcdStorePath", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PlanBlocksTargetDiskTooSmall()
    {
        var context = NewContext();
        var report = Plan(context, options => options with { TargetDiskSizeBytes = 1 });

        Assert.IsFalse(report.CanProceed);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("too small", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void PlanAllowsValidBiosTargetAndIncludesHyperVBootProofSteps()
    {
        var context = NewContext();
        var report = Plan(context);

        Assert.IsTrue(report.CanProceed, string.Join(Environment.NewLine, report.Blockers));
        Assert.IsTrue(report.HyperVBootProofSteps.Count >= 5);
        Assert.IsTrue(report.HyperVBootProofSteps.Any(s => s.Contains("Boot", StringComparison.OrdinalIgnoreCase)));
    }

    private static RestorePlanReport Plan(TestContextData context, Func<RestorePlanOptions, RestorePlanOptions>? mutate = null)
    {
        var options = new RestorePlanOptions(
            context.ImagePath,
            "secret",
            "disk-fixture-1",
            TargetDiskSizeBytes: 1024 * 1024,
            RequiredBytes: null,
            TargetIsCurrentSystemDisk: false,
            RestoreBootMode.Bios,
            HasEfiSystemPartition: false,
            context.BcdPath);
        return new RestorePlanner().Plan(mutate?.Invoke(options) ?? options);
    }

    private static TestContextData NewContext()
    {
        var root = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(root, "backup.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, "secret"));
        var bcd = Path.Combine(root, "BCD");
        File.WriteAllText(bcd, "fixture");
        return new TestContextData(image, bcd);
    }

    private sealed record TestContextData(string ImagePath, string BcdPath);
}
