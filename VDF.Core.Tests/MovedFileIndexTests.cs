using VDF.Core.Utils;
using Xunit;

namespace VDF.Core.Tests;

public sealed class MovedFileIndexTests {
    static FileEntry Entry(string path, string? hash, long size = 200_000) => new() { _Path = path, OsHash = hash, FileSize = size };

    [Fact]
    public void UnrelatedSameSizeFilesNeverTriggerOldPathReads() {
        int reads = 0;
        var index = new MovedFileIndex(_ => { reads++; return false; }, _ => "new-content");
        for (int i = 0; i < 10_000; i++) index.Add(Entry("old-" + i, "hash-" + i));
        var incoming = Entry("new-photo", null);
        Assert.Null(index.FindUniqueMissing(incoming));
        Assert.Equal(0, reads);
        Assert.Equal(1, index.FingerprintReads);
        Assert.Equal("new-content", incoming.OsHash); // GatherInfos can reuse this read.
    }

    [Fact]
    public void OnlyMatchingFingerprintPathsAreCheckedAndExistingCopiesAreKept() {
        var checkedPaths = new List<string>();
        bool exists = true;
        var index = new MovedFileIndex(path => { checkedPaths.Add(path); return exists; }, _ => "matching-hash");
        for (int i = 0; i < 1000; i++) index.Add(Entry("unrelated-" + i, "other-" + i));
        var old = Entry("matching-old-path", "matching-hash"); index.Add(old);
        var incoming = Entry("incoming", null);
        Assert.Null(index.FindUniqueMissing(incoming)); // It is a copy while the old path exists.
        Assert.Equal(new[] { "matching-old-path" }, checkedPaths);
        exists = false;
        Assert.Same(old, index.FindUniqueMissing(incoming)); // Recheck live; no stale per-scan existence cache.
        Assert.Equal(1, index.FingerprintReads);
        index.Remove(old);
        Assert.Null(index.FindUniqueMissing(incoming));
    }

    [Fact]
    public void SmallFilesAndUnknownSizesDoNotReadDiskForMoveDetection() {
        var index = new MovedFileIndex(_ => throw new Exception("Unexpected path read"), _ => throw new Exception("Unexpected fingerprint read"));
        index.Add(Entry("old", "hash"));
        Assert.Null(index.FindUniqueMissing(Entry("tiny-photo", null, OsHashUtils.MinimumFileSize - 1)));
        Assert.Null(index.FindUniqueMissing(Entry("unseen-size", null, 300_000)));
        Assert.Equal(1, index.SmallFilesSkipped);
        Assert.Equal(0, index.FingerprintReads);
    }

    [Fact]
    public void AmbiguousMovesAndUnreadableFingerprintsStayOnNormalAnalysisPath() {
        var index = new MovedFileIndex(_ => false, _ => "same");
        index.Add(Entry("missing-a", "same")); index.Add(Entry("missing-b", "same"));
        Assert.Null(index.FindUniqueMissing(Entry("new", null)));
        var unreadable = new MovedFileIndex(_ => throw new Exception("No hash means no path checks"), _ => null);
        unreadable.Add(Entry("old", "hash"));
        Assert.Null(unreadable.FindUniqueMissing(Entry("new", null)));
    }
}
