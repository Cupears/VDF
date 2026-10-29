using VDF.Core.Utils;
using VDF.Core.ViewModels;
using Xunit;

namespace VDF.Core.Tests;

public sealed class DarkPhotoOptionTests {
    static FileEntry Photo(string name) => new() { _Path = name, Folder = "photos", IsImage = true, FileSize = 1000, mediaInfo = new() };
    static byte[] DarkSample() {
        var gray = new byte[1024];
        for (int i = 0; i < 20; i++) gray[i] = 100;
        return gray;
    }

    [Fact]
    public void DefaultSkipsDarkPhotosAndOptionRetainsTheirSampleAndLabel() {
        var photo = Photo("dark.jpg");
        Assert.False(new Settings().IncludeVeryDarkPhotos);
        Assert.False(ScanEngine.CacheImageGray(photo, DarkSample(), false));
        Assert.True(photo.IsTooDark);
        Assert.Empty(photo.grayBytes);
        var sample = DarkSample();
        Assert.True(ScanEngine.CacheImageGray(photo, sample, true));
        Assert.Same(sample, photo.grayBytes[0]);
        Assert.True(photo.IsTooDark); // Retained so disabling the option restores the exclusion.
        Assert.True(new DuplicateItem(photo, 0, Guid.NewGuid(), DuplicateFlags.None).IsDarkPhoto);
    }

    [Fact]
    public void PreviouslyFlaggedPhotosAreRetriedButDarkVideosStayExcluded() {
        var engine = new ScanEngine();
        engine.Settings.IncludeNonExistingFiles = true;
        engine.Settings.IncludeList.Add("photos");
        var photo = Photo("dark.jpg"); photo.Flags.Set(EntryFlags.TooDark);
        Assert.True(engine.InvalidEntry(photo, out _, out _));
        engine.Settings.IncludeVeryDarkPhotos = true;
        Assert.False(engine.InvalidEntry(photo, out _, out _));
        photo.IsImage = false;
        Assert.True(engine.InvalidEntry(photo, out _, out _));
    }

    [Fact]
    public void AcceptedDarkPicturesActuallyReachWholePictureGrayscaleComparison() {
        var engine = new ScanEngine(); engine.Settings.IncludeVeryDarkPhotos = true;
        var a = Photo("a.jpg"); var b = Photo("b.jpg");
        Assert.True(ScanEngine.CacheImageGray(a, DarkSample(), true));
        Assert.True(ScanEngine.CacheImageGray(b, DarkSample(), true));
        Assert.True(engine.TryBuildCompareSnapshot(a, false));
        Assert.True(engine.TryBuildCompareSnapshot(b, false));
        Assert.True(engine.CheckIfDuplicate(a, null, null, b, out float difference));
        Assert.Equal(0, difference);
    }

    [Fact]
    public void SuccessfulBrightResampleClearsTheOldDarkFlag() {
        var photo = Photo("bright.jpg"); photo.Flags.Set(EntryFlags.TooDark);
        Assert.True(ScanEngine.CacheImageGray(photo, Enumerable.Repeat((byte)150, 1024).ToArray(), false));
        Assert.False(photo.IsTooDark);
    }
}
