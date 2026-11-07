using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class E2eUnlockPanelViewModelTests
{
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<IE2eProfilePresenter> mockPresenter = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<IPasskeyKeysService> mockPasskeys = new();
    private readonly Mock<IToastService> mockToasts = new();
    private readonly List<string> calls = [];
    private readonly TestText testText = new();
    private readonly E2eProfileRow notes = new() { Name = "notes", Id = Guid.NewGuid().ToString("D"), DisplayName = "Notes", State = E2eProfileState.Locked };

    public E2eUnlockPanelViewModelTests()
    {
        mockPresenter.Setup(x => x.GetProfileAsync("notes")).ReturnsAsync(() => notes);
        mockPasskeys.Setup(x => x.EnsureKeyAsync("e2e-kek")).ReturnsAsync(true);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(null!, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, null!, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object)).ParamName);
        Assert.Equal("presenter", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, null!, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, null!, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, null!, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, null!, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object)).ParamName);
        Assert.Equal("scheduler", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, null!, mockPasskeys.Object, mockToasts.Object)).ParamName);
        Assert.Equal("passkeys", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, null!, mockToasts.Object)).ParamName);
        Assert.Equal("toasts", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenALockedProfile_WhenShown_ThenItsPassphraseIsAskedFor()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.SetProfileAsync("notes");

        // Assert
        Assert.True(sut.IsLocked);
        Assert.False(sut.IsUnlocked);
        Assert.Equal("Notes is locked. Enter its passphrase to use it.", sut.Message);
    }

    [Fact]
    public async Task GivenAProfileWithoutAPassphrase_WhenShown_ThenItPointsToSettings()
    {
        // Arrange
        notes.State = E2eProfileState.NotSetUp;
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        sut.OpenSettingsCommand.Execute(null);

        // Assert
        Assert.True(sut.NeedsPassphrase);
        Assert.Equal("Notes needs a passphrase before it can be used. Set one in Settings.", sut.Message);
        mockNavigation.Verify(x => x.NavigateTo("/settings"), Times.Once);
    }

    [Fact]
    public async Task GivenProfilesNotLoaded_WhenShown_ThenChecking()
    {
        // Arrange
        mockPresenter.Setup(x => x.GetProfileAsync("notes")).ReturnsAsync((E2eProfileRow?)null);
        using var sut = CreateSut();

        // Act
        await sut.SetProfileAsync("notes");

        // Assert
        Assert.False(sut.IsUnlocked);
        Assert.False(sut.IsLocked);
        Assert.Equal("Checking…", sut.Message);
    }

    [Fact]
    public async Task GivenThePassphrase_WhenUnlocked_ThenTheContentIsShown()
    {
        // Arrange
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(notes.Id), "correct horse battery", It.IsAny<CancellationToken>()))
            .Callback(() => notes.State = E2eProfileState.Unlocked)
            .Returns(Task.CompletedTask);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "correct horse battery";

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsUnlocked);
        Assert.Empty(sut.Passphrase);
        Assert.Null(sut.Error);
    }

    [Fact]
    public async Task GivenAWrongPassphrase_WhenUnlocked_ThenAnErrorAndStillLocked()
    {
        // Arrange
        mockManager.Setup(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), "wrong", It.IsAny<CancellationToken>())).ThrowsAsync(new E2eWrongPassphraseException());
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "wrong";

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsLocked);
        Assert.Equal("That passphrase is not correct. Try again.", sut.Error);
    }

    [Fact]
    public async Task GivenNoPassphrase_WhenUnlocked_ThenAskedForIt()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Enter your passphrase.", sut.Error);
        mockManager.Verify(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenThePasskey_WhenUnlockedWithIt_ThenTheProfileServiceUnlocksIt()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.UnlockWithDeviceCommand.ExecuteAsync(null);

        // Assert
        mockProfiles.Verify(x => x.UnlockWithDeviceAsync(new E2eKeyScope(notes.Id)), Times.Once);
    }

    [Fact]
    public async Task GivenUnlocked_WhenItLocksItself_ThenThePanelIsShownAgain()
    {
        // Arrange
        notes.State = E2eProfileState.Unlocked;
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        notes.State = E2eProfileState.Locked;
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(notes.Id));

        // Assert
        Assert.True(sut.IsLocked);
    }

    [Fact]
    public async Task GivenALockedProfile_WhenUnlockedWithThePassphrase_ThenItIsSyncedFirst()
    {
        // Arrange
        mockScheduler.Setup(x => x.SyncProfileAsync("notes")).Callback(() => calls.Add("sync")).Returns(Task.CompletedTask);
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(notes.Id), "pass", It.IsAny<CancellationToken>())).Callback(() => calls.Add("unlock")).Returns(Task.CompletedTask);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["sync", "unlock"], calls);
    }

    [Fact]
    public async Task GivenALockedProfile_WhenUnlockedWithThePasskey_ThenItIsSyncedFirst()
    {
        // Arrange
        mockScheduler.Setup(x => x.SyncProfileAsync("notes")).Callback(() => calls.Add("sync")).Returns(Task.CompletedTask);
        mockProfiles.Setup(x => x.UnlockWithDeviceAsync(new E2eKeyScope(notes.Id))).Callback(() => calls.Add("unlock")).Returns(Task.CompletedTask);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.UnlockWithDeviceCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["sync", "unlock"], calls);
    }

    [Fact]
    public async Task GivenSyncingFails_WhenUnlocked_ThenItIsStillUnlocked()
    {
        // Arrange
        mockScheduler.Setup(x => x.SyncProfileAsync("notes")).ThrowsAsync(new InvalidOperationException("sync failed"));
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(notes.Id), "pass", It.IsAny<CancellationToken>()))
            .Callback(() => notes.State = E2eProfileState.Unlocked)
            .Returns(Task.CompletedTask);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsUnlocked);
        Assert.Null(sut.Error);
    }

    [Fact]
    public async Task GivenThePasskeyIsCancelled_WhenUnlockedWithBiometrics_ThenStillLocked_AndThePassphraseIsAsked()
    {
        // Arrange
        mockPasskeys.Setup(x => x.EnsureKeyAsync("e2e-kek")).ReturnsAsync(false);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.UnlockWithDeviceCommand.ExecuteAsync(null);

        // Assert
        mockProfiles.Verify(x => x.UnlockWithDeviceAsync(It.IsAny<E2eKeyScope>()), Times.Never);
        Assert.NotNull(sut.Error);
        Assert.True(sut.IsLocked);
    }

    [Fact]
    public async Task GivenRememberBiometricsTicked_WhenUnlockedWithThePassphrase_ThenBiometricsAreTurnedOn()
    {
        // Arrange
        notes.CanSetUpDeviceUnlock = true;
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(notes.Id), "pass", It.IsAny<CancellationToken>())).Callback(() => calls.Add("unlock")).Returns(Task.CompletedTask);
        mockPasskeys.Setup(x => x.EnsureKeyAsync("e2e-kek")).Callback(() => calls.Add("passkey")).ReturnsAsync(true);
        mockProfiles.Setup(x => x.EnableDeviceUnlockAsync(new E2eKeyScope(notes.Id))).Callback(() => calls.Add("biometrics on")).Returns(Task.CompletedTask);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";
        sut.RememberBiometrics = true;

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["unlock", "passkey", "biometrics on"], calls);
        mockToasts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenRememberBiometricsTicked_AndThePasskeyCancelled_WhenUnlocked_ThenUnlocked_AndItSaysBiometricsAreNotOn()
    {
        // Arrange
        notes.CanSetUpDeviceUnlock = true;
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(notes.Id), "pass", It.IsAny<CancellationToken>()))
            .Callback(() => notes.State = E2eProfileState.Unlocked)
            .Returns(Task.CompletedTask);
        mockPasskeys.Setup(x => x.EnsureKeyAsync("e2e-kek")).ReturnsAsync(false);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";
        sut.RememberBiometrics = true;

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsUnlocked);
        mockProfiles.Verify(x => x.EnableDeviceUnlockAsync(It.IsAny<E2eKeyScope>()), Times.Never);
        mockToasts.Verify(x => x.Show("Unlocked, but unlocking with biometrics could not be turned on. Try again next time.", ToastTone.Warning, null), Times.Once);
    }

    [Fact]
    public async Task GivenRememberBiometricsNotOffered_WhenUnlockedWithThePassphrase_ThenThePasskeyIsNotAskedFor()
    {
        // Arrange: e.g. no passkey on this device.
        notes.CanSetUpDeviceUnlock = false;
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";
        sut.RememberBiometrics = true;

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        mockPasskeys.Verify(x => x.EnsureKeyAsync(It.IsAny<string>()), Times.Never);
        mockProfiles.Verify(x => x.EnableDeviceUnlockAsync(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    private E2eUnlockPanelViewModel CreateSut() => new(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object);
}
