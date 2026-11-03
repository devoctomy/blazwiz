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
        Assert.Equal("failed", new SyncStorageMigrationException("failed").Code);
        var ex = new SyncStorageMigrationException("failed", cause, "a");
        Assert.Equal("failed", ex.Code);
        Assert.Equal(["a"], ex.Arguments);
        Assert.Same(cause, ex.InnerException);
    }

    [Fact]
    public void GivenNullFactory_WhenCreatingStorageOptions_ThenThrowsArgumentNullException()
    {
        Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => new SyncStorageOptions(SyncStorageKind.Opfs, null!)).ParamName);
    }
}
