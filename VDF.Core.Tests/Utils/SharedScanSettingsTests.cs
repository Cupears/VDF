using System.Text.Json.Nodes;
using VDF.Core.Utils;

namespace VDF.Core.Tests.Utils;

public sealed class SharedScanSettingsTests : IDisposable {
    readonly string folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vdf-sync-" + Guid.NewGuid().ToString("N"))).FullName;
    string PathName => Path.Combine(folder, "settings.json");
    public void Dispose() => Directory.Delete(folder, true);
    static List<string> Order() => ["Resolution", "Duration"];

    [Fact]
    public void FirstDesktopSeeds_ThenWebPulls_AndEditsFlowBothWays() {
        var desktop = new Settings { Percent = 91, HddMaxDegreeOfParallelism = 3 };
        desktop.IncludeList.Add("D:\\Videos");
        var web = new Settings();
        var desktopStore = new SharedScanSettings(PathName); var webStore = new SharedScanSettings(PathName);
        var dOrder = Order(); var wOrder = new List<string>(); var disabled = new List<string>();
        Assert.True(desktopStore.Synchronize(desktop, dOrder, disabled));
        Assert.True(webStore.Synchronize(web, wOrder, new()));
        Assert.Equal(91, web.Percent); Assert.Contains("D:\\Videos", web.IncludeList); Assert.Equal(dOrder, wOrder);
        web.Percent = 95;
        Assert.True(webStore.Synchronize(web, wOrder, new()));
        Assert.True(desktopStore.Synchronize(desktop, dOrder, disabled));
        Assert.Equal(95, desktop.Percent);
        desktop.HddMaxDegreeOfParallelism = 6;
        desktopStore.Synchronize(desktop, dOrder, disabled); webStore.Synchronize(web, wOrder, new());
        Assert.Equal(6, web.HddMaxDegreeOfParallelism);
        Assert.False(webStore.Synchronize(web, wOrder, new()));
    }

    [Fact]
    public void DifferentSimultaneousEdits_AreMergedWithoutStaleValuesWinning() {
        var a = new Settings(); var b = new Settings();
        var first = new SharedScanSettings(PathName); var second = new SharedScanSettings(PathName);
        first.Synchronize(a, Order(), new()); second.Synchronize(b, Order(), new());
        a.Percent = 88; b.ThumbnailCount = 7;
        first.Synchronize(a, Order(), new()); second.Synchronize(b, Order(), new()); first.Synchronize(a, Order(), new());
        Assert.Equal(88, b.Percent); Assert.Equal(7, a.ThumbnailCount);
    }

    [Fact]
    public void DatabasePathsAndDisplayOptions_AreNeverShared() {
        var a = new Settings { CustomDatabaseFolder = "desktop-db", ThumbnailMaxWidth = 120, GeneratePreviewThumbnails = true };
        var b = new Settings { CustomDatabaseFolder = "web-db", ThumbnailMaxWidth = 640 };
        new SharedScanSettings(PathName).Synchronize(a, Order(), new());
        new SharedScanSettings(PathName).Synchronize(b, Order(), new());
        Assert.Equal("web-db", b.CustomDatabaseFolder); Assert.Equal(640, b.ThumbnailMaxWidth); Assert.False(b.GeneratePreviewThumbnails);
        Assert.DoesNotContain("CustomDatabaseFolder", File.ReadAllText(PathName));
    }

    [Fact]
    public void UnknownFutureKeys_DoNotCauseRepeatedUpdates() {
        var settings = new Settings(); var session = new SharedScanSettings(PathName);
        session.Synchronize(settings, Order(), new());
        var json = JsonNode.Parse(File.ReadAllText(PathName))!.AsObject(); json["FutureOption"] = true;
        File.WriteAllText(PathName, json.ToJsonString());
        Assert.False(session.Synchronize(settings, Order(), new()));
    }

    [Fact]
    public void InvalidFile_IsNotOverwritten_AndDoesNotChangeLocalValues() {
        File.WriteAllText(PathName, "broken");
        var local = new Settings { Percent = 93 };
        Assert.ThrowsAny<Exception>(() => new SharedScanSettings(PathName).Synchronize(local, Order(), new()));
        Assert.Equal(93, local.Percent); Assert.Equal("broken", File.ReadAllText(PathName));
    }
}
