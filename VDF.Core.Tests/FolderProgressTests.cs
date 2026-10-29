using VDF.Core.Utils;
using Xunit;

namespace VDF.Core.Tests;

public sealed class FolderProgressTests {
    static FileEntry Entry(string folder, string name) => new() { _Path = Path.Combine(folder, name), Folder = folder, invalid = false };
    [Fact]
    public void AnalysisCountsSelectedFoldersAndInvalidatesUnselectedCachedHistory() {
        string root = Path.Combine(Path.GetTempPath(), "vdf-selected");
        var engine = new ScanEngine(); engine.Settings.IncludeList.Add(root);
        var inside = Entry(root, "a.mp4"); var outside = Entry(root + "-other", "history.mp4");
        var selected = engine.SelectAnalysisEntries(new[] { inside, outside });
        Assert.Single(selected); Assert.Same(inside, selected[0]); Assert.True(outside.invalid);
        engine.Settings.ScanAgainstEntireDatabase = true;
        Assert.Equal(2, engine.SelectAnalysisEntries(new[] { inside, outside }).Count);
    }
    [Fact]
    public void FolderTotalsAreSeparateAndOverlappingIncludesCountEachFileOnce() {
        string root = Path.Combine(Path.GetTempPath(), "vdf-folders"); string nested = Path.Combine(root, "nested");
        var a = Entry(root, "a.jpg"); var b = Entry(root, "b.jpg"); var child = Entry(nested, "c.jpg");
        var tracker = new FolderProgressTracker(new[] { a, b, child }, new[] { root, nested });
        tracker.Begin(a, TimeSpan.FromSeconds(5)); tracker.Complete(a);
        var snapshot = tracker.Snapshot(a.Path, TimeSpan.FromSeconds(15))!;
        Assert.Equal(root, snapshot.Path); Assert.Equal(1, snapshot.Done); Assert.Equal(2, snapshot.Total);
        Assert.Equal(TimeSpan.FromSeconds(10), snapshot.Remaining);
        Assert.Equal(1, tracker.Snapshot(child.Path, TimeSpan.FromSeconds(15))!.Total);
        Assert.False(FolderProgressTracker.IsWithin(root + "-other", root));
    }
    [Fact]
    public void SavedCachePreparationDoesNotLookLikeAMillionOriginalFiles() {
        var display = ScanEtaDisplay.From(ScanProgressSnapshot.Default with { MaxPosition = 1_240_484, CurrentPosition = 500_000, CountsSavedCache = true }, false, DateTimeOffset.Now);
        Assert.Equal("Preparing", display.Remaining);
        Assert.DoesNotContain("1,240,484", display.Count);
        Assert.True(display.UnknownTotal);
    }
}
