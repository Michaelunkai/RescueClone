using System;
using System.Text.Json;
using System.Windows;
using RescueClone.Core;
using RescueClone.Core.Jobs;
using RescueClone.Core.Logs;
using RescueClone.Core.Native;
using RescueClone.Core.Operations;
using RescueClone.Core.Retention;
using RescueClone.Core.RestorePlanning;
using RescueClone.Core.Scheduling;
using RescueClone.Core.Storage;

namespace RescueClone.App;

public partial class MainWindow : Window
{
    private readonly ImageEngine _engine = new();
    private readonly BackupJobRunner _jobRunner = new();
    private readonly BackupLogCatalog _logCatalog = new();
    private readonly RestorePlanner _restorePlanner = new();
    private readonly OperationRunner _operationRunner = new();
    private readonly RetentionManager _retentionManager = new();
    private readonly ScheduleManager _scheduleManager = new();
    private readonly VolumeEnumerator _volumeEnumerator = new();
    private readonly DiskEnumerator _diskEnumerator = new();

    public MainWindow()
    {
        InitializeComponent();
        FeatureCatalog.AssertImplementedParity();
    }

    private void CreateImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() =>
        {
            var compression = Enum.Parse<CompressionMode>(((System.Windows.Controls.ComboBoxItem)CompressionBox.SelectedItem).Content.ToString()!);
            var format = Enum.Parse<ImageContainerFormat>(((System.Windows.Controls.ComboBoxItem)FormatBox.SelectedItem).Content.ToString()!);
            return _engine.Create(new ImageOptions(SourcePathBox.Text, ImagePathBox.Text, compression, EmptyToNull(CreatePasswordBox.Password), format));
        });
    }

    private void VerifyImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _engine.Verify(VerifyImagePathBox.Text, EmptyToNull(VerifyPasswordBox.Password)));
    }

    private void RestoreImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _engine.Restore(new RestoreOptions(RestoreImagePathBox.Text, TargetPathBox.Text, EmptyToNull(RestorePasswordBox.Password), OverwriteBox.IsChecked == true)));
    }

    private void CreateJob_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() =>
        {
            var compression = Enum.Parse<CompressionMode>(((System.Windows.Controls.ComboBoxItem)JobCompressionBox.SelectedItem).Content.ToString()!);
            var job = new BackupJobDefinition(
                JobIdBox.Text,
                JobNameBox.Text,
                JobEnabledBox.IsChecked == true,
                JobSourcePathBox.Text,
                JobImagePathBox.Text,
                compression,
                Password: null,
                JobVerifyAfterCreateBox.IsChecked == true,
                EmptyToNull(JobLogDirectoryBox.Text));
            return _jobRunner.Save(JobPathBox.Text, job);
        });
    }

    private void ValidateJob_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.Validate(_jobRunner.Load(JobPathBox.Text)));
    }

    private void UpdateJob_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() =>
        {
            var compression = Enum.Parse<CompressionMode>(((System.Windows.Controls.ComboBoxItem)JobCompressionBox.SelectedItem).Content.ToString()!);
            return _jobRunner.Update(JobPathBox.Text, new BackupJobUpdateOptions(
                EmptyToNull(JobIdBox.Text),
                EmptyToNull(JobNameBox.Text),
                JobEnabledBox.IsChecked == true,
                EmptyToNull(JobSourcePathBox.Text),
                EmptyToNull(JobImagePathBox.Text),
                compression,
                Password: null,
                JobVerifyAfterCreateBox.IsChecked == true,
                EmptyToNull(JobLogDirectoryBox.Text)));
        });
    }

    private void DeleteJob_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.Delete(JobPathBox.Text));
    }

    private void RunJob_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.Run(_jobRunner.Load(JobPathBox.Text), ForceDisabledJobBox.IsChecked == true));
    }

    private void PlanRestore_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() =>
        {
            var bootMode = Enum.Parse<RestoreBootMode>(((System.Windows.Controls.ComboBoxItem)PlanBootModeBox.SelectedItem).Content.ToString()!);
            return _restorePlanner.Plan(new RestorePlanOptions(
                PlanImagePathBox.Text,
                EmptyToNull(PlanPasswordBox.Password),
                PlanTargetDiskIdBox.Text,
                ParseNullableLong(PlanTargetDiskSizeBox.Text),
                ParseNullableLong(PlanRequiredBytesBox.Text),
                PlanCurrentSystemDiskBox.IsChecked == true,
                bootMode,
                PlanHasEfiBox.IsChecked == true,
                EmptyToNull(PlanBcdStorePathBox.Text)));
        });
    }

    private void RunOperation_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _operationRunner.Run(
            _operationRunner.LoadRequest(OperationRequestPathBox.Text),
            EmptyToNull(OperationLogDirectoryBox.Text)));
    }

    private void PlanRetention_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _retentionManager.Plan(new RetentionOptions(
            RetentionRepositoryBox.Text,
            string.IsNullOrWhiteSpace(RetentionPatternBox.Text) ? "*.rcimg" : RetentionPatternBox.Text,
            ParseNullableInt(RetentionKeepCountBox.Text),
            ParseNullableInt(RetentionMaxAgeDaysBox.Text),
            ParseNullableLong(RetentionMinFreeBytesBox.Text))));
    }

    private void ApplyRetention_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _retentionManager.Apply(new RetentionOptions(
            RetentionRepositoryBox.Text,
            string.IsNullOrWhiteSpace(RetentionPatternBox.Text) ? "*.rcimg" : RetentionPatternBox.Text,
            ParseNullableInt(RetentionKeepCountBox.Text),
            ParseNullableInt(RetentionMaxAgeDaysBox.Text),
            ParseNullableLong(RetentionMinFreeBytesBox.Text))));
    }

    private void PlanSchedule_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _scheduleManager.Plan(ReadScheduleDefinition()));
    }

    private void RegisterSchedule_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _scheduleManager.Register(ReadScheduleDefinition()));
    }

    private void UnregisterSchedule_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _scheduleManager.Unregister(ScheduleTaskNameBox.Text));
    }

    private void RefreshVolumes_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _volumeEnumerator.ListVolumes());
    }

    private void RefreshDisks_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _diskEnumerator.ListDisks());
    }

    private void ListLogs_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _logCatalog.List(new LogListOptions(
            LogDirectoryBox.Text,
            string.IsNullOrWhiteSpace(LogPatternBox.Text) ? "*.json" : LogPatternBox.Text)));
    }

    private void NativeStatus_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(NativeDiagnostics.GetStatus);
    }

    private void RunAndReport<T>(Func<T> action)
    {
        try
        {
            OutputBox.Text = JsonSerializer.Serialize(action(), new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            OutputBox.Text = ex.Message;
        }
    }

    private static string? EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static int? ParseNullableInt(string value) => string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);

    private static long? ParseNullableLong(string value) => string.IsNullOrWhiteSpace(value) ? null : long.Parse(value);

    private ScheduleDefinition ReadScheduleDefinition()
    {
        var frequency = Enum.Parse<ScheduleFrequency>(((System.Windows.Controls.ComboBoxItem)ScheduleFrequencyBox.SelectedItem).Content.ToString()!);
        return new ScheduleDefinition(
            ScheduleTaskNameBox.Text,
            ScheduleJobFileBox.Text,
            string.IsNullOrWhiteSpace(ScheduleCliPathBox.Text) ? Environment.ProcessPath ?? "rc.exe" : ScheduleCliPathBox.Text,
            frequency,
            TimeOnly.Parse(ScheduleTimeBox.Text),
            ScheduleRunMissedBox.IsChecked == true,
            EmptyToNull(ScheduleEventLogBox.Text),
            ParseNullableInt(ScheduleEventIdBox.Text),
            EmptyToNull(ScheduleEventSourceBox.Text));
    }
}
