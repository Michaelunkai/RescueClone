using System.Text.Json;
using System.Text.Json.Serialization;
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
        Assert.IsTrue(File.Exists(report.RecoveryStatePath));
        Assert.IsNotNull(report.Result);
        Assert.AreEqual(1, report.Result.Value.GetProperty("fileCount").GetInt32());
        AssertAuditEvents(report, OperationState.Succeeded);

        var state = ReadRecoveryState(report.RecoveryStatePath!);
        Assert.IsNotNull(state);
        Assert.AreEqual("image.verify", state.Request.Kind);
        Assert.AreEqual(OperationState.Succeeded, state.Report.State);
        AssertAuditEvents(state.Report, OperationState.Succeeded);
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
        Assert.IsTrue(File.Exists(report.RecoveryStatePath));
        Assert.IsTrue(report.Error!.Contains("missing.rcimg", StringComparison.OrdinalIgnoreCase));
        AssertAuditEvents(report, OperationState.Failed);
        Assert.IsNotNull(report.ErrorDetail);
        Assert.AreEqual("not_found", report.ErrorDetail.Code);
        Assert.AreEqual(nameof(FileNotFoundException), report.ErrorDetail.ExceptionType);

        var state = ReadRecoveryState(report.RecoveryStatePath!);
        Assert.IsNotNull(state);
        Assert.AreEqual("image.verify", state.Request.Kind);
        Assert.AreEqual(OperationState.Failed, state.Report.State);
        Assert.IsTrue(state.Report.Error!.Contains("missing.rcimg", StringComparison.OrdinalIgnoreCase));
        AssertAuditEvents(state.Report, OperationState.Failed);
        Assert.IsNotNull(state.Report.ErrorDetail);
        Assert.AreEqual("not_found", state.Report.ErrorDetail.Code);
    }

    [TestMethod]
    public void RunBrowseAndExtractImageOperations()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "extract");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(source, "nested", "beta.txt"), "beta");
        var image = Path.Combine(root, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.High, null));
        var runner = new OperationRunner();

        var browse = runner.Run(new OperationRequest(
            "image.browse",
            new Dictionary<string, JsonElement>
            {
                ["image"] = Json("image", image)
            },
            "browse-image"), Path.Combine(root, "ops"));
        var extract = runner.Run(new OperationRequest(
            "image.extract.directory",
            new Dictionary<string, JsonElement>
            {
                ["image"] = Json("image", image),
                ["target"] = Json("target", target),
                ["paths"] = Json("paths", new[] { "nested/beta.txt" })
            },
            "extract-image"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, browse.State);
        Assert.AreEqual(2, browse.Result!.Value.GetProperty("fileCount").GetInt32());
        Assert.AreEqual(OperationState.Succeeded, extract.State);
        Assert.AreEqual(1, extract.Result!.Value.GetProperty("fileCount").GetInt32());
        Assert.AreEqual("beta", File.ReadAllText(Path.Combine(target, "nested", "beta.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(target, "alpha.txt")));
        Assert.IsTrue(File.Exists(extract.LogPath));
        Assert.IsTrue(File.Exists(extract.RecoveryStatePath));
    }

    [TestMethod]
    public async Task PipeServiceRunsOperationAndWritesRecoveryState()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var logs = Path.Combine(root, "ops");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(root, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        var pipeName = "rescueclone-test-" + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource();
        var serverTask = new OperationPipeServer().RunAsync(pipeName, logs, cancellation.Token);

        var response = await new OperationPipeClient().RunOperationAsync(
            pipeName,
            new OperationServiceRequest(new OperationRequest(
                "image.verify",
                new Dictionary<string, JsonElement>
                {
                    ["image"] = Json("image", image)
                },
                "pipe-verify")),
            TimeSpan.FromSeconds(10),
            CancellationToken.None);
        cancellation.Cancel();
        await serverTask;

        Assert.IsTrue(response.Succeeded);
        Assert.IsNotNull(response.Report);
        Assert.AreEqual(OperationState.Succeeded, response.Report.State);
        Assert.AreEqual(1, response.Report.Result!.Value.GetProperty("fileCount").GetInt32());
        Assert.IsTrue(File.Exists(response.Report.LogPath));
        Assert.IsTrue(File.Exists(response.Report.RecoveryStatePath));
    }

    [TestMethod]
    public void RunJobManagementOperations()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var updatedSource = Path.Combine(root, "updated-source");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(updatedSource);
        var jobPath = Path.Combine(root, "jobs", "operation-job.json");
        var exportPath = Path.Combine(root, "exports", "operation-job.json");
        var importPath = Path.Combine(root, "imports", "operation-job.json");
        var runner = new OperationRunner();

        var create = runner.Run(new OperationRequest(
            "job.backup.directory.create",
            new Dictionary<string, JsonElement>
            {
                ["file"] = Json("file", jobPath),
                ["jobId"] = Json("jobId", "operation-job"),
                ["name"] = Json("name", "Operation Job"),
                ["source"] = Json("source", source),
                ["image"] = Json("image", Path.Combine(root, "images", "operation-job.rcimg")),
                ["compression"] = Json("compression", "Medium"),
                ["verifyAfterCreate"] = Json("verifyAfterCreate", true),
                ["logDirectory"] = Json("logDirectory", Path.Combine(root, "logs"))
            },
            "job-create"), Path.Combine(root, "ops"));
        var update = runner.Run(new OperationRequest(
            "job.backup.directory.update",
            new Dictionary<string, JsonElement>
            {
                ["file"] = Json("file", jobPath),
                ["name"] = Json("name", "Operation Job Updated"),
                ["source"] = Json("source", updatedSource),
                ["compression"] = Json("compression", "High"),
                ["verifyAfterCreate"] = Json("verifyAfterCreate", false)
            },
            "job-update"), Path.Combine(root, "ops"));
        var status = runner.Run(new OperationRequest(
            "job.backup.directory.status",
            new Dictionary<string, JsonElement>
            {
                ["file"] = Json("file", jobPath)
            },
            "job-status"), Path.Combine(root, "ops"));
        var export = runner.Run(new OperationRequest(
            "job.backup.directory.export",
            new Dictionary<string, JsonElement>
            {
                ["file"] = Json("file", jobPath),
                ["output"] = Json("output", exportPath)
            },
            "job-export"), Path.Combine(root, "ops"));
        var import = runner.Run(new OperationRequest(
            "job.backup.directory.import",
            new Dictionary<string, JsonElement>
            {
                ["file"] = Json("file", exportPath),
                ["target"] = Json("target", importPath)
            },
            "job-import"), Path.Combine(root, "ops"));
        var delete = runner.Run(new OperationRequest(
            "job.backup.directory.delete",
            new Dictionary<string, JsonElement>
            {
                ["file"] = Json("file", jobPath)
            },
            "job-delete"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, create.State);
        Assert.AreEqual(OperationState.Succeeded, update.State);
        Assert.AreEqual("Operation Job Updated", status.Result!.Value.GetProperty("job").GetProperty("name").GetString());
        Assert.AreEqual("High", status.Result!.Value.GetProperty("job").GetProperty("compression").GetString());
        Assert.AreEqual(OperationState.Succeeded, export.State);
        Assert.IsTrue(File.Exists(exportPath));
        Assert.AreEqual(OperationState.Succeeded, import.State);
        Assert.IsTrue(File.Exists(importPath));
        Assert.AreEqual(OperationState.Succeeded, delete.State);
        Assert.IsFalse(File.Exists(jobPath));
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

    [TestMethod]
    public void FeatureCatalogIncludesServiceOperationParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "operation.run.service");

        Assert.AreEqual("Operations", feature.Gui);
        Assert.AreEqual("rc service run-operation", feature.Cli);
        Assert.AreEqual("Start-RCServiceOperation", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }

    private static JsonElement Json<T>(string name, T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, T> { [name] = value }));
        return document.RootElement.GetProperty(name).Clone();
    }

    private static OperationRecoveryState? ReadRecoveryState(string path)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<OperationRecoveryState>(
            File.ReadAllText(path),
            options);
    }

    private static void AssertAuditEvents(OperationReport report, OperationState expectedState)
    {
        Assert.IsNotNull(report.AuditEvents);
        Assert.IsTrue(report.AuditEvents.Count >= 2);
        Assert.AreEqual("operation.started", report.AuditEvents[0].EventType);
        Assert.AreEqual(
            expectedState == OperationState.Succeeded ? "operation.succeeded" : "operation.failed",
            report.AuditEvents[^1].EventType);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
