using VDF.Core.Utils;
using Xunit;

namespace VDF.Core.Tests;

public sealed class DatabaseSessionLeaseTests {
    [Fact]
    public void SecondWriterIsBlockedAndCleanExitReleasesLibrary() {
        string folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vdf-lease-" + Guid.NewGuid())).FullName;
        try {
            using (var owner = DatabaseSessionLease.Acquire(folder)) {
                Assert.Throws<IOException>(() => DatabaseSessionLease.Acquire(folder));
            }
            using var nextOwner = DatabaseSessionLease.Acquire(folder);
        } finally { Directory.Delete(folder, true); }
    }
}
