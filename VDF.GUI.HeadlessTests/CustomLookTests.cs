using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VDF.GUI.Data;
using VDF.GUI.Utils;

namespace VDF.GUI.HeadlessTests;

public sealed class CustomLookTests {
    [Fact]
    public Task ColorsApplyLiveAndHighContrastCanRestoreStandardColors() => HeadlessUi.Run(() => {
        var settings = SettingsFile.Instance;
        var old = (settings.CustomAppearanceEnabled, settings.CustomTextColor, settings.CustomBackgroundColor, settings.CustomBackgroundImage);
        var text = new TextBlock { Text = "Custom text preview" };
        var window = HeadlessUi.Show(text);
        try {
            settings.CustomAppearanceEnabled = true; settings.CustomTextColor = "#ABCDEF";
            CustomLook.Apply(window); HeadlessUi.Pump();
            Assert.Equal(Color.Parse("#ABCDEF"), ((ISolidColorBrush)text.Foreground!).Color);
            settings.CustomTextColor = "invalid-color";
            Assert.Contains("invalid", settings.CustomLookStatus);
            settings.CustomTextColor = "#ABCDEF";
            Appearance.SetSystemHighContrast(true); CustomLook.Apply(window); HeadlessUi.Pump();
            Assert.NotEqual(Color.Parse("#ABCDEF"), ((ISolidColorBrush)text.Foreground!).Color);
        } finally {
            (settings.CustomAppearanceEnabled, settings.CustomTextColor, settings.CustomBackgroundColor, settings.CustomBackgroundImage) = old;
            Appearance.SetSystemHighContrast(null); CustomLook.Detach(window); window.Close();
        }
    });
}
