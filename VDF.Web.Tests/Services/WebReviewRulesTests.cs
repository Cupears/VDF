using VDF.Core;
using VDF.Core.ViewModels;
using VDF.Web.Services;

namespace VDF.Web.Tests.Services;

public class WebReviewRulesTests {
    [Fact]
    public void NearEqualDurations_LetResolutionDecideLikeDesktop() {
        var small = new DuplicateItem { Duration = TimeSpan.FromSeconds(60.2), FrameSizeInt = 640 * 480 };
        var large = new DuplicateItem { Duration = TimeSpan.FromSeconds(60), FrameSizeInt = 1920 * 1080 };
        Assert.Same(large, WebReviewRules.PickKeeper([small, large], new WebSettingsService()));
    }

    [Fact]
    public void AudioRule_UsesAudioBitrateRatherThanSampleRate() {
        var low = new DuplicateItem { AudioBitRateKbs = 64, AudioSampleRate = 96000 };
        var high = new DuplicateItem { AudioBitRateKbs = 320, AudioSampleRate = 44100 };
        var settings = new WebSettingsService {
            QualityCriteriaOrder = ["Audio Bitrate"],
            QualityCriteriaDisabled = WebReviewRules.DefaultOrder.Where(n => n != "Audio Bitrate").ToList()
        };
        Assert.Same(high, WebReviewRules.PickKeeper([low, high], settings));
    }

    [Fact]
    public void LargerSizeAndDisabledRules_ChangeTheKeeper() {
        var small = new DuplicateItem { SizeLong = 10 };
        var large = new DuplicateItem { SizeLong = 50 };
        var settings = new WebSettingsService {
            QualityCriteriaOrder = ["SizeLarger"], QualityCriteriaDisabled = ["Size"]
        };
        Assert.Same(large, WebReviewRules.PickKeeper([small, large], settings));
        settings.QualityCriteriaDisabled = WebReviewRules.DefaultOrder.ToList();
        Assert.Same(small, WebReviewRules.PickKeeper([small, large], settings));
    }
}
