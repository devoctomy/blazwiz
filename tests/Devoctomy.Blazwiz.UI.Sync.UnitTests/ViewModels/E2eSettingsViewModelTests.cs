using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class E2eSettingsViewModelTests
{
    private readonly TestText testText = new();

    private const string Passphrase = "correct horse battery staple";

    private readonly Mock<ISyncEncryptionService> mockEncryption = new();
    private readonly OfflineModeService offlineMode = new();
    private E2eStatus status = E2eStatus.Disabled;

    public E2eSettingsViewModelTests()
    {
        mockEncryption.SetupGet(x => x.Status).Returns(() => status);
        mockEncryption.SetupGet(x => x.HasPasskeyKeys).Returns(true);
    }

    [Fact]
    public void GivenNullService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("encryptionService", Assert.Throws<ArgumentNullException>(() => new E2eSettingsViewModel(null!, offlineMode, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new E2eSettingsViewModel(mockEncryption.Object, null!, testText.Sync, testText.Errors)).ParamName);
    }

    [Theory]
    [InlineData(E2eStatus.Disabled, true, false, false)]
    [InlineData(E2eStatus.NeedsSetup, false, true, false)]
    [InlineData(E2eStatus.Unlocked, false, false, true)]
    [InlineData(E2eStatus.Blocked, false, false, false)]
    [InlineData(E2eStatus.Unavailable, false, false, false)]
    [InlineData(E2eStatus.Unsupported, false, false, false)]
    [InlineData(E2eStatus.Unknown, false, false, false)]
    public void GivenStatus_WhenGettingActions_ThenOnlyTheValidOnesOffered(E2eStatus current, bool canEnable, bool canSetUp, bool canManage)
    {
        // Arrange
        status = current;
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(canEnable, sut.CanEnable);
        Assert.Equal(canSetUp, sut.CanSetUp);
        Assert.Equal(canManage, sut.CanManage);
        Assert.False(string.IsNullOrEmpty(sut.StatusText));
    }

    [Fact]
    public void GivenNeedsSetupWithoutPasskeyKeys_WhenGettingCanSetUp_ThenFalse()
    {
        // Arrange
        status = E2eStatus.NeedsSetup;
        mockEncryption.SetupGet(x => x.HasPasskeyKeys).Returns(false);

        // Act & Assert
        Assert.False(CreateSut().CanSetUp);
    }

    [Fact]
    public void GivenDialog_WhenOpenDialog_ThenEditingWithEmptyFields()
    {
        // Arrange
        var sut = CreateSut();
        sut.Passphrase = "left over";

        // Act
        sut.OpenDialog(E2eDialogKind.Enable);

        // Assert
        Assert.Equal(E2eDialogKind.Enable, sut.ActiveDialog);
        Assert.True(sut.IsEditing);
        Assert.Empty(sut.Passphrase);
        Assert.False(sut.AcknowledgedNoRecovery);
    }

    [Theory]
    [InlineData("short", "short", true, "at least")]
    [InlineData(Passphrase, "different passphrase", true, "do not match")]
    [InlineData(Passphrase, Passphrase, false, "no passphrase recovery")]
    public async Task GivenInvalidInput_WhenConfirmEnable_ThenErrorAndNothingStarted(string passphrase, string confirm, bool acknowledged, string expected)
    {
        // Arrange
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.Enable);
        sut.Passphrase = passphrase;
        sut.ConfirmPassphrase = confirm;
        sut.AcknowledgedNoRecovery = acknowledged;

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.Contains(expected, sut.DialogError);
        Assert.True(sut.IsEditing);
        mockEncryption.Verify(x => x.EnableAsync(It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenValidInput_WhenConfirmEnable_ThenStepsTickedFinishedAndPassphraseCleared()
    {
        // Arrange
        mockEncryption.Setup(x => x.EnableAsync(Passphrase, It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, IProgress<E2eProgress>?, CancellationToken>((_, progress, _) =>
            {
                foreach (var step in Enum.GetValues<E2eStep>())
                {
                    progress!.Report(new E2eProgress(step, false, 0, step == E2eStep.EncryptFiles ? 2 : 0));
                    progress.Report(new E2eProgress(step, true, step == E2eStep.EncryptFiles ? 2 : 0, step == E2eStep.EncryptFiles ? 2 : 0));
                }

                status = E2eStatus.Unlocked;
            })
            .Returns(Task.CompletedTask);
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.Enable);
        sut.Passphrase = Passphrase;
        sut.ConfirmPassphrase = Passphrase;
        sut.AcknowledgedNoRecovery = true;

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.True(sut.IsFinished);
        Assert.False(sut.IsWorking);
        Assert.Null(sut.DialogError);
        Assert.Empty(sut.Passphrase);
        Assert.Empty(sut.ConfirmPassphrase);
        Assert.Equal(10, sut.Steps.Count);
        Assert.Equal(E2eStep.Sync, sut.Steps[0].Step);
        Assert.Equal(E2eStep.GoOnline, sut.Steps[^1].Step);
        Assert.All(sut.Steps, s => Assert.Equal(E2eStepState.Done, s.State));
        Assert.Equal("2 of 2", sut.Steps.Single(s => s.Step == E2eStep.EncryptFiles).Detail);
        Assert.True(sut.CanManage);
    }

    [Fact]
    public async Task GivenEnableFails_WhenConfirmEnable_ThenRunningStepFailedAndErrorShown()
    {
        // Arrange
        mockEncryption.Setup(x => x.EnableAsync(Passphrase, It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, IProgress<E2eProgress>?, CancellationToken>((_, progress, _) =>
            {
                progress!.Report(new E2eProgress(E2eStep.Sync, false));
                progress.Report(new E2eProgress(E2eStep.Sync, true));
                progress.Report(new E2eProgress(E2eStep.CheckExistingKeys, false));
            })
            .ThrowsAsync(new E2eAlreadyEnabledException());
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.Enable);
        sut.Passphrase = Passphrase;
        sut.ConfirmPassphrase = Passphrase;
        sut.AcknowledgedNoRecovery = true;

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.False(sut.IsFinished);
        Assert.Contains("already enabled", sut.DialogError);
        Assert.Equal(E2eStepState.Done, sut.Steps.Single(s => s.Step == E2eStep.Sync).State);
        Assert.Equal(E2eStepState.Failed, sut.Steps.Single(s => s.Step == E2eStep.CheckExistingKeys).State);
    }

    [Fact]
    public async Task GivenWrongPassphrase_WhenConfirmSetUp_ThenErrorAndCanTryAgain()
    {
        // Arrange
        status = E2eStatus.NeedsSetup;
        mockEncryption.Setup(x => x.SetUpDeviceAsync("wrong", It.IsAny<CancellationToken>())).ThrowsAsync(new E2eWrongPassphraseException());
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.SetUp);
        sut.Passphrase = "wrong";

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.Contains("not correct", sut.DialogError);
        Assert.True(sut.IsEditing);
        Assert.Empty(sut.Passphrase);

        // Act again, with the right one
        sut.Passphrase = Passphrase;
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.True(sut.IsFinished);
        mockEncryption.Verify(x => x.SetUpDeviceAsync(Passphrase, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNoPassphrase_WhenConfirmSetUpOrDisable_ThenAskedForIt()
    {
        // Arrange
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.SetUp);

        // Act & Assert
        await sut.ConfirmDialogAsync();
        Assert.Equal("Enter your passphrase.", sut.DialogError);
        sut.OpenDialog(E2eDialogKind.Disable);
        await sut.ConfirmDialogAsync();
        Assert.Equal("Enter your passphrase.", sut.DialogError);
    }

    [Fact]
    public async Task GivenValidInput_WhenConfirmChangePassphrase_ThenChangedAndFinished()
    {
        // Arrange
        status = E2eStatus.Unlocked;
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.ChangePassphrase);
        sut.CurrentPassphrase = "old passphrase";
        sut.Passphrase = Passphrase;
        sut.ConfirmPassphrase = Passphrase;
        sut.AcknowledgedNoRecovery = true;

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        mockEncryption.Verify(x => x.ChangePassphraseAsync("old passphrase", Passphrase, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(sut.IsFinished);
        Assert.Empty(sut.CurrentPassphrase);
    }

    [Fact]
    public async Task GivenNoCurrentPassphrase_WhenConfirmChangePassphrase_ThenAskedForIt()
    {
        // Arrange
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.ChangePassphrase);

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.Equal("Enter your current passphrase.", sut.DialogError);
    }

    [Fact]
    public async Task GivenPassphrase_WhenConfirmDisable_ThenDisableStepsRun()
    {
        // Arrange
        status = E2eStatus.Unlocked;
        mockEncryption.Setup(x => x.DisableAsync(Passphrase, It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => status = E2eStatus.Disabled)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.Disable);
        sut.Passphrase = Passphrase;

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.True(sut.IsFinished);
        Assert.Equal(
            [E2eStep.VerifyPassphrase, E2eStep.Sync, E2eStep.GoOffline, E2eStep.DecryptFiles, E2eStep.DeleteKeys, E2eStep.GoOnline],
            sut.Steps.Select(s => s.Step));
        Assert.True(sut.CanEnable);
    }

    [Fact]
    public async Task GivenWrongPassphrase_WhenConfirmDisable_ThenFirstStepFailed()
    {
        // Arrange
        mockEncryption.Setup(x => x.DisableAsync("wrong", It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, IProgress<E2eProgress>?, CancellationToken>((_, progress, _) => progress!.Report(new E2eProgress(E2eStep.VerifyPassphrase, false)))
            .ThrowsAsync(new E2eWrongPassphraseException());
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.Disable);
        sut.Passphrase = "wrong";

        // Act
        await sut.ConfirmDialogAsync();

        // Assert
        Assert.Equal("That passphrase is not correct.", sut.DialogError);
        Assert.Equal(E2eStepState.Failed, sut.Steps[0].State);
    }

    [Fact]
    public void GivenOpenDialog_WhenCloseDialog_ThenClosedAndCleared()
    {
        // Arrange
        var sut = CreateSut();
        sut.OpenDialog(E2eDialogKind.Disable);
        sut.Passphrase = "typed";

        // Act
        sut.CloseDialog();

        // Assert
        Assert.Null(sut.ActiveDialog);
        Assert.Empty(sut.Passphrase);
    }

    [Fact]
    public void GivenStatusChanges_WhenRaised_ThenStatusPropertiesNotified()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockEncryption.Raise(x => x.StatusChanged += null, mockEncryption.Object, E2eStatus.Unlocked);

        // Assert
        Assert.Contains(nameof(E2eSettingsViewModel.StatusText), changed);
        Assert.Contains(nameof(E2eSettingsViewModel.CanManage), changed);
    }

    [Theory]
    [InlineData(E2eStep.EncryptFiles, "Encrypt your files")]
    [InlineData(E2eStep.DeriveKey, "Derive your passphrase key (Argon2id)")]
    public void GivenStep_WhenGetStepLabel_ThenDescribed(E2eStep step, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, CreateSut().GetStepLabel(step));
    }

    [Theory]
    [InlineData(E2eStatus.Unknown)]
    [InlineData(E2eStatus.Disabled)]
    public void GivenOfflineWithNoKeysOnThisDevice_WhenGettingStatus_ThenOffOnThisDeviceAndSaysOffline(E2eStatus current)
    {
        // Arrange
        status = current;
        offlineMode.Enter(OfflineEntryReason.Chosen);

        // Act
        using var sut = CreateSut();

        // Assert
        Assert.Equal("Off on this device. You're offline, so this is this device's own state, and encryption can't be changed until you sign in online.", sut.StatusText);
        Assert.False(sut.CanEnable);
    }

    [Fact]
    public void GivenOfflineAndUnlocked_WhenGettingStatus_ThenOnSaysOfflineAndCannotBeChanged()
    {
        // Arrange
        status = E2eStatus.Unlocked;
        offlineMode.Enter(OfflineEntryReason.SignInUnreachable);

        // Act
        using var sut = CreateSut();

        // Assert
        Assert.StartsWith("On.", sut.StatusText);
        Assert.EndsWith("You're offline, so this is this device's own state, and encryption can't be changed until you sign in online.", sut.StatusText);
        Assert.False(sut.CanManage);
    }

    [Fact]
    public void GivenOfflineModeChanges_WhenRaised_ThenStatusNotified()
    {
        // Arrange
        using var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        offlineMode.Enter(OfflineEntryReason.Chosen);

        // Assert
        Assert.Contains(nameof(E2eSettingsViewModel.StatusText), changed);
        Assert.Contains(nameof(E2eSettingsViewModel.CanEnable), changed);
    }

    private E2eSettingsViewModel CreateSut() => new(mockEncryption.Object, offlineMode, testText.Sync, testText.Errors);
}
