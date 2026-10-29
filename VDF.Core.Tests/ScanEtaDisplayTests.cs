using VDF.Core.Utils;
using Xunit;

namespace VDF.Core.Tests;

public sealed class ScanEtaDisplayTests {
    static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.FromHours(-4));
    [Fact]
    public void KnownEstimateShowsRemainingFinishAndPhaseScope() {
        var display = ScanEtaDisplay.From(ScanProgressSnapshot.Default with {
            CurrentPosition = 375, MaxPosition = 1000, Remaining = TimeSpan.FromMinutes(12), Elapsed = TimeSpan.FromMinutes(4)
        }, false, Now);
        Assert.Equal("~ 12m 0s", display.Remaining);
        Assert.Equal("Today · 12:12 PM", display.Finish);
        Assert.Equal("37.5%", display.Percent);
        Assert.Equal(37.5, display.Progress);
        Assert.Contains("Current step only", display.Note);
    }
    [Fact]
    public void UnknownTotalAndZeroRemainingDoNotPromiseAFalseFinish() {
        var unknown = ScanEtaDisplay.From(ScanProgressSnapshot.Default, false, Now);
        Assert.True(unknown.UnknownTotal);
        Assert.Equal("Still counting", unknown.Remaining);
        Assert.Equal("Not available yet", unknown.Finish);
        var calculating = ScanEtaDisplay.From(ScanProgressSnapshot.Default with { MaxPosition = 100 }, false, Now);
        Assert.Equal("Calculating…", calculating.Remaining);
        var wrapping = ScanEtaDisplay.From(ScanProgressSnapshot.Default with { CurrentPosition = 100, MaxPosition = 100 }, false, Now);
        Assert.Equal("Wrapping up", wrapping.Remaining);
        Assert.NotEqual("Finished", wrapping.Remaining);
    }
    [Fact]
    public void PausedEstimateDoesNotKeepMovingTheFinishClock() {
        var display = ScanEtaDisplay.From(ScanProgressSnapshot.Default with { MaxPosition = 100, CurrentPosition = 50, Remaining = TimeSpan.FromMinutes(20) }, true, Now);
        Assert.Equal("Paused", display.Remaining);
        Assert.Equal("Updates when resumed", display.Finish);
    }
    [Fact]
    public void FinishDateHandlesMidnightAndProgressIsBounded() {
        var display = ScanEtaDisplay.From(ScanProgressSnapshot.Default with { CurrentPosition = 50, MaxPosition = 100, Remaining = TimeSpan.FromMinutes(12) }, false, Now.AddHours(11).AddMinutes(58));
        Assert.Equal("Tomorrow · 12:10 AM", display.Finish);
        Assert.Equal(100, ScanEtaDisplay.From(ScanProgressSnapshot.Default with { CurrentPosition = 150, MaxPosition = 100 }, false, Now).Progress);
    }
}
