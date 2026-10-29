using ReactiveUI;
using VDF.Core;
using VDF.Core.Utils;

namespace VDF.GUI.ViewModels;

public partial class MainWindowVM {
    ScanProgressSnapshot etaProgress = ScanProgressSnapshot.Default;
    ScanEtaDisplay scanEta = ScanEtaDisplay.From(ScanProgressSnapshot.Default, false, DateTimeOffset.Now);
    public ScanEtaDisplay ScanEta {
        get => scanEta;
        private set => this.RaiseAndSetIfChanged(ref scanEta, value);
    }
    void UpdateScanEta(ScanProgressSnapshot progress) {
        etaProgress = progress;
        ScanEta = ScanEtaDisplay.From(progress, IsPaused, DateTimeOffset.Now);
    }
}
