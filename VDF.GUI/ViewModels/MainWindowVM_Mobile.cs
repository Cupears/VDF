using System.Reactive.Linq;
using System.Linq;
using System.Collections.Specialized;
using ReactiveUI;
using Avalonia.Threading;
using Microsoft.AspNetCore.Builder;
using VDF.Core.Utils;
using VDF.GUI.Data;
using VDF.Web;
using VDF.Web.Services;

namespace VDF.GUI.ViewModels;

public partial class MainWindowVM {
    WebApplication? mobileServer;
    ScanService? mobileSession;
    bool applyingMobileState;
    bool mobileFileOperationBusy;
    DatabaseSessionLease? databaseSessionLease;
    public string MobileSessionStatus { get; private set; } = "Mobile server is off";

    async Task StartMobileServerAsync() {
        if (Environment.GetEnvironmentVariable("VDF_DESKTOP_MOBILE") != "1") return;
        SyncCoreSettings();
        AttachMobileSession(new WebSettingsService());
        string root = Environment.GetEnvironmentVariable("VDF_MOBILE_CONTENT_ROOT")
            ?? throw new InvalidOperationException("The mobile content folder is missing. Use Start VDF.cmd.");
        mobileServer = await WebServer.CreateAsync(new[] { "--urls", "http://127.0.0.1:5001" }, mobileSession, root);
        await mobileServer.StartAsync();
        MobileSessionStatus = "Phone shares this desktop scan and database · live progress, results and checked files";
        this.RaisePropertyChanged(nameof(MobileSessionStatus));
        Logger.Instance.Info(MobileSessionStatus);
    }

    internal ScanService AttachMobileSession(WebSettingsService settingsStore) {
        mobileSession = new ScanService(settingsStore, engine: Scanner);
        mobileSession.DesktopIsBusy = () => IsScanning || (IsBusy && !mobileFileOperationBusy);
        mobileSession.DesktopStartScan = () => OnDesktopAsync(async () => {
            if (IsScanning || IsBusy) throw new InvalidOperationException("The desktop is busy. Wait for it to finish.");
            DesktopSettingsSync.Apply(SettingsFile.Instance, mobileSession.Settings);
            await StartScanCommand.Execute("FullScan");
        });
        mobileSession.DesktopPause = paused => Dispatcher.UIThread.Post(() => {
            if (!IsScanning) return;
            if (paused) PauseScanCommand.Execute().Subscribe();
            else ResumeScanCommand.Execute().Subscribe();
        });
        mobileSession.DesktopStop = () => Dispatcher.UIThread.Post(() => {
            if (IsScanning) StopScanCommand.Execute().Subscribe();
        });
        mobileSession.StateChanged += OnMobileStateChanged;
        PublishDesktopResults();
        return mobileSession;
    }

    internal void CloseMobileSession() {
        mobileServer?.Lifetime.StopApplication();
        if (mobileSession != null) { mobileSession.StateChanged -= OnMobileStateChanged; mobileSession.Dispose(); mobileSession = null; }
        // The server shares this process's lifetime; no second database writer remains after exit.
        databaseSessionLease?.Dispose();
    }

    static Task OnDesktopAsync(Func<Task> action) {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () => {
            try { await action(); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        return completion.Task;
    }

    void OnMobileStateChanged() => Dispatcher.UIThread.Post(() => {
        if (mobileSession == null || IsScanning || applyingMobileState) return;
        if (mobileSession.FileOpRunning) {
            if (!IsBusy) { mobileFileOperationBusy = true; IsBusy = true; IsBusyOverlayText = "Phone: " + mobileSession.FileOpVerb + " files..."; }
            return;
        }
        if (mobileFileOperationBusy) { mobileFileOperationBusy = false; IsBusy = false; }
        applyingMobileState = true;
        try {
            var present = Scanner.Duplicates.ToHashSet();
            bool removed = false;
            foreach (var row in Duplicates.ToList()) {
                if (!present.Contains(row.ItemInfo)) { Duplicates.Remove(row); removed = true; }
                else row.Checked = mobileSession.SelectedItems.Contains(row.ItemInfo);
            }
            if (removed) { RefreshGroupStats(); RebuildSearchPathIndex(); RebuildResultsList(); }
        } finally { applyingMobileState = false; }
    });

    void PublishDesktopResults() {
        if (mobileSession == null || IsScanning || applyingMobileState) return;
        Scanner.Duplicates.Clear();
        Scanner.Duplicates.UnionWith(Duplicates.Select(d => d.ItemInfo));
        foreach (var selected in mobileSession.SelectedItems.ToList())
            if (!Scanner.Duplicates.Contains(selected)) mobileSession.SelectedItems.Remove(selected);
        foreach (var row in Duplicates) {
            if (row.Checked) mobileSession.SelectedItems.Add(row.ItemInfo);
            else mobileSession.SelectedItems.Remove(row.ItemInfo);
        }
        mobileSession.RefreshDesktopResults();
    }

    void PublishDesktopCheck(DuplicateItemVM row) {
        if (mobileSession == null || applyingMobileState) return;
        if (row.Checked) mobileSession.SelectedItems.Add(row.ItemInfo);
        else mobileSession.SelectedItems.Remove(row.ItemInfo);
    }

    void PublishDesktopCollectionChange(NotifyCollectionChangedEventArgs change) {
        if (mobileSession == null || IsScanning || applyingMobileState) return;
        if (change.Action == NotifyCollectionChangedAction.Reset) {
            Scanner.Duplicates.Clear();
            mobileSession.SelectedItems.Clear();
        }
        if (change.OldItems != null) foreach (DuplicateItemVM row in change.OldItems) {
            Scanner.Duplicates.Remove(row.ItemInfo);
            mobileSession.SelectedItems.Remove(row.ItemInfo);
        }
        if (change.NewItems != null) foreach (DuplicateItemVM row in change.NewItems) {
            Scanner.Duplicates.Add(row.ItemInfo);
            PublishDesktopCheck(row);
        }
        mobileSession.RefreshDesktopResults();
    }
}
