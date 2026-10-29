using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;

namespace VDF.Core.Utils;

/// <summary>Opt-in, two-way settings exchange. Only changed keys are written; each app keeps its own database and UI.</summary>
public sealed class SharedScanSettings {
    public const string EnvironmentKey = "VDF_SETTINGS_SYNC_PATH";
    public static readonly string[] Keys = [
        "IncludeList", "BlackList", "IncludeSubDirectories", "IncludeImages", "IgnoreReadOnlyFolders", "IgnoreReparsePoints", "ExcludeHardLinks",
        "Percent", "PercentDurationDifference", "DurationDifferenceMinSeconds", "DurationDifferenceMaxSeconds", "MaxSamplingDurationSeconds",
        "MaxDegreeOfParallelism", "MatchingMaxDegreeOfParallelism", "HddMaxDegreeOfParallelism", "DriveTypeOverrides", "ThumbnailCount",
        "ExtendedFFToolsLogging", "LogExcludedFiles", "AlwaysRetryFailedSampling", "IncludeVeryDarkPhotos", "CustomFFArguments", "UseNativeFfmpegBinding", "HardwareAccelerationMode",
        "IgnoreBlackPixels", "IgnoreWhitePixels", "CompareHorizontallyFlipped", "DatabaseCheckpointIntervalMinutes", "IncludeNonExistingFiles", "RememberDeletedContent",
        "FilterByFilePathContains", "FilePathContainsTexts", "FilterByFilePathNotContains", "FilePathNotContainsTexts", "ScanAgainstEntireDatabase", "FolderMatchMode", "SameFolderDepth",
        "UsePHashing", "CombineGrayscaleAndPHash", "PHashRequiredMatchingSampleRatio", "UseAiMatching", "AiPercent", "EnableAiPartialDetection", "AiPartialHitPercent",
        "UseExifCreationDate", "FilterByFileSize", "MaximumFileSize", "MinimumFileSize", "EnablePartialClipDetection", "PartialClipMinRatio",
        "PartialClipSimilarityThreshold", "PartialClipRequireVisualMatch", "PartialClipVisualThreshold"
    ];
    readonly string path;
    readonly object gate = new();
    JsonObject? baseline;
    public SharedScanSettings(string path) => this.path = Path.GetFullPath(path);

    public bool Synchronize(Settings settings, List<string> qualityOrder, List<string> qualityDisabled) {
        lock (gate) {
            var all = JsonSerializer.SerializeToNode(settings, CoreJsonContext.Default.Settings)!.AsObject();
            var local = new JsonObject();
            foreach (var key in Keys) local[key] = all[key]?.DeepClone();
            local["QualityCriteriaOrder"] = new JsonArray(qualityOrder.Select(n => (JsonNode?)JsonValue.Create(n)).ToArray());
            local["QualityCriteriaDisabled"] = new JsonArray(qualityDisabled.Select(n => (JsonNode?)JsonValue.Create(n)).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fileLock = AcquireLock();
            var disk = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new JsonException("Shared settings must contain an object.") : new JsonObject();
            var merged = (JsonObject)disk.DeepClone();
            foreach (var entry in local) {
                bool localEdit = baseline != null && !JsonNode.DeepEquals(entry.Value, baseline[entry.Key]);
                if (!merged.ContainsKey(entry.Key) || localEdit) merged[entry.Key] = entry.Value?.DeepClone();
            }
            bool changed = local.Any(entry => !JsonNode.DeepEquals(entry.Value, merged[entry.Key]));
            bool write = !JsonNode.DeepEquals(disk, merged);
            if (changed) {
                var incoming = merged.Deserialize(CoreJsonContext.Default.Settings) ?? throw new JsonException("Shared scan settings are invalid.");
                var order = ReadNames(merged["QualityCriteriaOrder"]).ToList();
                var disabled = ReadNames(merged["QualityCriteriaDisabled"]).ToList();
                Apply(settings, incoming);
                Replace(qualityOrder, order);
                Replace(qualityDisabled, disabled);
            }
            if (write) {
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, merged.ToJsonString());
                File.Move(temporary, path, true);
            }
            baseline = merged;
            return changed || write;
        }
    }

