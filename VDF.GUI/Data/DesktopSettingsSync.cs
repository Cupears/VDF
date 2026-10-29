using System.Collections.ObjectModel;
using System.Linq;
using VDF.Core.Utils;
using ScanSettings = VDF.Core.Settings;

namespace VDF.GUI.Data;

/// <summary>Maps the desktop's saved values to the same settings exchange the web app uses.</summary>
internal sealed class DesktopSettingsSync {
    readonly SharedScanSettings store;
    public DesktopSettingsSync(string path) => store = new(path);

    internal static ScanSettings Capture(SettingsFile gui) => new() {
            IncludeList = new(gui.Includes),
            BlackList = new(gui.Blacklists),
            IncludeSubDirectories = gui.IncludeSubDirectories,
            IncludeImages = gui.IncludeImages,
            IgnoreReadOnlyFolders = gui.IgnoreReadOnlyFolders,
            IgnoreReparsePoints = gui.IgnoreReparsePoints,
            ExcludeHardLinks = gui.ExcludeHardLinks,
            Percent = gui.Percent,
            PercentDurationDifference = gui.PercentDurationDifference,
            DurationDifferenceMinSeconds = gui.DurationDifferenceMinSeconds,
            DurationDifferenceMaxSeconds = gui.DurationDifferenceMaxSeconds,
            MaxSamplingDurationSeconds = gui.MaxSamplingDurationSeconds,
            MaxDegreeOfParallelism = gui.MaxDegreeOfParallelism,
            MatchingMaxDegreeOfParallelism = gui.MatchingMaxDegreeOfParallelism,
            HddMaxDegreeOfParallelism = gui.HddMaxDegreeOfParallelism,
            DriveTypeOverrides = new(gui.DriveTypeOverrides),
            ThumbnailCount = gui.Thumbnails,
            ExtendedFFToolsLogging = gui.ExtendedFFToolsLogging,
            LogExcludedFiles = gui.LogExcludedFiles,
            AlwaysRetryFailedSampling = gui.AlwaysRetryFailedSampling,
            IncludeVeryDarkPhotos = gui.IncludeVeryDarkPhotos,
            CustomFFArguments = gui.CustomFFArguments,
            UseNativeFfmpegBinding = gui.UseNativeFfmpegBinding,
            HardwareAccelerationMode = gui.HardwareAccelerationMode,
            IgnoreBlackPixels = gui.IgnoreBlackPixels,
            IgnoreWhitePixels = gui.IgnoreWhitePixels,
            CompareHorizontallyFlipped = gui.CompareHorizontallyFlipped,
            DatabaseCheckpointIntervalMinutes = gui.DatabaseCheckpointIntervalMinutes,
            IncludeNonExistingFiles = gui.IncludeNonExistingFiles,
            RememberDeletedContent = gui.RememberDeletedContent,
            FilterByFilePathContains = gui.FilterByFilePathContains,
            FilePathContainsTexts = new(gui.FilePathContainsTexts),
            FilterByFilePathNotContains = gui.FilterByFilePathNotContains,
            FilePathNotContainsTexts = new(gui.FilePathNotContainsTexts),
            ScanAgainstEntireDatabase = gui.ScanAgainstEntireDatabase,
            FolderMatchMode = gui.FolderMatchMode,
            SameFolderDepth = gui.SameFolderDepth,
            UsePHashing = gui.UsePHash,
            CombineGrayscaleAndPHash = gui.CombineGrayPHash,
            PHashRequiredMatchingSampleRatio = gui.PHashSampleRatioPercent / 100f,
            UseAiMatching = gui.UseAiMatching,
            AiPercent = gui.AiPercent,
            EnableAiPartialDetection = gui.EnableAiPartialDetection,
            AiPartialHitPercent = gui.AiPartialHitPercent,
            UseExifCreationDate = gui.UseExifCreationDate,
            FilterByFileSize = gui.FilterByFileSize,
            MaximumFileSize = gui.MaximumFileSize,
            MinimumFileSize = gui.MinimumFileSize,
            EnablePartialClipDetection = gui.EnablePartialClipDetection,
            PartialClipMinRatio = gui.PartialClipMinRatioPercent / 100d,
            PartialClipSimilarityThreshold = gui.PartialClipSimilarityThresholdPercent / 100d,
            PartialClipRequireVisualMatch = gui.PartialClipRequireVisualMatch,
            PartialClipVisualThreshold = gui.PartialClipVisualThresholdPercent / 100d,
    };

    public bool Synchronize(SettingsFile gui) {
        var scan = Capture(gui);
        var order = new List<string>(gui.QualityCriteriaOrder);
        var disabled = new List<string>(gui.QualityCriteriaDisabled);
        if (!store.Synchronize(scan, order, disabled)) return false;
        Apply(gui, scan);
        if (!gui.QualityCriteriaOrder.SequenceEqual(order)) gui.QualityCriteriaOrder = order;
        if (!gui.QualityCriteriaDisabled.SequenceEqual(disabled)) gui.QualityCriteriaDisabled = disabled;
        return true;
    }

