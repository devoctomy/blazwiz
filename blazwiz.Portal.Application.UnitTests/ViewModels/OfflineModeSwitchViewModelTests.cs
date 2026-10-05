using blazwiz.Core.Offline;
using blazwiz.Core.Offline.Models;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class OfflineModeSwitchViewModelTests
{
    private readonly OfflineModeService offlineMode = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new OfflineModeSwitchViewModel(null!, mockAuth.Object)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new OfflineModeSwitchViewModel(offlineMode, null!)).ParamName);
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
        Assert.Equal("Sign in before turning offline mode on.", sut.Error);
        Assert.False(sut.IsOffline);
    }

    [Fact]
    public void GivenDisposed_WhenOfflineModeChanges_ThenNotNotified()
    {
        var sut = CreateSut();
        var changed = 0;
        sut.PropertyChanged += (_, _) => changed++;
        sut.Dispose();
        offlineMode.Enter(OfflineEntryReason.Chosen);
        Assert.Equal(0, changed);
    }

    private OfflineModeSwitchViewModel CreateSut() => new(offlineMode, mockAuth.Object);
}
