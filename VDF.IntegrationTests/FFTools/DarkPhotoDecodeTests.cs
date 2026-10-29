using VDF.Core;
using VDF.IntegrationTests.Fixtures;

namespace VDF.IntegrationTests.FFTools;

[Collection("Ffmpeg")]
public sealed class DarkPhotoDecodeTests(FfmpegFixture fixture) {
    [SkippableFact]
    public void PreviouslyRejectedDarkPhotoDecodesAndCachesWhenEnabled() {
        Skip.If(!fixture.FfmpegCliAvailable, fixture.FfmpegNotFoundReason);
        string path = Path.Combine(fixture.TempDir, "mostly-black-option-test.bmp");
        var pixels = new byte[32 * 32 * 3];
        for (int i = 0; i < 20 * 3; i++) pixels[i] = 100;
        using (var writer = new BinaryWriter(File.Create(path))) {
            writer.Write((ushort)0x4D42); writer.Write(54 + pixels.Length); writer.Write(0); writer.Write(54);
            writer.Write(40); writer.Write(32); writer.Write(32); writer.Write((ushort)1); writer.Write((ushort)24);
            writer.Write(0); writer.Write(pixels.Length); writer.Write(new byte[16]); writer.Write(pixels);
        }
        var entry = new FileEntry(path);
        Assert.False(ScanEngine.GetGrayBytesFromImage(entry, false, false));
        Assert.True(entry.IsTooDark);
        Assert.Empty(entry.grayBytes);
        Assert.True(ScanEngine.GetGrayBytesFromImage(entry, false, false, includeVeryDarkPhotos: true));
        Assert.True(entry.IsTooDark);
        Assert.Equal(1024, entry.grayBytes[0]!.Length);
    }
}