    internal static void Apply(SettingsFile gui, ScanSettings scan) {
        Replace(gui.Includes, scan.IncludeList);
        Replace(gui.Blacklists, scan.BlackList);
        gui.IncludeSubDirectories = scan.IncludeSubDirectories;
        gui.IncludeImages = scan.IncludeImages;
        gui.IgnoreReadOnlyFolders = scan.IgnoreReadOnlyFolders;
        gui.IgnoreReparsePoints = scan.IgnoreReparsePoints;
        gui.ExcludeHardLinks = scan.ExcludeHardLinks;
        gui.Percent = scan.Percent;
        gui.PercentDurationDifference = scan.PercentDurationDifference;
        gui.DurationDifferenceMinSeconds = (int)scan.DurationDifferenceMinSeconds;
        gui.DurationDifferenceMaxSeconds = (int)scan.DurationDifferenceMaxSeconds;
        gui.MaxSamplingDurationSeconds = (int)scan.MaxSamplingDurationSeconds;
        gui.MaxDegreeOfParallelism = scan.MaxDegreeOfParallelism;
        gui.MatchingMaxDegreeOfParallelism = scan.MatchingMaxDegreeOfParallelism;
        gui.HddMaxDegreeOfParallelism = scan.HddMaxDegreeOfParallelism;
        gui.DriveTypeOverrides = new(scan.DriveTypeOverrides);
        gui.Thumbnails = scan.ThumbnailCount;
        gui.ExtendedFFToolsLogging = scan.ExtendedFFToolsLogging;
        gui.LogExcludedFiles = scan.LogExcludedFiles;
        gui.AlwaysRetryFailedSampling = scan.AlwaysRetryFailedSampling;
        gui.IncludeVeryDarkPhotos = scan.IncludeVeryDarkPhotos;
        gui.CustomFFArguments = scan.CustomFFArguments;
        gui.UseNativeFfmpegBinding = scan.UseNativeFfmpegBinding;
        gui.HardwareAccelerationMode = scan.HardwareAccelerationMode;
        gui.IgnoreBlackPixels = scan.IgnoreBlackPixels;
        gui.IgnoreWhitePixels = scan.IgnoreWhitePixels;
        gui.CompareHorizontallyFlipped = scan.CompareHorizontallyFlipped;
        gui.DatabaseCheckpointIntervalMinutes = scan.DatabaseCheckpointIntervalMinutes;
        gui.IncludeNonExistingFiles = scan.IncludeNonExistingFiles;
        gui.RememberDeletedContent = scan.RememberDeletedContent;
        gui.FilterByFilePathContains = scan.FilterByFilePathContains;
        Replace(gui.FilePathContainsTexts, scan.FilePathContainsTexts);
        gui.FilterByFilePathNotContains = scan.FilterByFilePathNotContains;
        Replace(gui.FilePathNotContainsTexts, scan.FilePathNotContainsTexts);
        gui.ScanAgainstEntireDatabase = scan.ScanAgainstEntireDatabase;
        gui.FolderMatchMode = scan.FolderMatchMode;
        gui.SameFolderDepth = scan.SameFolderDepth;
        gui.UsePHash = scan.UsePHashing;
        gui.CombineGrayPHash = scan.CombineGrayscaleAndPHash;
        gui.PHashSampleRatioPercent = scan.PHashRequiredMatchingSampleRatio * 100f;
        gui.UseAiMatching = scan.UseAiMatching;
        gui.AiPercent = scan.AiPercent;
        gui.EnableAiPartialDetection = scan.EnableAiPartialDetection;
        gui.AiPartialHitPercent = scan.AiPartialHitPercent;
        gui.UseExifCreationDate = scan.UseExifCreationDate;
        gui.FilterByFileSize = scan.FilterByFileSize;
        gui.MaximumFileSize = scan.MaximumFileSize;
        gui.MinimumFileSize = scan.MinimumFileSize;
        gui.EnablePartialClipDetection = scan.EnablePartialClipDetection;
        gui.PartialClipMinRatioPercent = scan.PartialClipMinRatio * 100d;
        gui.PartialClipSimilarityThresholdPercent = scan.PartialClipSimilarityThreshold * 100d;
        gui.PartialClipRequireVisualMatch = scan.PartialClipRequireVisualMatch;
        gui.PartialClipVisualThresholdPercent = scan.PartialClipVisualThreshold * 100d;
    }

    static void Replace(ObservableCollection<string> target, IEnumerable<string> source) {
        var values = source.ToList();
        if (target.SequenceEqual(values)) return;
        target.Clear();
        foreach (var value in values) target.Add(value);
    }
}