    FileStream AcquireLock() {
        for (int attempt = 0; ; attempt++) {
            try { return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 8) { Thread.Sleep(10); }
        }
    }
    static IEnumerable<string> ReadNames(JsonNode? value) => value?.AsArray().Select(n => n!.GetValue<string>()) ?? [];
    static void Replace(List<string> target, IEnumerable<string> source) { target.Clear(); target.AddRange(source); }

    static void Apply(Settings d, Settings s) {
        d.IncludeList = new(s.IncludeList); d.BlackList = new(s.BlackList);
        d.IncludeSubDirectories = s.IncludeSubDirectories; d.IncludeImages = s.IncludeImages;
        d.IgnoreReadOnlyFolders = s.IgnoreReadOnlyFolders; d.IgnoreReparsePoints = s.IgnoreReparsePoints; d.ExcludeHardLinks = s.ExcludeHardLinks;
        d.Percent = s.Percent; d.PercentDurationDifference = s.PercentDurationDifference;
        d.DurationDifferenceMinSeconds = s.DurationDifferenceMinSeconds; d.DurationDifferenceMaxSeconds = s.DurationDifferenceMaxSeconds; d.MaxSamplingDurationSeconds = s.MaxSamplingDurationSeconds;
        d.MaxDegreeOfParallelism = s.MaxDegreeOfParallelism; d.MatchingMaxDegreeOfParallelism = s.MatchingMaxDegreeOfParallelism; d.HddMaxDegreeOfParallelism = s.HddMaxDegreeOfParallelism;
        d.DriveTypeOverrides = new(s.DriveTypeOverrides); d.ThumbnailCount = s.ThumbnailCount;
        d.ExtendedFFToolsLogging = s.ExtendedFFToolsLogging; d.LogExcludedFiles = s.LogExcludedFiles; d.AlwaysRetryFailedSampling = s.AlwaysRetryFailedSampling;
        d.IncludeVeryDarkPhotos = s.IncludeVeryDarkPhotos;
        d.CustomFFArguments = s.CustomFFArguments; d.UseNativeFfmpegBinding = s.UseNativeFfmpegBinding; d.HardwareAccelerationMode = s.HardwareAccelerationMode;
        d.IgnoreBlackPixels = s.IgnoreBlackPixels; d.IgnoreWhitePixels = s.IgnoreWhitePixels; d.CompareHorizontallyFlipped = s.CompareHorizontallyFlipped;
        d.DatabaseCheckpointIntervalMinutes = s.DatabaseCheckpointIntervalMinutes; d.IncludeNonExistingFiles = s.IncludeNonExistingFiles; d.RememberDeletedContent = s.RememberDeletedContent;
        d.FilterByFilePathContains = s.FilterByFilePathContains; d.FilePathContainsTexts = new(s.FilePathContainsTexts);
        d.FilterByFilePathNotContains = s.FilterByFilePathNotContains; d.FilePathNotContainsTexts = new(s.FilePathNotContainsTexts);
        d.ScanAgainstEntireDatabase = s.ScanAgainstEntireDatabase; d.FolderMatchMode = s.FolderMatchMode; d.SameFolderDepth = s.SameFolderDepth;
        d.UsePHashing = s.UsePHashing; d.CombineGrayscaleAndPHash = s.CombineGrayscaleAndPHash; d.PHashRequiredMatchingSampleRatio = s.PHashRequiredMatchingSampleRatio;
        d.UseAiMatching = s.UseAiMatching; d.AiPercent = s.AiPercent; d.EnableAiPartialDetection = s.EnableAiPartialDetection; d.AiPartialHitPercent = s.AiPartialHitPercent;
        d.UseExifCreationDate = s.UseExifCreationDate; d.FilterByFileSize = s.FilterByFileSize; d.MaximumFileSize = s.MaximumFileSize; d.MinimumFileSize = s.MinimumFileSize;
        d.EnablePartialClipDetection = s.EnablePartialClipDetection; d.PartialClipMinRatio = s.PartialClipMinRatio; d.PartialClipSimilarityThreshold = s.PartialClipSimilarityThreshold;
        d.PartialClipRequireVisualMatch = s.PartialClipRequireVisualMatch; d.PartialClipVisualThreshold = s.PartialClipVisualThreshold;
    }
}
