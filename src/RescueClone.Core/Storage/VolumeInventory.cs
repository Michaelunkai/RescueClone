namespace RescueClone.Core.Storage;

public sealed record VolumeInfo(
    string Name,
    string RootPath,
    string DriveType,
    bool IsReady,
    string? FileSystem,
    string? VolumeLabel,
    long? TotalBytes,
    long? FreeBytes,
    bool IsSystemVolume);

public sealed class VolumeEnumerator
{
    public IReadOnlyList<VolumeInfo> ListVolumes()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;
        return DriveInfo.GetDrives()
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => ToVolumeInfo(d, systemRoot))
            .ToArray();
    }

    private static VolumeInfo ToVolumeInfo(DriveInfo drive, string systemRoot)
    {
        var root = Path.GetFullPath(drive.Name);
        if (!drive.IsReady)
        {
            return new VolumeInfo(
                drive.Name,
                root,
                drive.DriveType.ToString(),
                IsReady: false,
                FileSystem: null,
                VolumeLabel: null,
                TotalBytes: null,
                FreeBytes: null,
                IsSystemVolume: IsSameRoot(root, systemRoot));
        }

        return new VolumeInfo(
            drive.Name,
            root,
            drive.DriveType.ToString(),
            IsReady: true,
            drive.DriveFormat,
            drive.VolumeLabel,
            drive.TotalSize,
            drive.AvailableFreeSpace,
            IsSameRoot(root, systemRoot));
    }

    private static bool IsSameRoot(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(
                Path.GetPathRoot(left)?.TrimEnd('\\'),
                Path.GetPathRoot(right)?.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
    }
}
