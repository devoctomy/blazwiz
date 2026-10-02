using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.UnitTests.Sync.Storage;

public class SyncStorageModelTests
{
    [Fact]
    public void GivenEachConstructor_WhenCreatingMigrationException_ThenMessageAndCauseKept()
    {
        var cause = new InvalidOperationException("cause");
        Assert.NotNull(new SyncStorageMigrationException().Message);
        Assert.Equal("failed", new SyncStorageMigrationException("failed").Message);
        var ex = new SyncStorageMigrationException("failed", cause);
        Assert.Equal("failed", ex.Message);
        Assert.Same(cause, ex.InnerException);
    }

    [Fact]
    public void GivenNullFactory_WhenCreatingStorageOptions_ThenThrowsArgumentNullException()
    {
        Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => new SyncStorageOptions(SyncStorageKind.Opfs, null!)).ParamName);
    }
}
