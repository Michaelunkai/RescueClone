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
    public void SaveWritesValidatedBackupJobDefinition()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var path = Path.Combine(root, "jobs", "daily-docs.json");
        var job = new BackupJobDefinition(
            "daily-docs",
            "Daily Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "daily.rcimg"),
            CompressionMode.High,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"));

        var saved = new BackupJobRunner().Save(path, job);
        var loaded = new BackupJobRunner().Load(path);

        Assert.AreEqual(job.JobId, saved.JobId);
        Assert.AreEqual(job.JobId, loaded.JobId);
        Assert.AreEqual(CompressionMode.High, loaded.Compression);
        Assert.IsTrue(loaded.VerifyAfterCreate);
    }

    [TestMethod]
    public void DeleteRemovesBackupJobDefinition()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var path = Path.Combine(root, "jobs", "delete-me.json");
        var job = new BackupJobDefinition(
            "delete-me",
            "Delete Me",
            Enabled: true,
            source,
            Path.Combine(root, "images", "delete-me.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"));
        var runner = new BackupJobRunner();
        runner.Save(path, job);

        var report = runner.Delete(path);

        Assert.IsTrue(report.Deleted);
        Assert.AreEqual(Path.GetFullPath(path), report.Path);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void DeleteRejectsMissingBackupJobDefinition()
    {
        var root = NewTempDirectory();
        var path = Path.Combine(root, "missing.json");

        Assert.ThrowsException<FileNotFoundException>(() => new BackupJobRunner().Delete(path));
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
        Assert.IsTrue(File.Exists(result.HtmlReportPath));
        StringAssert.Contains(File.ReadAllText(result.HtmlReportPath), "RescueClone Backup Report");

        var log = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(result.LogPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(log);
        Assert.AreEqual(result.RootSha256, log.RootSha256);
        Assert.AreEqual(result.HtmlReportPath, log.HtmlReportPath);
    }

    [TestMethod]
    public void RunRetriesTransientCreateFailureAndRecordsAttempts()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var image = Path.Combine(root, "images", "retry.rcimg");
        var engine = new FlakyImageEngine(image);
        var job = new BackupJobDefinition(
            "retry-docs",
            "Retry Docs",
            Enabled: true,
            source,
            image,
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            RetryCount: 1,
            RetryDelaySeconds: 0);

        var result = new BackupJobRunner(engine).Run(job);

        Assert.AreEqual(2, engine.CreateCalls);
        Assert.IsTrue(result.Verified);
        Assert.IsNotNull(result.RetryAttempts);
        Assert.AreEqual(2, result.RetryAttempts.Count);
        Assert.IsFalse(result.RetryAttempts[0].Succeeded);
        Assert.IsTrue(result.RetryAttempts[1].Succeeded);
        StringAssert.Contains(File.ReadAllText(result.HtmlReportPath), "Retry Attempts");

        var log = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(result.LogPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(log);
        Assert.IsNotNull(log.RetryAttempts);
        Assert.AreEqual(2, log.RetryAttempts.Count);
    }

    [TestMethod]
    public void RunPerformsRestoreTestAndRecordsReport()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var restoreTarget = Path.Combine(root, "restore-test");
        var job = new BackupJobDefinition(
            "restore-test-docs",
            "Restore Test Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "restore-test.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            RestoreTestAfterCreate: true,
            RestoreTestTargetPath: restoreTarget);

        var result = new BackupJobRunner().Run(job);

        Assert.IsNotNull(result.RestoreTest);
        Assert.AreEqual(restoreTarget, result.RestoreTest.TargetPath);
        Assert.AreEqual("alpha", File.ReadAllText(Path.Combine(restoreTarget, "alpha.txt")));
        StringAssert.Contains(File.ReadAllText(result.HtmlReportPath), "Restore Test");

        var log = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(result.LogPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(log);
        Assert.IsNotNull(log.RestoreTest);
        Assert.AreEqual(1, log.RestoreTest.FileCount);
    }

    [TestMethod]
    public void RunAppliesRetentionAfterCreateAndRecordsReport()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var imageDirectory = Path.Combine(root, "images");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(imageDirectory);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var oldImage = Path.Combine(imageDirectory, "old.rcimg");
        File.WriteAllText(oldImage, "old");
        File.SetLastWriteTimeUtc(oldImage, DateTimeOffset.UtcNow.AddDays(-2).UtcDateTime);
        var newImage = Path.Combine(imageDirectory, "new.rcimg");
        var job = new BackupJobDefinition(
            "retention-docs",
            "Retention Docs",
            Enabled: true,
            source,
            newImage,
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            ApplyRetentionAfterCreate: true,
            RetentionKeepCount: 0);

        var result = new BackupJobRunner().Run(job);

        Assert.IsTrue(File.Exists(newImage));
        Assert.IsFalse(File.Exists(oldImage));
        Assert.IsNotNull(result.Retention);
        Assert.AreEqual(1, result.Retention.DeletedFileCount);
        StringAssert.Contains(File.ReadAllText(result.HtmlReportPath), "Retention");

        var log = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(result.LogPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(log);
        Assert.IsNotNull(log.Retention);
        Assert.AreEqual(1, log.Retention.DeletedFileCount);
    }

    [TestMethod]
    public void RunExecutesPreAndPostBackupScriptHooks()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var hookLog = Path.Combine(root, "hook.log");
        var preHook = WriteHook(root, "pre.cmd", hookLog);
        var postHook = WriteHook(root, "post.cmd", hookLog);
        var job = new BackupJobDefinition(
            "hooked-docs",
            "Hooked Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "hooked.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            PreBackupScriptPath: preHook,
            PostBackupScriptPath: postHook);

        var result = new BackupJobRunner().Run(job);

        Assert.IsNotNull(result.ScriptHooks);
        Assert.AreEqual(2, result.ScriptHooks.Count);
        CollectionAssert.AreEqual(new[] { "pre-backup", "post-backup" }, result.ScriptHooks.Select(h => h.Phase).ToArray());
        Assert.IsTrue(result.ScriptHooks.All(h => !h.TimedOut));
        Assert.IsTrue(File.ReadAllText(hookLog).Contains("hooked-docs|pre-backup", StringComparison.Ordinal));
        Assert.IsTrue(File.ReadAllText(hookLog).Contains("hooked-docs|post-backup", StringComparison.Ordinal));

        var log = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(result.LogPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(log);
        Assert.IsNotNull(log.ScriptHooks);
        Assert.AreEqual(2, log.ScriptHooks.Count);
    }

    [TestMethod]
    public void RunRotatesJsonAndHtmlReportsByKeepCount()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var logDirectory = Path.Combine(root, "logs");
        var job = new BackupJobDefinition(
            "rotating-docs",
            "Rotating Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "rotating.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: logDirectory,
            LogRetentionCount: 1);

        var runner = new BackupJobRunner();
        runner.Run(job);
        System.Threading.Thread.Sleep(1100);
        var second = runner.Run(job);

        CollectionAssert.AreEqual(new[] { second.LogPath }, Directory.GetFiles(logDirectory, "rotating-docs-*.json"));
        CollectionAssert.AreEqual(new[] { second.HtmlReportPath }, Directory.GetFiles(logDirectory, "rotating-docs-*.html"));
    }

    [TestMethod]
    public void RunCanWriteWindowsEventLogNotification()
    {
        var eventCreate = Path.Combine(Environment.SystemDirectory, "eventcreate.exe");
        Assert.IsTrue(File.Exists(eventCreate), "eventcreate.exe is required on supported Windows targets.");
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var job = new BackupJobDefinition(
            "event-log-docs",
            "Event Log Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "event-log.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            NotifyWindowsEventLog: true);

        var result = new BackupJobRunner().Run(job);

        Assert.IsNotNull(result.WindowsEventLogNotification);
        Assert.IsTrue(result.WindowsEventLogNotification.Requested);
        Assert.IsTrue(result.WindowsEventLogNotification.Succeeded, result.WindowsEventLogNotification.Message);
        StringAssert.Contains(File.ReadAllText(result.HtmlReportPath), "Notifications");

        var log = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(result.LogPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(log);
        Assert.IsNotNull(log.WindowsEventLogNotification);
        Assert.IsTrue(log.WindowsEventLogNotification.Succeeded, log.WindowsEventLogNotification.Message);
    }

    [TestMethod]
    public void RunCanWriteEmailNotificationToPickupDirectory()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var pickup = Path.Combine(root, "pickup");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var job = new BackupJobDefinition(
            "email-docs",
            "Email Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "email.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            NotifyEmail: true,
            EmailFrom: "rescueclone@example.invalid",
            EmailTo: "operator@example.invalid",
            EmailPickupDirectory: pickup);

        var result = new BackupJobRunner().Run(job);

        Assert.IsNotNull(result.EmailNotification);
        Assert.IsTrue(result.EmailNotification.Succeeded, result.EmailNotification.Message);
        Assert.AreEqual(1, Directory.GetFiles(pickup, "*.eml").Length);
        StringAssert.Contains(File.ReadAllText(result.HtmlReportPath), "Email");

        var log = JsonSerializer.Deserialize<BackupJobRunResult>(File.ReadAllText(result.LogPath), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(log);
        Assert.IsNotNull(log.EmailNotification);
        Assert.IsTrue(log.EmailNotification.Succeeded, log.EmailNotification.Message);
    }

    [TestMethod]
    public void RunWritesFailureEmailBeforeRethrowingOriginalFailure()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        var pickup = Path.Combine(root, "pickup");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var failingHook = Path.Combine(root, "fail.cmd");
        File.WriteAllText(failingHook, """
        @echo off
        echo failing email hook
        exit /b 17
        """);
        var job = new BackupJobDefinition(
            "failure-email-docs",
            "Failure Email Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "failure-email.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            PreBackupScriptPath: failingHook,
            NotifyEmail: true,
            EmailFrom: "rescueclone@example.invalid",
            EmailTo: "operator@example.invalid",
            EmailPickupDirectory: pickup);

        var ex = Assert.ThrowsException<InvalidOperationException>(() => new BackupJobRunner().Run(job));

        StringAssert.Contains(ex.Message, "pre-backup script failed with exit code 17");
        Assert.AreEqual(1, Directory.GetFiles(pickup, "*.eml").Length);
        StringAssert.Contains(File.ReadAllText(Directory.GetFiles(pickup, "*.eml").Single()), "failure-email-docs");
    }

    [TestMethod]
    public void RunRethrowsOriginalFailureWhenFailureNotificationIsEnabled()
    {
        var eventCreate = Path.Combine(Environment.SystemDirectory, "eventcreate.exe");
        Assert.IsTrue(File.Exists(eventCreate), "eventcreate.exe is required on supported Windows targets.");
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var failingHook = Path.Combine(root, "fail.cmd");
        File.WriteAllText(failingHook, """
        @echo off
        echo failing hook
        exit /b 17
        """);
        var job = new BackupJobDefinition(
            "failure-event-log-docs",
            "Failure Event Log Docs",
            Enabled: true,
            source,
            Path.Combine(root, "images", "failure-event-log.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            PreBackupScriptPath: failingHook,
            NotifyWindowsEventLog: true);

        var ex = Assert.ThrowsException<InvalidOperationException>(() => new BackupJobRunner().Run(job));

        StringAssert.Contains(ex.Message, "pre-backup script failed with exit code 17");
    }

    [TestMethod]
    public void RunFailsWhenScriptHookExceedsTimeout()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var slowHook = Path.Combine(root, "slow.cmd");
        File.WriteAllText(slowHook, """
        @echo off
        ping -n 6 127.0.0.1 >nul
        """);
        var job = new BackupJobDefinition(
            "slow-hook",
            "Slow Hook",
            Enabled: true,
            source,
            Path.Combine(root, "images", "slow.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            PreBackupScriptPath: slowHook,
            ScriptHookTimeoutSeconds: 1);

        Assert.ThrowsException<TimeoutException>(() => new BackupJobRunner().Run(job));
    }

    [TestMethod]
    public void ValidateRejectsMissingScriptHook()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var job = new BackupJobDefinition(
            "missing-hook",
            "Missing Hook",
            Enabled: true,
            source,
            Path.Combine(root, "out.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            PreBackupScriptPath: Path.Combine(root, "missing.cmd"));

        var result = new BackupJobRunner().Validate(job);

        Assert.IsFalse(result.Valid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("PreBackupScriptPath", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateRejectsInvalidScriptHookTimeout()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var job = new BackupJobDefinition(
            "bad-timeout",
            "Bad Timeout",
            Enabled: true,
            source,
            Path.Combine(root, "out.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            ScriptHookTimeoutSeconds: 0);

        var result = new BackupJobRunner().Validate(job);

        Assert.IsFalse(result.Valid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("ScriptHookTimeoutSeconds", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateRejectsInvalidLogRetentionCount()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var job = new BackupJobDefinition(
            "bad-retention",
            "Bad Retention",
            Enabled: true,
            source,
            Path.Combine(root, "out.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            LogRetentionCount: 0);

        var result = new BackupJobRunner().Validate(job);

        Assert.IsFalse(result.Valid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("LogRetentionCount", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateRejectsInvalidRetrySettings()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var job = new BackupJobDefinition(
            "bad-retry",
            "Bad Retry",
            Enabled: true,
            source,
            Path.Combine(root, "out.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            RetryCount: -1,
            RetryDelaySeconds: -1);

        var result = new BackupJobRunner().Validate(job);

        Assert.IsFalse(result.Valid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("RetryCount", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(e => e.Contains("RetryDelaySeconds", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateRejectsRestoreTestTargetWithoutRestoreTest()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var job = new BackupJobDefinition(
            "bad-restore-test",
            "Bad Restore Test",
            Enabled: true,
            source,
            Path.Combine(root, "out.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            RestoreTestAfterCreate: false,
            RestoreTestTargetPath: Path.Combine(root, "restore-test"));

        var result = new BackupJobRunner().Validate(job);

        Assert.IsFalse(result.Valid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("RestoreTestTargetPath", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateRejectsRetentionPolicyWithoutRetentionAndInvalidRetentionSettings()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var job = new BackupJobDefinition(
            "bad-retention",
            "Bad Retention",
            Enabled: true,
            source,
            Path.Combine(root, "out.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            ApplyRetentionAfterCreate: false,
            RetentionPattern: "*.rcimg",
            RetentionKeepCount: -1,
            RetentionMaxAgeDays: -1,
            RetentionMinFreeBytes: -1);

        var result = new BackupJobRunner().Validate(job);

        Assert.IsFalse(result.Valid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("ApplyRetentionAfterCreate", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(e => e.Contains("RetentionKeepCount", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(e => e.Contains("RetentionMaxAgeDays", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(e => e.Contains("RetentionMinFreeBytes", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ValidateRejectsIncompleteEmailConfiguration()
    {
        var root = NewTempDirectory();
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var job = new BackupJobDefinition(
            "bad-email",
            "Bad Email",
            Enabled: true,
            source,
            Path.Combine(root, "out.rcimg"),
            CompressionMode.Medium,
            Password: null,
            VerifyAfterCreate: true,
            LogDirectory: Path.Combine(root, "logs"),
            NotifyEmail: true,
            EmailFrom: "rescueclone@example.invalid");

        var result = new BackupJobRunner().Validate(job);

        Assert.IsFalse(result.Valid);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("EmailTo", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(e => e.Contains("EmailPickupDirectory", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void FeatureCatalogIncludesBackupJobCreateParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "job.backup.directory.create");

        Assert.AreEqual("Backup Job", feature.Gui);
        Assert.AreEqual("rc job create", feature.Cli);
        Assert.AreEqual("New-RCBackupJob", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }

    [TestMethod]
    public void FeatureCatalogIncludesBackupJobDeleteParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "job.backup.directory.delete");

        Assert.AreEqual("Backup Job", feature.Gui);
        Assert.AreEqual("rc job delete", feature.Cli);
        Assert.AreEqual("Remove-RCBackupJob", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
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

    private static string WriteHook(string root, string name, string hookLog)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, $"""
        @echo off
        echo %RESCUECLONE_JOB_ID%^|%RESCUECLONE_HOOK_PHASE%^|%RESCUECLONE_SOURCE_PATH%^|%RESCUECLONE_IMAGE_PATH%>>"{hookLog}"
        """);
        return path;
    }

    private sealed class FlakyImageEngine : IImageEngine
    {
        private readonly string _imagePath;

        public FlakyImageEngine(string imagePath)
        {
            _imagePath = imagePath;
        }

        public int CreateCalls { get; private set; }

        public ImageReport Create(ImageOptions options)
        {
            CreateCalls++;
            if (CreateCalls == 1)
                throw new IOException("Simulated transient create failure.");
            Directory.CreateDirectory(Path.GetDirectoryName(_imagePath)!);
            File.WriteAllText(_imagePath, "retry-image");
            return new ImageReport(_imagePath, 1, 5, 5, "abc123", new[] { new ImageFileEntry("alpha.txt", 5, 5, "abc123") }, FormatVersion: 2);
        }

        public ImageReport Verify(string imagePath, string? password)
        {
            return new ImageReport(imagePath, 1, 5, 5, "verified123", new[] { new ImageFileEntry("alpha.txt", 5, 5, "verified123") }, FormatVersion: 2);
        }

        public RestoreReport Restore(RestoreOptions options)
        {
            throw new NotSupportedException();
        }
    }
}
