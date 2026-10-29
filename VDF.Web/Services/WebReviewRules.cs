using VDF.Core.Utils;
using VDF.Core.ViewModels;

namespace VDF.Web.Services;

/// <summary>Desktop-equivalent quality ranking for web BEST badges and keep-best actions.</summary>
public static class WebReviewRules {
    public static readonly string[] DefaultOrder = ["Duration", "Resolution", "Bitrate", "FPS", "Bits per pixel", "Audio Bitrate", "Size", "SizeLarger"];
    public static List<string> NormalizeOrder(IEnumerable<string>? names) =>
        (names ?? []).Where(DefaultOrder.Contains).Concat(DefaultOrder).Distinct().ToList();

    static bool DurationTies(IComparable a, IComparable b) {
        double x = ((TimeSpan)a).TotalSeconds, y = ((TimeSpan)b).TotalSeconds;
        return Math.Abs(x - y) <= Math.Max(1, Math.Max(x, y) * 0.01);
    }
    static bool BitrateTies(IComparable a, IComparable b) => Math.Abs((decimal)a - (decimal)b) <= Math.Max((decimal)a, (decimal)b) * 0.05m;
    static decimal BitsPerPixel(DuplicateItem d) => d.BitRateKbs > 0 && d.FrameSizeInt > 0 && d.Fps > 0
        ? d.BitRateKbs * 1000m / ((decimal)d.FrameSizeInt * (decimal)d.Fps) : 0;

    static readonly Dictionary<string, QualityRanker.Criterion<DuplicateItem>> Criteria = new() {
        ["Duration"] = new("Duration", d => d.Duration, true, nearTie: DurationTies),
        ["Resolution"] = new("Resolution", d => d.FrameSizeInt, false),
        ["Bitrate"] = new("Bitrate", d => d.BitRateKbs, true, nearTie: BitrateTies),
        ["FPS"] = new("FPS", d => d.Fps, true, nearTie: (a, b) => Math.Abs((float)a - (float)b) <= 0.5f),
        ["Bits per pixel"] = new("Bits per pixel", d => BitsPerPixel(d), true, nearTie: BitrateTies),
        ["Audio Bitrate"] = new("Audio Bitrate", d => d.AudioBitRateKbs, true, nearTie: BitrateTies),
        ["Size"] = new("Size", d => d.SizeLong, false, ascending: true),
        ["SizeLarger"] = new("SizeLarger", d => d.SizeLong, false),
    };

    public static DuplicateItem PickKeeper(List<DuplicateItem> items, WebSettingsService settings) =>
        QualityRanker.PickKeeper(items, NormalizeOrder(settings.QualityCriteriaOrder)
            .Where(n => !settings.QualityCriteriaDisabled.Contains(n)).Select(n => Criteria[n]), d => d.IsImage);

    public static string Label(string name) => name switch {
        "Size" => "File size — smaller wins", "SizeLarger" => "File size — larger wins", _ => name
    };
}
