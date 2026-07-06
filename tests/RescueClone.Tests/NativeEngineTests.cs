using RescueClone.Core;
using RescueClone.Core.Native;

namespace RescueClone.Tests;

[TestClass]
public sealed class NativeEngineTests
{
    [TestMethod]
    public void NativePlannerReturnsExpectedV2Blocks()
    {
        var blocks = NativeBlockPlanner.PlanV2Blocks(2_500_000, 1_000_000);

        Assert.AreEqual(3, blocks.Count);
        Assert.AreEqual(new NativeBlockPlan(0, 0, 1_000_000), blocks[0]);
        Assert.AreEqual(new NativeBlockPlan(1, 1_000_000, 1_000_000), blocks[1]);
        Assert.AreEqual(new NativeBlockPlan(2, 2_000_000, 500_000), blocks[2]);
    }

    [TestMethod]
    public void NativeDiagnosticsReportsLoadedAbi()
    {
        var status = NativeDiagnostics.GetStatus();

        Assert.IsTrue(status.Loaded);
        Assert.AreEqual((uint)1, status.AbiVersion);
        Assert.AreEqual(4, status.SampleBlockCount);
    }

    [TestMethod]
    public void FeatureCatalogIncludesNativeStatusParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "native.status");

        Assert.AreEqual("Native Engine", feature.Gui);
        Assert.AreEqual("rc native status", feature.Cli);
        Assert.AreEqual("Get-RCNativeStatus", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }
}
