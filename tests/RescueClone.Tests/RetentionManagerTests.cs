using RescueClone.Core;
using RescueClone.Core.Retention;

namespace RescueClone.Tests;

[TestClass]
public sealed class RetentionManagerTests
{
    [TestMethod]
    public void PlanDeletesOldestFilesBeyondKeepCount()
    {
        var root = NewTempDirectory();
        var newest = WriteImage(root, "newest.rcimg", 3, DateTimeOffset.UtcNow);
        var middle = WriteImage(root, "middle.rcimg", 3, DateTimeOffset.UtcNow.AddMinutes(-10));
        var oldest = WriteImage(root, "oldest.rcimg", 3, DateTimeOffset.UtcNow.AddMinutes(-20));

        var plan = new RetentionManager().Plan(new RetentionOptions(root, "*.rcimg", KeepCount: 2, MaxAgeDays: null, MinFreeBytes: null));

        CollectionAssert.AreEquivalent(new[] { newest, middle }, plan.Keep.Select(k => k.Path).ToArray());
        CollectionAssert.AreEquivalent(new[] { oldest }, plan.Delete.Select(d => d.Path).ToArray());
        Assert.IsTrue(plan.Delete.Single().Reasons.Single().Contains("keep-count", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PlanDeletesFilesOlderThanMaxAge()
    {
        var root = NewTempDirectory();
        var old = WriteImage(root, "old.rcimg", 3, DateTimeOffset.UtcNow.AddDays(-5));
        var fresh = WriteImage(root, "fresh.rcimg", 3, DateTimeOffset.UtcNow);

        var plan = new RetentionManager().Plan(new RetentionOptions(root, "*.rcimg", KeepCount: null, MaxAgeDays: 1, MinFreeBytes: null));

        CollectionAssert.AreEquivalent(new[] { fresh }, plan.Keep.Select(k => k.Path).ToArray());
        CollectionAssert.AreEquivalent(new[] { old }, plan.Delete.Select(d => d.Path).ToArray());
    }

    [TestMethod]
    public void PlanDeletesOldestFilesUntilFreeSpaceTargetIsMet()
    {
        var root = NewTempDirectory();
        var old = WriteImage(root, "old.rcimg", 1024, DateTimeOffset.UtcNow.AddDays(-2));
        var fresh = WriteImage(root, "fresh.rcimg", 1024, DateTimeOffset.UtcNow);
        var free = new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace;

        var plan = new RetentionManager().Plan(new RetentionOptions(root, "*.rcimg", KeepCount: null, MaxAgeDays: null, MinFreeBytes: free + 1));

        Assert.AreEqual(old, plan.Delete.Single().Path);
        Assert.AreEqual(fresh, plan.Keep.Single().Path);
    }

    [TestMethod]
    public void ApplyDeletesOnlyPlannedFiles()
    {
        var root = NewTempDirectory();
        var keep = WriteImage(root, "keep.rcimg", 3, DateTimeOffset.UtcNow);
        var delete = WriteImage(root, "delete.rcimg", 3, DateTimeOffset.UtcNow.AddHours(-1));

        var report = new RetentionManager().Apply(new RetentionOptions(root, "*.rcimg", KeepCount: 1, MaxAgeDays: null, MinFreeBytes: null));

        Assert.AreEqual(1, report.DeletedFileCount);
        Assert.IsTrue(File.Exists(keep));
        Assert.IsFalse(File.Exists(delete));
        Assert.AreEqual(delete, report.DeletedPaths.Single());
    }

    [TestMethod]
    public void FeatureCatalogIncludesRetentionParity()
    {
        var plan = FeatureCatalog.All.Single(f => f.FeatureId == "retention.plan");
        var apply = FeatureCatalog.All.Single(f => f.FeatureId == "retention.apply");

        Assert.AreEqual("Retention", plan.Gui);
        Assert.AreEqual("rc retention plan", plan.Cli);
        Assert.AreEqual("Get-RCRetentionPlan", plan.PowerShell);
        Assert.AreEqual("Retention", apply.Gui);
        Assert.AreEqual("rc retention apply", apply.Cli);
        Assert.AreEqual("Invoke-RCRetention", apply.PowerShell);
    }

    private static string WriteImage(string root, string name, int bytes, DateTimeOffset lastWriteUtc)
    {
        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, Enumerable.Repeat((byte)'x', bytes).ToArray());
        File.SetLastWriteTimeUtc(path, lastWriteUtc.UtcDateTime);
        return path;
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
