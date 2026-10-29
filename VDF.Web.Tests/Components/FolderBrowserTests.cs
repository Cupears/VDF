using Bunit;
using Microsoft.Extensions.DependencyInjection;
using VDF.Web.Components.Layout;
using VDF.Web.Services;

namespace VDF.Web.Tests.Components;

public sealed class FolderBrowserTests : BunitContext {
    readonly string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vdf-picker-" + Guid.NewGuid().ToString("N"))).FullName;
    public FolderBrowserTests() {
        Directory.CreateDirectory(Path.Combine(root, "Alpha"));
        Directory.CreateDirectory(Path.Combine(root, "Beta"));
        Directory.CreateDirectory(Path.Combine(root, ".secret"));
        Services.AddSingleton<IFolderBrowserSource, FolderBrowserSource>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); Directory.Delete(root, true); }

    [Fact]
    public async Task OneTapOpens_UseFolderSelects_AndBackReturns() {
        string? selected = null;
        var page = Render<FolderBrowser>(p => p.Add(c => c.OnFolderSelected, (string path) => selected = path));
        await page.InvokeAsync(() => page.Instance.Open(root));
        Assert.Equal(2, page.FindAll(".folder-row").Count);
        page.FindAll(".folder-row").Single(b => b.QuerySelector("strong")!.TextContent == "Alpha").Click();
        page.WaitForAssertion(() => Assert.Equal(Path.Combine(root, "Alpha"), page.Find(".folder-selected strong").TextContent));
        page.Find("button[aria-label='Back']").Click();
        page.WaitForAssertion(() => Assert.Equal(root, page.Find(".folder-selected strong").TextContent));
        page.Find(".folder-use").Click();
        Assert.Equal(root, selected); Assert.Empty(page.FindAll(".folder-picker"));
    }

    [Fact]
    public async Task InvalidTypedPath_DoesNotReplaceTheLoadedFolder() {
        string? selected = null;
        var page = Render<FolderBrowser>(p => p.Add(c => c.OnFolderSelected, (string path) => selected = path));
        await page.InvokeAsync(() => page.Instance.Open(root));
        page.Find("input[aria-label='Folder path on this PC']").Input(Path.Combine(root, "missing"));
        page.Find(".folder-go").Click();
        page.WaitForAssertion(() => Assert.Contains("not found", page.Find(".folder-error").TextContent));
        Assert.Equal(root, page.Find(".folder-selected strong").TextContent);
        page.Find(".folder-use").Click();
        Assert.Equal(root, selected);
    }

    [Fact]
    public async Task SearchAndHiddenToggle_FilterFoldersWithoutSelectingThem() {
        var page = Render<FolderBrowser>();
        await page.InvokeAsync(() => page.Instance.Open(root));
        page.Find(".folder-search-row input[type=checkbox]").Change(true);
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".folder-row").Count));
        page.Find("input[aria-label='Filter folders']").Input("alp");
        Assert.Single(page.FindAll(".folder-row"));
        Assert.Equal(root, page.Find(".folder-selected strong").TextContent);
        page.Find("input[aria-label='Filter folders']").Input("does-not-exist");
        Assert.Contains("No folders match", page.Find(".folder-empty").TextContent);
    }

    [Fact]
    public async Task DrivesHaveLabelsAndSpace_AndNeedOneTap() {
        Services.AddSingleton<IFolderBrowserSource>(new DriveSource(root));
        var page = Render<FolderBrowser>();
        await page.InvokeAsync(() => page.Instance.Open());
        Assert.True(page.Find(".folder-use").HasAttribute("disabled"));
        Assert.Contains("Archive disk", page.Find(".folder-row").TextContent);
        Assert.Contains("10 GB free", page.Find(".folder-row").TextContent);
        Assert.Single(page.FindAll(".drive-capacity"));
        page.Find(".folder-row").Click();
        page.WaitForAssertion(() => Assert.Equal(root, page.Find(".folder-selected strong").TextContent));
    }

    [Fact]
    public async Task LargeFolders_RenderInBatches() {
        Services.AddSingleton<IFolderBrowserSource>(new ManyFoldersSource(root));
        var page = Render<FolderBrowser>();
        await page.InvokeAsync(() => page.Instance.Open(root));
        Assert.Equal(80, page.FindAll(".folder-row").Count);
        page.Find(".folder-show-more").Click();
        Assert.Equal(85, page.FindAll(".folder-row").Count);
        Assert.Empty(page.FindAll(".folder-show-more"));
    }

    [Fact]
    public async Task ClosingWhileLoading_DoesNotReopenWhenReadFinishes() {
        var source = new DelayedSource(); Services.AddSingleton<IFolderBrowserSource>(source);
        var page = Render<FolderBrowser>();
        var opening = page.InvokeAsync(() => page.Instance.Open(root));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".folder-picker")));
        page.Find("button[aria-label='Close folder browser']").Click();
        source.Completion.SetResult(new(root, Array.Empty<FolderBrowserEntry>()));
        await opening;
        Assert.Empty(page.FindAll(".folder-picker"));
    }

    sealed class DriveSource(string path) : IFolderBrowserSource {
        public Task<FolderBrowserSnapshot> ReadAsync(string? requested, bool hidden, CancellationToken ct) => requested == null
            ? Task.FromResult(new FolderBrowserSnapshot(null, [new(path, "Archive disk", "10 GB free of 20 GB", true, true, 50)]))
            : new FolderBrowserSource().ReadAsync(requested, hidden, ct);
    }
    sealed class ManyFoldersSource(string path) : IFolderBrowserSource {
        public Task<FolderBrowserSnapshot> ReadAsync(string? requested, bool hidden, CancellationToken ct) =>
            Task.FromResult(new FolderBrowserSnapshot(path, Enumerable.Range(0, 85).Select(i => new FolderBrowserEntry(Path.Combine(path, i.ToString()), $"Folder {i}", "Folder")).ToList()));
    }
    sealed class DelayedSource : IFolderBrowserSource {
        public TaskCompletionSource<FolderBrowserSnapshot> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<FolderBrowserSnapshot> ReadAsync(string? path, bool hidden, CancellationToken ct) => Completion.Task;
    }
}
