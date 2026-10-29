using VDF.GUI.Data;

namespace VDF.GUI.Tests;

public class DesktopSettingsSyncTests {
    [Fact]
    public void AdapterConvertsPercentagesAndPreservesDesktopOnlySettings() {
        var gui = new SettingsFile {
            Percent = 94, Thumbnails = 8, PHashSampleRatioPercent = 75,
            PartialClipMinRatioPercent = 25, PartialClipSimilarityThresholdPercent = 90,
            PartialClipVisualThresholdPercent = 88, CustomDatabaseFolder = "desktop-db",
            ThemeMode = ThemeMode.Light, UsePHash = true, CombineGrayPHash = true, IncludeVeryDarkPhotos = true
        };
        gui.Includes.Add("folder-a");
        var core = DesktopSettingsSync.Capture(gui);
        Assert.Equal(8, core.ThumbnailCount); Assert.Equal(0.75f, core.PHashRequiredMatchingSampleRatio);
        Assert.Equal(0.25, core.PartialClipMinRatio); Assert.Equal(0.9, core.PartialClipSimilarityThreshold);
        Assert.True(core.CombineGrayscaleAndPHash);
        Assert.True(core.IncludeVeryDarkPhotos);
        core.IncludeVeryDarkPhotos = false;
        core.Percent = 97; core.PHashRequiredMatchingSampleRatio = 0.5f;
        core.IncludeList = new() { "folder-b" }; core.CustomDatabaseFolder = "web-db";
        DesktopSettingsSync.Apply(gui, core);
        Assert.False(gui.IncludeVeryDarkPhotos);
        Assert.Equal(97, gui.Percent); Assert.Equal(50, gui.PHashSampleRatioPercent);
        Assert.Equal(new[] { "folder-b" }, gui.Includes);
        Assert.Equal("desktop-db", gui.CustomDatabaseFolder); Assert.Equal(ThemeMode.Light, gui.ThemeMode);
    }

    [Theory]
    [InlineData(ScanProfile.ExactAndNear, "Exact & near copies")]
    [InlineData(ScanProfile.EditedAndAltered, "Edited & altered")]
    [InlineData(ScanProfile.AiScan, "AI scan")]
    [InlineData(ScanProfile.DeepClean, "Deep clean")]
    public void WebAndDesktopProfileValuesMatch(ScanProfile desktopProfile, string webProfile) {
        var gui = new SettingsFile(); ScanProfileMapper.Apply(desktopProfile, gui);
        var core = DesktopSettingsSync.Capture(gui);
        Assert.Equal(webProfile, VDF.Core.Utils.ScanPresets.Detect(core));
    }
}
