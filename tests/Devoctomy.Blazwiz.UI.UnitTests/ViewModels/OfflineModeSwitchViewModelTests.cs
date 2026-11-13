using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
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

    [Fact]
    public void GivenNullText_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new OfflineModeSwitchViewModel(offlineMode, mockAuth.Object, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new OfflineModeSwitchViewModel(offlineMode, mockAuth.Object, testText.Ui, null!)).ParamName);
    }

    [Fact]
    public async Task GivenGoingOfflineFailsWithCode_WhenToggled_ThenErrorDescribesTheCode()
    {
        // Arrange
        mockAuth.Setup(x => x.GoOfflineAsync()).ThrowsAsync(new BlazwizException(AuthErrorCodes.NotSignedIn));
        using var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Offline mode could not be turned on: Please sign in first.", sut.Error);
        Assert.Contains(nameof(OfflineModeSwitchViewModel.IsOffline), changed);
        Assert.False(sut.IsWorking);
    }

    [Fact]
    public async Task GivenPreviousError_WhenToggledAgainSuccessfully_ThenErrorCleared()
    {
        // Arrange
        mockAuth.SetupSequence(x => x.GoOfflineAsync())
            .ThrowsAsync(new HttpRequestException("network down"))
            .Returns(Task.CompletedTask);
        using var sut = CreateSut();
        await sut.ToggleCommand.ExecuteAsync(null);
        Assert.NotNull(sut.Error);

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(sut.Error);
        mockAuth.Verify(x => x.GoOfflineAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenGoingOfflineInProgress_WhenToggledAgain_ThenSecondToggleIgnored()
    {
        // Arrange
        var pending = new TaskCompletionSource();
        mockAuth.Setup(x => x.GoOfflineAsync()).Returns(pending.Task);
        using var sut = CreateSut();

        // Act
        var first = sut.ToggleAsync();
        var workingDuringFirst = sut.IsWorking;
        await sut.ToggleAsync();
        pending.SetResult();
        await first;

        // Assert
        Assert.True(workingDuringFirst);
        Assert.False(sut.IsWorking);
        mockAuth.Verify(x => x.GoOfflineAsync(), Times.Once);
    }

    [Fact]
    public void GivenSwitch_WhenOfflineModeChangesElsewhere_ThenIsOfflineNotified()
    {
        // Arrange
        using var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        offlineMode.Enter(OfflineEntryReason.SignInUnreachable);

        // Assert
        Assert.Equal([nameof(OfflineModeSwitchViewModel.IsOffline)], changed);
        Assert.True(sut.IsOffline);
    }

    private OfflineModeSwitchViewModel CreateSut() => new(offlineMode, mockAuth.Object, testText.Ui, testText.Errors);
}
