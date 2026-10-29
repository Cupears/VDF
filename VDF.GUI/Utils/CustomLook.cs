using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using VDF.GUI.Data;

namespace VDF.GUI.Utils;

internal static class CustomLook {
    sealed class State {
        internal Styles? Styles;
        internal string Text = "", ImagePath = "";
        internal DateTime ImageStamp;
        internal Bitmap? Bitmap;
    }
    static readonly Dictionary<Window, State> states = new();
    internal static void Detach(Window window) {
        if (states.Remove(window, out var state)) state.Bitmap?.Dispose();
    }
    internal static void Apply(Window window) {
        if (!states.TryGetValue(window, out var state)) states[window] = state = new();
        var settings = SettingsFile.Instance;
        bool enabled = settings.CustomAppearanceEnabled && !Appearance.HighContrastNow;
        string text = enabled && Color.TryParse(settings.CustomTextColor, out _) ? settings.CustomTextColor : "";
        if (text != state.Text) {
            if (state.Styles != null) window.Styles.Remove(state.Styles);
            state.Styles = null; state.Text = text;
            if (text.Length > 0) {
                var brush = new SolidColorBrush(Color.Parse(text));
                state.Styles = new Styles {
                    new Style(s => s.OfType<TextBlock>()) { Setters = { new Setter(TextBlock.ForegroundProperty, brush) } },
                    new Style(s => s.OfType<TextBox>()) { Setters = { new Setter(TextBox.ForegroundProperty, brush) } },
                    new Style(s => s.OfType<ComboBox>()) { Setters = { new Setter(ComboBox.ForegroundProperty, brush) } }
                };
                window.Styles.Add(state.Styles);
            }
        }
        // Dialogs (and test windows) have no custom look layers. Searching the logical tree
        // instead of the name scope keeps this a no-op there rather than throwing.
        static T? Find<T>(Window window, string name) where T : Control =>
            window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name);
        if (Find<Border>(window, "CustomBackgroundLayer") is { } colorLayer)
            colorLayer.Background = enabled && Color.TryParse(settings.CustomBackgroundColor, out var color) ? new SolidColorBrush(color) : null;
        if (Find<Image>(window, "CustomBackgroundImageLayer") is not { } image) return;
        string path = enabled && File.Exists(settings.CustomBackgroundImage) ? settings.CustomBackgroundImage : "";
        DateTime stamp = path.Length > 0 ? File.GetLastWriteTimeUtc(path) : default;
        if (state.ImagePath != path || state.ImageStamp != stamp) {
            image.Source = null; state.Bitmap?.Dispose(); state.Bitmap = null;
            state.ImagePath = path; state.ImageStamp = stamp;
            if (path.Length > 0) {
                try {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    state.Bitmap = Bitmap.DecodeToWidth(stream, 2048);
                } catch (Exception ex) { VDF.Core.Utils.Logger.Instance.Warn("Background image could not load: " + ex.Message); }
            }
        }
        image.Source = state.Bitmap;
        image.Opacity = settings.CustomBackgroundOpacityPercent / 100d;
    }
}
