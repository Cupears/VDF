using VDF.Core.Utils;

namespace VDF.Core.Tests;

public class WhiteBackgroundImageTests {
    static byte[] Drawing(int start) {
        var frame = Enumerable.Repeat((byte)255, 1024).ToArray();
        Array.Fill(frame, (byte)0, start, 18);
        frame[1000] = 0; // shared ink: the old white mask compared only this overlap
        return frame;
    }

    static FileEntry Image(string name, byte[] frame) => new() {
        Path = Path.Combine(Path.GetTempPath(), name), IsImage = true, compareGray = new byte[]?[] { frame }
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DifferentDrawingsAreRejectedWithWhiteIgnoringEitherOnOrOff(bool ignoreWhite) {
        var a = Drawing(200); var b = Drawing(600);
        Assert.True(1 - GrayBytesUtils.PercentageDifference(a, b) > 0.96f);
        var engine = new ScanEngine();
        engine.Settings.Percent = 96;
        engine.Settings.IgnoreWhitePixels = ignoreWhite;
        engine.Settings.UsePHashing = true;
        Assert.False(engine.CheckIfDuplicate(Image("drawing-a.png", a), null, null, Image("drawing-b.png", b), out _));
    }

    [Fact]
    public void SameDrawingWithMinorGrayVariationStillMatches() {
        var a = Drawing(200); var b = (byte[])a.Clone(); b[205] = 10;
        var engine = new ScanEngine(); engine.Settings.Percent = 96;
        Assert.True(engine.CheckIfDuplicate(Image("same-a.png", a), null, null, Image("same-b.png", b), out float difference));
        Assert.InRange(difference, 0, 0.04f);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(1024)]
    public void WhiteMaskCountsInkAgainstWhiteInScalarAndVectorPaths(int length) {
        var a = Enumerable.Repeat((byte)255, length).ToArray();
        var b = (byte[])a.Clone();
        a[0] = 0; b[1] = 0;
        Assert.Equal(255f / 256, GrayBytesUtils.PercentageDifferenceWithoutSpecificPixels(a, b, false, true), 6);
    }

    [Fact]
    public void AnEmptyWhiteCanvasIsNotEnoughEvidenceForAMatch() {
        var blank = Enumerable.Repeat((byte)255, 1024).ToArray();
        var engine = new ScanEngine();
        Assert.False(engine.CheckIfDuplicate(Image("blank-a.png", blank), null, null, Image("blank-b.png", blank), out _));
    }
}
