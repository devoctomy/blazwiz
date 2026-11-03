using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Storage;

public class SyncStorageModelTests
{
    [Fact]
    public void GivenEachConstructor_WhenCreatingMigrationException_ThenMessageAndCauseKept()
    {
        // Act & Assert
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
        // Act & Assert
        Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => new SyncStorageOptions(SyncStorageKind.Opfs, null!)).ParamName);
    }
}
