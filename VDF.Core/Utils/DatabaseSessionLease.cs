namespace VDF.Core.Utils;

/// <summary>Keeps separate app processes from writing the same library at once.</summary>
public sealed class DatabaseSessionLease : IDisposable {
    readonly FileStream stream;
    DatabaseSessionLease(FileStream stream) => this.stream = stream;
    public static DatabaseSessionLease Acquire(string? folder) {
        string resolved = CoreUtils.ResolveDatabaseFolder(folder);
        Directory.CreateDirectory(resolved);
        try {
            return new DatabaseSessionLease(new FileStream(Path.Combine(resolved, ".vdf-database.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        } catch (IOException ex) {
            throw new IOException("This database is already open in another VDF process. Use the running desktop and its mobile link.", ex);
        }
    }
    public void Dispose() => stream.Dispose();
}
