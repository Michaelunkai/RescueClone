using System.Text.Json;
using RescueClone.Core;
using RescueClone.Core.Jobs;

namespace RescueClone.Tests;

[TestClass]
public sealed class BackupJobRunnerTests
{
    [TestMethod]
    public void ValidateRejectsMissingSource()
    {
        var root = NewTempDirectory();
        var job = new BackupJobDefinition(
            "missing-source",
            "Missing Source",
            Enabled: true,
            Path.Combine(root, "missing"),
            Path.Combine(root, "out", "backup.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"));

        var result = new BackupJobRunner().Validate(job);

        Assert.IsFalse(result.Valid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("SourcePath", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RunCreatesVerifiesAndLogsBackupJob()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var job = new BackupJobDefinition(
            "daily-docs",
            "Daily Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "daily.rcimg"),
            CompressionMode.High,
            Password: "secret",
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"));

        var result = new BackupJobRunner().Run(job);

        Assert.AreEqual("daily-docs", result.JobId);
        Assert.IsTrue(result.Verified);
        Assert.AreEqual(1, result.FileCount);
        Assert.IsTrue(File.Exists(result.ImagePath));
        Assert.IsTrue(File.Exists(result.LogPath));

        var log = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(result.LogPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(log);
        Assert.AreEqual(result.RootSha256, log.RootSha256);
    }

    [TestMethod]
    public void LoadAcceptsStringCompressionFromJson()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var jobPath = Path.Combine(root, "job.json");
        File.WriteAllText(jobPath, $$"""
        {
          "jobId": "json-job",
          "name": "Json Job",
          "enabled": true,
          "sourcePath": "{{source.Replace("\\", "\\\\")}}",
          "imagePath": "{{Path.Combine(root, "out.rcimg").Replace("\\", "\\\\")}}",
          "compression": "High",
          "password": null,
          "verifyAfterCreate": true,
          "logDirectory": null
        }
        """);

        var job = new BackupJobRunner().Load(jobPath);

        Assert.AreEqual(CompressionMode.High, job.Compression);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
