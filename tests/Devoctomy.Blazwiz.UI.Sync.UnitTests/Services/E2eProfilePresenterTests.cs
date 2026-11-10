using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class E2eProfilePresenterTests
{
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<ISyncProfileDisplayNames> mockDisplayNames = new();
    private readonly Mock<IPasskeyKeysService> mockPasskeys = new();
    private readonly List<SyncProtectedProfile> profiles = [];

    public E2eProfilePresenterTests()
    {
        mockProfiles.SetupGet(x => x.Profiles).Returns(() => profiles);
        mockDisplayNames.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns((string name) => name == "notes" ? "Notes" : name);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2eProfilePresenter(null!, mockManager.Object, mockDisplayNames.Object, mockPasskeys.Object)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eProfilePresenter(mockProfiles.Object, null!, mockDisplayNames.Object, mockPasskeys.Object)).ParamName);
        Assert.Equal("displayNames", Assert.Throws<ArgumentNullException>(() => new E2eProfilePresenter(mockProfiles.Object, mockManager.Object, null!, mockPasskeys.Object)).ParamName);
        Assert.Equal("passkeys", Assert.Throws<ArgumentNullException>(() => new E2eProfilePresenter(mockProfiles.Object, mockManager.Object, mockDisplayNames.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenProfilesInEachState_WhenDescribed_ThenEachHasItsNameStateAndOrigin()
    {
        // Arrange
        var notSetUp = Add("notes", isSetUp: false);
        var locked = Add(Guid.NewGuid().ToString("D"), isSetUp: true, isCreatedByUser: true);
        var unlocked = Add(Guid.NewGuid().ToString("D"), isSetUp: true, isCreatedByUser: true);
        mockManager.Setup(x => x.IsUnlocked(unlocked.Scope)).Returns(true);

        // Act
        var rows = await CreateSut().GetProfilesAsync();

        // Assert
        Assert.Collection(
            rows,
            r =>
            {
                Assert.Equal("Notes", r.DisplayName);
                Assert.Equal(notSetUp.Scope.Id, r.Id);
                Assert.Equal(E2eProfileState.NotSetUp, r.State);
                Assert.False(r.IsCreatedByUser);
            },
            r =>
            {
                Assert.Equal(locked.Name, r.DisplayName);
                Assert.Equal(E2eProfileState.Locked, r.State);
                Assert.True(r.IsCreatedByUser);
            },
            r => Assert.Equal(E2eProfileState.Unlocked, r.State));
    }

    [Fact]
    public async Task GivenAnUnlockedProfileWithARotationInProgress_WhenDescribed_ThenItsPassphraseChangeMustBeFinished()
    {
        // Arrange
        var profile = Add(Guid.NewGuid().ToString("D"), isSetUp: true, isCreatedByUser: true);
        mockManager.Setup(x => x.IsUnlocked(profile.Scope)).Returns(true);
        mockManager.Setup(x => x.IsRotationInProgress(profile.Scope)).Returns(true);

        // Act
        var row = await CreateSut().GetProfileAsync(profile.Name);

        // Assert
        Assert.Equal(E2eProfileState.RotationInProgress, row!.State);
    }

    [Fact]
    public async Task GivenAPasskeyAndBiometricsOnHere_WhenDescribed_ThenItCanBeUnlockedWithThem()
    {
        // Arrange: whether or not the passkey's key is in memory now (it is asked for when needed).
        mockPasskeys.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
        var allowed = Add("notes", isSetUp: true, allowDeviceUnlock: true);
        var notAllowed = Add(Guid.NewGuid().ToString("D"), isSetUp: true, isCreatedByUser: true);
        mockManager.Setup(x => x.HasDeviceUnlockAsync(It.IsAny<E2eKeyScope>())).ReturnsAsync(true);

        // Act
        var rows = await CreateSut().GetProfilesAsync();

        // Assert
        Assert.True(rows[0].AllowDeviceUnlock);
        Assert.True(rows[0].CanUnlockWithDevice);
        Assert.False(rows[0].CanSetUpDeviceUnlock);
        Assert.False(rows[1].CanUnlockWithDevice);
        Assert.False(rows[1].CanSetUpDeviceUnlock);
        mockManager.Verify(x => x.HasDeviceUnlockAsync(notAllowed.Scope), Times.Never);
        mockManager.Verify(x => x.HasDeviceUnlockAsync(allowed.Scope), Times.Once);
    }

    [Fact]
    public async Task GivenAPasskeyAndBiometricsNotOnHereYet_WhenDescribed_ThenTheyCanBeTurnedOn()
    {
        // Arrange
        mockPasskeys.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
        Add("notes", isSetUp: true, allowDeviceUnlock: true);

        // Act
        var row = await CreateSut().GetProfileAsync("notes");

        // Assert
        Assert.False(row!.CanUnlockWithDevice);
        Assert.True(row.CanSetUpDeviceUnlock);
    }

    [Fact]
    public async Task GivenNoPasskeyHere_WhenDescribed_ThenBiometricsAreNotOffered()
    {
        // Arrange: e.g. not remembered ("Remember me") on this device.
        mockPasskeys.Setup(x => x.IsAvailableAsync()).ReturnsAsync(false);
        Add("notes", isSetUp: true, allowDeviceUnlock: true);
        mockManager.Setup(x => x.HasDeviceUnlockAsync(It.IsAny<E2eKeyScope>())).ReturnsAsync(true);

        // Act
        var row = await CreateSut().GetProfileAsync("notes");

        // Assert
        Assert.False(row!.CanUnlockWithDevice);
        Assert.False(row.CanSetUpDeviceUnlock);
    }

    [Fact]
    public async Task GivenThisDevicesKeysCannotBeRead_WhenDescribed_ThenThePasskeyCannotUnlockIt()
    {
        // Arrange
        mockPasskeys.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
        Add("notes", isSetUp: true, allowDeviceUnlock: true);
        mockManager.Setup(x => x.HasDeviceUnlockAsync(It.IsAny<E2eKeyScope>())).ThrowsAsync(new InvalidOperationException("no storage"));

        // Act
        var row = await CreateSut().GetProfileAsync("notes");

        // Assert
        Assert.False(row!.CanUnlockWithDevice);
    }

    [Fact]
    public async Task GivenAnUnknownProfile_WhenDescribed_ThenNull()
    {
        // Act & Assert
        Assert.Null(await CreateSut().GetProfileAsync("gone"));
    }

    private SyncProtectedProfile Add(string name, bool isSetUp, bool isCreatedByUser = false, bool allowDeviceUnlock = false)
    {
        var profile = new SyncProtectedProfile
        {
            Name = name,
            Scope = new E2eKeyScope(Guid.NewGuid().ToString("D")),
            IsSetUp = isSetUp,
            IsCreatedByUser = isCreatedByUser,
            Settings = new E2eProfileSettings { AllowDeviceUnlock = allowDeviceUnlock },
        };
        profiles.Add(profile);
        return profile;
    }

    private E2eProfilePresenter CreateSut() => new(mockProfiles.Object, mockManager.Object, mockDisplayNames.Object, mockPasskeys.Object);
}
