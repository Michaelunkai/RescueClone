using RescueClone.Core;
using RescueClone.Core.Storage;

namespace RescueClone.Tests;

[TestClass]
public sealed class VolumeEnumeratorTests
{
    [TestMethod]
    public void ListVolumesReturnsNamedRoots()
    {
        var volumes = new VolumeEnumerator().ListVolumes();

        Assert.IsTrue(volumes.Count > 0);
        Assert.IsTrue(volumes.All(v => !string.IsNullOrWhiteSpace(v.Name)));
        Assert.IsTrue(volumes.All(v => !string.IsNullOrWhiteSpace(v.RootPath)));
    }

    [TestMethod]
    public void FeatureCatalogIncludesVolumeParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "storage.volume.list");

        Assert.AreEqual("Volumes", feature.Gui);
        Assert.AreEqual("rc storage volumes", feature.Cli);
        Assert.AreEqual("Get-RCVolume", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }
}
