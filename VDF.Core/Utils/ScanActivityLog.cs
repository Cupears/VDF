namespace VDF.Core.Utils;

/// <summary>Reports quiet work without treating a moving clock as completed work.</summary>
internal sealed class ScanActivityLog {
    readonly object gate = new();
    readonly Func<DateTimeOffset> now;
    ScanProgressSnapshot? latest;
    DateTimeOffset lastChange, lastLog;
    internal ScanActivityLog(Func<DateTimeOffset>? clock = null) => now = clock ?? (() => DateTimeOffset.UtcNow);
    internal void Observe(ScanProgressSnapshot snapshot) {
        lock (gate) {
            if (latest == null || latest.CurrentFile != snapshot.CurrentFile || latest.CurrentStage != snapshot.CurrentStage ||
                latest.CurrentPosition != snapshot.CurrentPosition || latest.MaxPosition != snapshot.MaxPosition ||
                latest.StageCurrent != snapshot.StageCurrent || latest.StageMax != snapshot.StageMax) lastChange = now();
            if (latest == null) lastLog = now();
            latest = snapshot;
        }
    }
    internal void Reset() { lock (gate) { latest = null; lastChange = lastLog = now(); } }
    internal void Resumed() { lock (gate) { lastChange = lastLog = now(); } }
    internal (string Message, bool Quiet)? Tick() {
        lock (gate) {
            var time = now();
            if (latest == null || time - lastLog < TimeSpan.FromSeconds(10)) return null;
            lastLog = time;
            var quiet = time - lastChange;
            string count = latest.MaxPosition > 0 ? $"{latest.CurrentPosition:N0}/{latest.MaxPosition:N0}" : $"{latest.CurrentPosition:N0} (total not known yet)";
            string message = $"Activity: {latest.CurrentStage}; count {count}; elapsed {latest.Elapsed:hh\\:mm\\:ss}; last reported work changed {quiet.TotalSeconds:0}s ago.";
            if (!string.IsNullOrEmpty(latest.CurrentFile)) message += $" Latest file/folder: {latest.CurrentFile}";
            if (quiet >= TimeSpan.FromSeconds(30)) message += " No new progress has been reported. A slow file, drive, network share, or decoder may be waiting; this alone does not prove the scan is frozen.";
            return (message, quiet >= TimeSpan.FromSeconds(30));
        }
    }
}
