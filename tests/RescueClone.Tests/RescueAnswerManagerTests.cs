using RescueClone.Core;
using RescueClone.Core.Rescue;
using RescueClone.Core.RestorePlanning;

namespace RescueClone.Tests;

[TestClass]
public sealed class RescueAnswerManagerTests
{
    [TestMethod]
    public void CreateWritesValidUnattendedAnswerAndValidateVerifiesImage()
    {
        var context = NewContext();
        var output = Path.Combine(context.Root, "rescue-answer.json");
        var target = Path.Combine(context.Root, "executed-restore");

        var created = new RescueAnswerManager().Create(new RescueAnswerOptions(
            output,
            context.Repository,
            Path.GetFileName(context.Image),
            "secret",
            "disk-fixture-1",
            RestoreBootMode.Bios,
            1024 * 1024,
            null,
            TargetIsCurrentSystemDisk: false,
            HasEfiSystemPartition: false,
            context.BcdPath,
            new[] { context.DriverDirectory },
            new[] { @"\\backup\repository" },
            RepairBoot: true,
            RebootAfterRestore: true,
            VerifyImage: true,
            target));

        Assert.IsTrue(File.Exists(output));
        Assert.IsTrue(created.Valid, string.Join(Environment.NewLine, created.Blockers));
        Assert.IsTrue(created.ImageVerified);
        Assert.IsNotNull(created.RestorePlan);
        Assert.IsTrue(created.RestorePlan.CanProceed);

        var validated = new RescueAnswerManager().Validate(output, verifyImage: true);

        Assert.IsTrue(validated.Valid, string.Join(Environment.NewLine, validated.Blockers));
        Assert.IsTrue(validated.ImageVerified);
        Assert.AreEqual(context.Image, validated.RestorePlan!.ImagePath);

        var executed = new RescueAnswerManager().Execute(output, verifyImage: true, overwrite: false);

        Assert.IsTrue(executed.Valid, string.Join(Environment.NewLine, executed.Blockers));
        Assert.IsTrue(executed.DirectoryRestorePerformed);
        Assert.AreEqual("alpha", File.ReadAllText(Path.Combine(target, "alpha.txt")));
    }

    [TestMethod]
    public void ValidateReportsMissingDriverDirectory()
    {
        var context = NewContext();
        var output = Path.Combine(context.Root, "rescue-answer.json");
        new RescueAnswerManager().Create(new RescueAnswerOptions(
            output,
            context.Repository,
            context.Image,
            "secret",
            "disk-fixture-1",
            RestoreBootMode.Bios,
            1024 * 1024,
            null,
            TargetIsCurrentSystemDisk: false,
            HasEfiSystemPartition: false,
            context.BcdPath,
            new[] { context.DriverDirectory },
            Array.Empty<string>(),
            RepairBoot: true,
            RebootAfterRestore: false,
            VerifyImage: false));
        Directory.Delete(context.DriverDirectory);

        var report = new RescueAnswerManager().Validate(output, verifyImage: false);

        Assert.IsFalse(report.Valid);
        Assert.IsTrue(report.Blockers.Any(b => b.Contains("Driver directory", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void FeatureCatalogIncludesRescueAnswerParity()
    {
        var create = FeatureCatalog.All.Single(f => f.FeatureId == "rescue.answer.create");
        var validate = FeatureCatalog.All.Single(f => f.FeatureId == "rescue.answer.validate");
        var execute = FeatureCatalog.All.Single(f => f.FeatureId == "rescue.answer.execute");

        Assert.AreEqual("Rescue", create.Gui);
        Assert.AreEqual("rc rescue answer-create", create.Cli);
        Assert.AreEqual("New-RCRescueAnswer", create.PowerShell);
        Assert.IsTrue(create.Implemented);
        Assert.AreEqual("Rescue", validate.Gui);
        Assert.AreEqual("rc rescue answer-validate", validate.Cli);
        Assert.AreEqual("Test-RCRescueAnswer", validate.PowerShell);
        Assert.IsTrue(validate.Implemented);
        Assert.AreEqual("Rescue", execute.Gui);
        Assert.AreEqual("rc rescue answer-execute", execute.Cli);
        Assert.AreEqual("Start-RCRescueAnswer", execute.PowerShell);
        Assert.IsTrue(execute.Implemented);
    }

    private static TestContextData NewContext()
    {
        var root = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var repository = Path.Combine(root, "repo");
        var drivers = Path.Combine(root, "drivers");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(repository);
        Directory.CreateDirectory(drivers);
        File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
        var image = Path.Combine(repository, "backup.rcimg");
        new ImageEngine().Create(new ImageOptions(source, image, CompressionMode.Medium, "secret"));
        var bcd = Path.Combine(root, "BCD");
        File.WriteAllText(bcd, "fixture");
        return new TestContextData(root, repository, image, drivers, bcd);
    }

    private sealed record TestContextData(string Root, string Repository, string Image, string DriverDirectory, string BcdPath);
}
