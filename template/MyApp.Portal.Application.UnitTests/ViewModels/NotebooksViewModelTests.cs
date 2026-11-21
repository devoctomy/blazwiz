using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Application.ViewModels;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.UnitTests.ViewModels;

public class NotebooksViewModelTests
{
    private const string Pass = "correct horse battery";

    private readonly FrameworkText testText = new(typeof(SyncUiText));
    private readonly Mock<INotebookService> mockNotebooks = new();
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly Mock<IArgon2idKeyDerivationService> mockArgon2 = new();
    private readonly E2ePassphraseFormViewModel form;
    private readonly List<Notebook> notebooks =
    [
        new Notebook { Name = "notes", Id = Guid.NewGuid().ToString("D"), Title = "Notes", IsDefault = true },
    ];

    public NotebooksViewModelTests()
    {
        mockNotebooks.Setup(x => x.GetNotebooksAsync()).ReturnsAsync(() => notebooks.ToList());
        mockProfiles.Setup(x => x.GetArgon2ParametersAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(() => AppCosts());
        mockArgon2.Setup(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new byte[32]);
        var text = testText.For<SyncUiText>();
        var benchmark = new Argon2idBenchmarkViewModel(mockArgon2.Object, new Argon2idOptions { Parameters = AppCosts() }, TimeProvider.System, text, testText.Errors);
        form = new E2ePassphraseFormViewModel(mockProfiles.Object, benchmark, text);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("notebookService", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(null!, mockProfiles.Object, mockManager.Object, mockNavigation.Object, testText.Errors, form)).ParamName);
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, null!, mockManager.Object, mockNavigation.Object, testText.Errors, form)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, mockProfiles.Object, null!, mockNavigation.Object, testText.Errors, form)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, mockProfiles.Object, mockManager.Object, null!, testText.Errors, form)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, mockProfiles.Object, mockManager.Object, mockNavigation.Object, null!, form)).ParamName);
        Assert.Equal("passphraseForm", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, mockProfiles.Object, mockManager.Object, mockNavigation.Object, testText.Errors, null!)).ParamName);
    }

    [Fact]
    public async Task GivenNotebooks_WhenInitialized_ThenListed()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsLoading);
        Assert.Equal(["Notes"], sut.Notebooks.Select(n => n.Title));
    }

    [Theory]
    [InlineData(false, false, "No passphrase yet")]
    [InlineData(true, false, "Locked")]
    [InlineData(true, true, "Unlocked")]
    public void GivenANotebook_WhenDescribed_ThenItsStateIsSaid(bool hasPassphrase, bool isUnlocked, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, NotebooksViewModel.GetStateText(new Notebook { HasPassphrase = hasPassphrase, IsUnlocked = isUnlocked }));
    }

    [Fact]
    public async Task GivenANotebook_WhenOpened_ThenItsPagesAreShown()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.OpenCommand.ExecuteAsync("nb1");

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
        mockNotebooks.Verify(x => x.UnlockWithBiometricsAsync(It.IsAny<Notebook>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenALockedNotebookBiometricsCanUnlock_WhenOpened_ThenItIsUnlockedWithThemFirstAndOpenedEitherWay(bool unlockedWithBiometrics)
    {
        // Arrange
        var work = new Notebook { Name = "nb1", Id = "nb1", Title = "Work", HasPassphrase = true, CanUnlockWithBiometrics = true };
        notebooks.Add(work);
        string? openingDuringUnlock = null;
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        mockNotebooks
            .Setup(x => x.UnlockWithBiometricsAsync(It.Is<Notebook>(n => n.Name == "nb1")))
            .Callback(() => openingDuringUnlock = sut.OpeningNotebook)
            .ReturnsAsync(unlockedWithBiometrics);

        // Act
        await sut.OpenCommand.ExecuteAsync("nb1");

        // Assert
        Assert.Equal("nb1", openingDuringUnlock);
        Assert.Null(sut.OpeningNotebook);
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
    }

    [Fact]
    public async Task GivenANotebookBeingUnlockedWithBiometrics_WhenAnotherIsOpened_ThenItIsIgnored()
    {
        // Arrange
        notebooks.Add(new Notebook { Name = "nb1", Id = "nb1", Title = "Work", HasPassphrase = true, CanUnlockWithBiometrics = true });
        var unlocking = new TaskCompletionSource<bool>();
        mockNotebooks.Setup(x => x.UnlockWithBiometricsAsync(It.IsAny<Notebook>())).Returns(unlocking.Task);
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        var first = sut.OpenAsync("nb1");

        // Act
        await sut.OpenAsync("notes");

        // Assert
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
        unlocking.SetResult(true);
        await first;
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
    }

    [Fact]
    public async Task GivenAnUnlockedNotebook_WhenOpened_ThenBiometricsAreNotAskedFor()
    {
        // Arrange
        notebooks.Add(new Notebook { Name = "nb1", Id = "nb1", Title = "Work", HasPassphrase = true, IsUnlocked = true, CanUnlockWithBiometrics = true });
        using var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.OpenCommand.ExecuteAsync("nb1");

        // Assert
        mockNotebooks.Verify(x => x.UnlockWithBiometricsAsync(It.IsAny<Notebook>()), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
    }

    [Fact]
    public async Task GivenBiometricsUnlocking_WhenUnlockThrows_ThenOpeningIsClearedAndNothingIsOpened()
    {
        // Arrange
        notebooks.Add(new Notebook { Name = "nb1", Id = "nb1", Title = "Work", HasPassphrase = true, CanUnlockWithBiometrics = true });
        mockNotebooks.Setup(x => x.UnlockWithBiometricsAsync(It.IsAny<Notebook>())).ThrowsAsync(new InvalidOperationException("unexpected"));
        using var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.OpenAsync("nb1"));

        // Assert
        Assert.Null(sut.OpeningNotebook);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GivenANotebook_WhenForgettingBiometricsIsAskedThenCancelled_ThenNothingIsForgotten()
    {
        // Arrange
        var work = new Notebook { Name = "nb1", Id = "nb1", Title = "Work", HasPassphrase = true, CanUnlockWithBiometrics = true };
        using var sut = CreateSut();
        sut.AskForgetBiometricsCommand.Execute(work);
        var askedFor = sut.ForgetCandidate;

        // Act
        sut.CancelForgetBiometricsCommand.Execute(null);

        // Assert
        Assert.Same(work, askedFor);
        Assert.Null(sut.ForgetCandidate);
        mockNotebooks.Verify(x => x.ForgetBiometricsAsync(It.IsAny<Notebook>()), Times.Never);
    }

    [Fact]
    public async Task GivenForgettingBiometricsConfirmed_WhenForgotten_ThenTheDialogClosesAndTheListIsRefreshed()
    {
        // Arrange
        var work = new Notebook { Name = "nb1", Id = "nb1", Title = "Work", HasPassphrase = true, CanUnlockWithBiometrics = true };
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        sut.AskForgetBiometricsCommand.Execute(work);

        // Act
        await sut.ForgetBiometricsCommand.ExecuteAsync(null);

        // Assert
        mockNotebooks.Verify(x => x.ForgetBiometricsAsync(work), Times.Once);
        Assert.Null(sut.ForgetCandidate);
        Assert.False(sut.IsWorking);
        mockNotebooks.Verify(x => x.GetNotebooksAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenBiometricsBeingForgotten_WhenCancelledOrConfirmedAgain_ThenBothAreIgnored()
    {
        // Arrange
        var work = new Notebook { Name = "nb1", Id = "nb1", Title = "Work", HasPassphrase = true, CanUnlockWithBiometrics = true };
        var forgetting = new TaskCompletionSource();
        mockNotebooks.Setup(x => x.ForgetBiometricsAsync(work)).Returns(forgetting.Task);
        using var sut = CreateSut();
        sut.AskForgetBiometricsCommand.Execute(work);
        var first = sut.ForgetBiometricsAsync();

        // Act
        sut.CancelForgetBiometrics();
        await sut.ForgetBiometricsAsync();

        // Assert
        Assert.Same(work, sut.ForgetCandidate);
        forgetting.SetResult();
        await first;
        mockNotebooks.Verify(x => x.ForgetBiometricsAsync(It.IsAny<Notebook>()), Times.Once);
        Assert.Null(sut.ForgetCandidate);
    }

    [Fact]
    public async Task GivenNoNotebookAskedAbout_WhenForgetBiometrics_ThenNothingIsForgotten()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.ForgetBiometricsAsync();

        // Assert
        mockNotebooks.Verify(x => x.ForgetBiometricsAsync(It.IsAny<Notebook>()), Times.Never);
    }

    [Fact]
    public async Task GivenForgettingBiometricsFails_WhenConfirmed_ThenTheDialogStaysOpenWithTheError()
    {
        // Arrange
        var work = new Notebook { Name = "nb1", Id = "nb1", Title = "Work", HasPassphrase = true, CanUnlockWithBiometrics = true };
        mockNotebooks.Setup(x => x.ForgetBiometricsAsync(work)).ThrowsAsync(new InvalidOperationException("storage unavailable"));
        using var sut = CreateSut();
        sut.AskForgetBiometricsCommand.Execute(work);

        // Act
        await sut.ForgetBiometricsCommand.ExecuteAsync(null);

        // Assert
        Assert.Same(work, sut.ForgetCandidate);
        Assert.False(string.IsNullOrEmpty(sut.ForgetError));
        Assert.False(sut.IsWorking);
    }

    [Theory]
    [InlineData("", Pass, Pass, true, "Give the notebook a name.")]
    [InlineData("Work", "", "", true, "Enter your passphrase.")]
    [InlineData("Work", Pass, "different", true, "The passphrases do not match.")]
    [InlineData("Work", Pass, Pass, false, "Confirm you understand there is no passphrase recovery.")]
    public async Task GivenInvalidFields_WhenCreating_ThenNothingIsCreated(string title, string passphrase, string confirmation, bool acknowledged, string error)
    {
        // Arrange
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = title;
        sut.PassphraseForm.Passphrase = passphrase;
        sut.PassphraseForm.ConfirmPassphrase = confirmation;
        sut.PassphraseForm.AcknowledgedNoRecovery = acknowledged;

        // Act
        await sut.CreateCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(error, sut.DialogError);
        Assert.True(sut.IsCreateDialogOpen);
        mockNotebooks.Verify(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Argon2idParameters?>(), It.IsAny<IProgress<E2eProgress>?>()), Times.Never);
    }

    [Fact]
    public async Task GivenValidFields_WhenCreating_ThenTheNotebookIsCreatedAndOpened()
    {
        // Arrange
        mockNotebooks.Setup(x => x.CreateAsync("Work", Pass, It.IsAny<Argon2idParameters?>(), null)).ReturnsAsync(new Notebook { Name = "nb1", Title = "Work" });
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = "Work";
        sut.PassphraseForm.Passphrase = Pass;
        sut.PassphraseForm.ConfirmPassphrase = Pass;
        sut.PassphraseForm.AcknowledgedNoRecovery = true;

        // Act
        await sut.CreateCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsCreateDialogOpen);
        Assert.Empty(sut.PassphraseForm.Passphrase);
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
    }

    [Fact]
    public async Task GivenTheDialogOpened_WhenShown_ThenTheAdvancedTabStartsAtTheSettingsNewNotebooksAreGiven()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.ShowCreateDialogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsCreateDialogOpen);
        Assert.Equal(E2ePassphraseFormTab.Passphrase, sut.PassphraseForm.ActiveTab);
        Assert.Equal(19, sut.PassphraseForm.Argon2.MemoryMib);
        Assert.Equal(2, sut.PassphraseForm.Argon2.Iterations);
        Assert.True(sut.PassphraseForm.Argon2.IsKeyLengthFixed);
        Assert.False(sut.PassphraseForm.Argon2.NeedsBenchmark);
    }

    [Fact]
    public void GivenTheCreateDialog_WhenItsPassphraseFormChanges_ThenThePageIsToldToShowItAgain()
    {
        // Arrange
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.PassphraseForm.ShowTab(E2ePassphraseFormTab.Advanced);
        var afterTab = changed.Count(c => c == nameof(NotebooksViewModel.PassphraseForm));
        sut.PassphraseForm.Argon2.Iterations = 250;
        var afterSettings = changed.Count(c => c == nameof(NotebooksViewModel.PassphraseForm));

        // Assert
        Assert.Equal(1, afterTab);
        Assert.True(afterSettings > afterTab);
    }

    [Fact]
    public async Task GivenArgon2SettingsChanged_WhenCreating_ThenRefusedUntilTheyAreBenchmarkedThenCreatedWithThem()
    {
        // Arrange
        Argon2idParameters? used = null;
        mockNotebooks.Setup(x => x.CreateAsync("Work", Pass, It.IsAny<Argon2idParameters?>(), null))
            .Callback<string, string, Argon2idParameters?, IProgress<E2eProgress>?>((_, _, argon2, _) => used = argon2)
            .ReturnsAsync(new Notebook { Name = "nb1", Title = "Work" });
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = "Work";
        sut.PassphraseForm.Passphrase = Pass;
        sut.PassphraseForm.ConfirmPassphrase = Pass;
        sut.PassphraseForm.AcknowledgedNoRecovery = true;
        sut.PassphraseForm.Argon2.MemoryMib = 4096;
        sut.PassphraseForm.Argon2.Iterations = 250;

        // Act
        var canCreateBefore = sut.CanCreate;
        await sut.CreateCommand.ExecuteAsync(null);
        var refused = sut.DialogError;
        var tab = sut.PassphraseForm.ActiveTab;
        var createdBeforeBenchmark = used;
        await sut.PassphraseForm.Argon2.BenchmarkCommand.ExecuteAsync(null);
        var canCreateAfter = sut.CanCreate;
        await sut.CreateCommand.ExecuteAsync(null);

        // Assert
        Assert.False(canCreateBefore);
        Assert.True(canCreateAfter);
        Assert.Equal("Benchmark these settings on this device before they can be used.", refused);
        Assert.Equal(E2ePassphraseFormTab.Advanced, tab);
        Assert.Null(createdBeforeBenchmark);
        Assert.NotNull(used);
        Assert.Equal(4096 * 1024, used.MemoryKib);
        Assert.Equal(250, used.Iterations);
        Assert.Equal(32, used.KeyLength);
        Assert.False(sut.IsCreateDialogOpen);
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
    }

    [Fact]
    public async Task GivenNoNameAndTheAdvancedTabShown_WhenCreating_ThenTheFirstTabIsShownWithTheError()
    {
        // Arrange
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.PassphraseForm.ShowTabCommand.Execute(E2ePassphraseFormTab.Advanced);

        // Act
        await sut.CreateCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Give the notebook a name.", sut.DialogError);
        Assert.Equal(E2ePassphraseFormTab.Passphrase, sut.PassphraseForm.ActiveTab);
    }

    [Fact]
    public async Task GivenAShortPassphrase_WhenCreating_ThenTheErrorIsShown()
    {
        // Arrange
        mockNotebooks.Setup(x => x.CreateAsync("Work", "short", It.IsAny<Argon2idParameters?>(), null)).ThrowsAsync(new E2ePassphraseTooShortException(12));
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = "Work";
        sut.PassphraseForm.Passphrase = "short";
        sut.PassphraseForm.ConfirmPassphrase = "short";
        sut.PassphraseForm.AcknowledgedNoRecovery = true;

        // Act
        await sut.CreateCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsCreateDialogOpen);
        Assert.Equal("Use a passphrase of at least 12 characters.", sut.DialogError);
    }

    [Fact]
    public async Task GivenListed_WhenANotebookIsUnlocked_ThenTheListIsRefreshed()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        notebooks.Add(new Notebook { Name = "nb1", Title = "Work", IsUnlocked = true });

        // Act
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(Guid.NewGuid().ToString("D")));

        // Assert
        Assert.Equal(["Notes", "Work"], sut.Notebooks.Select(n => n.Title));
    }

    [Fact]
    public void GivenTheDialog_WhenFieldsAreSetToNull_ThenTheyAreEmpty()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        sut.Title = null!;
        sut.PassphraseForm.Passphrase = null!;
        sut.PassphraseForm.ConfirmPassphrase = null!;

        // Assert
        Assert.Equal(string.Empty, sut.Title);
        Assert.Equal(string.Empty, sut.PassphraseForm.Passphrase);
        Assert.Equal(string.Empty, sut.PassphraseForm.ConfirmPassphrase);
    }

    [Fact]
    public void GivenAFilledInDialog_WhenClosed_ThenItClosesAndThePassphrasesAreCleared()
    {
        // Arrange
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = "Work";
        sut.PassphraseForm.Passphrase = Pass;
        sut.PassphraseForm.ConfirmPassphrase = Pass;
        sut.PassphraseForm.AcknowledgedNoRecovery = true;

        // Act
        sut.CloseCreateDialogCommand.Execute(null);

        // Assert
        Assert.False(sut.IsCreateDialogOpen);
        Assert.Empty(sut.PassphraseForm.Passphrase);
        Assert.Empty(sut.PassphraseForm.ConfirmPassphrase);
        Assert.False(sut.PassphraseForm.AcknowledgedNoRecovery);
    }

    [Fact]
    public async Task GivenANotebookBeingCreated_WhenClosedOrCreatedAgain_ThenBothAreIgnored()
    {
        // Arrange
        var created = new TaskCompletionSource<Notebook>();
        mockNotebooks.Setup(x => x.CreateAsync("Work", Pass, It.IsAny<Argon2idParameters?>(), null)).Returns(created.Task);
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = "Work";
        sut.PassphraseForm.Passphrase = Pass;
        sut.PassphraseForm.ConfirmPassphrase = Pass;
        sut.PassphraseForm.AcknowledgedNoRecovery = true;
        var creating = sut.CreateAsync();

        // Act
        sut.CloseCreateDialogCommand.Execute(null);
        await sut.CreateAsync();
        var openWhileWorking = sut.IsCreateDialogOpen;
        var workingWhileCreating = sut.IsWorking;
        var passphraseWhileWorking = sut.PassphraseForm.Passphrase;
        created.SetResult(new Notebook { Name = "nb1", Title = "Work" });
        await creating;

        // Assert
        Assert.True(openWhileWorking);
        Assert.True(workingWhileCreating);
        Assert.Equal(Pass, passphraseWhileWorking);
        Assert.False(sut.IsWorking);
        Assert.False(sut.IsCreateDialogOpen);
        mockNotebooks.Verify(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Argon2idParameters?>(), It.IsAny<IProgress<E2eProgress>?>()), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
    }

    [Fact]
    public async Task GivenTheNameIsRejected_WhenCreating_ThenItsMessageIsShown()
    {
        // Arrange
        mockNotebooks.Setup(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Argon2idParameters?>(), It.IsAny<IProgress<E2eProgress>?>())).ThrowsAsync(new NotesException("Give the notebook a name."));
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = "Work";
        sut.PassphraseForm.Passphrase = Pass;
        sut.PassphraseForm.ConfirmPassphrase = Pass;
        sut.PassphraseForm.AcknowledgedNoRecovery = true;

        // Act
        await sut.CreateCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsCreateDialogOpen);
        Assert.False(sut.IsWorking);
        Assert.Equal("Give the notebook a name.", sut.DialogError);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenListed_WhenANotebookIsCreatedOrDeleted_ThenTheListIsRefreshed()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        notebooks.Add(new Notebook { Name = "nb1", Title = "Work" });

        // Act
        mockProfiles.Raise(x => x.ProfilesChanged += null, mockProfiles.Object, EventArgs.Empty);

        // Assert
        Assert.Equal(["Notes", "Work"], sut.Notebooks.Select(n => n.Title));
    }

    [Fact]
    public async Task GivenListed_WhenARefreshFails_ThenTheListIsKeptWithoutCrashing()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        mockNotebooks.Setup(x => x.GetNotebooksAsync()).ThrowsAsync(new InvalidOperationException("unexpected"));

        // Act
        mockProfiles.Raise(x => x.ProfilesChanged += null, mockProfiles.Object, EventArgs.Empty);
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, new E2eKeyScope(Guid.NewGuid().ToString("D")));

        // Assert
        Assert.Equal(["Notes"], sut.Notebooks.Select(n => n.Title));
        mockNotebooks.Verify(x => x.GetNotebooksAsync(), Times.Exactly(3));
    }

    private static Argon2idParameters AppCosts() => new() { MemoryKib = 19 * 1024, Iterations = 2, Parallelism = 1, KeyLength = 32 };

    private NotebooksViewModel CreateSut() => new(mockNotebooks.Object, mockProfiles.Object, mockManager.Object, mockNavigation.Object, testText.Errors, form);
}
