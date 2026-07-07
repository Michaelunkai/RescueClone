using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using RescueClone.Core;
using RescueClone.Core.Cloning;
using RescueClone.Core.Jobs;
using RescueClone.Core.Logs;
using RescueClone.Core.Native;
using RescueClone.Core.Operations;
using RescueClone.Core.Retention;
using RescueClone.Core.Rescue;
using RescueClone.Core.RestorePlanning;
using RescueClone.Core.Scheduling;
using RescueClone.Core.Services;
using RescueClone.Core.Storage;

namespace RescueClone.App;

public partial class MainWindow : Window
{
    private readonly ImageEngine _engine = new();
    private readonly BackupJobRunner _jobRunner = new();
    private readonly BackupLogCatalog _logCatalog = new();
    private readonly RestorePlanner _restorePlanner = new();
    private readonly RescueAnswerManager _rescueAnswerManager = new();
    private readonly DirectoryCloneManager _cloneManager = new();
    private readonly OperationRunner _operationRunner = new();
    private readonly WindowsServiceManager _serviceManager = new();
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

    private void CompareImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new ImageComparer(_engine).Compare(new ImageCompareOptions(
            VerifyImagePathBox.Text,
            SourcePathBox.Text,
            EmptyToNull(VerifyPasswordBox.Password))));
    }

    private void AuditImageProtection_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new ImageRepositoryCatalog(_engine).AuditProtection(new ImageRepositoryProtectionOptions(
            VerifyRepositoryPathBox.Text,
            EmptyToDefault(VerifyRepositoryPatternBox.Text, "*.rcimg"))));
    }

    private void ProtectImages_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new ImageRepositoryCatalog(_engine).ApplyProtection(new ImageRepositoryProtectionOptions(
            VerifyRepositoryPathBox.Text,
            EmptyToDefault(VerifyRepositoryPatternBox.Text, "*.rcimg"))));
    }

    private void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _engine.Browse(RestoreImagePathBox.Text, EmptyToNull(RestorePasswordBox.Password)));
    }

    private void ListImages_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new ImageRepositoryCatalog(_engine).List(new ImageRepositoryListOptions(
            RestoreImagePathBox.Text,
            "*.rcimg",
            VerifyListBox.IsChecked == true,
            EmptyToNull(RestorePasswordBox.Password))));
    }

    private void AuditImages_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new ImageRepositoryCatalog(_engine).Audit(new ImageRepositoryAuditOptions(
            RestoreImagePathBox.Text,
            "*.rcimg",
            EmptyToNull(RestorePasswordBox.Password))));
    }

    private void ExtractImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _engine.Extract(new ExtractOptions(
            RestoreImagePathBox.Text,
            TargetPathBox.Text,
            SplitPaths(ExtractPathsBox.Text),
            EmptyToNull(RestorePasswordBox.Password),
            OverwriteBox.IsChecked == true)));
    }

    private void ProjectImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new ImageProjectionManager(_engine).Project(new ImageProjectionOptions(
            RestoreImagePathBox.Text,
            TargetPathBox.Text,
            EmptyToNull(RestorePasswordBox.Password),
            OverwriteBox.IsChecked == true)));
    }

    private void ListProjections_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new ImageProjectionManager(_engine).List(new ImageProjectionListOptions(TargetPathBox.Text)));
    }

    private void UnprojectImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new ImageProjectionManager(_engine).Unproject(new ImageUnprojectionOptions(TargetPathBox.Text)));
    }

    private void RestoreImage_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _engine.Restore(new RestoreOptions(RestoreImagePathBox.Text, TargetPathBox.Text, EmptyToNull(RestorePasswordBox.Password), OverwriteBox.IsChecked == true)));
    }

    private void CloneDirectory_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _cloneManager.Clone(new DirectoryCloneOptions(
            CloneSourcePathBox.Text,
            CloneTargetPathBox.Text,
            CloneOverwriteBox.IsChecked == true)));
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

    private void ExportJob_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.Export(JobPathBox.Text, JobTransferPathBox.Text));
    }

    private void ImportJob_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.Import(JobPathBox.Text, JobTransferPathBox.Text));
    }

    private void JobStatus_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.Status(JobPathBox.Text));
    }

    private void JobHistory_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.History(JobPathBox.Text));
    }

    private void ListJobs_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.List(JobDirectoryBox.Text));
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

    private void CreateRescueAnswer_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() =>
        {
            var bootMode = Enum.Parse<RestoreBootMode>(((System.Windows.Controls.ComboBoxItem)RescueBootModeBox.SelectedItem).Content.ToString()!);
            return _rescueAnswerManager.Create(new RescueAnswerOptions(
                RescueAnswerPathBox.Text,
                RescueRepositoryBox.Text,
                RescueImageBox.Text,
                EmptyToNull(RescuePasswordBox.Password),
                RescueTargetDiskBox.Text,
                bootMode,
                ParseNullableLong(RescueTargetDiskSizeBox.Text),
                ParseNullableLong(RescueRequiredBytesBox.Text),
                RescueCurrentSystemDiskBox.IsChecked == true,
                RescueHasEfiBox.IsChecked == true,
                EmptyToNull(RescueBcdStoreBox.Text),
                SplitPaths(RescueDriverDirectoriesBox.Text),
                SplitPaths(RescueNetworkSharesBox.Text),
                RescueRepairBootBox.IsChecked == true,
                RescueRebootBox.IsChecked == true,
                RescueVerifyImageBox.IsChecked == true));
        });
    }

    private void ValidateRescueAnswer_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _rescueAnswerManager.Validate(RescueAnswerPathBox.Text, RescueVerifyImageBox.IsChecked == true));
    }

    private void RunOperation_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _operationRunner.Run(
            _operationRunner.LoadRequest(OperationRequestPathBox.Text),
            EmptyToNull(OperationLogDirectoryBox.Text)));
    }

    private void ListOperationKinds_Click(object sender, RoutedEventArgs e)
    {
        OperationKindCatalog.AssertUniqueKinds();
        RunAndReport(() => OperationKindCatalog.All);
    }

    private void ValidateOperation_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _operationRunner.Validate(_operationRunner.LoadRequest(OperationRequestPathBox.Text)));
    }

    private void RunServiceOperation_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() =>
        {
            var response = new OperationPipeClient().RunOperationAsync(
                OperationPipeNameBox.Text,
                new OperationServiceRequest(
                    _operationRunner.LoadRequest(OperationRequestPathBox.Text),
                    EmptyToNull(OperationLogDirectoryBox.Text)),
                TimeSpan.FromSeconds(30),
                CancellationToken.None).GetAwaiter().GetResult();
            if (!response.Succeeded)
                throw new InvalidOperationException(response.Error ?? "Operation service request failed.");
            return response.Report ?? throw new InvalidDataException("Operation service returned no report.");
        });
    }

    private void PlanService_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _serviceManager.Plan(ReadServiceInstallDefinition()));
    }

    private void InstallService_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _serviceManager.Install(ReadServiceInstallDefinition()));
    }

    private void ServiceStatus_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _serviceManager.Status(ServiceNameBox.Text));
    }

    private void StartService_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _serviceManager.Start(ServiceNameBox.Text));
    }

    private void StopService_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _serviceManager.Stop(ServiceNameBox.Text));
    }

    private void SetServiceRecovery_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _serviceManager.ConfigureRecovery(new WindowsServiceRecoveryOptions(
            ServiceNameBox.Text,
            ResetPeriodSeconds: 86400,
            RestartDelayMilliseconds: 60000,
            RestartOnFailure: true)));
    }

    private void ServiceRecoveryStatus_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _serviceManager.GetRecovery(ServiceNameBox.Text));
    }

    private void UninstallService_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _serviceManager.Uninstall(ServiceNameBox.Text));
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

    private void PlanGfsRetention_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _retentionManager.PlanGfs(ReadGfsRetentionOptions()));
    }

    private void ApplyGfsRetention_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _retentionManager.ApplyGfs(ReadGfsRetentionOptions()));
    }

    private void PlanSchedule_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _scheduleManager.Plan(ReadScheduleDefinition()));
    }

    private void RegisterSchedule_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _scheduleManager.Register(ReadScheduleDefinition()));
    }

    private void ScheduleStatus_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _scheduleManager.Status(ScheduleTaskNameBox.Text));
    }

    private void RunSchedule_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _scheduleManager.RunNow(ScheduleTaskNameBox.Text));
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

    private void CheckDiskSafety_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => new DiskTargetSafetyEvaluator().Evaluate(
            _diskEnumerator.ListDisks(),
            new DiskTargetSafetyOptions(
                int.Parse(DiskSafetyNumberBox.Text),
                EmptyToNull(DiskSafetyFingerprintBox.Text),
                DiskSafetyAllowBootSystemBox.IsChecked == true)));
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

    private static string EmptyToDefault(string value, string defaultValue) => string.IsNullOrWhiteSpace(value) ? defaultValue : value;

    private static int? ParseNullableInt(string value) => string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);

    private static long? ParseNullableLong(string value) => string.IsNullOrWhiteSpace(value) ? null : long.Parse(value);

    private static IReadOnlyList<string> SplitPaths(string value)
    {
        return value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

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

    private GfsRetentionOptions ReadGfsRetentionOptions()
    {
        return new GfsRetentionOptions(
            RetentionRepositoryBox.Text,
            string.IsNullOrWhiteSpace(RetentionPatternBox.Text) ? "*.rcimg" : RetentionPatternBox.Text,
            ParseNullableInt(RetentionDailyKeepBox.Text),
            ParseNullableInt(RetentionWeeklyKeepBox.Text),
            ParseNullableInt(RetentionMonthlyKeepBox.Text));
    }

    private WindowsServiceInstallDefinition ReadServiceInstallDefinition()
    {
        return new WindowsServiceInstallDefinition(
            ServiceNameBox.Text,
            string.IsNullOrWhiteSpace(ServiceCliPathBox.Text) ? Environment.ProcessPath ?? "rc.exe" : ServiceCliPathBox.Text,
            OperationPipeNameBox.Text,
            EmptyToNull(OperationLogDirectoryBox.Text),
            EmptyToNull(ServiceDisplayNameBox.Text),
            "demand");
    }
}
