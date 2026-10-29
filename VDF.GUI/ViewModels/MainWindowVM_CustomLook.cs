using System.Reactive;
using Avalonia.Platform.Storage;
using ReactiveUI;
using VDF.GUI.Data;

namespace VDF.GUI.ViewModels;

public partial class MainWindowVM {
    public ReactiveCommand<Unit, Unit> BrowseCustomBackgroundCommand => ReactiveCommand.CreateFromTask(async () => {
        string? path = await Utils.PickerDialogUtils.OpenFilePicker(new FilePickerOpenOptions {
            Title = "Choose a background image", AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Pictures") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" } } }
        });
        if (!string.IsNullOrEmpty(path)) SettingsFile.Instance.CustomBackgroundImage = path;
    });
    public ReactiveCommand<Unit, Unit> ResetCustomLookCommand => ReactiveCommand.Create(() => {
        var settings = SettingsFile.Instance;
        settings.CustomAppearanceEnabled = false; settings.CustomTextColor = "";
        settings.CustomBackgroundColor = ""; settings.CustomBackgroundImage = "";
        settings.CustomBackgroundOpacityPercent = 20;
    });
}
