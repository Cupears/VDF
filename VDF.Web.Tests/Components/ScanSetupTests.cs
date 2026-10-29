using Bunit;
using Microsoft.Extensions.DependencyInjection;
using VDF.Core.Utils;
using VDF.Web.Services;

namespace VDF.Web.Tests.Components;

[Collection("WebSettingsOverride")]
public sealed class ScanSetupTests : BunitContext {
    readonly ScanService scan;
    readonly WebSettingsService web = new();
    readonly string settingsPath = Path.Combine(Path.GetTempPath(), "vdf-setup-" + Guid.NewGuid().ToString("N") + ".json");
    public ScanSetupTests() {
        WebSettingsService.TestOverrideSettingsPath = settingsPath;
        scan = new ScanService(web);
        Services.AddSingleton(scan); Services.AddSingleton(web); Services.AddSingleton(new FFmpegSetupService());
        Services.AddSingleton<IFolderBrowserSource, FolderBrowserSource>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing); scan.Dispose(); WebSettingsService.TestOverrideSettingsPath = null;
        if (File.Exists(settingsPath)) File.Delete(settingsPath);
    }

    [Fact]
    public async Task EtaCardShowsRemainingAndHonorsSharedPauseState() {
        typeof(ScanService).GetProperty(nameof(ScanService.State))!.SetValue(scan, ScanState.Scanning);
        typeof(ScanService).GetProperty(nameof(ScanService.LastProgress))!.SetValue(scan, new ScanProgressArgs { Current = 25, Max = 100, Remaining = TimeSpan.FromMinutes(12) });
        var page = Render<VDF.Web.Components.Pages.Index>();
        Assert.Contains("12m 0s", page.Find(".eta-time").TextContent);
        await page.InvokeAsync(() => scan.SetPaused(true));
        page.WaitForAssertion(() => Assert.Equal("Paused", page.Find(".eta-time").TextContent));
    }

    [Fact]
    public void FolderListingShowsItsPathAndCountBeforeTotalIsKnown() {
        typeof(ScanService).GetProperty(nameof(ScanService.State))!.SetValue(scan, ScanState.Scanning);
        typeof(ScanService).GetProperty(nameof(ScanService.LastProgress))!.SetValue(scan, new ScanProgressArgs {
            CurrentFile = @"Z:\videos\currently-reading", CurrentStage = "Listing folders", Current = 123, Max = 0,
            Elapsed = TimeSpan.FromSeconds(40)
        });
        var page = Render<VDF.Web.Components.Pages.Index>();
        Assert.Contains("Listing folders", page.Find(".state-label").TextContent);
        Assert.Contains(@"Z:\videos\currently-reading", page.Find(".file-path").TextContent);
        Assert.Contains("123", page.Find(".scan-stat-grid").TextContent);
        Assert.Contains("still counting", page.Markup);
        Assert.NotNull(page.Find("a[href='/log']"));
    }

    [Fact]
    public void SetupShowsFoldersProfilesAndSummary_AndCustomIsRestored() {
        scan.Settings.Percent = 95; scan.Settings.IncludeList.Add("C:\\videos");
        scan.Settings.HddMaxDegreeOfParallelism = 4; scan.Settings.UsePHashing = true;
        var page = Render<VDF.Web.Components.Pages.Index>();
        Assert.Single(page.FindAll(".scan-folders")); Assert.Single(page.FindAll(".scan-ready"));
        Assert.Equal(5, page.FindAll(".scan-profile").Count);
        page.FindAll(".scan-profile").Single(b => b.TextContent.Contains("Deep clean")).Click();
        Assert.True(scan.Settings.NeedsAiComponents); Assert.True(scan.Settings.EnablePartialClipDetection);
        Assert.Equal(92, scan.Settings.Percent); Assert.Equal(4, scan.Settings.HddMaxDegreeOfParallelism);
        Assert.True(scan.Settings.UsePHashing); Assert.Contains("C:\\videos", scan.Settings.IncludeList);
        Assert.Contains("Deep clean", page.Find(".scan-ready").TextContent);
        page.FindAll(".scan-profile").Single(b => b.TextContent.Contains("Custom")).Click();
        Assert.Equal(95, scan.Settings.Percent); Assert.False(scan.Settings.UseAiMatching);
        Assert.NotNull(web.CustomScanKnobs);
    }

    [Fact]
    public void WebSync_PullsDesktopOptionsAndPushesUserEdits() {
        string syncPath = settingsPath + ".shared";
        try {
            var desktop = new VDF.Core.Settings { Percent = 90, HddMaxDegreeOfParallelism = 3 };
            var desktopSession = new SharedScanSettings(syncPath);
            var order = new List<string> { "Resolution", "Duration" }; var disabled = new List<string>();
            desktopSession.Synchronize(desktop, order, disabled);
            using var syncedWeb = new ScanService(new WebSettingsService(), new SharedScanSettings(syncPath));
            Assert.Equal(90, syncedWeb.Settings.Percent);
            syncedWeb.Settings.Percent = 96; syncedWeb.SaveSettings();
            desktopSession.Synchronize(desktop, order, disabled);
            Assert.Equal(96, desktop.Percent);
            desktop.HddMaxDegreeOfParallelism = 7;
            desktopSession.Synchronize(desktop, order, disabled); syncedWeb.SyncSharedSettings();
            Assert.Equal(7, syncedWeb.Settings.HddMaxDegreeOfParallelism);
            typeof(ScanService).GetProperty(nameof(ScanService.State))!.SetValue(syncedWeb, ScanState.Scanning);
            desktop.Percent = 88; desktopSession.Synchronize(desktop, order, disabled);
            Assert.False(syncedWeb.SyncSharedSettings()); Assert.Equal(96, syncedWeb.Settings.Percent);
            typeof(ScanService).GetProperty(nameof(ScanService.State))!.SetValue(syncedWeb, ScanState.Done);
            syncedWeb.SyncSharedSettings(); Assert.Equal(88, syncedWeb.Settings.Percent);
        } finally { if (File.Exists(syncPath)) File.Delete(syncPath); if (File.Exists(syncPath + ".lock")) File.Delete(syncPath + ".lock"); }
    }
}
