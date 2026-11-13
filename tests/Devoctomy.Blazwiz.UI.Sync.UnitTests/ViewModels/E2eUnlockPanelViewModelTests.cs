using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
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
    private readonly Mock<ISyncKeyRotationService> mockRotation = new();
    private readonly Mock<IE2eRotationSignOut> mockSignOut = new();
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
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(null!, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, null!, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("presenter", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, null!, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, null!, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, null!, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, null!, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("scheduler", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, null!, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("passkeys", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, null!, mockToasts.Object, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("toasts", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, null!, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("rotation", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, null!, mockSignOut.Object)).ParamName);
        Assert.Equal("rotationSignOut", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAnInterruptedPassphraseChange_WhenShown_ThenTheProfileIsNotShownAndFinishingIsOffered()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        using var sut = CreateSut();

        // Act
        await sut.SetProfileAsync("notes");

        // Assert
        Assert.True(sut.IsRotationInProgress);
        Assert.False(sut.IsUnlocked);
        Assert.False(sut.IsLocked);
        Assert.Equal("A passphrase change of Notes was interrupted. It must be finished before Notes can be used, and it must not be interrupted again.", sut.Message);
    }

    [Fact]
    public async Task GivenAnInterruptedPassphraseChange_WhenFinished_ThenTheFilesAreEncryptedAgainWithProgressAndEveryDeviceSignedOut()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        var percents = new List<int>();
        mockRotation.Setup(x => x.FinishAsync(new E2eKeyScope(notes.Id), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, IProgress<E2eProgress>?, CancellationToken>((_, progress, _) =>
            {
                progress!.Report(new E2eProgress(E2eStep.ReencryptFiles, false, 0, 4));
                progress.Report(new E2eProgress(E2eStep.ReencryptFiles, false, 1, 4));
                progress.Report(new E2eProgress(E2eStep.ReencryptFiles, true, 4, 4));
            })
            .Returns(Task.CompletedTask);
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).ReturnsAsync(true);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.FilesPercent))
            {
                percents.Add(sut.FilesPercent);
            }
        };

        // Act
        await sut.FinishRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([0, 25, 100], percents);
        Assert.False(sut.IsFinishingRotation);
        Assert.False(sut.NeedsSignOut);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenOffline_WhenFinishingAPassphraseChange_ThenRefusedAndNobodyIsSignedOut()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        mockRotation.Setup(x => x.FinishAsync(It.IsAny<E2eKeyScope>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncKeyRotationException(SyncErrorCodes.RotationOffline));
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.FinishRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.StartsWith("Changing a passphrase needs a connection", sut.Error);
        Assert.True(sut.IsRotationInProgress);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheSignOutFails_WhenFinishingAPassphraseChange_ThenItIsOfferedUntilItSucceeds()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        mockSignOut.SetupSequence(x => x.SignOutEverywhereAsync()).ReturnsAsync(false).ReturnsAsync(true);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        await sut.FinishRotationCommand.ExecuteAsync(null);
        var offered = sut.NeedsSignOut;

        // Act
        await sut.RetrySignOutCommand.ExecuteAsync(null);

        // Assert
        Assert.True(offered);
        Assert.False(sut.NeedsSignOut);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenTheKeysWereRotatedElsewhere_WhenUnlockedWithThePasskey_ThenItSaysTheNewPassphraseIsNeeded()
    {
        // Arrange
        notes.CanUnlockWithDevice = true;
        mockProfiles.Setup(x => x.UnlockWithDeviceAsync(new E2eKeyScope(notes.Id)))
            .ThrowsAsync(new E2ePassphraseRequiredException(E2eErrorCodes.KeysChanged));
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.UnlockWithDeviceCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("The passphrase of this profile was changed on another device, so your passkey can no longer unlock it here. Enter the new passphrase.", sut.Error);
        Assert.True(sut.IsLocked);
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
    public async Task GivenRememberBiometricsTicked_WhenUnlockedWithThePassphrase_ThenThePasskeyIsAskedForFirst_AndBiometricsAreTurnedOn()
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
        Assert.Equal(["passkey", "unlock", "biometrics on"], calls);
        mockToasts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenRememberBiometricsTicked_AndTheWrongPassphrase_WhenUnlocked_ThenBiometricsAreNotTurnedOn_AndItSaysThePassphraseIsWrong()
    {
        // Arrange
        notes.CanSetUpDeviceUnlock = true;
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(notes.Id), "wrong", It.IsAny<CancellationToken>())).ThrowsAsync(new E2eWrongPassphraseException());
        mockPasskeys.Setup(x => x.EnsureKeyAsync("e2e-kek")).ReturnsAsync(true);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "wrong";
        sut.RememberBiometrics = true;

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        mockProfiles.Verify(x => x.EnableDeviceUnlockAsync(It.IsAny<E2eKeyScope>()), Times.Never);
        Assert.NotNull(sut.Error);
        Assert.True(sut.RememberBiometrics);
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

    [Fact]
    public void GivenNoProfileSet_WhenShown_ThenNoStateIsClaimed()
    {
        // Act
        using var sut = CreateSut();

        // Assert
        Assert.Null(sut.Profile);
        Assert.False(sut.IsUnlocked);
        Assert.False(sut.IsLocked);
        Assert.False(sut.NeedsPassphrase);
        Assert.False(sut.IsRotationInProgress);
        Assert.Equal("Checking…", sut.Message);
        Assert.Equal(0, sut.FilesPercent);
        Assert.Equal(testText.Sync["Sync.E2e.Rotation.Warning"].Value, sut.RotationWarning);
    }

    [Fact]
    public async Task GivenAnUnlockedProfile_WhenShown_ThenThePanelSaysNothing()
    {
        // Arrange
        notes.State = E2eProfileState.Unlocked;
        using var sut = CreateSut();

        // Act
        await sut.SetProfileAsync("notes");

        // Assert
        Assert.True(sut.IsUnlocked);
        Assert.Empty(sut.Message);
    }

    [Fact]
    public async Task GivenTheSameProfile_WhenSetAgain_ThenItIsNotReloadedAndThePassphraseIsKept()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";

        // Act
        await sut.SetProfileAsync("notes");

        // Assert
        Assert.Equal("pass", sut.Passphrase);
        mockPresenter.Verify(x => x.GetProfileAsync("notes"), Times.Once);
    }

    [Fact]
    public void GivenANullPassphrase_WhenSet_ThenItIsEmpty()
    {
        // Arrange
        using var sut = CreateSut();
        sut.Passphrase = "pass";

        // Act
        sut.Passphrase = null!;

        // Assert
        Assert.Empty(sut.Passphrase);
    }

    [Fact]
    public async Task GivenNoProfileKnownYet_WhenUnlocked_ThenNothingIsSyncedOrUnlocked()
    {
        // Arrange
        mockPresenter.Setup(x => x.GetProfileAsync("notes")).ReturnsAsync((E2eProfileRow?)null);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";
        sut.RememberBiometrics = true;

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);
        await sut.UnlockWithDeviceCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(sut.Error);
        Assert.Equal("pass", sut.Passphrase);
        mockScheduler.Verify(x => x.SyncProfileAsync(It.IsAny<string>()), Times.Never);
        mockPasskeys.Verify(x => x.EnsureKeyAsync(It.IsAny<string>()), Times.Never);
        mockManager.Verify(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mockProfiles.Verify(x => x.UnlockWithDeviceAsync(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public async Task GivenAnUnlockRunning_WhenUnlockedAgain_ThenTheSecondIsIgnored()
    {
        // Arrange
        var release = new TaskCompletionSource();
        mockManager.Setup(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(release.Task);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";
        var running = sut.UnlockAsync();

        // Act
        await sut.UnlockAsync();
        await sut.UnlockWithDeviceAsync();

        // Assert
        Assert.True(sut.IsWorking);
        release.SetResult();
        await running;
        Assert.False(sut.IsWorking);
        mockManager.Verify(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockProfiles.Verify(x => x.UnlockWithDeviceAsync(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public async Task GivenThePasskeyFails_WhenUnlockedWithThePassphraseRememberingBiometrics_ThenUnlockedAndItSaysBiometricsAreNotOn()
    {
        // Arrange
        notes.CanSetUpDeviceUnlock = true;
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(notes.Id), "pass", It.IsAny<CancellationToken>()))
            .Callback(() => notes.State = E2eProfileState.Unlocked)
            .Returns(Task.CompletedTask);
        mockPasskeys.Setup(x => x.EnsureKeyAsync("e2e-kek")).ThrowsAsync(new InvalidOperationException("NotAllowedError"));
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";
        sut.RememberBiometrics = true;

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsUnlocked);
        Assert.Null(sut.Error);
        mockProfiles.Verify(x => x.EnableDeviceUnlockAsync(It.IsAny<E2eKeyScope>()), Times.Never);
        mockToasts.Verify(x => x.Show("Unlocked, but unlocking with biometrics could not be turned on. Try again next time.", ToastTone.Warning, null), Times.Once);
    }

    [Fact]
    public async Task GivenTurningOnBiometricsFails_WhenUnlockedWithThePassphrase_ThenUnlockedAndItSaysBiometricsAreNotOn()
    {
        // Arrange
        notes.CanSetUpDeviceUnlock = true;
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(notes.Id), "pass", It.IsAny<CancellationToken>()))
            .Callback(() => notes.State = E2eProfileState.Unlocked)
            .Returns(Task.CompletedTask);
        mockProfiles.Setup(x => x.EnableDeviceUnlockAsync(new E2eKeyScope(notes.Id))).ThrowsAsync(new E2eLockedException());
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        sut.Passphrase = "pass";
        sut.RememberBiometrics = true;

        // Act
        await sut.UnlockCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsUnlocked);
        Assert.Null(sut.Error);
        Assert.True(sut.RememberBiometrics);
        mockToasts.Verify(x => x.Show("Unlocked, but unlocking with biometrics could not be turned on. Try again next time.", ToastTone.Warning, null), Times.Once);
    }

    [Fact]
    public async Task GivenThePasskeyThrows_WhenUnlockedWithIt_ThenTheErrorIsShownAndStillLocked()
    {
        // Arrange
        mockPasskeys.Setup(x => x.EnsureKeyAsync("e2e-kek")).ThrowsAsync(new InvalidOperationException("NotAllowedError"));
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.UnlockWithDeviceCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Something went wrong.", sut.Error);
        Assert.True(sut.IsLocked);
        Assert.False(sut.IsWorking);
        mockProfiles.Verify(x => x.UnlockWithDeviceAsync(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public async Task GivenThePasskeyIsCancelled_WhenUnlockedWithIt_ThenItAsksForThePassphrase()
    {
        // Arrange
        mockPasskeys.Setup(x => x.EnsureKeyAsync("e2e-kek")).ReturnsAsync(false);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.UnlockWithDeviceCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Your passkey cannot unlock this device's keys. Enter your passphrase instead.", sut.Error);
    }

    [Fact]
    public async Task GivenTheKeysAreLocked_WhenUnlockedWithThePasskey_ThenItSaysSo()
    {
        // Arrange
        mockProfiles.Setup(x => x.UnlockWithDeviceAsync(new E2eKeyScope(notes.Id))).ThrowsAsync(new E2eLockedException());
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.UnlockWithDeviceCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("End to end encryption is locked on this device.", sut.Error);
        Assert.True(sut.IsLocked);
    }

    [Fact]
    public async Task GivenNoInterruptedPassphraseChange_WhenFinishing_ThenNothingRuns()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.FinishRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsFinishingRotation);
        mockRotation.Verify(x => x.FinishAsync(It.IsAny<E2eKeyScope>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenFinishingRunning_WhenFinishedAgainOrUnlocked_ThenIgnored()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        var release = new TaskCompletionSource();
        mockRotation.Setup(x => x.FinishAsync(It.IsAny<E2eKeyScope>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>())).Returns(release.Task);
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).ReturnsAsync(true);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        var running = sut.FinishRotationAsync();

        // Act
        await sut.FinishRotationAsync();
        await sut.RetrySignOutAsync();

        // Assert
        Assert.True(sut.IsWorking);
        Assert.True(sut.IsFinishingRotation);
        release.SetResult();
        await running;
        Assert.False(sut.IsFinishingRotation);
        mockRotation.Verify(x => x.FinishAsync(It.IsAny<E2eKeyScope>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenNoSignOutNeeded_WhenRetried_ThenNobodyIsSignedOut()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.RetrySignOutCommand.ExecuteAsync(null);

        // Assert
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheSignOutThrows_WhenFinishingAPassphraseChange_ThenTheErrorIsShownAndTheSignOutOfferedAgain()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).ThrowsAsync(new InvalidOperationException("network"));
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.FinishRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.NeedsSignOut);
        Assert.Equal("Something went wrong.", sut.Error);
        Assert.False(sut.IsWorking);
    }

    [Fact]
    public async Task GivenTheSignOutFails_WhenFinishingAPassphraseChange_ThenItSaysSo()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).ReturnsAsync(false);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        await sut.FinishRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.NeedsSignOut);
        Assert.Equal(testText.Sync["Sync.E2e.Rotation.SignOutFailed"].Value, sut.Error);
    }

    [Fact]
    public async Task GivenProgressForOtherSteps_WhenFinishingAPassphraseChange_ThenOnlyTheFilesCount()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        mockRotation.Setup(x => x.FinishAsync(It.IsAny<E2eKeyScope>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, IProgress<E2eProgress>?, CancellationToken>((_, progress, _) =>
            {
                progress!.Report(new E2eProgress(E2eStep.DeriveKey, true, 5, 5));
                progress.Report(new E2eProgress(E2eStep.UploadFiles, false, 1, 3));
            })
            .Returns(Task.CompletedTask);
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).ReturnsAsync(true);
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        var filesTotals = new List<int>();
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.FilesTotal))
            {
                filesTotals.Add(sut.FilesTotal);
            }
        };

        // Act
        await sut.FinishRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([3], filesTotals);
        Assert.Equal(1, sut.FilesDone);
        Assert.Equal(33, sut.FilesPercent);
    }

    [Fact]
    public void GivenNoProfileSet_WhenProfilesChange_ThenNothingIsLookedUp()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        mockProfiles.Raise(x => x.ProfilesChanged += null, EventArgs.Empty);

        // Assert
        Assert.Null(sut.Profile);
        Assert.Equal("Checking…", sut.Message);
        mockPresenter.Verify(x => x.GetProfileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenAPassphraseIsSetElsewhere_WhenProfilesChange_ThenThePanelIsRefreshed()
    {
        // Arrange
        notes.State = E2eProfileState.NotSetUp;
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        notes.State = E2eProfileState.Locked;
        mockProfiles.Raise(x => x.ProfilesChanged += null, EventArgs.Empty);

        // Assert
        Assert.False(sut.NeedsPassphrase);
        Assert.True(sut.IsLocked);
    }

    [Fact]
    public async Task GivenTheProfileCannotBeRead_WhenRefreshedInTheBackground_ThenNothingThrowsAndThePanelIsKept()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.SetProfileAsync("notes");
        mockPresenter.Setup(x => x.GetProfileAsync("notes")).ThrowsAsync(new InvalidOperationException("storage"));

        // Act
        var exception = Record.Exception(() =>
        {
            mockProfiles.Raise(x => x.ProfilesChanged += null, EventArgs.Empty);
            mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(notes.Id));
        });

        // Assert
        Assert.Null(exception);
        Assert.Same(notes, sut.Profile);
        Assert.True(sut.IsLocked);
    }

    [Fact]
    public async Task GivenDisposed_WhenProfilesChange_ThenThePanelIsNotRefreshed()
    {
        // Arrange
        var sut = CreateSut();
        await sut.SetProfileAsync("notes");

        // Act
        sut.Dispose();
        mockProfiles.Raise(x => x.ProfilesChanged += null, EventArgs.Empty);
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(notes.Id));

        // Assert
        mockPresenter.Verify(x => x.GetProfileAsync("notes"), Times.Once);
    }

    private E2eUnlockPanelViewModel CreateSut() => new(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors, mockScheduler.Object, mockPasskeys.Object, mockToasts.Object, mockRotation.Object, mockSignOut.Object);
}
