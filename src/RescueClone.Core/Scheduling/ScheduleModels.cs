namespace RescueClone.Core.Scheduling;

public enum ScheduleFrequency
{
    Daily,
    Weekly,
    Monthly
}

public sealed record ScheduleDefinition(
    string TaskName,
    string JobFilePath,
    string CliPath,
    ScheduleFrequency Frequency,
    TimeOnly StartTime,
    bool RunMissedOnStart);

public sealed record SchedulePlan(
    string TaskName,
    string JobFilePath,
    string CliPath,
    ScheduleFrequency Frequency,
    TimeOnly StartTime,
    bool RunMissedOnStart,
    string TaskXml);

public sealed record ScheduleRegistrationReport(
    string TaskName,
    string Action,
    bool Succeeded,
    string Output);
