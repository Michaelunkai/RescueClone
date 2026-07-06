namespace RescueClone.Core;

public sealed record FeatureSurface(string FeatureId, string Gui, string Cli, string PowerShell, bool Implemented);

public static class FeatureCatalog
{
    public static IReadOnlyList<FeatureSurface> All { get; } = new List<FeatureSurface>
    {
        new("image.create.directory", "Create Image", "rc image create", "New-RCImage", true),
        new("image.verify", "Verify Image", "rc image verify", "Test-RCImage", true),
        new("image.restore.directory", "Restore Image", "rc image restore", "Restore-RCImage", true)
    };

    public static void AssertImplementedParity()
    {
        var missing = All.Where(f => string.IsNullOrWhiteSpace(f.Gui) || string.IsNullOrWhiteSpace(f.Cli) || string.IsNullOrWhiteSpace(f.PowerShell)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Feature parity catalog contains incomplete surfaces.");
    }
}
