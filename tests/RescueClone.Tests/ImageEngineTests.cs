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

        Assert.AreEqual(2, created.FormatVersion);
        Assert.AreEqual(2, verified.FormatVersion);
        Assert.AreEqual(2, created.FileCount);
        Assert.AreEqual(created.RootSha256, verified.RootSha256);
        Assert.AreEqual(2, report.FileCount);
        Assert.AreEqual("alpha", File.ReadAllText(Path.Combine(restored, "alpha.txt")));
        Assert.AreEqual("beta", File.ReadAllText(Path.Combine(restored, "nested", "beta.txt")));
    }

    [TestMethod]
    public void VerifyAndRestoreReadV1Images()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var restored = Path.Combine(root, "restored");
        var image = Path.Combine(root, "backup-v1.rcimg");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");

        var engine = new ImageEngine();
        var created = engine.Create(new ImageOptions(source, image, CompressionMode.None, null, ImageContainerFormat.V1));
        var verified = engine.Verify(image, null);
        var report = engine.Restore(new RestoreOptions(image, restored, null, Overwrite: false));

        Assert.AreEqual(1, created.FormatVersion);
        Assert.AreEqual(1, verified.FormatVersion);
        Assert.AreEqual(created.RootSha256, verified.RootSha256);
        Assert.AreEqual(1, report.FileCount);
        Assert.AreEqual("alpha", File.ReadAllText(Path.Combine(restored, "alpha.txt")));
    }

    [TestMethod]
    public void VerifyRejectsCorruptedV2BlockPayload()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var image = Path.Combine(root, "backup-v2.rcimg");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");

        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.None, null, ImageContainerFormat.V2));
        var bytes = File.ReadAllBytes(image);
        var payloadOffset = FindBytes(bytes, "alpha"u8.ToArray());
        Assert.IsTrue(payloadOffset > 0);
        bytes[payloadOffset] = (byte)'z';
        File.WriteAllBytes(image, bytes);

        Assert.ThrowsException<InvalidDataException>(() => new ImageEngine().Verify(image, null));
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

    private static int FindBytes(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var found = true;
            for (var j = 0; j < needle.Length; j++)
                found &= haystack[i + j] == needle[j];
            if (found)
                return i;
        }
        return -1;
    }
}
