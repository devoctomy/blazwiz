using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class OfflineModeSwitchViewModelTests
{
    private readonly FrameworkText testText = new();

    private readonly OfflineModeService offlineMode = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new OfflineModeSwitchViewModel(null!, mockAuth.Object, testText.Ui, testText.Errors)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new OfflineModeSwitchViewModel(offlineMode, null!, testText.Ui, testText.Errors)).ParamName);
    }

    [Fact]
    public async Task GivenOnline_WhenToggled_ThenSessionGoesOffline()
    {
        // Arrange
        mockAuth.Setup(x => x.GoOfflineAsync()).Callback(() => offlineMode.Enter(OfflineEntryReason.Chosen)).Returns(Task.CompletedTask);
        using var sut = CreateSut();

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        mockAuth.Verify(x => x.GoOfflineAsync(), Times.Once);
        Assert.True(sut.IsOffline);
        Assert.False(sut.IsWorking);
    }

    [Fact]
    public async Task GivenOffline_WhenToggled_ThenOfflineModeLeftWithoutGoingOfflineAgain()
    {
        // Arrange
        offlineMode.Enter(OfflineEntryReason.Chosen);
        using var sut = CreateSut();

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.False(offlineMode.IsOffline);
        mockAuth.Verify(x => x.GoOfflineAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenGoingOfflineFails_WhenToggled_ThenErrorShownAndStillOnline()
    {
        // Arrange
        mockAuth.Setup(x => x.GoOfflineAsync()).ThrowsAsync(new InvalidOperationException("Sign in before turning offline mode on."));
        using var sut = CreateSut();

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Offline mode could not be turned on: Something went wrong.", sut.Error);
        Assert.False(sut.IsOffline);
    }

    [Fact]
    public void GivenDisposed_WhenOfflineModeChanges_ThenNotNotified()
    {
        // Arrange
        var sut = CreateSut();
        var changed = 0;
        sut.PropertyChanged += (_, _) => changed++;
        sut.Dispose();

        // Act
        offlineMode.Enter(OfflineEntryReason.Chosen);

        // Assert
        Assert.Equal(0, changed);
    }

    private OfflineModeSwitchViewModel CreateSut() => new(offlineMode, mockAuth.Object, testText.Ui, testText.Errors);
}
