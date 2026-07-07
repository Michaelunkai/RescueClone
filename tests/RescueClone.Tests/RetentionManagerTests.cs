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
    public void PlanDeletesFilesForFreeSpaceTarget()
    {
        var root = NewTempDirectory();
        var old = WriteImage(root, "old.rcimg", 1024, DateTimeOffset.UtcNow.AddDays(-2));
        var fresh = WriteImage(root, "fresh.rcimg", 1024, DateTimeOffset.UtcNow);

        var plan = new RetentionManager().Plan(new RetentionOptions(root, "*.rcimg", KeepCount: null, MaxAgeDays: null, MinFreeBytes: long.MaxValue));

        CollectionAssert.AreEqual(new[] { fresh, old }, plan.Delete.Select(d => d.Path).ToArray());
        Assert.IsTrue(plan.Delete.All(d => d.Reasons.Any(r => r.Contains("min-free-bytes", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void ApplyDeletesOnlyPlannedFiles()
    {
        var root = NewTempDirectory();
        var keep = WriteImage(root, "keep.rcimg", 3, DateTimeOffset.UtcNow);
        var delete = WriteImage(root, "delete.rcimg", 3, DateTimeOffset.UtcNow.AddHours(-1));
        File.SetAttributes(delete, File.GetAttributes(delete) | FileAttributes.ReadOnly);

        var report = new RetentionManager().Apply(new RetentionOptions(root, "*.rcimg", KeepCount: 1, MaxAgeDays: null, MinFreeBytes: null));

        Assert.AreEqual(1, report.DeletedFileCount);
        Assert.IsTrue(File.Exists(keep));
        Assert.IsFalse(File.Exists(delete));
        Assert.AreEqual(delete, report.DeletedPaths.Single());
    }

    [TestMethod]
    public void PlanGfsKeepsNewestDailyWeeklyAndMonthlyBuckets()
    {
        var root = NewTempDirectory();
        var newest = WriteImage(root, "newest.rcimg", 3, new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero));
        var yesterday = WriteImage(root, "yesterday.rcimg", 3, new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero));
        var lastWeek = WriteImage(root, "last-week.rcimg", 3, new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero));
        var lastMonth = WriteImage(root, "last-month.rcimg", 3, new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero));
        var older = WriteImage(root, "older.rcimg", 3, new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));

        var plan = new RetentionManager().PlanGfs(new GfsRetentionOptions(root, "*.rcimg", DailyKeepCount: 2, WeeklyKeepCount: 2, MonthlyKeepCount: 2));

        CollectionAssert.Contains(plan.Keep.Select(k => k.Path).ToArray(), newest);
        CollectionAssert.Contains(plan.Keep.Select(k => k.Path).ToArray(), yesterday);
        CollectionAssert.Contains(plan.Keep.Select(k => k.Path).ToArray(), lastWeek);
        CollectionAssert.Contains(plan.Keep.Select(k => k.Path).ToArray(), lastMonth);
        CollectionAssert.Contains(plan.Delete.Select(d => d.Path).ToArray(), older);
        Assert.IsTrue(plan.Delete.Single(d => d.Path == older).Reasons.Single().Contains("GFS", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ApplyGfsDeletesUnselectedReadOnlyImages()
    {
        var root = NewTempDirectory();
        var keep = WriteImage(root, "keep.rcimg", 3, DateTimeOffset.UtcNow);
        var delete = WriteImage(root, "delete.rcimg", 3, DateTimeOffset.UtcNow.AddDays(-10));
        File.SetAttributes(delete, File.GetAttributes(delete) | FileAttributes.ReadOnly);

        var report = new RetentionManager().ApplyGfs(new GfsRetentionOptions(root, "*.rcimg", DailyKeepCount: 1, WeeklyKeepCount: null, MonthlyKeepCount: null));

        Assert.AreEqual(1, report.DeletedFileCount);
        Assert.IsTrue(File.Exists(keep));
        Assert.IsFalse(File.Exists(delete));
    }

    [TestMethod]
    public void PlanDoesNotDeleteExcludedPaths()
    {
        var root = NewTempDirectory();
        var oldExcluded = WriteImage(root, "old-excluded.rcimg", 3, DateTimeOffset.UtcNow.AddDays(-5));
        var oldDelete = WriteImage(root, "old-delete.rcimg", 3, DateTimeOffset.UtcNow.AddDays(-4));

        var plan = new RetentionManager().Plan(new RetentionOptions(root, "*.rcimg", KeepCount: null, MaxAgeDays: 1, MinFreeBytes: null, ExcludedPaths: new[] { oldExcluded }));

        CollectionAssert.DoesNotContain(plan.Delete.Select(d => d.Path).ToArray(), oldExcluded);
        CollectionAssert.Contains(plan.Delete.Select(d => d.Path).ToArray(), oldDelete);
    }

    [TestMethod]
    public void FeatureCatalogIncludesRetentionParity()
    {
        var plan = FeatureCatalog.All.Single(f => f.FeatureId == "retention.plan");
        var apply = FeatureCatalog.All.Single(f => f.FeatureId == "retention.apply");
        var gfsPlan = FeatureCatalog.All.Single(f => f.FeatureId == "retention.gfs.plan");
        var gfsApply = FeatureCatalog.All.Single(f => f.FeatureId == "retention.gfs.apply");

        Assert.AreEqual("Retention", plan.Gui);
        Assert.AreEqual("rc retention plan", plan.Cli);
        Assert.AreEqual("Get-RCRetentionPlan", plan.PowerShell);
        Assert.AreEqual("Retention", apply.Gui);
        Assert.AreEqual("rc retention apply", apply.Cli);
        Assert.AreEqual("Invoke-RCRetention", apply.PowerShell);
        Assert.AreEqual("Retention", gfsPlan.Gui);
        Assert.AreEqual("rc retention gfs-plan", gfsPlan.Cli);
        Assert.AreEqual("Get-RCGfsRetentionPlan", gfsPlan.PowerShell);
        Assert.AreEqual("Retention", gfsApply.Gui);
        Assert.AreEqual("rc retention gfs-apply", gfsApply.Cli);
        Assert.AreEqual("Invoke-RCGfsRetention", gfsApply.PowerShell);
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
