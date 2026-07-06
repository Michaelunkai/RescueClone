using System.Text.Json;
using RescueClone.Core;
using RescueClone.Core.Operations;

namespace RescueClone.Tests;

[TestClass]
public sealed class OperationRunnerTests
{
    [TestMethod]
    public void RunVerifyOperationWritesSucceededReport()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(root, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, "secret"));

        var request = new OperationRequest(
            "image.verify",
            new Dictionary<string, JsonElement>
            {
                ["image"] = Json("image", image),
                ["password"] = Json("password", "secret")
            },
            "verify-alpha");

        var report = new OperationRunner().Run(request, Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, report.State);
        Assert.IsTrue(File.Exists(report.LogPath));
        Assert.IsNotNull(report.Result);
        Assert.AreEqual(1, report.Result.Value.GetProperty("fileCount").GetInt32());
    }

    [TestMethod]
    public void RunOperationCapturesFailureInReport()
    {
        var root = NewTempDirectory();
        var request = new OperationRequest(
            "image.verify",
            new Dictionary<string, JsonElement>
            {
                ["image"] = Json("image", Path.Combine(root, "missing.rcimg"))
            },
            "missing-image");

        var report = new OperationRunner().Run(request, Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Failed, report.State);
        Assert.IsTrue(File.Exists(report.LogPath));
        Assert.IsTrue(report.Error!.Contains("missing.rcimg", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void FeatureCatalogIncludesOperationParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "operation.run.local");

        Assert.AreEqual("Operations", feature.Gui);
        Assert.AreEqual("rc operation run", feature.Cli);
        Assert.AreEqual("Start-RCOperation", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }

    private static JsonElement Json<T>(string name, T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, T> { [name] = value }));
        return document.RootElement.GetProperty(name).Clone();
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
