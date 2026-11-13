using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class E2eProfilesViewModelTests
{
    private const string Pass = "correct horse battery";

    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<IE2eProfilePresenter> mockPresenter = new();
    private readonly Mock<IOfflineModeService> mockOffline = new();
    private readonly Mock<ISyncKeyRotationService> mockRotation = new();
    private readonly Mock<IE2eRotationSignOut> mockSignOut = new();
    private readonly TestText testText = new();
    private readonly E2eProfileRow notes = new() { Name = "notes", Id = Guid.NewGuid().ToString("D"), DisplayName = "Notes", State = E2eProfileState.NotSetUp };
    private readonly E2eProfileRow created;
    private readonly List<E2eProfileRow> rows = [];

    public E2eProfilesViewModelTests()
    {
        var id = Guid.NewGuid().ToString("D");
        created = new E2eProfileRow { Name = id, Id = id, DisplayName = id, State = E2eProfileState.Locked, IsCreatedByUser = true };
        rows.Add(notes);
        rows.Add(created);
        mockProfiles.SetupGet(x => x.IsLoaded).Returns(true);
        mockPresenter.Setup(x => x.GetProfilesAsync()).ReturnsAsync(() => rows.ToList());
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2eProfilesViewModel(null!, mockManager.Object, mockPresenter.Object, mockOffline.Object, testText.Sync, testText.Errors, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eProfilesViewModel(mockProfiles.Object, null!, mockPresenter.Object, mockOffline.Object, testText.Sync, testText.Errors, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("presenter", Assert.Throws<ArgumentNullException>(() => new E2eProfilesViewModel(mockProfiles.Object, mockManager.Object, null!, mockOffline.Object, testText.Sync, testText.Errors, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new E2eProfilesViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, null!, testText.Sync, testText.Errors, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new E2eProfilesViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockOffline.Object, null!, testText.Errors, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new E2eProfilesViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockOffline.Object, testText.Sync, null!, mockRotation.Object, mockSignOut.Object)).ParamName);
        Assert.Equal("rotation", Assert.Throws<ArgumentNullException>(() => new E2eProfilesViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockOffline.Object, testText.Sync, testText.Errors, null!, mockSignOut.Object)).ParamName);
        Assert.Equal("rotationSignOut", Assert.Throws<ArgumentNullException>(() => new E2eProfilesViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockOffline.Object, testText.Sync, testText.Errors, mockRotation.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenProfiles_WhenInitialized_ThenThoseInCodeAndThoseCreatedAreListedApart()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsLoaded);
        Assert.Equal([notes], sut.AppProfiles);
        Assert.Equal([created], sut.CreatedProfiles);
        Assert.False(sut.CanLockAll);
        Assert.Equal("No passphrase yet", sut.GetStateText(notes));
        Assert.Equal("Locked", sut.GetStateText(created));
    }

    [Fact]
    public async Task GivenAProfileInCode_WhenItsFirstPassphraseIsSet_ThenItsKeysAreCreatedStepByStep()
    {
        // Arrange
        mockProfiles.Setup(x => x.SetUpAsync(new E2eKeyScope(notes.Id), Pass, It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, string, IProgress<E2eProgress>?, CancellationToken>((_, _, progress, _) =>
            {
                foreach (var step in new[] { E2eStep.DeriveKey, E2eStep.CreateKeys, E2eStep.SaveKeys, E2eStep.StoreDeviceKey })
                {
                    progress!.Report(new E2eProgress(step, false));
                    progress.Report(new E2eProgress(step, true));
                }

                notes.State = E2eProfileState.Unlocked;
            })
            .Returns(Task.CompletedTask);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenSetPassphraseCommand.Execute("notes");
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsFinished);
        Assert.All(sut.Steps, s => Assert.Equal(E2eStepState.Done, s.State));
        Assert.Equal("Passphrase set", sut.FinishedText);
        Assert.Empty(sut.Passphrase);
        Assert.True(sut.CanLockAll);
    }

    [Fact]
    public async Task GivenAShortPassphrase_WhenSet_ThenBackToTheFormWithTheError()
    {
        // Arrange
        mockProfiles.Setup(x => x.SetUpAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new E2ePassphraseTooShortException(12));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenSetPassphraseCommand.Execute("notes");
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsEditing);
        Assert.Equal("Use a passphrase of at least 12 characters.", sut.DialogError);
    }

    [Theory]
    [InlineData(Pass, "different", true, "The passphrases do not match.")]
    [InlineData(Pass, Pass, false, "Confirm you understand there is no passphrase recovery.")]
    [InlineData("", "", true, "Enter your passphrase.")]
    public async Task GivenInvalidFields_WhenCreating_ThenNothingIsCreated(string passphrase, string confirmation, bool acknowledged, string error)
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.OpenCreateCommand.Execute(null);
        sut.Passphrase = passphrase;
        sut.ConfirmPassphrase = confirmation;
        sut.AcknowledgedNoRecovery = acknowledged;

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(error, sut.DialogError);
        mockProfiles.Verify(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAPassphrase_WhenCreating_ThenTheNewProfilesIdIsShown()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("D");
        mockProfiles.Setup(x => x.CreateAsync(Pass, It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new E2eKeyScope(id));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenCreateCommand.Execute(null);
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsFinished);
        Assert.Equal(id, sut.CreatedId);
        Assert.Equal($"Created {id}", sut.FinishedText);
    }

    [Fact]
    public async Task GivenAWrongPassphrase_WhenUnlocking_ThenAnError()
    {
        // Arrange
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(created.Id), "wrong", It.IsAny<CancellationToken>())).ThrowsAsync(new E2eWrongPassphraseException());
        using var sut = await CreateInitializedSutAsync();
        sut.OpenUnlockCommand.Execute(created.Name);
        sut.Passphrase = "wrong";

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsFinished);
        Assert.Equal("That passphrase is not correct. Try again.", sut.DialogError);
        Assert.Equal($"Enter the passphrase of {created.Id}.", sut.DialogText);
    }

    [Fact]
    public async Task GivenNoCurrentPassphrase_WhenChangingIt_ThenAskedForIt()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute("notes");
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Enter your current passphrase.", sut.DialogError);
    }

    [Fact]
    public async Task GivenBothPassphrases_WhenChangingIt_ThenTheProfileIsSyncedAndEstimatedAndNothingChangesYet()
    {
        // Arrange
        mockRotation.Setup(x => x.PrepareAsync(new E2eKeyScope(notes.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncKeyRotationEstimate(12, 3 * 1024 * 1024, TimeSpan.FromSeconds(90)));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute("notes");
        sut.CurrentPassphrase = "old passphrase";
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsConfirmingRotation);
        Assert.False(sut.IsEditing);
        Assert.Equal("Files to encrypt again and upload: 12 (3 MB). Time needed: about 2 min.", sut.EstimateText);
        Assert.StartsWith("Every file is encrypted again and uploaded.", sut.RotationWarning);
        mockRotation.Verify(x => x.RotateAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenOffline_WhenChangingThePassphrase_ThenRefusedWithNothingChanged()
    {
        // Arrange
        mockRotation.Setup(x => x.PrepareAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncKeyRotationException(SyncErrorCodes.RotationOffline));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute("notes");
        sut.CurrentPassphrase = "old passphrase";
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsEditing);
        Assert.False(sut.IsConfirmingRotation);
        Assert.StartsWith("Changing a passphrase needs a connection", sut.DialogError);
        Assert.Equal(Pass, sut.Passphrase);
    }

    [Fact]
    public async Task GivenTheEstimateConfirmed_WhenStarted_ThenTheFilesAreEncryptedAgainWithProgressAndEveryDeviceSignedOut()
    {
        // Arrange
        var percents = new List<int>();
        mockRotation.Setup(x => x.PrepareAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncKeyRotationEstimate(2, 100, TimeSpan.FromSeconds(1)));
        mockRotation.Setup(x => x.RotateAsync(new E2eKeyScope(notes.Id), "old passphrase", Pass, It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, string, string, IProgress<E2eProgress>?, CancellationToken>((_, _, _, progress, _) =>
            {
                progress!.Report(new E2eProgress(E2eStep.ReencryptFiles, false, 0, 2));
                progress.Report(new E2eProgress(E2eStep.UploadFiles, false, 0, 2));
                progress.Report(new E2eProgress(E2eStep.ReencryptFiles, false, 1, 2));
                progress.Report(new E2eProgress(E2eStep.ReencryptFiles, true, 2, 2));
                progress.Report(new E2eProgress(E2eStep.UploadFiles, false, 1, 2));
                progress.Report(new E2eProgress(E2eStep.UploadFiles, true, 2, 2));
            })
            .Returns(Task.CompletedTask);
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).ReturnsAsync(true);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute("notes");
        sut.CurrentPassphrase = "old passphrase";
        EnterNewPassphrase(sut);
        await sut.ConfirmDialogCommand.ExecuteAsync(null);
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(sut.FilesPercent))
            {
                percents.Add(sut.FilesPercent);
            }
        };

        // Act
        await sut.StartRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal([0, 0, 25, 50, 75, 100], percents);
        Assert.Equal(E2eStepState.Done, Assert.Single(sut.Steps, s => s.Step == E2eStep.ReencryptFiles).State);
        Assert.Equal(E2eStepState.Done, Assert.Single(sut.Steps, s => s.Step == E2eStep.UploadFiles).State);
        Assert.False(sut.NeedsSignOut);
        Assert.Empty(sut.Passphrase);
        Assert.Empty(sut.CurrentPassphrase);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenTheFilesCannotBeUploaded_WhenRotating_ThenTheStepFailsAndNobodyIsSignedOut()
    {
        // Arrange
        mockRotation.Setup(x => x.PrepareAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncKeyRotationEstimate(2, 100, TimeSpan.FromSeconds(1)));
        mockRotation.Setup(x => x.RotateAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, string, string, IProgress<E2eProgress>?, CancellationToken>((_, _, _, progress, _) =>
                progress!.Report(new E2eProgress(E2eStep.ReencryptFiles, false, 0, 2)))
            .ThrowsAsync(new SyncKeyRotationException(SyncErrorCodes.RotationSyncFailed));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute("notes");
        sut.CurrentPassphrase = "old passphrase";
        EnterNewPassphrase(sut);
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Act
        await sut.StartRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(E2eStepState.Failed, Assert.Single(sut.Steps, s => s.Step == E2eStep.ReencryptFiles).State);
        Assert.StartsWith("The profile's files could not be synced.", sut.DialogError);
        Assert.Empty(sut.Passphrase);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheSignOutFails_WhenRotated_ThenItIsOfferedUntilItSucceeds()
    {
        // Arrange
        mockRotation.Setup(x => x.PrepareAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncKeyRotationEstimate(0, 0, TimeSpan.Zero));
        mockSignOut.SetupSequence(x => x.SignOutEverywhereAsync()).ReturnsAsync(false).ReturnsAsync(true);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute("notes");
        sut.CurrentPassphrase = "old passphrase";
        EnterNewPassphrase(sut);
        await sut.ConfirmDialogCommand.ExecuteAsync(null);
        await sut.StartRotationCommand.ExecuteAsync(null);
        var offered = sut.NeedsSignOut;
        var error = sut.DialogError;

        // Act
        await sut.RetrySignOutCommand.ExecuteAsync(null);

        // Assert
        Assert.True(offered);
        Assert.StartsWith("Your files are encrypted with the new keys, but every device could not be signed out", error);
        Assert.False(sut.NeedsSignOut);
        Assert.Null(sut.DialogError);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenAnInterruptedRotation_WhenFinished_ThenTheFilesAreEncryptedAgainAndEveryDeviceSignedOut()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).ReturnsAsync(true);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenFinishRotationCommand.Execute("notes");
        var text = sut.DialogText;

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("A passphrase change of Notes was interrupted. It must be finished before Notes can be used, and it must not be interrupted again.", text);
        Assert.Equal("Passphrase change unfinished", sut.GetStateText(notes));
        Assert.True(sut.CanLockAll);
        mockRotation.Verify(x => x.FinishAsync(new E2eKeyScope(notes.Id), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenTheIdTypedWrongly_WhenDeleting_ThenNothingIsDeleted()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.OpenDeleteCommand.Execute(created.Name);
        sut.DeleteConfirmation = "nope";

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Type the id exactly to confirm.", sut.DialogError);
        mockProfiles.Verify(x => x.DeleteAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTheIdTyped_WhenDeleting_ThenItIsDeleted()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.OpenDeleteCommand.Execute(created.Name);
        sut.DeleteConfirmation = created.Id.ToUpperInvariant();

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsFinished);
        mockProfiles.Verify(x => x.DeleteAsync(new E2eKeyScope(created.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenUnlockedProfiles_WhenLockedOneOrAll_ThenTheManagerLocksThem()
    {
        // Arrange
        notes.State = E2eProfileState.Unlocked;
        using var sut = await CreateInitializedSutAsync();

        // Act
        sut.LockCommand.Execute("notes");
        sut.LockAllCommand.Execute(null);

        // Assert
        Assert.True(sut.CanLockAll);
        mockManager.Verify(x => x.Lock(new E2eKeyScope(notes.Id)), Times.Once);
        mockManager.Verify(x => x.LockAll(), Times.Once);
    }

    [Fact]
    public async Task GivenAProfileLocks_WhenShown_ThenTheListIsRefreshed()
    {
        // Arrange
        notes.State = E2eProfileState.Unlocked;
        using var sut = await CreateInitializedSutAsync();

        // Act
        notes.State = E2eProfileState.Locked;
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(notes.Id));

        // Assert
        Assert.False(sut.CanLockAll);
        mockPresenter.Verify(x => x.GetProfilesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenADialogRunning_WhenClosed_ThenItStaysOpen()
    {
        // Arrange
        var release = new TaskCompletionSource();
        mockManager.Setup(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(release.Task);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenUnlockCommand.Execute(created.Name);
        sut.Passphrase = Pass;
        var running = sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Act
        sut.CloseDialogCommand.Execute(null);

        // Assert
        Assert.Equal(E2eDialogKind.Unlock, sut.ActiveDialog);
        release.SetResult();
        await running;
        sut.CloseDialogCommand.Execute(null);
        Assert.Null(sut.ActiveDialog);
    }

    [Theory]
    [InlineData(E2eDialogKind.SetPassphrase, "Sync.E2e.SetPassphrase", "Sync.E2e.Dialog.SetPassphrase.Text")]
    [InlineData(E2eDialogKind.Create, "Sync.E2e.New", "Sync.E2e.Dialog.Create.Text")]
    [InlineData(E2eDialogKind.Unlock, "Sync.E2e.Unlock", "Sync.E2e.Dialog.Unlock.Text")]
    [InlineData(E2eDialogKind.Delete, "Sync.E2e.Dialog.Delete", "Sync.E2e.Dialog.Delete.Text")]
    [InlineData(E2eDialogKind.FinishRotation, "Sync.E2e.FinishRotation", "Sync.E2e.Rotation.Interrupted")]
    [InlineData(E2eDialogKind.ChangePassphrase, "Sync.E2e.ChangePassphrase", "Sync.E2e.Dialog.ChangePassphrase.Text")]
    public async Task GivenEachDialog_WhenOpenedForAProfile_ThenItsTitleAndTextNameTheProfile(E2eDialogKind kind, string titleKey, string textKey)
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();

        // Act
        Open(sut, kind, notes.Name);

        // Assert
        Assert.Equal(kind, sut.ActiveDialog);
        Assert.False(testText.Sync[titleKey].ResourceNotFound);
        Assert.Equal(testText.Sync[titleKey].Value, sut.DialogTitle);
        if (kind == E2eDialogKind.Create)
        {
            Assert.Null(sut.DialogProfile);
            Assert.Equal(testText.Sync[textKey].Value, sut.DialogText);
        }
        else
        {
            Assert.Same(notes, sut.DialogProfile);
            Assert.Equal(testText.Sync[textKey, notes.DisplayName].Value, sut.DialogText);
            Assert.Contains(notes.DisplayName, sut.DialogText, StringComparison.Ordinal);
        }

        Assert.True(sut.IsEditing);
    }

    [Theory]
    [InlineData(E2eDialogKind.SetPassphrase, "Sync.E2e.Dialog.SetPassphrase.Text")]
    [InlineData(E2eDialogKind.Unlock, "Sync.E2e.Dialog.Unlock.Text")]
    [InlineData(E2eDialogKind.Delete, "Sync.E2e.Dialog.Delete.Text")]
    [InlineData(E2eDialogKind.FinishRotation, "Sync.E2e.Rotation.Interrupted")]
    [InlineData(E2eDialogKind.ChangePassphrase, "Sync.E2e.Dialog.ChangePassphrase.Text")]
    public async Task GivenAProfileNoLongerListed_WhenItsDialogIsOpened_ThenTheTextHasNoName(E2eDialogKind kind, string textKey)
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();

        // Act
        Open(sut, kind, "missing");

        // Assert
        Assert.Null(sut.DialogProfile);
        Assert.Equal(testText.Sync[textKey, string.Empty].Value, sut.DialogText);
    }

    [Theory]
    [InlineData(E2eDialogKind.SetPassphrase, "Sync.E2e.Dialog.PassphraseSet")]
    [InlineData(E2eDialogKind.Create, "Sync.E2e.Dialog.Created")]
    [InlineData(E2eDialogKind.Unlock, "Sync.E2e.Dialog.Unlocked")]
    [InlineData(E2eDialogKind.Delete, "Sync.E2e.Dialog.Deleted")]
    [InlineData(E2eDialogKind.ChangePassphrase, "Sync.E2e.Dialog.PassphraseChanged")]
    [InlineData(E2eDialogKind.FinishRotation, "Sync.E2e.Dialog.PassphraseChanged")]
    public async Task GivenEachDialog_WhenOpened_ThenItsFinishedTextMatchesIt(E2eDialogKind kind, string finishedKey)
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();

        // Act
        Open(sut, kind, notes.Name);

        // Assert
        Assert.False(testText.Sync[finishedKey].ResourceNotFound);
        Assert.Equal(testText.Sync[finishedKey, string.Empty].Value, sut.FinishedText);
    }

    [Theory]
    [InlineData(E2eDialogKind.ChangePassphrase, "Sync.E2e.Rotation.Preparing")]
    [InlineData(E2eDialogKind.Unlock, "Sync.E2e.Dialog.Working")]
    public async Task GivenADialog_WhenItsChangeRuns_ThenTheWorkingTextSuitsIt(E2eDialogKind kind, string workingKey)
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();

        // Act
        Open(sut, kind, notes.Name);

        // Assert
        Assert.Equal(testText.Sync[workingKey].Value, sut.WorkingText);
    }

    [Fact]
    public async Task GivenNothingStarted_WhenShown_ThenNoEstimateNoProgressAndTheWarningsComeFromTheText()
    {
        // Arrange
        mockOffline.SetupGet(x => x.IsOffline).Returns(true);

        // Act
        using var sut = await CreateInitializedSutAsync();

        // Assert
        Assert.True(sut.IsOffline);
        Assert.Null(sut.Error);
        Assert.Null(sut.Estimate);
        Assert.Empty(sut.EstimateText);
        Assert.Equal(0, sut.FilesTotal);
        Assert.Equal(0, sut.FilesPercent);
        Assert.Equal(testText.Sync["Sync.E2e.NoRecoveryWarning"].Value, sut.NoRecoveryWarning);
    }

    [Fact]
    public async Task GivenNullInputs_WhenSet_ThenTheyAreEmpty()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.Passphrase = Pass;
        sut.ConfirmPassphrase = Pass;
        sut.CurrentPassphrase = Pass;
        sut.DeleteConfirmation = Pass;

        // Act
        sut.Passphrase = null!;
        sut.ConfirmPassphrase = null!;
        sut.CurrentPassphrase = null!;
        sut.DeleteConfirmation = null!;

        // Assert
        Assert.Empty(sut.Passphrase);
        Assert.Empty(sut.ConfirmPassphrase);
        Assert.Empty(sut.CurrentPassphrase);
        Assert.Empty(sut.DeleteConfirmation);
    }

    [Fact]
    public async Task GivenAQuickPassphraseChange_WhenEstimated_ThenItSaysUnderAMinute()
    {
        // Arrange
        mockRotation.Setup(x => x.PrepareAsync(new E2eKeyScope(notes.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncKeyRotationEstimate(2, 100, TimeSpan.FromSeconds(30)));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute(notes.Name);
        sut.CurrentPassphrase = "old passphrase";
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains(testText.Sync["Sync.E2e.Rotation.UnderAMinute"].Value, sut.EstimateText, StringComparison.Ordinal);
        Assert.True(sut.IsConfirmingRotation);
    }

    [Fact]
    public async Task GivenNoDialogOpen_WhenConfirmed_ThenNothingRuns()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.Null(sut.DialogError);
        Assert.False(sut.IsWorking);
        mockManager.Verify(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mockProfiles.Verify(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenADialogRunning_WhenConfirmedAgainOrAnotherDialogOpened_ThenBothAreIgnored()
    {
        // Arrange
        var release = new TaskCompletionSource();
        mockManager.Setup(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(release.Task);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenUnlockCommand.Execute(created.Name);
        sut.Passphrase = Pass;
        var running = sut.ConfirmDialogAsync();

        // Act
        await sut.ConfirmDialogAsync();
        sut.OpenCreate();
        await sut.StartRotationAsync();

        // Assert
        Assert.True(sut.IsWorking);
        Assert.False(sut.IsEditing);
        Assert.Equal(E2eDialogKind.Unlock, sut.ActiveDialog);
        Assert.Same(created, sut.DialogProfile);
        release.SetResult();
        await running;
        Assert.True(sut.IsFinished);
        mockManager.Verify(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mockRotation.Verify(x => x.RotateAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenThePassphrase_WhenUnlocking_ThenItIsUnlockedAndTheInputsAreCleared()
    {
        // Arrange
        mockManager.Setup(x => x.UnlockAsync(new E2eKeyScope(created.Id), Pass, It.IsAny<CancellationToken>()))
            .Callback(() => created.State = E2eProfileState.Unlocked)
            .Returns(Task.CompletedTask);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenUnlockCommand.Execute(created.Name);
        sut.Passphrase = Pass;

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsFinished);
        Assert.Null(sut.DialogError);
        Assert.Empty(sut.Passphrase);
        Assert.Equal(testText.Sync["Sync.E2e.Dialog.Unlocked"].Value, sut.FinishedText);
        Assert.True(sut.CanLockAll);
    }

    [Fact]
    public async Task GivenNoPassphrase_WhenUnlocking_ThenAskedForItAndNothingIsUnlocked()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.OpenUnlockCommand.Execute(created.Name);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Enter your passphrase.", sut.DialogError);
        mockManager.Verify(x => x.UnlockAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoEstimateYet_WhenTheRotationIsStarted_ThenNothingChanges()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute(notes.Name);
        sut.CurrentPassphrase = "old passphrase";
        EnterNewPassphrase(sut);

        // Act
        await sut.StartRotationAsync();

        // Assert
        Assert.True(sut.IsEditing);
        Assert.Equal(Pass, sut.Passphrase);
        mockRotation.Verify(x => x.RotateAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAnotherDialogOpen_WhenTheRotationIsStarted_ThenNothingChanges()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.OpenUnlockCommand.Execute(created.Name);

        // Act
        await sut.StartRotationAsync();

        // Assert
        Assert.Equal(E2eDialogKind.Unlock, sut.ActiveDialog);
        mockRotation.Verify(x => x.RotateAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTheProfileIsNotListed_WhenItsEstimatedRotationIsStarted_ThenNothingChanges()
    {
        // Arrange
        mockRotation.Setup(x => x.PrepareAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncKeyRotationEstimate(1, 10, TimeSpan.FromSeconds(1)));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute("missing");
        sut.CurrentPassphrase = "old passphrase";
        EnterNewPassphrase(sut);
        await sut.ConfirmDialogAsync();

        // Act
        await sut.StartRotationAsync();

        // Assert
        Assert.NotNull(sut.Estimate);
        Assert.True(sut.IsConfirmingRotation);
        mockRotation.Verify(x => x.RotateAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTheWrongCurrentPassphrase_WhenRotating_ThenBackToTheFormSayingSoAndNobodyIsSignedOut()
    {
        // Arrange
        mockRotation.Setup(x => x.PrepareAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncKeyRotationEstimate(2, 100, TimeSpan.FromSeconds(1)));
        mockRotation.Setup(x => x.RotateAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new E2eWrongPassphraseException());
        using var sut = await CreateInitializedSutAsync();
        sut.OpenChangePassphraseCommand.Execute(notes.Name);
        sut.CurrentPassphrase = "wrong passphrase";
        EnterNewPassphrase(sut);
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Act
        await sut.StartRotationCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("That passphrase is not correct. Try again.", sut.DialogError);
        Assert.Empty(sut.Steps);
        Assert.True(sut.IsEditing);
        Assert.False(sut.IsFinished);
        Assert.Equal(Pass, sut.Passphrase);
        Assert.Equal("wrong passphrase", sut.CurrentPassphrase);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheKeysAreLocked_WhenFinishingAnInterruptedRotation_ThenItSaysSoAndNobodyIsSignedOut()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        mockRotation.Setup(x => x.FinishAsync(It.IsAny<E2eKeyScope>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new E2eLockedException());
        using var sut = await CreateInitializedSutAsync();
        sut.OpenFinishRotationCommand.Execute(notes.Name);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("End to end encryption is locked on this device.", sut.DialogError);
        Assert.False(sut.IsFinished);
        Assert.False(sut.NeedsSignOut);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheSignOutThrows_WhenAnInterruptedRotationIsFinished_ThenTheErrorIsShownAndTheSignOutOfferedAgain()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).ThrowsAsync(new InvalidOperationException("network"));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenFinishRotationCommand.Execute(notes.Name);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.NeedsSignOut);
        Assert.Equal("Something went wrong.", sut.DialogError);
        Assert.False(sut.IsWorking);
        Assert.False(sut.IsEditing);
    }

    [Fact]
    public async Task GivenNoSignOutNeeded_WhenRetried_ThenNobodyIsSignedOut()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();

        // Act
        await sut.RetrySignOutCommand.ExecuteAsync(null);

        // Assert
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheSignOutRunning_WhenRetried_ThenItIsNotStartedTwice()
    {
        // Arrange
        notes.State = E2eProfileState.RotationInProgress;
        var release = new TaskCompletionSource<bool>();
        mockSignOut.Setup(x => x.SignOutEverywhereAsync()).Returns(release.Task);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenFinishRotationCommand.Execute(notes.Name);
        var running = sut.ConfirmDialogAsync();

        // Act
        await sut.RetrySignOutAsync();

        // Assert
        Assert.True(sut.IsWorking);
        release.SetResult(true);
        await running;
        Assert.False(sut.NeedsSignOut);
        mockSignOut.Verify(x => x.SignOutEverywhereAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenProgressForAStepNotShown_WhenSettingAPassphrase_ThenItIsIgnored()
    {
        // Arrange
        mockProfiles.Setup(x => x.SetUpAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, string, IProgress<E2eProgress>?, CancellationToken>((_, _, progress, _) =>
                progress!.Report(new E2eProgress(E2eStep.ReencryptFiles, false, 1, 2)))
            .Returns(Task.CompletedTask);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenSetPassphraseCommand.Execute(notes.Name);
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsFinished);
        Assert.DoesNotContain(sut.Steps, s => s.Step == E2eStep.ReencryptFiles);
        Assert.All(sut.Steps, s => Assert.Equal(E2eStepState.Pending, s.State));
        Assert.Equal(0, sut.FilesTotal);
    }

    [Fact]
    public async Task GivenAStepDoneAndTheNextNotStarted_WhenSettingAPassphraseFails_ThenTheNextStepFails()
    {
        // Arrange
        mockProfiles.Setup(x => x.SetUpAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<E2eKeyScope, string, IProgress<E2eProgress>?, CancellationToken>((_, _, progress, _) =>
            {
                progress!.Report(new E2eProgress(E2eStep.DeriveKey, false));
                progress.Report(new E2eProgress(E2eStep.DeriveKey, true));
            })
            .ThrowsAsync(new InvalidOperationException("storage full"));
        using var sut = await CreateInitializedSutAsync();
        sut.OpenSetPassphraseCommand.Execute(notes.Name);
        EnterNewPassphrase(sut);

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(E2eStepState.Done, Assert.Single(sut.Steps, s => s.Step == E2eStep.DeriveKey).State);
        Assert.Equal(E2eStepState.Failed, Assert.Single(sut.Steps, s => s.Step == E2eStep.CreateKeys).State);
        Assert.Equal(E2eStepState.Pending, Assert.Single(sut.Steps, s => s.Step == E2eStep.SaveKeys).State);
        Assert.Equal("Something went wrong.", sut.DialogError);
        Assert.Empty(sut.Passphrase);
        Assert.False(sut.IsFinished);
    }

    [Fact]
    public async Task GivenAProfileNoLongerListed_WhenDeleting_ThenNoIdMatches()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        sut.OpenDeleteCommand.Execute("missing");
        sut.DeleteConfirmation = created.Id;

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Type the id exactly to confirm.", sut.DialogError);
        mockProfiles.Verify(x => x.DeleteAsync(It.IsAny<E2eKeyScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTheIdTyped_WhenDeleted_ThenItSaysDeletedAndTheListIsRefreshed()
    {
        // Arrange
        mockProfiles.Setup(x => x.DeleteAsync(new E2eKeyScope(created.Id), It.IsAny<CancellationToken>()))
            .Callback(() => rows.Remove(created))
            .Returns(Task.CompletedTask);
        using var sut = await CreateInitializedSutAsync();
        sut.OpenDeleteCommand.Execute(created.Name);
        sut.DeleteConfirmation = $"  {created.Id}  ";

        // Act
        await sut.ConfirmDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsFinished);
        Assert.Empty(sut.DeleteConfirmation);
        Assert.Equal(testText.Sync["Sync.E2e.Dialog.Deleted"].Value, sut.FinishedText);
        Assert.Empty(sut.CreatedProfiles);
    }

    [Fact]
    public async Task GivenAnUnknownProfile_WhenLocked_ThenNothingIsLocked()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();

        // Act
        sut.LockCommand.Execute("missing");

        // Assert
        mockManager.Verify(x => x.Lock(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public async Task GivenOfflineModeTurnedOn_WhenShown_ThenItSaysOfflineAndTheListIsRefreshed()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        mockOffline.SetupGet(x => x.IsOffline).Returns(true);

        // Act
        mockOffline.Raise(x => x.Changed += null, mockOffline.Object, true);

        // Assert
        Assert.True(sut.IsOffline);
        Assert.Contains(nameof(E2eProfilesViewModel.IsOffline), changed);
        Assert.Contains(nameof(E2eProfilesViewModel.AppProfiles), changed);
        mockPresenter.Verify(x => x.GetProfilesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenAProfileIsCreatedElsewhere_WhenShown_ThenTheListIsRefreshed()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        var id = Guid.NewGuid().ToString("D");
        rows.Add(new E2eProfileRow { Name = id, Id = id, DisplayName = id, State = E2eProfileState.Locked, IsCreatedByUser = true });

        // Act
        mockProfiles.Raise(x => x.ProfilesChanged += null, EventArgs.Empty);

        // Assert
        Assert.Equal(2, sut.CreatedProfiles.Count);
    }

    [Fact]
    public async Task GivenTheProfilesCannotBeRead_WhenRefreshedInTheBackground_ThenNothingThrowsAndTheListIsKept()
    {
        // Arrange
        using var sut = await CreateInitializedSutAsync();
        mockPresenter.Setup(x => x.GetProfilesAsync()).ThrowsAsync(new InvalidOperationException("storage"));

        // Act
        var exception = Record.Exception(() =>
        {
            mockProfiles.Raise(x => x.ProfilesChanged += null, EventArgs.Empty);
            mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(notes.Id));
            mockOffline.Raise(x => x.Changed += null, mockOffline.Object, false);
        });

        // Assert
        Assert.Null(exception);
        Assert.Equal([notes], sut.AppProfiles);
        Assert.Equal([created], sut.CreatedProfiles);
    }

    [Fact]
    public async Task GivenDisposed_WhenProfilesOrOfflineModeChange_ThenTheListIsNotRefreshed()
    {
        // Arrange
        var sut = await CreateInitializedSutAsync();

        // Act
        sut.Dispose();
        mockProfiles.Raise(x => x.ProfilesChanged += null, EventArgs.Empty);
        mockOffline.Raise(x => x.Changed += null, mockOffline.Object, true);
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(notes.Id));

        // Assert
        mockPresenter.Verify(x => x.GetProfilesAsync(), Times.Once);
    }

    private static void Open(E2eProfilesViewModel sut, E2eDialogKind kind, string name)
    {
        switch (kind)
        {
            case E2eDialogKind.SetPassphrase:
                sut.OpenSetPassphraseCommand.Execute(name);
                break;
            case E2eDialogKind.Create:
                sut.OpenCreateCommand.Execute(null);
                break;
            case E2eDialogKind.Unlock:
                sut.OpenUnlockCommand.Execute(name);
                break;
            case E2eDialogKind.Delete:
                sut.OpenDeleteCommand.Execute(name);
                break;
            case E2eDialogKind.FinishRotation:
                sut.OpenFinishRotationCommand.Execute(name);
                break;
            default:
                sut.OpenChangePassphraseCommand.Execute(name);
                break;
        }
    }

    private static void EnterNewPassphrase(E2eProfilesViewModel sut)
    {
        sut.Passphrase = Pass;
        sut.ConfirmPassphrase = Pass;
        sut.AcknowledgedNoRecovery = true;
    }

    private async Task<E2eProfilesViewModel> CreateInitializedSutAsync()
    {
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        return sut;
    }

    private E2eProfilesViewModel CreateSut() => new(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockOffline.Object, testText.Sync, testText.Errors, mockRotation.Object, mockSignOut.Object);
}
