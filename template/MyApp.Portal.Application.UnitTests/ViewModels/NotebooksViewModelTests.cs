using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Moq;
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
    private readonly List<Notebook> notebooks =
    [
        new Notebook { Name = "notes", Id = Guid.NewGuid().ToString("D"), Title = "Notes", IsDefault = true },
    ];

    public NotebooksViewModelTests()
    {
        mockNotebooks.Setup(x => x.GetNotebooksAsync()).ReturnsAsync(() => notebooks.ToList());
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("notebookService", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(null!, mockProfiles.Object, mockManager.Object, mockNavigation.Object, testText.Errors)).ParamName);
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, null!, mockManager.Object, mockNavigation.Object, testText.Errors)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, mockProfiles.Object, null!, mockNavigation.Object, testText.Errors)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, mockProfiles.Object, mockManager.Object, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new NotebooksViewModel(mockNotebooks.Object, mockProfiles.Object, mockManager.Object, mockNavigation.Object, null!)).ParamName);
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
    public void GivenANotebook_WhenOpened_ThenItsPagesAreShown()
    {
        // Act
        CreateSut().OpenCommand.Execute("nb1");

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
    }

    [Theory]
    [InlineData("", Pass, Pass, true, "Give the notebook a name.")]
    [InlineData("Work", "", "", true, "Enter a passphrase.")]
    [InlineData("Work", Pass, "different", true, "The passphrases do not match.")]
    [InlineData("Work", Pass, Pass, false, "Confirm you understand there is no passphrase recovery.")]
    public async Task GivenInvalidFields_WhenCreating_ThenNothingIsCreated(string title, string passphrase, string confirmation, bool acknowledged, string error)
    {
        // Arrange
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = title;
        sut.Passphrase = passphrase;
        sut.ConfirmPassphrase = confirmation;
        sut.AcknowledgedNoRecovery = acknowledged;

        // Act
        await sut.CreateCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(error, sut.DialogError);
        Assert.True(sut.IsCreateDialogOpen);
        mockNotebooks.Verify(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>()), Times.Never);
    }

    [Fact]
    public async Task GivenValidFields_WhenCreating_ThenTheNotebookIsCreatedAndOpened()
    {
        // Arrange
        mockNotebooks.Setup(x => x.CreateAsync("Work", Pass, null)).ReturnsAsync(new Notebook { Name = "nb1", Title = "Work" });
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = "Work";
        sut.Passphrase = Pass;
        sut.ConfirmPassphrase = Pass;
        sut.AcknowledgedNoRecovery = true;

        // Act
        await sut.CreateCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsCreateDialogOpen);
        Assert.Empty(sut.Passphrase);
        mockNavigation.Verify(x => x.NavigateTo("files?notebook=nb1"), Times.Once);
    }

    [Fact]
    public async Task GivenAShortPassphrase_WhenCreating_ThenTheErrorIsShown()
    {
        // Arrange
        mockNotebooks.Setup(x => x.CreateAsync("Work", "short", null)).ThrowsAsync(new E2ePassphraseTooShortException(12));
        using var sut = CreateSut();
        sut.ShowCreateDialogCommand.Execute(null);
        sut.Title = "Work";
        sut.Passphrase = "short";
        sut.ConfirmPassphrase = "short";
        sut.AcknowledgedNoRecovery = true;

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

    private NotebooksViewModel CreateSut() => new(mockNotebooks.Object, mockProfiles.Object, mockManager.Object, mockNavigation.Object, testText.Errors);
}
