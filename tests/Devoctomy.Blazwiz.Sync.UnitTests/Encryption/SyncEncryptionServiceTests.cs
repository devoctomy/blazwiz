using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class SyncEncryptionServiceTests
{
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly E2eKeyRings keyRings = new(new InMemoryDerivedKeyStore());
    private readonly SyncOptions options = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly E2eKeyScope notes = new(Guid.NewGuid().ToString("D"));
    private readonly E2eKeyScope photos = new(Guid.NewGuid().ToString("D"));
    private readonly HashSet<E2eKeyScope> setUp = [];
    private readonly HashSet<E2eKeyScope> unlocked = [];

    public SyncEncryptionServiceTests()
    {
        options.Profiles.Add(new SyncProfileOptions { Name = "notes", RemotePattern = "notes/**/*", KeyScope = notes });
        options.Profiles.Add(new SyncProfileOptions { Name = "photos", RemotePattern = "photos/**/*", KeyScope = photos });
        mockProfiles.Setup(x => x.IsSetUpAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>())).ReturnsAsync((E2eKeyScope s, CancellationToken _) => setUp.Contains(s));
        mockProfiles.Setup(x => x.SetUpAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, string, IProgress<E2eProgress>?, CancellationToken>((s, _, _, _) =>
            {
                setUp.Add(s);
                unlocked.Add(s);
            })
            .Returns(Task.CompletedTask);
        mockManager.Setup(x => x.IsUnlocked(It.IsAny<E2eKeyScope>())).Returns<E2eKeyScope>(s => unlocked.Contains(s));
        mockManager.Setup(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, string, CancellationToken>((s, _, _) => unlocked.Add(s))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(null!, mockManager.Object, keyRings, options, mockScheduler.Object)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockProfiles.Object, null!, keyRings, options, mockScheduler.Object)).ParamName);
        Assert.Equal("keyRings", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockProfiles.Object, mockManager.Object, null!, options, mockScheduler.Object)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockProfiles.Object, mockManager.Object, keyRings, null!, mockScheduler.Object)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockProfiles.Object, mockManager.Object, keyRings, options, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAProfileWithoutKeys_WhenInitialized_ThenDisabled()
    {
        // Arrange
        setUp.Add(notes);

        // Act & Assert
        Assert.Equal(E2eStatus.Disabled, await CreateSut().InitializeAsync());
    }

    [Fact]
    public async Task GivenAllSetUpButLocked_WhenInitialized_ThenNeedsSetup()
    {
        // Arrange
        setUp.Add(notes);
        setUp.Add(photos);

        // Act & Assert
        Assert.Equal(E2eStatus.NeedsSetup, await CreateSut().InitializeAsync());
    }

    [Fact]
    public async Task GivenKeysThatCannotBeReached_WhenInitialized_ThenUnsupported()
    {
        // Arrange
        mockProfiles.Setup(x => x.IsSetUpAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("no store"));

        // Act & Assert
        Assert.Equal(E2eStatus.Unsupported, await CreateSut().InitializeAsync());
    }

    [Fact]
    public async Task GivenOneProfileSetUp_WhenEnabled_ThenTheOtherIsSetUpTheFirstUnlockedAndBothSync()
    {
        // Arrange
        setUp.Add(notes);
        using var sut = CreateSut();
        await sut.InitializeAsync();

        // Act
        await sut.EnableAsync("correct horse battery");

        // Assert
        mockManager.Verify(x => x.UnlockAsync(notes, "correct horse battery", It.IsAny<CancellationToken>()), Times.Once);
        mockProfiles.Verify(x => x.SetUpAsync(photos, "correct horse battery", null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(E2eStatus.Unlocked, sut.Status);
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenLocked_WhenSetUpDevice_ThenEveryProfileIsUnlocked()
    {
        // Arrange
        setUp.Add(notes);
        setUp.Add(photos);
        using var sut = CreateSut();
        await sut.InitializeAsync();

        // Act
        await sut.SetUpDeviceAsync("pass");

        // Assert
        Assert.Equal([notes, photos], unlocked.OrderBy(s => s == photos));
        Assert.Equal(E2eStatus.Unlocked, sut.Status);
    }

    [Fact]
    public async Task GivenUnlocked_WhenAProfileLocks_ThenNeedsSetupIsRaised()
    {
        // Arrange
        setUp.Add(notes);
        setUp.Add(photos);
        unlocked.Add(notes);
        unlocked.Add(photos);
        using var sut = CreateSut();
        await sut.InitializeAsync();
        var raised = new List<E2eStatus>();
        sut.StatusChanged += (_, s) => raised.Add(s);

        // Act
        unlocked.Remove(notes);
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, notes);

        // Assert
        Assert.Equal([E2eStatus.NeedsSetup], raised);
    }

    [Fact]
    public async Task GivenANewPassphrase_WhenChanged_ThenEveryProfileIsChanged()
    {
        // Act
        await CreateSut().ChangePassphraseAsync("old", "new passphrase");

        // Assert
        mockProfiles.Verify(x => x.ChangePassphraseAsync(notes, "old", "new passphrase", It.IsAny<CancellationToken>()), Times.Once);
        mockProfiles.Verify(x => x.ChangePassphraseAsync(photos, "old", "new passphrase", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GivenAKeyOfAProfileHeld_WhenCanDecrypt_ThenTrueOnlyForIt()
    {
        // Arrange
        keyRings.Get(photos).Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_1");

        // Act & Assert
        Assert.True(CreateSut().CanDecrypt("k_1"));
        Assert.False(CreateSut().CanDecrypt("k_2"));
    }

    [Fact]
    public async Task GivenAWrongPassphrase_WhenSetUpDevice_ThenThrows()
    {
        // Arrange
        mockManager.Setup(x => x.UnlockAsync(notes, "wrong", It.IsAny<CancellationToken>())).ThrowsAsync(new E2eWrongPassphraseException());

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => CreateSut().SetUpDeviceAsync("wrong"));
    }

    private SyncEncryptionService CreateSut() => new(mockProfiles.Object, mockManager.Object, keyRings, options, mockScheduler.Object);
}
