using System.Linq;

namespace VDF.Core.Utils;

internal sealed class FolderProgressTracker {
    sealed class Counter(string path) {
        internal readonly string Path = path;
        internal int Total, Done;
        internal TimeSpan? Started;
    }
    readonly string[] roots;
    readonly Dictionary<string, Counter> folders = new(CoreUtils.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    readonly Dictionary<string, Counter> groups = new(CoreUtils.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    readonly object gate = new();
    internal FolderProgressTracker(IEnumerable<FileEntry> entries, IEnumerable<string> includes) {
        roots = includes.OrderByDescending(p => p.Length).ToArray();
        foreach (var entry in entries) Resolve(entry.Folder).Total++;
    }
    Counter Resolve(string folder) {
        if (folders.TryGetValue(folder, out var counter)) return counter;
        string root = roots.FirstOrDefault(p => IsWithin(folder, p)) ?? System.IO.Path.GetPathRoot(folder) ?? folder;
        if (!groups.TryGetValue(root, out counter)) groups[root] = counter = new Counter(root);
        return folders[folder] = counter;
    }
    internal static bool IsWithin(string folder, string root) {
        string relative = System.IO.Path.GetRelativePath(root, folder);
        return relative == "." || (!System.IO.Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar));
    }
    internal void Begin(FileEntry entry, TimeSpan elapsed) { lock (gate) Resolve(entry.Folder).Started ??= elapsed; }
    internal void Complete(FileEntry entry) { lock (gate) Resolve(entry.Folder).Done++; }
    internal FolderProgress? Snapshot(string path, TimeSpan elapsed) {
        if (string.IsNullOrEmpty(path)) return null;
        lock (gate) {
            string folder = System.IO.Path.GetDirectoryName(path) ?? path;
            var counter = Resolve(folder);
            if (counter.Total == 0) return null;
            var spent = counter.Started.HasValue ? elapsed - counter.Started.Value : TimeSpan.Zero;
            return new(counter.Path, counter.Done, counter.Total, spent, ScanEngine.EstimateRemainingCompleted(spent, counter.Done, counter.Total));
        }
    }
}
