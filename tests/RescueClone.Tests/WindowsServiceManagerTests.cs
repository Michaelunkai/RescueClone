using RescueClone.Core;
using RescueClone.Core.Services;

namespace RescueClone.Tests;

[TestClass]
public sealed class WindowsServiceManagerTests
{
    [TestMethod]
    public void PlanBuildsServiceHostBinaryPath()
    {
        var root = NewTempDirectory();
        var cli = Path.Combine(root, "rc.exe");
        File.WriteAllText(cli, string.Empty);
        var logs = Path.Combine(root, "logs");

        var plan = new WindowsServiceManager().Plan(new WindowsServiceInstallDefinition(
            "RescueCloneTest",
            cli,
            "rescueclone-test",
            logs,
            "RescueClone Test Service",
            "demand"));

        Assert.AreEqual("RescueCloneTest", plan.ServiceName);
        Assert.AreEqual("RescueClone Test Service", plan.DisplayName);
        Assert.AreEqual(Path.GetFullPath(cli), plan.CliPath);
        Assert.AreEqual("rescueclone-test", plan.PipeName);
        Assert.AreEqual(Path.GetFullPath(logs), plan.LogDirectory);
        Assert.AreEqual("demand", plan.StartMode);
        StringAssert.Contains(plan.BinaryPath, "service host");
        StringAssert.Contains(plan.BinaryPath, "--pipe");
        StringAssert.Contains(plan.BinaryPath, "--log-directory");
    }

    [TestMethod]
    public void StatusReportsMissingServiceWithoutThrowing()
    {
        var serviceName = "RescueCloneMissing" + Guid.NewGuid().ToString("N");

        var status = new WindowsServiceManager().Status(serviceName);

        Assert.AreEqual(serviceName, status.ServiceName);
        Assert.IsFalse(status.Exists);
        Assert.IsNull(status.State);
        Assert.AreNotEqual(0, status.ExitCode);
    }

    [TestMethod]
    public void FeatureCatalogIncludesWindowsServiceParity()
    {
        AssertServiceFeature("service.install.plan", "rc service plan-install", "Get-RCServiceInstallPlan");
        AssertServiceFeature("service.install", "rc service install", "Install-RCService");
        AssertServiceFeature("service.status", "rc service status", "Get-RCServiceStatus");
        AssertServiceFeature("service.start", "rc service start", "Start-RCService");
        AssertServiceFeature("service.stop", "rc service stop", "Stop-RCService");
        AssertServiceFeature("service.uninstall", "rc service uninstall", "Uninstall-RCService");
    }

    private static void AssertServiceFeature(string id, string cli, string powerShell)
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == id);

        Assert.AreEqual("Operations", feature.Gui);
        Assert.AreEqual(cli, feature.Cli);
        Assert.AreEqual(powerShell, feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
