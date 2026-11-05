using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
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
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2eProfilePresenter(null!, mockManager.Object, mockDisplayNames.Object)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eProfilePresenter(mockProfiles.Object, null!, mockDisplayNames.Object)).ParamName);
        Assert.Equal("displayNames", Assert.Throws<ArgumentNullException>(() => new E2eProfilePresenter(mockProfiles.Object, mockManager.Object, null!)).ParamName);
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
    public async Task GivenDeviceUnlockAllowedAndSetUpOnThisDevice_WhenDescribed_ThenThePasskeyCanUnlockIt()
    {
        // Arrange
        var allowed = Add("notes", isSetUp: true, allowDeviceUnlock: true);
        var notAllowed = Add(Guid.NewGuid().ToString("D"), isSetUp: true, isCreatedByUser: true);
        mockManager.Setup(x => x.CanUnlockWithDeviceAsync(It.IsAny<E2eKeyScope>())).ReturnsAsync(true);

        // Act
        var rows = await CreateSut().GetProfilesAsync();

        // Assert
        Assert.True(rows[0].AllowDeviceUnlock);
        Assert.True(rows[0].CanUnlockWithDevice);
        Assert.False(rows[1].CanUnlockWithDevice);
        mockManager.Verify(x => x.CanUnlockWithDeviceAsync(notAllowed.Scope), Times.Never);
        mockManager.Verify(x => x.CanUnlockWithDeviceAsync(allowed.Scope), Times.Once);
    }

    [Fact]
    public async Task GivenThisDevicesKeysCannotBeRead_WhenDescribed_ThenThePasskeyCannotUnlockIt()
    {
        // Arrange
        Add("notes", isSetUp: true, allowDeviceUnlock: true);
        mockManager.Setup(x => x.CanUnlockWithDeviceAsync(It.IsAny<E2eKeyScope>())).ThrowsAsync(new InvalidOperationException("no storage"));

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

    private E2eProfilePresenter CreateSut() => new(mockProfiles.Object, mockManager.Object, mockDisplayNames.Object);
}
