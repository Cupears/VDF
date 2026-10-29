using System.Reflection;
using VDF.Core;
using VDF.Core.ViewModels;
using VDF.GUI.ViewModels;
using VDF.Web.Services;

namespace VDF.GUI.HeadlessTests;

public sealed class MobileSessionTests {
    [Fact]
    public Task NativeAndPhoneShareEngineResultsAndChecks() => HeadlessUi.Run(async () => {
        var vm = new MainWindowVM();
        var group = Guid.NewGuid();
        var a = new DuplicateItemVM(new DuplicateItem { Path = @"C:\vdf-test\a.mp4", GroupId = group });
        var b = new DuplicateItemVM(new DuplicateItem { Path = @"C:\vdf-test\b.mp4", GroupId = group });
        vm.Duplicates.Add(a);
        vm.Duplicates.Add(b);
        using var scan = vm.AttachMobileSession(new WebSettingsService());
        try {
            Assert.Same(vm.Scanner, scan.Engine);
            Assert.Contains(a.ItemInfo, scan.Duplicates);
            a.Checked = true;
            Assert.Contains(a.ItemInfo, scan.SelectedItems);
            scan.SelectedItems.Add(b.ItemInfo);
            await Task.Delay(150);
            HeadlessUi.Pump();
            Assert.True(b.Checked);
            scan.SelectedItems.Remove(a.ItemInfo);
            await Task.Delay(150);
            HeadlessUi.Pump();
            Assert.False(a.Checked);
            scan.RemoveFromResults(new[] { a.ItemInfo });
            HeadlessUi.Pump();
            Assert.Empty(vm.Duplicates); // Both views drop the remaining singleton.
        } finally { vm.CloseMobileSession(); }
    });

    [Fact]
    public Task DesktopProgressAndPhonePauseUseOneScanner() => HeadlessUi.Run(async () => {
        var vm = new MainWindowVM();
        using var scan = vm.AttachMobileSession(new WebSettingsService());
        try {
            vm.IsScanning = true;
            Assert.Equal(ScanState.Scanning, scan.State);
            var progress = (ScanProgress)typeof(ScanEngine).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm.Scanner)!;
            progress.Update(p => p with { CurrentFile = @"C:\test\clip.mp4", CurrentPosition = 7, MaxPosition = 10 }, true);
            HeadlessUi.Pump();
            Assert.Equal(7, scan.LastProgress!.Current);
            Assert.Equal(vm.ScanCurrentFile, scan.LastProgress.CurrentFile);
            scan.Pause();
            await Task.Delay(80);
            HeadlessUi.Pump();
            Assert.True(vm.IsPaused);
            Assert.True(scan.IsPaused);
            scan.Resume();
            await Task.Delay(80);
            HeadlessUi.Pump();
            Assert.False(vm.IsPaused);
            Assert.False(scan.IsPaused);
        } finally { vm.IsScanning = false; vm.CloseMobileSession(); }
    });
}
