namespace VDF.Web.Services;

public sealed record FolderBrowserEntry(string Path, string Name, string Detail, bool IsDrive = false, bool IsReady = true, int? UsedPercent = null);
public sealed record FolderBrowserSnapshot(string? Path, IReadOnlyList<FolderBrowserEntry> Entries);
public interface IFolderBrowserSource {
    Task<FolderBrowserSnapshot> ReadAsync(string? path, bool showHidden, CancellationToken cancellationToken);
}

/// <summary>Reads server folders off the UI thread. No files are opened or changed.</summary>
public sealed class FolderBrowserSource : IFolderBrowserSource {
    public Task<FolderBrowserSnapshot> ReadAsync(string? path, bool showHidden, CancellationToken cancellationToken) => Task.Run(() => {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path)) {
            var drives = new List<FolderBrowserEntry>();
            foreach (var drive in DriveInfo.GetDrives()) {
                cancellationToken.ThrowIfCancellationRequested();
                try {
                    bool ready = drive.IsReady;
                    string label = ready && !string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.VolumeLabel : "Drive";
                    string detail = ready ? $"{Size(drive.AvailableFreeSpace)} free of {Size(drive.TotalSize)}" : "Not ready or disconnected";
                    int? used = ready && drive.TotalSize > 0 ? (int)Math.Clamp(100d * (drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize, 0, 100) : null;
                    drives.Add(new(drive.RootDirectory.FullName, $"{label} ({drive.Name})", detail, true, ready, used));
                } catch (IOException) { drives.Add(new(drive.Name, drive.Name, "Not available", true, false)); }
                catch (UnauthorizedAccessException) { drives.Add(new(drive.Name, drive.Name, "Access denied", true, false)); }
            }
            return new FolderBrowserSnapshot(null, drives);
        }
        string canonical = System.IO.Path.GetFullPath(path.Trim());
        if (!Directory.Exists(canonical)) throw new DirectoryNotFoundException("Folder not found or unavailable on this PC.");
        var folders = new List<FolderBrowserEntry>();
        foreach (var directory in new DirectoryInfo(canonical).EnumerateDirectories()) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!showHidden && (directory.Name.StartsWith('.') || (directory.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)) continue;
            folders.Add(new(directory.FullName, directory.Name, "Folder"));
        }
        return new FolderBrowserSnapshot(canonical, folders.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }, cancellationToken);
    static string Size(long bytes) => bytes >= 1L << 40 ? $"{bytes / (double)(1L << 40):0.#} TB" : $"{bytes / (double)(1L << 30):0.#} GB";
}
