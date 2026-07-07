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
        Assert.IsTrue((File.GetAttributes(image) & FileAttributes.ReadOnly) != 0);
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
        Assert.IsTrue((File.GetAttributes(image) & FileAttributes.ReadOnly) != 0);
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
        File.SetAttributes(image, File.GetAttributes(image) & ~FileAttributes.ReadOnly);
        File.WriteAllBytes(image, bytes);

        Assert.ThrowsException<InvalidDataException>(() => new ImageEngine().Verify(image, null));
    }

    [TestMethod]
    public void BrowseReturnsVerifiedImageContents()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var image = Path.Combine(root, "backup.rcimg");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(source, "nested", "beta.txt"), "beta");

        var engine = new ImageEngine();
        var created = engine.Create(new ImageOptions(source, image, CompressionMode.Medium, "secret"));
        var browsed = engine.Browse(image, "secret");

        Assert.AreEqual(created.RootSha256, browsed.RootSha256);
        Assert.AreEqual(2, browsed.FileCount);
        CollectionAssert.AreEquivalent(new[] { "alpha.txt", "nested/beta.txt" }, browsed.Files.Select(f => f.RelativePath).ToArray());
    }

    [TestMethod]
    public void ExtractRestoresOnlySelectedFiles()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        var image = Path.Combine(root, "backup.rcimg");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(source, "nested", "beta.txt"), "beta");
        File.WriteAllText(Path.Combine(source, "nested", "gamma.txt"), "gamma");

        var engine = new ImageEngine();
        engine.Create(new ImageOptions(source, image, CompressionMode.High, null));
        var report = engine.Extract(new ExtractOptions(image, target, new[] { "nested/beta.txt" }, null, Overwrite: false));

        Assert.AreEqual(1, report.FileCount);
        Assert.AreEqual("beta", File.ReadAllText(Path.Combine(target, "nested", "beta.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(target, "alpha.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(target, "nested", "gamma.txt")));
    }

    [TestMethod]
    public void ExtractRejectsMissingSelectedPath()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        var image = Path.Combine(root, "backup.rcimg");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");

        var engine = new ImageEngine();
        engine.Create(new ImageOptions(source, image, CompressionMode.None, null));

        Assert.ThrowsException<FileNotFoundException>(() =>
            engine.Extract(new ExtractOptions(image, target, new[] { "missing.txt" }, null, Overwrite: false)));
    }

    [TestMethod]
    public void ProjectCreatesReadOnlyManagedProjectionAndUnprojectRemovesIt()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "projection");
        var image = Path.Combine(root, "backup.rcimg");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(source, "nested", "beta.txt"), "beta");
        var manager = new ImageProjectionManager();

        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        var projected = manager.Project(new ImageProjectionOptions(image, target, null, Overwrite: false));

        Assert.AreEqual(2, projected.FileCount);
        Assert.IsTrue(File.Exists(projected.ManifestPath));
        Assert.IsTrue((File.GetAttributes(Path.Combine(target, "alpha.txt")) & FileAttributes.ReadOnly) != 0);
        Assert.IsTrue((File.GetAttributes(Path.Combine(target, "nested", "beta.txt")) & FileAttributes.ReadOnly) != 0);

        var listed = manager.List(new ImageProjectionListOptions(root));

        Assert.AreEqual(1, listed.ProjectionCount);
        Assert.AreEqual(target, listed.Projections[0].TargetPath);
        Assert.AreEqual(image, listed.Projections[0].ImagePath);

        var removed = manager.Unproject(new ImageUnprojectionOptions(target));

        Assert.AreEqual(2, removed.RemovedFileCount);
        Assert.IsFalse(File.Exists(Path.Combine(target, "alpha.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(target, "nested", "beta.txt")));
        Assert.IsFalse(File.Exists(projected.ManifestPath));
    }

    [TestMethod]
    public void UnprojectRejectsUnmanagedDirectory()
    {
        var root = NewTempDirectory();

        Assert.ThrowsException<FileNotFoundException>(() =>
            new ImageProjectionManager().Unproject(new ImageUnprojectionOptions(root)));
    }

    [TestMethod]
    public void ImageRepositoryCatalogListsImagesWithOptionalVerification()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var repository = Path.Combine(root, "repo");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(repository);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(repository, "backup.rcimg");

        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        var catalog = new ImageRepositoryCatalog();
        var metadataOnly = catalog.List(new ImageRepositoryListOptions(repository, "*.rcimg", Verify: false, Password: null));
        var verified = catalog.List(new ImageRepositoryListOptions(repository, "*.rcimg", Verify: true, Password: null));
        var audit = catalog.Audit(new ImageRepositoryAuditOptions(repository, "*.rcimg", Password: null));

        Assert.AreEqual(1, metadataOnly.ImageCount);
        Assert.AreEqual(image, metadataOnly.Images[0].ImagePath);
        Assert.IsFalse(metadataOnly.Images[0].Verified);
        Assert.IsNull(metadataOnly.Images[0].FileCount);
        Assert.AreEqual(1, verified.ImageCount);
        Assert.IsTrue(verified.Images[0].Verified);
        Assert.AreEqual(1, verified.Images[0].FileCount);
        Assert.AreEqual(2, verified.Images[0].FormatVersion);
        Assert.IsFalse(string.IsNullOrWhiteSpace(verified.Images[0].RootSha256));
        Assert.AreEqual(1, audit.ImageCount);
        Assert.AreEqual(1, audit.VerifiedCount);
        Assert.AreEqual(0, audit.FailedCount);
    }

    [TestMethod]
    public void ImageComparerReportsEquivalentAndChangedSource()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(root, "backup.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        var comparer = new ImageComparer();

        var equivalent = comparer.Compare(new ImageCompareOptions(image, source, null));
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "changed");
        File.WriteAllText(Path.Combine(source, "extra.txt"), "extra");
        var changed = comparer.Compare(new ImageCompareOptions(image, source, null));

        Assert.IsTrue(equivalent.Equivalent);
        Assert.AreEqual(1, equivalent.MatchedCount);
        Assert.IsFalse(changed.Equivalent);
        Assert.AreEqual(1, changed.ChangedCount);
        Assert.AreEqual(1, changed.ExtraCount);
        Assert.IsTrue(changed.Differences.Any(d => d.DifferenceType == "changed" && d.RelativePath == "alpha.txt"));
        Assert.IsTrue(changed.Differences.Any(d => d.DifferenceType == "extra" && d.RelativePath == "extra.txt"));
    }

    [TestMethod]
    public void RepositoryProtectionAuditAndApplyMarksImagesReadOnly()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var repository = Path.Combine(root, "images");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(repository);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(repository, "backup.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        File.SetAttributes(image, File.GetAttributes(image) & ~FileAttributes.ReadOnly);
        var catalog = new ImageRepositoryCatalog();

        var audit = catalog.AuditProtection(new ImageRepositoryProtectionOptions(repository, "*.rcimg"));
        var apply = catalog.ApplyProtection(new ImageRepositoryProtectionOptions(repository, "*.rcimg"));
        var after = catalog.AuditProtection(new ImageRepositoryProtectionOptions(repository, "*.rcimg"));

        Assert.AreEqual(1, audit.ImageCount);
        Assert.AreEqual(0, audit.ProtectedCount);
        Assert.IsFalse(audit.Applied);
        Assert.AreEqual(1, apply.ProtectedCount);
        Assert.AreEqual(1, apply.ChangedCount);
        Assert.IsTrue(apply.Applied);
        Assert.AreEqual(1, after.ProtectedCount);
        Assert.IsTrue((File.GetAttributes(image) & FileAttributes.ReadOnly) != 0);
    }

    [TestMethod]
    public void FeatureCatalogIncludesImageBrowseAndExtractParity()
    {
        var browse = FeatureCatalog.All.Single(f => f.FeatureId == "image.browse");
        var list = FeatureCatalog.All.Single(f => f.FeatureId == "image.list.repository");
        var audit = FeatureCatalog.All.Single(f => f.FeatureId == "image.audit.repository");
        var protectionAudit = FeatureCatalog.All.Single(f => f.FeatureId == "image.protect.audit");
        var protectionApply = FeatureCatalog.All.Single(f => f.FeatureId == "image.protect.apply");
        var compare = FeatureCatalog.All.Single(f => f.FeatureId == "image.compare.source");
        var extract = FeatureCatalog.All.Single(f => f.FeatureId == "image.extract.directory");

        Assert.AreEqual("Restore Image", browse.Gui);
        Assert.AreEqual("rc image browse", browse.Cli);
        Assert.AreEqual("Get-RCImageContent", browse.PowerShell);
        Assert.AreEqual("Restore Image", list.Gui);
        Assert.AreEqual("rc image list", list.Cli);
        Assert.AreEqual("Get-RCImage", list.PowerShell);
        Assert.AreEqual("Verify Image", audit.Gui);
        Assert.AreEqual("rc image audit", audit.Cli);
        Assert.AreEqual("Test-RCImageRepository", audit.PowerShell);
        Assert.AreEqual("Verify Image", protectionAudit.Gui);
        Assert.AreEqual("rc image protect-audit", protectionAudit.Cli);
        Assert.AreEqual("Test-RCImageRepositoryProtection", protectionAudit.PowerShell);
        Assert.AreEqual("Verify Image", protectionApply.Gui);
        Assert.AreEqual("rc image protect", protectionApply.Cli);
        Assert.AreEqual("Set-RCImageRepositoryProtection", protectionApply.PowerShell);
        Assert.AreEqual("Verify Image", compare.Gui);
        Assert.AreEqual("rc image compare", compare.Cli);
        Assert.AreEqual("Compare-RCImage", compare.PowerShell);
        Assert.AreEqual("Restore Image", extract.Gui);
        Assert.AreEqual("rc image extract", extract.Cli);
        Assert.AreEqual("Export-RCImageFile", extract.PowerShell);
    }

    [TestMethod]
    public void FeatureCatalogIncludesImageProjectionParity()
    {
        var project = FeatureCatalog.All.Single(f => f.FeatureId == "image.project.readonly");
        var list = FeatureCatalog.All.Single(f => f.FeatureId == "image.project.list");
        var unproject = FeatureCatalog.All.Single(f => f.FeatureId == "image.project.remove");

        Assert.AreEqual("Restore Image", project.Gui);
        Assert.AreEqual("rc image project", project.Cli);
        Assert.AreEqual("Mount-RCImage", project.PowerShell);
        Assert.AreEqual("Restore Image", list.Gui);
        Assert.AreEqual("rc image projections", list.Cli);
        Assert.AreEqual("Get-RCImageMount", list.PowerShell);
        Assert.AreEqual("Restore Image", unproject.Gui);
        Assert.AreEqual("rc image unproject", unproject.Cli);
        Assert.AreEqual("Dismount-RCImage", unproject.PowerShell);
    }

    [TestMethod]
    public void FeatureCatalogHasGuiCliAndPowerShellForEveryImplementedFeature()
    {
        FeatureCatalog.AssertImplementedParity();
        Assert.IsTrue(FeatureCatalog.All.All(f => f.Implemented));
        Assert.AreEqual(FeatureCatalog.All.Count, FeatureCatalog.All.Select(f => f.FeatureId).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var feature in FeatureCatalog.All.Where(f => f.Implemented))
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(feature.Gui), feature.FeatureId);
            Assert.IsTrue(feature.Cli.StartsWith("rc ", StringComparison.Ordinal), feature.FeatureId);
            Assert.IsTrue(feature.PowerShell.StartsWith("Get-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("New-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Test-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Start-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Set-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Compare-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Restore-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Mount-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Dismount-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Invoke-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Install-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Stop-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Uninstall-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Export-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Import-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Register-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Unregister-RC", StringComparison.Ordinal) ||
                feature.PowerShell.StartsWith("Remove-RC", StringComparison.Ordinal), feature.FeatureId);
        }

        Assert.AreEqual(FeatureCatalog.All.Count, FeatureCatalog.All.Select(f => f.Cli).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.AreEqual(FeatureCatalog.All.Count, FeatureCatalog.All.Select(f => f.PowerShell).Distinct(StringComparer.OrdinalIgnoreCase).Count());
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
