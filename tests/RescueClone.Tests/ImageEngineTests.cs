using RescueClone.Core;

namespace RescueClone.Tests;

[TestClass]
public sealed class ImageEngineTests
{
    [TestMethod]
    public void CreateVerifyRestoreEncryptedCompressedImage()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var restored = Path.Combine(root, "restored");
        var image = Path.Combine(root, "backup.rcimg");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(source, "nested", "beta.txt"), "beta");

        var engine = new ImageEngine();
        var created = engine.Create(new ImageOptions(source, image, CompressionMode.High, "secret"));
        var verified = engine.Verify(image, "secret");
        var report = engine.Restore(new RestoreOptions(image, restored, "secret", Overwrite: false));

        Assert.AreEqual(2, created.FileCount);
        Assert.AreEqual(created.RootSha256, verified.RootSha256);
        Assert.AreEqual(2, report.FileCount);
        Assert.AreEqual("alpha", File.ReadAllText(Path.Combine(restored, "alpha.txt")));
        Assert.AreEqual("beta", File.ReadAllText(Path.Combine(restored, "nested", "beta.txt")));
    }

    [TestMethod]
    public void FeatureCatalogHasGuiCliAndPowerShellForEveryImplementedFeature()
    {
        FeatureCatalog.AssertImplementedParity();
        Assert.IsTrue(FeatureCatalog.All.All(f => f.Implemented));
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
