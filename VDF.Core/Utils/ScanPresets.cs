namespace VDF.Core.Utils;

public sealed record ScanPresetKnobs(float Percent, bool Flipped, bool IgnoreBlack, bool IgnoreWhite, bool AudioClips, bool Ai, bool AiClips);

/// <summary>The desktop's four guided scan profiles, expressed independently of the UI.</summary>
public static class ScanPresets {
    public static readonly string[] Names = ["Exact & near copies", "Edited & altered", "AI scan", "Deep clean", "Custom"];
    static readonly ScanPresetKnobs[] Presets = [
        new(98, false, false, false, false, false, false),
        new(92, true, true, true, false, false, false),
        new(92, true, true, true, false, true, true),
        new(92, true, true, true, true, true, true)
    ];
    public static ScanPresetKnobs Capture(Settings s) => new(s.Percent, s.CompareHorizontallyFlipped, s.IgnoreBlackPixels, s.IgnoreWhitePixels, s.EnablePartialClipDetection, s.UseAiMatching, s.EnableAiPartialDetection);
    public static string Detect(Settings s) { int index = Array.IndexOf(Presets, Capture(s)); return index < 0 ? "Custom" : Names[index]; }
    public static void Apply(Settings s, string name, ScanPresetKnobs? custom = null) {
        int index = Array.IndexOf(Names, name);
        var knobs = index is >= 0 and < 4 ? Presets[index] : custom;
        if (knobs == null) return;
        s.Percent = knobs.Percent; s.CompareHorizontallyFlipped = knobs.Flipped; s.IgnoreBlackPixels = knobs.IgnoreBlack; s.IgnoreWhitePixels = knobs.IgnoreWhite;
        s.EnablePartialClipDetection = knobs.AudioClips; s.UseAiMatching = knobs.Ai; s.EnableAiPartialDetection = knobs.AiClips;
    }
}
