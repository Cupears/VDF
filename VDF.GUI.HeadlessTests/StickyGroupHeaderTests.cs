using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using VDF.Core.ViewModels;
using VDF.GUI.Data;
using VDF.GUI.ViewModels;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

public sealed class StickyGroupHeaderTests {
    sealed class Screen : IDisposable {
        readonly bool previews = SettingsFile.Instance.GeneratePreviewThumbnails;
        public MainWindowVM Vm { get; }
        public DuplicateResultsView View { get; }
        public Window Window { get; }
        public ListBox List { get; }
        public ScrollViewer Scroll { get; }
        public ScrollContentPresenter Viewport { get; }
        public ContentControl Pinned => View.FindControl<ContentControl>("StickyGroupHeader")!;
        public ResultsGroupHeader[] Groups => Vm.ResultsRows.OfType<ResultsGroupHeader>().ToArray();

        public Screen() {
            SettingsFile.Instance.GeneratePreviewThumbnails = false;
            Vm = new MainWindowVM();
            string folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "sticky-header-files", Guid.NewGuid().ToString("N"))).FullName;
            for (int groupIndex = 0; groupIndex < 2; groupIndex++) {
                var id = Guid.NewGuid();
                for (int n = 0; n < 20; n++) {
                    string path = Path.Combine(folder, $"group-{groupIndex}-{n:00}.mp4");
                    File.WriteAllBytes(path, new byte[16]);
                    Vm.Duplicates.Add(new DuplicateItemVM(new DuplicateItem {
                        Path = path, Folder = folder, GroupId = id, SizeLong = 16, Similarity = 100,
                        Duration = TimeSpan.FromSeconds(10), FrameSize = "1920x1080", FrameSizeInt = 1920 * 1080
                    }));
                }
            }
            Vm.RebuildResultsList();
            View = new DuplicateResultsView { DataContext = Vm };
            Window = HeadlessUi.Show(View, 1300, 700);
            List = View.FindControl<ListBox>("ResultsList")!;
            Scroll = List.GetVisualDescendants().OfType<ScrollViewer>().First();
            Viewport = Scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
        }

        public void Align(object row, double top = 0) {
            int index = Vm.ResultsRows.IndexOf(row);
            List.ScrollIntoView(index);
            HeadlessUi.Pump();
            var container = List.ContainerFromIndex(index)!;
            var point = container.TranslatePoint(default, Viewport)!.Value;
            Scroll.Offset = new Vector(0, Scroll.Offset.Y + point.Y - top);
            HeadlessUi.Pump();
        }

        public void Dispose() {
            Window.Close();
            SettingsFile.Instance.GeneratePreviewThumbnails = previews;
            // The generated files live under bin/SyncTests, recycled with build output.
        }
    }

    [Fact]
    public Task ScrollingPinsTheWholeHeaderAndCheckAllTargetsOnlyTheCurrentGroup() => HeadlessUi.Run(() => {
        using var screen = new Screen();
        Assert.False(screen.Pinned.IsVisible);
        var first = screen.Groups[0];
        screen.Align(first.Rows[8]);
        Assert.True(screen.Pinned.IsVisible);
        Assert.Equal(first.GroupId, Assert.IsType<ResultsGroupHeader>(screen.Pinned.Content).GroupId);
        var button = screen.Pinned.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check all"));
        Assert.NotNull(button.Command);
        button.Command.Execute(button.CommandParameter);
        HeadlessUi.Pump();
        Assert.All(screen.Vm.Duplicates.Where(d => d.ItemInfo.GroupId == first.GroupId), d => Assert.True(d.Checked));
        Assert.All(screen.Vm.Duplicates.Where(d => d.ItemInfo.GroupId != first.GroupId), d => Assert.False(d.Checked));
        Assert.Equal(first.GroupId, Assert.IsType<ResultsGroupHeader>(screen.Pinned.Content).GroupId);

        string? preview = Environment.GetEnvironmentVariable("VDF_STICKY_PREVIEW_DIR");
        if (!string.IsNullOrEmpty(preview)) {
            Directory.CreateDirectory(preview);
            using var bitmap = new RenderTargetBitmap(new PixelSize(1300, 700), new Vector(96, 96));
            bitmap.Render(screen.Window);
            bitmap.Save(Path.Combine(preview, "desktop-sticky-group.png"));
        }

        var second = screen.Groups[1];
        screen.Align(second.Rows[5]);
        Assert.Equal(second.GroupId, Assert.IsType<ResultsGroupHeader>(screen.Pinned.Content).GroupId);
        var click = screen.Pinned.TranslatePoint(new Point(65, 14), screen.Window)!.Value;
        screen.Window.MouseDown(click, MouseButton.Left);
        screen.Window.MouseUp(click, MouseButton.Left);
        HeadlessUi.Pump();
        Assert.True(screen.Groups.Single(g => g.GroupId == second.GroupId).IsCollapsed);
        Assert.DoesNotContain(screen.Vm.ResultsRows.OfType<ResultsItemRow>(), r => r.Group.GroupId == second.GroupId);
    });

    [Fact]
    public Task TheNextHeaderPushesThePinnedOneAwayAndClearingResultsRemovesIt() => HeadlessUi.Run(() => {
        using var screen = new Screen();
        var second = screen.Groups[1];
        screen.Align(screen.Groups[0].Rows[8]);
        double height = screen.Pinned.Bounds.Height;
        Assert.True(height > 0);
        Assert.Equal(screen.Viewport.Bounds.Width, screen.Pinned.Bounds.Width, 1);
        screen.Align(second, height / 2);
        Assert.True(screen.Pinned.IsVisible);
        Assert.InRange(((TranslateTransform)screen.Pinned.RenderTransform!).Y, -height, -1);
        screen.Align(second);
        Assert.False(screen.Pinned.IsVisible);
        screen.Align(second.Rows[5]);
        Assert.True(screen.Pinned.IsVisible);
        screen.Vm.Duplicates.Clear();
        screen.Vm.RebuildResultsList();
        HeadlessUi.Pump();
        Assert.False(screen.Pinned.IsVisible);
        Assert.Null(screen.Pinned.Content);
    });

    [Fact]
    public Task TheMouseWheelStillScrollsWhenItIsOverThePinnedHeader() => HeadlessUi.Run(() => {
        using var screen = new Screen();
        screen.Align(screen.Groups[0].Rows[6]);
        double before = screen.Scroll.Offset.Y;
        var point = screen.Pinned.TranslatePoint(new Point(65, 14), screen.Window)!.Value;
        screen.Window.MouseWheel(point, new Vector(0, -1));
        HeadlessUi.Pump();
        Assert.True(screen.Scroll.Offset.Y > before);
    });
}
