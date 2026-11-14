using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class E2eDeviceUnlockServiceTests
{
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<IPasskeyKeysService> mockPasskeys = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly List<SyncProtectedProfile> profiles = [];
    private readonly E2eKeyScope scope = new(Guid.NewGuid().ToString("D"));

    public E2eDeviceUnlockServiceTests()
    {
        mockProfiles.SetupGet(x => x.Profiles).Returns(() => profiles);
        profiles.Add(new SyncProtectedProfile { Name = scope.Id, Scope = scope, IsCreatedByUser = true, IsSetUp = true });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2eDeviceUnlockService(null!, mockManager.Object, mockPasskeys.Object, mockScheduler.Object)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eDeviceUnlockService(mockProfiles.Object, null!, mockPasskeys.Object, mockScheduler.Object)).ParamName);
        Assert.Equal("passkeys", Assert.Throws<ArgumentNullException>(() => new E2eDeviceUnlockService(mockProfiles.Object, mockManager.Object, null!, mockScheduler.Object)).ParamName);
        Assert.Equal("scheduler", Assert.Throws<ArgumentNullException>(() => new E2eDeviceUnlockService(mockProfiles.Object, mockManager.Object, mockPasskeys.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenNullScope_WhenCalled_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CanUnlockAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UnlockAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ForgetAsync(null!));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task GivenDeviceUnlockAndPasskey_WhenCanUnlock_ThenTrueOnlyWhenBoth(bool hasDeviceUnlock, bool hasPasskey, bool expected)
    {
        // Arrange
        mockManager.Setup(x => x.HasDeviceUnlockAsync(scope)).ReturnsAsync(hasDeviceUnlock);
        mockPasskeys.Setup(x => x.IsAvailableAsync()).ReturnsAsync(hasPasskey);

        // Act
        var canUnlock = await CreateSut().CanUnlockAsync(scope);

        // Assert
        Assert.Equal(expected, canUnlock);
    }

    [Fact]
    public async Task GivenTheDeviceKeysCannotBeRead_WhenCanUnlock_ThenFalse()
    {
        // Arrange
        mockManager.Setup(x => x.HasDeviceUnlockAsync(scope)).ThrowsAsync(new InvalidOperationException("corrupt"));

        // Act
        var canUnlock = await CreateSut().CanUnlockAsync(scope);

        // Assert
        Assert.False(canUnlock);
    }

    [Fact]
    public async Task GivenALockedProfile_WhenUnlocked_ThenItIsSyncedThenThePasskeyKeyUnlocksIt()
    {
        // Arrange
        var calls = new List<string>();
        mockScheduler.Setup(x => x.SyncProfileAsync(scope.Id)).Callback(() => calls.Add("sync")).Returns(Task.CompletedTask);
        mockPasskeys.Setup(x => x.EnsureKeyAsync(E2eKeyNames.KeyEncryptionKey)).Callback(() => calls.Add("passkey")).ReturnsAsync(true);
        mockProfiles.Setup(x => x.UnlockWithDeviceAsync(scope)).Callback(() => calls.Add("unlock")).Returns(Task.CompletedTask);

        // Act
        var unlocked = await CreateSut().UnlockAsync(scope);

        // Assert
        Assert.True(unlocked);
        Assert.Equal(["sync", "passkey", "unlock"], calls);
    }

    [Fact]
    public async Task GivenAnUnlockedProfile_WhenUnlocked_ThenNothingIsAskedFor()
    {
        // Arrange
        mockManager.Setup(x => x.IsUnlocked(scope)).Returns(true);

        // Act
        var unlocked = await CreateSut().UnlockAsync(scope);

        // Assert
        Assert.True(unlocked);
        mockPasskeys.Verify(x => x.EnsureKeyAsync(It.IsAny<string>()), Times.Never);
        mockScheduler.Verify(x => x.SyncProfileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenThePasskeyIsCancelled_WhenUnlocked_ThenFalseAndTheProfileStaysLocked()
    {
        // Arrange
        mockPasskeys.Setup(x => x.EnsureKeyAsync(E2eKeyNames.KeyEncryptionKey)).ReturnsAsync(false);

        // Act
        var unlocked = await CreateSut().UnlockAsync(scope);

        // Assert
        Assert.False(unlocked);
        mockProfiles.Verify(x => x.UnlockWithDeviceAsync(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public async Task GivenAnotherPasskey_WhenUnlocked_ThenFalse()
    {
        // Arrange
        mockPasskeys.Setup(x => x.EnsureKeyAsync(E2eKeyNames.KeyEncryptionKey)).ReturnsAsync(true);
        mockProfiles.Setup(x => x.UnlockWithDeviceAsync(scope)).ThrowsAsync(new InvalidOperationException("cannot unwrap"));

        // Act
        var unlocked = await CreateSut().UnlockAsync(scope);

        // Assert
        Assert.False(unlocked);
    }

    [Fact]
    public async Task GivenSyncFails_WhenUnlocked_ThenItIsUnlockedWithTheFilesHere()
    {
        // Arrange
        mockScheduler.Setup(x => x.SyncProfileAsync(scope.Id)).ThrowsAsync(new InvalidOperationException("offline"));
        mockPasskeys.Setup(x => x.EnsureKeyAsync(E2eKeyNames.KeyEncryptionKey)).ReturnsAsync(true);

        // Act
        var unlocked = await CreateSut().UnlockAsync(scope);

        // Assert
        Assert.True(unlocked);
        mockProfiles.Verify(x => x.UnlockWithDeviceAsync(scope), Times.Once);
    }

    [Fact]
    public async Task GivenAnUnknownProfile_WhenUnlocked_ThenNothingIsSyncedButItIsStillUnlocked()
    {
        // Arrange
        var unknown = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        mockPasskeys.Setup(x => x.EnsureKeyAsync(E2eKeyNames.KeyEncryptionKey)).ReturnsAsync(true);

        // Act
        var unlocked = await CreateSut().UnlockAsync(unknown);

        // Assert
        Assert.True(unlocked);
        mockScheduler.Verify(x => x.SyncProfileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenAProfile_WhenForgotten_ThenDeviceUnlockIsTurnedOffHere()
    {
        // Act
        await CreateSut().ForgetAsync(scope);

        // Assert
        mockProfiles.Verify(x => x.DisableDeviceUnlockAsync(scope), Times.Once);
    }

    private E2eDeviceUnlockService CreateSut() => new(mockProfiles.Object, mockManager.Object, mockPasskeys.Object, mockScheduler.Object);
}
