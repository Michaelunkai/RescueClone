using System.Text.Json;
using RescueClone.Core;
using RescueClone.Core.Jobs;
using RescueClone.Core.Logs;

namespace RescueClone.Tests;

[TestClass]
public sealed class BackupLogCatalogTests
{
    [TestMethod]
    public void ListReturnsParsedAndMalformedLogEntries()
    {
        var root = NewTempDirectory();
        var validPath = Path.Combine(root, "valid.json");
        var invalidPath = Path.Combine(root, "invalid.json");
        var started = DateTimeOffset.UtcNow.AddMinutes(-1);
        var finished = DateTimeOffset.UtcNow;
        var report = new BackupJobRunResult(
            "daily-docs",
            "Daily Docs",
            Path.Combine(root, "daily.rcimg"),
            Verified: true,
            RootSha256: "abc123",
            FileCount: 1,
            OriginalBytes: 5,
            StoredBytes: 5,
            validPath,
            Path.Combine(root, "valid.html"),
            started,
            finished);
        File.WriteAllText(validPath, JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        File.WriteAllText(invalidPath, "{not-json");

        var entries = new BackupLogCatalog().List(new LogListOptions(root));

        Assert.AreEqual(2, entries.Count);
        var parsed = entries.Single(entry => entry.Parsed);
        Assert.AreEqual("daily-docs", parsed.JobId);
        Assert.AreEqual("Daily Docs", parsed.Name);
        Assert.AreEqual("abc123", parsed.RootSha256);
        Assert.IsTrue(parsed.Verified);
        var malformed = entries.Single(entry => !entry.Parsed);
        Assert.AreEqual(invalidPath, malformed.Path);
        Assert.IsFalse(string.IsNullOrWhiteSpace(malformed.Error));
    }

    [TestMethod]
    public void FeatureCatalogIncludesLogParity()
    {
        var feature = FeatureCatalog.All.Single(f => f.FeatureId == "logs.backup.list");

        Assert.AreEqual("Logs", feature.Gui);
        Assert.AreEqual("rc logs list", feature.Cli);
        Assert.AreEqual("Get-RCLog", feature.PowerShell);
        Assert.IsTrue(feature.Implemented);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
