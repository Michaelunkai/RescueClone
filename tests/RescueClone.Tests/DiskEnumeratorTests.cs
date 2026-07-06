using RescueClone.Core;
using RescueClone.Core.Storage;

namespace RescueClone.Tests;

[TestClass]
public sealed class DiskEnumeratorTests
{
    [TestMethod]
    public void ListDisksReturnsStructuredDiskMetadata()
    {
        var disks = new DiskEnumerator().ListDisks();

        Assert.IsTrue(disks.Count > 0);
        Assert.IsTrue(disks.All(d => d.Number >= 0));
        Assert.IsTrue(disks.Any(d => d.SizeBytes is > 0));
    }

    [TestMethod]
    public void FeatureCatalogIncludesDiskParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "storage.disk.list");

        Assert.AreEqual("Disks", feature.Gui);
        Assert.AreEqual("rc storage disks", feature.Cli);
        Assert.AreEqual("Get-RCDisk", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }
}
