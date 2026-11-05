using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
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
    private readonly TestText testText = new();
    private readonly E2eProfileRow notes = new() { Name = "notes", Id = Guid.NewGuid().ToString("D"), DisplayName = "Notes", State = E2eProfileState.Locked };

    public E2eUnlockPanelViewModelTests()
    {
        mockPresenter.Setup(x => x.GetProfileAsync("notes")).ReturnsAsync(() => notes);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(null!, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, null!, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("presenter", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, null!, mockNavigation.Object, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, null!, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new E2eUnlockPanelViewModel(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, null!)).ParamName);
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

    private E2eUnlockPanelViewModel CreateSut() => new(mockProfiles.Object, mockManager.Object, mockPresenter.Object, mockNavigation.Object, testText.Sync, testText.Errors);
}
