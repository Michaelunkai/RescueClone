using System;
using System.Text.Json;
using System.Windows;
using RescueClone.Core;

namespace RescueClone.App;

public partial class MainWindow : Window
{
    private readonly ImageEngine _engine = new();

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
}
