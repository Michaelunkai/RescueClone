namespace RescueClone.Core.Scheduling;

public enum ScheduleFrequency
{
    Daily,
    Weekly,
    Monthly,
    Event
}

public sealed record ScheduleDefinition(
    string TaskName,
    string JobFilePath,
    string CliPath,
    ScheduleFrequency Frequency,
    TimeOnly StartTime,
    bool RunMissedOnStart,
    string? EventLogName = null,
    int? EventId = null,
    string? EventSource = null);

public sealed record SchedulePlan(
    string TaskName,
    string JobFilePath,
    string CliPath,
    ScheduleFrequency Frequency,
    TimeOnly StartTime,
    bool RunMissedOnStart,
    string? EventLogName,
    int? EventId,
    string? EventSource,
    string TaskXml);

public sealed record ScheduleRegistrationReport(
    string TaskName,
    string Action,
    bool Succeeded,
    string Output);
