using ReactiveUI;
using VDF.Core;
using VDF.Core.Utils;

namespace VDF.GUI.ViewModels;

public partial class MainWindowVM {
    ScanProgressSnapshot etaProgress = ScanProgressSnapshot.Default;
    ScanEtaDisplay scanEta = ScanEtaDisplay.From(ScanProgressSnapshot.Default, false, DateTimeOffset.Now);
    public string ActiveScanFolder { get; private set; } = "Preparing scan";
    ScanEtaDisplay folderEta = ScanEtaDisplay.From(ScanProgressSnapshot.Default, false, DateTimeOffset.Now);
    public ScanEtaDisplay FolderEta { get => folderEta; private set => this.RaiseAndSetIfChanged(ref folderEta, value); }
    public ScanEtaDisplay ScanEta {
        get => scanEta;
        private set => this.RaiseAndSetIfChanged(ref scanEta, value);
    }
    void UpdateScanEta(ScanProgressSnapshot progress) {
        etaProgress = progress;
        ScanEta = ScanEtaDisplay.From(progress, IsPaused, DateTimeOffset.Now);
        ActiveScanFolder = progress.Folder?.Path ?? "Current step: " + (string.IsNullOrEmpty(progress.CurrentStage) ? "Preparing scan" : progress.CurrentStage);
        this.RaisePropertyChanged(nameof(ActiveScanFolder));
        FolderEta = progress.Folder is { } folder
            ? ScanEtaDisplay.From(progress with { CurrentPosition = folder.Done, MaxPosition = folder.Total, Elapsed = folder.Elapsed, Remaining = folder.Remaining }, IsPaused, DateTimeOffset.Now)
            : ScanEta;
    }
}
