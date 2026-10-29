using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using VDF.Core;
using VDF.GUI.ViewModels;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

public sealed class ScanEtaVisualTests {
    [Fact]
    public Task EtaCardShowsTimePercentAndPausedStateInASmallWindow() => HeadlessUi.Run(async () => {
        var vm = new MainWindowVM { IsScanning = true };
        var progress = (ScanProgress)typeof(ScanEngine).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm.Scanner)!;
        progress.Update(p => p with { CurrentPosition = 375, MaxPosition = 1000, Remaining = TimeSpan.FromMinutes(12), Elapsed = TimeSpan.FromMinutes(4), CurrentStage = "Reading photos from HDD" }, true);
        var window = HeadlessUi.Show(new ScanningView { DataContext = vm }, 740, 650);
        try {
            HeadlessUi.Pump();
            await Task.Delay(200); HeadlessUi.Pump();
            Assert.Equal(37.5, window.GetVisualDescendants().OfType<ProgressBar>().First().Value);
            var text = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
            Assert.Contains("~ 12m 0s", text);
            Assert.Contains("37.5%", text);
            Assert.Contains(window.GetVisualDescendants().OfType<ScrollViewer>(), s => s.IsVisible);
            string? preview = Environment.GetEnvironmentVariable("VDF_ETA_PREVIEW_DIR");
            if (!string.IsNullOrEmpty(preview)) {
                Directory.CreateDirectory(preview);
                using var bitmap = new RenderTargetBitmap(new PixelSize(740, 650), new Vector(96, 96));
                bitmap.Render(window); bitmap.Save(Path.Combine(preview, "desktop-eta.png"));
            }
            vm.IsPaused = true; HeadlessUi.Pump();
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Paused");
        } finally { vm.IsScanning = false; window.Close(); }
    });
}
