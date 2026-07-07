using System.Diagnostics;
using System.Security;
using System.Text;

namespace RescueClone.Core.Scheduling;

public sealed class ScheduleManager
{
    public SchedulePlan Plan(ScheduleDefinition definition)
    {
        Validate(definition);
        var xml = BuildTaskXml(definition);
        return new SchedulePlan(
            definition.TaskName,
            definition.JobFilePath,
            definition.CliPath,
            definition.Frequency,
            definition.StartTime,
            definition.RunMissedOnStart,
            definition.EventLogName,
            definition.EventId,
            definition.EventSource,
            xml);
    }

    public ScheduleRegistrationReport Register(ScheduleDefinition definition)
    {
        var plan = Plan(definition);
        var xmlPath = Path.Combine(Path.GetTempPath(), $"rescueclone-{Guid.NewGuid():N}.xml");
        File.WriteAllText(xmlPath, plan.TaskXml, Encoding.Unicode);
        try
        {
            return RunSchTasks("/Create", "/TN", NormalizeTaskName(definition.TaskName), "/XML", xmlPath, "/F");
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    public ScheduleRegistrationReport Unregister(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("TaskName is required.");
        return RunSchTasks("/Delete", "/TN", NormalizeTaskName(taskName), "/F");
    }

    public ScheduleRegistrationReport Status(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("TaskName is required.");
        return RunSchTasks("/Query", "/TN", NormalizeTaskName(taskName), "/FO", "LIST", "/V");
    }

    public ScheduleRegistrationReport RunNow(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("TaskName is required.");
        return RunSchTasks("/Run", "/TN", NormalizeTaskName(taskName));
    }

    private static void Validate(ScheduleDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.TaskName))
            throw new ArgumentException("TaskName is required.");
        if (string.IsNullOrWhiteSpace(definition.JobFilePath))
            throw new ArgumentException("JobFilePath is required.");
        if (!File.Exists(definition.JobFilePath))
            throw new FileNotFoundException(definition.JobFilePath);
        if (string.IsNullOrWhiteSpace(definition.CliPath))
            throw new ArgumentException("CliPath is required.");
        if (!File.Exists(definition.CliPath))
            throw new FileNotFoundException(definition.CliPath);
        if (definition.Frequency == ScheduleFrequency.Event)
        {
            if (string.IsNullOrWhiteSpace(definition.EventLogName))
                throw new ArgumentException("EventLogName is required for event schedules.");
            if (definition.EventId is null or <= 0)
                throw new ArgumentException("EventId must be greater than zero for event schedules.");
        }
    }

    private static string BuildTaskXml(ScheduleDefinition definition)
    {
        var escapedCli = SecurityElement.Escape(Path.GetFullPath(definition.CliPath));
        var escapedJob = SecurityElement.Escape(Path.GetFullPath(definition.JobFilePath));
        var trigger = BuildTriggerXml(definition);

        return $$"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Author>RescueClone</Author>
            <Description>RescueClone scheduled backup job</Description>
          </RegistrationInfo>
          <Triggers>
            {{trigger}}
          </Triggers>
          <Principals>
            <Principal id="Author">
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>LeastPrivilege</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <AllowHardTerminate>true</AllowHardTerminate>
            <StartWhenAvailable>{{definition.RunMissedOnStart.ToString().ToLowerInvariant()}}</StartWhenAvailable>
            <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
            <IdleSettings>
              <StopOnIdleEnd>false</StopOnIdleEnd>
              <RestartOnIdle>false</RestartOnIdle>
            </IdleSettings>
            <AllowStartOnDemand>true</AllowStartOnDemand>
            <Enabled>true</Enabled>
            <Hidden>false</Hidden>
            <RunOnlyIfIdle>false</RunOnlyIfIdle>
            <WakeToRun>false</WakeToRun>
            <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
            <Priority>7</Priority>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{{escapedCli}}</Command>
              <Arguments>job run --file "{{escapedJob}}"</Arguments>
            </Exec>
          </Actions>
        </Task>
        """;
    }

    private static string BuildTriggerXml(ScheduleDefinition definition)
    {
        if (definition.Frequency == ScheduleFrequency.Event)
        {
            var eventLog = definition.EventLogName!.Trim();
            var providerClause = string.IsNullOrWhiteSpace(definition.EventSource)
                ? string.Empty
                : $" and *[System[Provider[@Name='{definition.EventSource.Trim()}']]]";
            var subscription = SecurityElement.Escape($"<QueryList><Query Id=\"0\" Path=\"{eventLog}\"><Select Path=\"{eventLog}\">*[System[(EventID={definition.EventId})]]{providerClause}</Select></Query></QueryList>");

            return $$"""
            <EventTrigger>
              <Enabled>true</Enabled>
              <Subscription>{{subscription}}</Subscription>
            </EventTrigger>
            """;
        }

        var startBoundary = DateTime.Today.Add(definition.StartTime.ToTimeSpan()).ToString("s");
        var trigger = definition.Frequency switch
        {
            ScheduleFrequency.Daily => "<ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>",
            ScheduleFrequency.Weekly => "<ScheduleByWeek><WeeksInterval>1</WeeksInterval><DaysOfWeek><Monday /></DaysOfWeek></ScheduleByWeek>",
            ScheduleFrequency.Monthly => "<ScheduleByMonth><DaysOfMonth><Day>1</Day></DaysOfMonth><Months><January /><February /><March /><April /><May /><June /><July /><August /><September /><October /><November /><December /></Months></ScheduleByMonth>",
            _ => throw new ArgumentOutOfRangeException(nameof(definition.Frequency))
        };

        return $$"""
            <CalendarTrigger>
              <StartBoundary>{{startBoundary}}</StartBoundary>
              <Enabled>true</Enabled>
              {{trigger}}
            </CalendarTrigger>
            """;
    }

    private static ScheduleRegistrationReport RunSchTasks(params string[] args)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }.WithArguments(args)) ?? throw new InvalidOperationException("Could not start schtasks.exe.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ScheduleRegistrationReport(
            TaskName: args.SkipWhile(a => !string.Equals(a, "/TN", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault() ?? string.Empty,
            Action: args.FirstOrDefault() ?? string.Empty,
            Succeeded: process.ExitCode == 0,
            Output: string.IsNullOrWhiteSpace(error) ? output.Trim() : $"{output.Trim()}{Environment.NewLine}{error.Trim()}".Trim());
    }

    private static string NormalizeTaskName(string taskName)
    {
        var name = taskName.Trim();
        return name.StartsWith("\\", StringComparison.Ordinal) ? name : $"\\RescueClone\\{name}";
    }
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo WithArguments(this ProcessStartInfo info, IEnumerable<string> args)
    {
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        return info;
    }
}
