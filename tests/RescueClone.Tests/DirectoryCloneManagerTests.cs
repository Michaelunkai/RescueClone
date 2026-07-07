using RescueClone.Core;
using RescueClone.Core.Cloning;

namespace RescueClone.Tests;

[TestClass]
public sealed class DirectoryCloneManagerTests
{
    [TestMethod]
    public void CloneCopiesFilesAndVerifiesHashes()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(source, "nested", "beta.txt"), "beta");

        var report = new DirectoryCloneManager().Clone(new DirectoryCloneOptions(source, target));

        Assert.IsTrue(report.Verified);
        Assert.AreEqual(2, report.FileCount);
        Assert.AreEqual("alpha", File.ReadAllText(Path.Combine(target, "alpha.txt")));
        Assert.AreEqual("beta", File.ReadAllText(Path.Combine(target, "nested", "beta.txt")));
        Assert.IsTrue(report.TotalBytes > 0);
        Assert.AreEqual(2, report.Files.Count);
    }

    [TestMethod]
    public void CloneBlocksTargetInsideSource()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var target = Path.Combine(source, "target");
        Directory.CreateDirectory(source);

        Assert.ThrowsException<InvalidOperationException>(() =>
            new DirectoryCloneManager().Clone(new DirectoryCloneOptions(source, target)));
    }

    [TestMethod]
    public void FeatureCatalogIncludesDirectoryCloneParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "clone.directory");

        Assert.AreEqual("Clone", feature.Gui);
        Assert.AreEqual("rc clone directory", feature.Cli);
        Assert.AreEqual("Copy-RCDirectoryClone", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
