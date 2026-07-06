using RescueClone.Core;
using RescueClone.Core.Scheduling;

namespace RescueClone.Tests;

[TestClass]
public sealed class ScheduleManagerTests
{
    [TestMethod]
    public void PlanCreatesDailyTaskXmlWithJobRunAction()
    {
        var root = NewTempDirectory();
        var job = WriteFile(root, "job.json");
        var cli = WriteFile(root, "rc.exe");

        var plan = new ScheduleManager().Plan(new ScheduleDefinition(
            "nightly-docs",
            job,
            cli,
            ScheduleFrequency.Daily,
            new TimeOnly(2, 30),
            RunMissedOnStart: true));

        StringAssert.Contains(plan.TaskXml, "<ScheduleByDay>");
        StringAssert.Contains(plan.TaskXml, "<StartWhenAvailable>true</StartWhenAvailable>");
        StringAssert.Contains(plan.TaskXml, "T02:30:00");
        StringAssert.Contains(plan.TaskXml, "<Command>");
        StringAssert.Contains(plan.TaskXml, "job run --file");
        StringAssert.Contains(plan.TaskXml, Path.GetFullPath(job));
        StringAssert.Contains(plan.TaskXml, "<Principal id=\"Author\">");
        Assert.AreEqual("nightly-docs", plan.TaskName);
    }

    [TestMethod]
    public void PlanCreatesWeeklyAndMonthlyTaskXml()
    {
        var root = NewTempDirectory();
        var job = WriteFile(root, "job.json");
        var cli = WriteFile(root, "rc.exe");

        var weekly = new ScheduleManager().Plan(new ScheduleDefinition("weekly-docs", job, cli, ScheduleFrequency.Weekly, new TimeOnly(3, 0), false));
        var monthly = new ScheduleManager().Plan(new ScheduleDefinition("monthly-docs", job, cli, ScheduleFrequency.Monthly, new TimeOnly(4, 0), false));

        StringAssert.Contains(weekly.TaskXml, "<ScheduleByWeek>");
        StringAssert.Contains(weekly.TaskXml, "<Monday />");
        StringAssert.Contains(monthly.TaskXml, "<ScheduleByMonth>");
        StringAssert.Contains(monthly.TaskXml, "<Day>1</Day>");
    }

    [TestMethod]
    public void FeatureCatalogIncludesScheduleParity()
    {
        var plan = FeatureCatalog.All.Single(f => f.FeatureId == "schedule.plan");
        var register = FeatureCatalog.All.Single(f => f.FeatureId == "schedule.register");
        var unregister = FeatureCatalog.All.Single(f => f.FeatureId == "schedule.unregister");

        Assert.AreEqual("Scheduler", plan.Gui);
        Assert.AreEqual("rc schedule plan", plan.Cli);
        Assert.AreEqual("Get-RCSchedulePlan", plan.PowerShell);
        Assert.AreEqual("Scheduler", register.Gui);
        Assert.AreEqual("rc schedule register", register.Cli);
        Assert.AreEqual("Register-RCSchedule", register.PowerShell);
        Assert.AreEqual("Scheduler", unregister.Gui);
        Assert.AreEqual("rc schedule unregister", unregister.Cli);
        Assert.AreEqual("Unregister-RCSchedule", unregister.PowerShell);
    }

    private static string WriteFile(string root, string name)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, "{}");
        return path;
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "rescueclone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
