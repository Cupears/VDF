using System.Globalization;

namespace VDF.Core.Utils;

/// <summary>Shared wording for the desktop and phone; estimates describe one step, not the whole scan.</summary>
public sealed record ScanEtaDisplay(string Remaining, string Finish, string Status, string Note,
    string Percent, string Count, string Elapsed, double Progress, bool UnknownTotal) {
    public static ScanEtaDisplay From(ScanProgressSnapshot progress, bool paused, DateTimeOffset now) {
        int current = Math.Max(0, progress.CurrentPosition);
        int total = progress.MaxPosition;
        bool unknown = total <= 0 || progress.CountsSavedCache;
        double percent = unknown ? 0 : Math.Clamp(100d * current / total, 0, 100);
        string count = unknown ? $"{current:N0} found · still counting the total" : $"{current:N0} / {total:N0} items";
        string remaining, finish, status, note;
        if (paused) {
            remaining = "Paused"; finish = "Updates when resumed"; status = "SCAN PAUSED";
            note = "The clock is paused. Resume the scan to refresh the estimate.";
        } else if (progress.CountsSavedCache) {
            remaining = "Preparing"; finish = "Not available yet"; status = "PREPARING SAVED CACHE";
            note = "Reading the SSD cache. This is not scanning every original file.";
            count = "Preparing saved cache";
        } else if (unknown) {
            remaining = "Still counting"; finish = "Not available yet"; status = "FINDING THE TOTAL";
            note = "VDF is listing folders or preparing work. No finish time is known yet.";
        } else if (current >= total) {
            remaining = "Wrapping up"; finish = "Waiting for next step"; status = "STEP COUNTER COMPLETE";
            note = "This step's counter is complete. Saving or another scan step may still be needed.";
        } else if (current == 0 || progress.Remaining <= TimeSpan.Zero) {
            remaining = "Calculating…"; finish = "Not available yet"; status = "BUILDING AN ESTIMATE";
            note = "Waiting for enough completed work to estimate this step.";
        } else {
            remaining = "~ " + Duration(progress.Remaining); status = "ESTIMATED TIME LEFT IN THIS STEP";
            note = "Current step only. Later scan steps add time; file sizes and drive speed can change this estimate.";
            try {
                var end = now + progress.Remaining;
                string day = end.Date == now.Date ? "Today" : end.Date == now.Date.AddDays(1) ? "Tomorrow" : end.ToString("MMM d", CultureInfo.InvariantCulture);
                finish = day + " · " + end.ToString("h:mm tt", CultureInfo.InvariantCulture);
            } catch (ArgumentOutOfRangeException) { finish = "Estimate too far ahead"; }
        }
        return new(remaining, finish, status, note, unknown ? "Total unknown" : percent.ToString("0.0", CultureInfo.InvariantCulture) + "%",
            count, Duration(progress.Elapsed), percent, unknown);
    }

    public static string Duration(TimeSpan time) {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        if (time.TotalDays >= 1) return $"{Math.Floor(time.TotalDays):0}d {time.Hours}h";
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes}m";
        if (time.TotalMinutes >= 1) return $"{(int)time.TotalMinutes}m {time.Seconds}s";
        return $"{Math.Ceiling(time.TotalSeconds):0} sec";
    }
}
