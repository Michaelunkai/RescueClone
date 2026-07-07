using System.Text.Json;
using System.Text.Json.Serialization;
using RescueClone.Core;
using RescueClone.Core.Jobs;
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
    public void RunImageRepositoryListOperation()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var repository = Path.Combine(root, "repo");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(repository);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(repository, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));

        var report = new OperationRunner().Run(new OperationRequest(
            "image.list.repository",
            new Dictionary<string, JsonElement>
            {
                ["repository"] = Json("repository", repository),
                ["verify"] = Json("verify", true)
            },
            "list-images"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, report.State);
        Assert.AreEqual(1, report.Result!.Value.GetProperty("imageCount").GetInt32());
        Assert.AreEqual(image, report.Result.Value.GetProperty("images")[0].GetProperty("imagePath").GetString());
        Assert.IsTrue(report.Result.Value.GetProperty("images")[0].GetProperty("verified").GetBoolean());
        Assert.AreEqual(1, report.Result.Value.GetProperty("images")[0].GetProperty("fileCount").GetInt32());

        var audit = new OperationRunner().Run(new OperationRequest(
            "image.audit.repository",
            new Dictionary<string, JsonElement>
            {
                ["repository"] = Json("repository", repository)
            },
            "audit-images"), Path.Combine(root, "ops"));
        File.SetAttributes(image, File.GetAttributes(image) & ~FileAttributes.ReadOnly);
        var protectionAudit = new OperationRunner().Run(new OperationRequest(
            "image.protect.audit",
            new Dictionary<string, JsonElement>
            {
                ["repository"] = Json("repository", repository)
            },
            "audit-protection"), Path.Combine(root, "ops"));
        var protectionApply = new OperationRunner().Run(new OperationRequest(
            "image.protect.apply",
            new Dictionary<string, JsonElement>
            {
                ["repository"] = Json("repository", repository)
            },
            "apply-protection"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, audit.State);
        Assert.AreEqual(1, audit.Result!.Value.GetProperty("imageCount").GetInt32());
        Assert.AreEqual(1, audit.Result.Value.GetProperty("verifiedCount").GetInt32());
        Assert.AreEqual(0, audit.Result.Value.GetProperty("failedCount").GetInt32());
        Assert.AreEqual(OperationState.Succeeded, protectionAudit.State);
        Assert.AreEqual(0, protectionAudit.Result!.Value.GetProperty("protectedCount").GetInt32());
        Assert.AreEqual(OperationState.Succeeded, protectionApply.State);
        Assert.AreEqual(1, protectionApply.Result!.Value.GetProperty("protectedCount").GetInt32());
        Assert.AreEqual(1, protectionApply.Result.Value.GetProperty("changedCount").GetInt32());
    }

    [TestMethod]
    public void RunImageCompareOperation()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(root, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));

        var report = new OperationRunner().Run(new OperationRequest(
            "image.compare.source",
            new Dictionary<string, JsonElement>
            {
                ["image"] = Json("image", image),
                ["source"] = Json("source", source)
            },
            "compare-image"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, report.State);
        Assert.IsTrue(report.Result!.Value.GetProperty("equivalent").GetBoolean());
        Assert.AreEqual(1, report.Result.Value.GetProperty("matchedCount").GetInt32());
    }

    [TestMethod]
    public void RunProjectAndUnprojectImageOperations()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "projection");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(source, "nested", "beta.txt"), "beta");
        var image = Path.Combine(root, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        var runner = new OperationRunner();

        var project = runner.Run(new OperationRequest(
            "image.project.readonly",
            new Dictionary<string, JsonElement>
            {
                ["image"] = Json("image", image),
                ["target"] = Json("target", target)
            },
            "project-image"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, project.State);
        Assert.AreEqual(2, project.Result!.Value.GetProperty("fileCount").GetInt32());
        Assert.AreEqual("alpha", File.ReadAllText(Path.Combine(target, "alpha.txt")));
        Assert.IsTrue((File.GetAttributes(Path.Combine(target, "alpha.txt")) & FileAttributes.ReadOnly) != 0);
        Assert.IsTrue(File.Exists(Path.Combine(target, ".rescueclone-projection.json")));

        var list = runner.Run(new OperationRequest(
            "image.project.list",
            new Dictionary<string, JsonElement>
            {
                ["root"] = Json("root", root)
            },
            "list-projections"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, list.State);
        Assert.AreEqual(1, list.Result!.Value.GetProperty("projectionCount").GetInt32());
        Assert.AreEqual(target, list.Result.Value.GetProperty("projections")[0].GetProperty("targetPath").GetString());

        var unproject = runner.Run(new OperationRequest(
            "image.project.remove",
            new Dictionary<string, JsonElement>
            {
                ["target"] = Json("target", target)
            },
            "unproject-image"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, unproject.State);
        Assert.AreEqual(2, unproject.Result!.Value.GetProperty("removedFileCount").GetInt32());
        Assert.IsFalse(File.Exists(Path.Combine(target, "alpha.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(target, "nested", "beta.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(target, ".rescueclone-projection.json")));
        Assert.IsTrue(File.Exists(unproject.LogPath));
        Assert.IsTrue(File.Exists(unproject.RecoveryStatePath));
    }

    [TestMethod]
    public void RunGfsRetentionOperations()
    {
        var root = NewTempDirectory();
        var repository = Path.Combine(root, "images");
        Directory.CreateDirectory(repository);
        for (var i = 0; i < 5; i++)
        {
            var path = Path.Combine(repository, $"image-{i}.rcimg");
            File.WriteAllText(path, $"image {i}");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-i));
        }

        var runner = new OperationRunner();
        var plan = runner.Run(new OperationRequest(
            "retention.gfs.plan",
            new Dictionary<string, JsonElement>
            {
                ["repository"] = Json("repository", repository),
                ["dailyKeep"] = Json("dailyKeep", 2)
            },
            "gfs-plan"), Path.Combine(root, "ops"));
        var apply = runner.Run(new OperationRequest(
            "retention.gfs.apply",
            new Dictionary<string, JsonElement>
            {
                ["repository"] = Json("repository", repository),
                ["dailyKeep"] = Json("dailyKeep", 2)
            },
            "gfs-apply"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, plan.State);
        Assert.AreEqual(2, plan.Result!.Value.GetProperty("keep").GetArrayLength());
        Assert.AreEqual(3, plan.Result.Value.GetProperty("delete").GetArrayLength());
        Assert.AreEqual(OperationState.Succeeded, apply.State);
        Assert.AreEqual(3, apply.Result!.Value.GetProperty("deletedFileCount").GetInt32());
        Assert.AreEqual(2, Directory.EnumerateFiles(repository, "*.rcimg").Count());
        Assert.IsTrue(File.Exists(apply.LogPath));
        Assert.IsTrue(File.Exists(apply.RecoveryStatePath));
    }

    [TestMethod]
    public void RunRescueAnswerOperations()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var repository = Path.Combine(root, "images");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(repository);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(repository, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        var bcdStore = Path.Combine(root, "BCD");
        File.WriteAllText(bcdStore, "fixture");
        var driverDirectory = Path.Combine(root, "drivers");
        Directory.CreateDirectory(driverDirectory);
        var answer = Path.Combine(root, "answer.json");
        var runner = new OperationRunner();

        var create = runner.Run(new OperationRequest(
            "rescue.answer.create",
            new Dictionary<string, JsonElement>
            {
                ["output"] = Json("output", answer),
                ["repository"] = Json("repository", repository),
                ["image"] = Json("image", "image.rcimg"),
                ["targetDiskId"] = Json("targetDiskId", "disk-fixture-1"),
                ["bootMode"] = Json("bootMode", "Bios"),
                ["targetDiskSizeBytes"] = Json("targetDiskSizeBytes", 1048576),
                ["bcdStore"] = Json("bcdStore", bcdStore),
                ["driverDirectories"] = Json("driverDirectories", new[] { driverDirectory }),
                ["networkShares"] = Json("networkShares", new[] { @"\\server\share" }),
                ["repairBoot"] = Json("repairBoot", true),
                ["rebootAfterRestore"] = Json("rebootAfterRestore", true),
                ["verifyImage"] = Json("verifyImage", true)
            },
            "rescue-answer-create"), Path.Combine(root, "ops"));
        var validate = runner.Run(new OperationRequest(
            "rescue.answer.validate",
            new Dictionary<string, JsonElement>
            {
                ["file"] = Json("file", answer),
                ["verifyImage"] = Json("verifyImage", true)
            },
            "rescue-answer-validate"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, create.State);
        Assert.IsTrue(File.Exists(answer));
        Assert.IsTrue(create.Result!.Value.GetProperty("valid").GetBoolean());
        Assert.IsTrue(create.Result.Value.GetProperty("imageVerified").GetBoolean());
        Assert.AreEqual(OperationState.Succeeded, validate.State);
        Assert.IsTrue(validate.Result!.Value.GetProperty("valid").GetBoolean());
        Assert.IsTrue(validate.Result.Value.GetProperty("imageVerified").GetBoolean());
        Assert.IsTrue(File.Exists(validate.LogPath));
        Assert.IsTrue(File.Exists(validate.RecoveryStatePath));
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
    public async Task PipeServiceProjectsAndUnprojectsImage()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var projection = Path.Combine(root, "projection");
        var logs = Path.Combine(root, "ops");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(root, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        var pipeName = "rescueclone-test-" + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource();
        var serverTask = new OperationPipeServer().RunAsync(pipeName, logs, cancellation.Token);
        var client = new OperationPipeClient();

        var project = await client.RunOperationAsync(
            pipeName,
            new OperationServiceRequest(new OperationRequest(
                "image.project.readonly",
                new Dictionary<string, JsonElement>
                {
                    ["image"] = Json("image", image),
                    ["target"] = Json("target", projection)
                },
                "pipe-project")),
            TimeSpan.FromSeconds(10),
            CancellationToken.None);
        var unproject = await client.RunOperationAsync(
            pipeName,
            new OperationServiceRequest(new OperationRequest(
                "image.project.remove",
                new Dictionary<string, JsonElement>
                {
                    ["target"] = Json("target", projection)
                },
                "pipe-unproject")),
            TimeSpan.FromSeconds(10),
            CancellationToken.None);
        cancellation.Cancel();
        await serverTask;

        Assert.IsTrue(project.Succeeded);
        Assert.IsNotNull(project.Report);
        Assert.AreEqual(OperationState.Succeeded, project.Report.State);
        Assert.AreEqual(1, project.Report.Result!.Value.GetProperty("fileCount").GetInt32());
        Assert.IsTrue(unproject.Succeeded);
        Assert.IsNotNull(unproject.Report);
        Assert.AreEqual(OperationState.Succeeded, unproject.Report.State);
        Assert.AreEqual(1, unproject.Report.Result!.Value.GetProperty("removedFileCount").GetInt32());
        Assert.IsFalse(File.Exists(Path.Combine(projection, "alpha.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(projection, ".rescueclone-projection.json")));
    }

    [TestMethod]
    public async Task PipeServiceRunsRescueAnswerOperation()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var repository = Path.Combine(root, "images");
        var logs = Path.Combine(root, "ops");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(repository);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(repository, "image.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, null));
        var bcdStore = Path.Combine(root, "BCD");
        File.WriteAllText(bcdStore, "fixture");
        var answer = Path.Combine(root, "answer.json");
        var pipeName = "rescueclone-test-" + Guid.NewGuid().ToString("N");
        using var cancellation = new CancellationTokenSource();
        var serverTask = new OperationPipeServer().RunAsync(pipeName, logs, cancellation.Token);

        var response = await new OperationPipeClient().RunOperationAsync(
            pipeName,
            new OperationServiceRequest(new OperationRequest(
                "rescue.answer.create",
                new Dictionary<string, JsonElement>
                {
                    ["output"] = Json("output", answer),
                    ["repository"] = Json("repository", repository),
                    ["image"] = Json("image", "image.rcimg"),
                    ["targetDiskId"] = Json("targetDiskId", "disk-fixture-1"),
                    ["bootMode"] = Json("bootMode", "Bios"),
                    ["targetDiskSizeBytes"] = Json("targetDiskSizeBytes", 1048576),
                    ["bcdStore"] = Json("bcdStore", bcdStore),
                    ["verifyImage"] = Json("verifyImage", true)
                },
                "pipe-rescue-answer")),
            TimeSpan.FromSeconds(10),
            CancellationToken.None);
        cancellation.Cancel();
        await serverTask;

        Assert.IsTrue(response.Succeeded);
        Assert.IsNotNull(response.Report);
        Assert.AreEqual(OperationState.Succeeded, response.Report.State);
        Assert.IsTrue(response.Report.Result!.Value.GetProperty("valid").GetBoolean());
        Assert.IsTrue(response.Report.Result.Value.GetProperty("imageVerified").GetBoolean());
        Assert.IsTrue(File.Exists(answer));
        Assert.IsTrue(File.Exists(response.Report.LogPath));
        Assert.IsTrue(File.Exists(response.Report.RecoveryStatePath));
    }

    [TestMethod]
    public void RunScheduleOperations()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var jobPath = Path.Combine(root, "jobs", "schedule.json");
        var cliPath = Path.Combine(root, "rc.exe");
        File.WriteAllText(cliPath, "fixture");
        new BackupJobRunner().Save(jobPath, new BackupJobDefinition(
            "schedule-job",
            "Schedule Job",
            Enabled: true,
            source,
            Path.Combine(root, "images", "schedule.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs")));
        var runner = new OperationRunner();

        var plan = runner.Run(new OperationRequest(
            "schedule.plan",
            new Dictionary<string, JsonElement>
            {
                ["taskName"] = Json("taskName", "operation-schedule"),
                ["jobFile"] = Json("jobFile", jobPath),
                ["cliPath"] = Json("cliPath", cliPath),
                ["frequency"] = Json("frequency", "Daily"),
                ["time"] = Json("time", "03:15"),
                ["runMissed"] = Json("runMissed", true)
            },
            "schedule-plan"), Path.Combine(root, "ops"));
        var status = runner.Run(new OperationRequest(
            "schedule.status",
            new Dictionary<string, JsonElement>
            {
                ["taskName"] = Json("taskName", "operation-schedule-that-does-not-exist")
            },
            "schedule-status"), Path.Combine(root, "ops"));

        Assert.AreEqual(OperationState.Succeeded, plan.State);
        Assert.AreEqual("operation-schedule", plan.Result!.Value.GetProperty("taskName").GetString());
        Assert.AreEqual("Daily", plan.Result.Value.GetProperty("frequency").GetString());
        StringAssert.Contains(plan.Result.Value.GetProperty("taskXml").GetString(), "StartWhenAvailable>true</StartWhenAvailable");
        Assert.AreEqual(OperationState.Succeeded, status.State);
        Assert.IsFalse(status.Result!.Value.GetProperty("succeeded").GetBoolean());
        Assert.IsTrue(File.Exists(status.LogPath));
        Assert.IsTrue(File.Exists(status.RecoveryStatePath));
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
        var history = runner.Run(new OperationRequest(
            "job.backup.directory.history",
            new Dictionary<string, JsonElement>
            {
                ["file"] = Json("file", jobPath)
            },
            "job-history"), Path.Combine(root, "ops"));
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
        var list = runner.Run(new OperationRequest(
            "job.backup.directory.list",
            new Dictionary<string, JsonElement>
            {
                ["directory"] = Json("directory", Path.Combine(root, "jobs"))
            },
            "job-list"), Path.Combine(root, "ops"));
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
        Assert.AreEqual(OperationState.Succeeded, history.State);
        Assert.AreEqual(0, history.Result!.Value.GetProperty("entryCount").GetInt32());
        Assert.AreEqual("operation-job", history.Result.Value.GetProperty("jobId").GetString());
        Assert.AreEqual(OperationState.Succeeded, export.State);
        Assert.IsTrue(File.Exists(exportPath));
        Assert.AreEqual(OperationState.Succeeded, import.State);
        Assert.IsTrue(File.Exists(importPath));
        Assert.AreEqual(OperationState.Succeeded, list.State);
        Assert.AreEqual(1, list.Result!.Value.GetProperty("loadedCount").GetInt32());
        Assert.AreEqual("Operation Job Updated", list.Result.Value.GetProperty("jobs")[0].GetProperty("job").GetProperty("name").GetString());
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
