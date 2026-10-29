namespace VDF.Core.Utils;

/// <summary>Finds moved-file candidates in RAM before asking disks about old paths.</summary>
internal sealed class MovedFileIndex {
    readonly Dictionary<(long Size, string Hash), List<FileEntry>> byFingerprint = new();
    readonly HashSet<long> sizes = new();
    readonly Func<string, bool> exists;
    readonly Func<string, string?> fingerprint;
    internal long SmallFilesSkipped { get; private set; }
    internal long FingerprintReads { get; private set; }
    internal long OldPathChecks { get; private set; }
    internal long HashMisses { get; private set; }

    internal MovedFileIndex(Func<string, bool>? fileExists = null, Func<string, string?>? readFingerprint = null) {
        exists = fileExists ?? File.Exists;
        fingerprint = readFingerprint ?? OsHashUtils.TryCompute;
    }

    internal void Add(FileEntry entry) {
        if (entry.OsHash == null || entry.FileSize < OsHashUtils.MinimumFileSize) return;
        var key = (entry.FileSize, entry.OsHash);
        if (!byFingerprint.TryGetValue(key, out var candidates)) byFingerprint[key] = candidates = new();
        candidates.Add(entry);
        sizes.Add(entry.FileSize);
    }

    internal FileEntry? FindUniqueMissing(FileEntry incoming) {
        if (incoming.FileSize < OsHashUtils.MinimumFileSize) { SmallFilesSkipped++; return null; }
        if (!sizes.Contains(incoming.FileSize)) return null;
        if (incoming.OsHash == null) { FingerprintReads++; incoming.OsHash = fingerprint(incoming.Path); }
        if (incoming.OsHash == null) return null;
        if (!byFingerprint.TryGetValue((incoming.FileSize, incoming.OsHash), out var candidates)) { HashMisses++; return null; }
        FileEntry? match = null;
        foreach (var candidate in candidates) {
            // Always check the matching paths live: a file can appear/disappear during a scan.
            OldPathChecks++;
            if (exists(candidate.Path)) continue;
            if (match != null) return null; // Ambiguous; leave the incoming file for normal analysis.
            match = candidate;
        }
        return match;
    }

    internal void Remove(FileEntry entry) {
        if (entry.OsHash != null && byFingerprint.TryGetValue((entry.FileSize, entry.OsHash), out var candidates)) candidates.Remove(entry);
    }

    internal string Summary => $"HDD cache reuse: skipped {SmallFilesSkipped:N0} unnecessary small-file fingerprint attempts; read {FingerprintReads:N0} new fingerprints (retained for analysis); checked {OldPathChecks:N0} matching old paths; rejected {HashMisses:N0} unrelated fingerprints in memory.";
}
