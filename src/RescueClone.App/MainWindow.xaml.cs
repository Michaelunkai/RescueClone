using System;
using System.Text.Json;
using System.Windows;
using RescueClone.Core;
using RescueClone.Core.Jobs;
using RescueClone.Core.Operations;
using RescueClone.Core.RestorePlanning;

namespace RescueClone.App;

public partial class MainWindow : Window
{
    private readonly ImageEngine _engine = new();
    private readonly BackupJobRunner _jobRunner = new();
    private readonly RestorePlanner _restorePlanner = new();
    private readonly OperationRunner _operationRunner = new();

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
            return _engine.Create(new ImageOptions(SourcePathBox.Text, ImagePathBox.Text, compression, EmptyToNull(CreatePasswordBox.Password)));
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

    private void ValidateJob_Click(object sender, RoutedEventArgs e)
    {
        RunAndReport(() => _jobRunner.Validate(_jobRunner.Load(JobPathBox.Text)));
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

    private static long? ParseNullableLong(string value) => string.IsNullOrWhiteSpace(value) ? null : long.Parse(value);
}
