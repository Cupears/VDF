using System.Linq;

namespace VDF.GUI.Utils;

internal static class DesktopMobilePaths {
    internal static string? FindContentRoot() {
        string? configured = Environment.GetEnvironmentVariable("VDF_MOBILE_CONTENT_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
            return Directory.Exists(Path.Combine(configured, "wwwroot")) ? Path.GetFullPath(configured) : null;
        string baseDir = AppContext.BaseDirectory;
        return new[] {
            Path.Combine(baseDir, "mobile"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "mobile")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "mobile"))
        }.FirstOrDefault(path => Directory.Exists(Path.Combine(path, "wwwroot")));
    }
}
