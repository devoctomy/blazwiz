using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Encryption;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Encryption;

public class E2eSyncGateTests
{
    private readonly Mock<IE2eKeyManager> mockKeys = new();
    private readonly SyncOptions options = new();

    public E2eSyncGateTests()
    {
        options.Profiles.Add(new SyncProfileOptions { Name = "notes", IsEncrypted = true });
        options.Profiles.Add(new SyncProfileOptions { Name = "photos" });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("keyManager", Assert.Throws<ArgumentNullException>(() => new E2eSyncGate(null!, options)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new E2eSyncGate(mockKeys.Object, null!)).ParamName);
    }

    [Theory]
    [InlineData(E2eStatus.Unsupported, true)]
    [InlineData(E2eStatus.Unavailable, true)]
    [InlineData(E2eStatus.Disabled, true)]
    [InlineData(E2eStatus.Unlocked, true)]
    [InlineData(E2eStatus.Unknown, false)]
    [InlineData(E2eStatus.Blocked, false)]
    [InlineData(E2eStatus.NeedsSetup, false)]
    [InlineData(E2eStatus.DisabledElsewhere, false)]
    public void GivenStatus_WhenCanSyncEncryptedProfile_ThenOnlyWhenNothingPlainCanReachEncryptedFiles(E2eStatus status, bool expected)
    {
        // Arrange
        mockKeys.SetupGet(x => x.Status).Returns(status);
        var sut = new E2eSyncGate(mockKeys.Object, options);

        // Act & Assert
        Assert.Equal(expected, sut.CanSync("notes"));
        Assert.True(sut.CanSync("photos"));
    }
}
