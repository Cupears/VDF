using VDF.Core.Utils;
using Xunit;

namespace VDF.Core.Tests;

public sealed class ScanActivityLogTests {
    [Fact]
    public async Task HeartbeatKeepsTheLatestFolderEvenWhenDisplayUpdatesAreThrottled() {
        var progress = new ScanProgress(TimeSpan.FromMilliseconds(100));
        var reports = new List<ScanProgressSnapshot>();
        progress.Progress += (_, snapshot) => reports.Add(snapshot);
        progress.Update(p => p with { CurrentFile = "first-folder" }, true);
        progress.Update(p => p with { CurrentFile = "folder-now-being-read" }, keepLatestWhenThrottled: true);
        Assert.Single(reports);
        await Task.Delay(150);
        progress.Heartbeat(p => p);
        Assert.Equal("folder-now-being-read", reports[^1].CurrentFile);
    }

    [Fact]
    public void MovingClockDoesNotHideQuietWorkAndRealWorkClearsWarning() {
        var time = DateTimeOffset.UtcNow;
        var log = new ScanActivityLog(() => time);
        var snapshot = ScanProgressSnapshot.Default with { CurrentFile = @"Z:\videos", CurrentStage = "Listing folders" };
        log.Observe(snapshot);
        time += TimeSpan.FromSeconds(9);
        Assert.Null(log.Tick());
        time += TimeSpan.FromSeconds(1);
        Assert.False(log.Tick()!.Value.Quiet);
        time += TimeSpan.FromSeconds(21);
        log.Observe(snapshot with { Elapsed = TimeSpan.FromSeconds(31) });
        var warning = log.Tick()!.Value;
        Assert.True(warning.Quiet);
        Assert.Contains(@"Z:\videos", warning.Message);
        Assert.Contains("does not prove", warning.Message);
        log.Observe(snapshot with { CurrentPosition = 5 });
        time += TimeSpan.FromSeconds(10);
        Assert.False(log.Tick()!.Value.Quiet);
    }

    [Fact]
    public void PauseResumeAndFinishedScanDoNotProduceStaleWarnings() {
        var time = DateTimeOffset.UtcNow;
        var log = new ScanActivityLog(() => time);
        log.Observe(ScanProgressSnapshot.Default);
        time += TimeSpan.FromMinutes(5);
        log.Resumed();
        Assert.Null(log.Tick());
        time += TimeSpan.FromSeconds(10);
        Assert.False(log.Tick()!.Value.Quiet);
        log.Reset();
        time += TimeSpan.FromMinutes(5);
        Assert.Null(log.Tick());
    }

    [Fact]
    public void DirectoryReportsStartAndCountsAndHonorsCancellation() {
        string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vdf-list-log-" + Guid.NewGuid())).FullName;
        try {
            Directory.CreateDirectory(Path.Combine(root, "child"));
            File.WriteAllText(Path.Combine(root, "one.mp4"), "");
            File.WriteAllText(Path.Combine(root, "child", "two.mp4"), "");
            var reports = new List<(string Path, int Folders, int Files, int Entries)>();
            var found = FileUtils.GetFilesRecursive(root, false, false, true, false, new(), CancellationToken.None,
                (path, folders, files, entries) => reports.Add((path, folders, files, entries)));
            Assert.Equal(2, found.Count);
            Assert.Equal(root, reports[0].Path);
            Assert.Equal(0, reports[0].Files);
            Assert.Equal(2, reports[^1].Files);
            Assert.Equal(2, reports[^1].Folders);
            using var stop = new CancellationTokenSource();
            var cancelled = FileUtils.GetFilesRecursive(root, false, false, true, false, new(), stop.Token,
                (_, _, _, _) => stop.Cancel());
            Assert.Empty(cancelled);
        } finally { Directory.Delete(root, true); }
    }
}
