using Bunit;
using VDF.Core.Utils;

namespace VDF.Web.Tests.Components;

public sealed class LogPageTests : BunitContext {
    [Fact]
    public void SharedLogShowsPathsAndFiltersMessages() {
        string marker = "log-test-" + Guid.NewGuid();
        Logger.Instance.Info(marker + @" listing C:\videos\current-folder");
        Logger.Instance.Warn(marker + " waiting for a drive");
        var page = Render<VDF.Web.Components.Pages.Log>();
        page.Find("input[type=search]").Input(marker);
        Assert.Equal(2, page.FindAll(".activity-log-entry").Count);
        Assert.Contains(@"C:\videos\current-folder", page.Markup);
        page.Find("select").Change("warning");
        Assert.Single(page.FindAll(".activity-log-entry"));
        Assert.Contains("waiting for a drive", page.Find(".activity-log-entry").TextContent);
    }
}
