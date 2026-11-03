using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Devoctomy.Blazwiz.UI.ViewModels;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class OfflineBannerViewModelTests
{
    private readonly FrameworkText testText = new();

    private readonly OfflineModeService offlineMode = new();

    [Fact]
    public void GivenNullOfflineModeService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new OfflineBannerViewModel(null!, testText.Ui)).ParamName);
    }

    [Fact]
    public void GivenOnline_WhenRead_ThenHidden()
    {
        // Act
        using var sut = new OfflineBannerViewModel(offlineMode, testText.Ui);

        // Assert
        Assert.False(sut.IsVisible);
    }

    [Theory]
    [InlineData(OfflineEntryReason.Chosen, "You're in offline mode. Nothing is synced or sent to the cloud, and you'll be signed out 15 minutes after going offline.")]
    [InlineData(OfflineEntryReason.SignInUnreachable, "Couldn't reach the sign-in service, so you're signed in offline. Sync and some features are disabled, and the session lasts 15 minutes.")]
    public void GivenOfflineMode_WhenEntered_ThenShownWithWhy(OfflineEntryReason reason, string message)
    {
        // Arrange
        using var sut = new OfflineBannerViewModel(offlineMode, testText.Ui);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        offlineMode.Enter(reason);

        // Assert
        Assert.True(sut.IsVisible);
        Assert.Equal(message, sut.Message);
        Assert.Contains(nameof(OfflineBannerViewModel.IsVisible), changed);
    }

    [Fact]
    public void GivenOfflineMode_WhenSignInOnline_ThenOfflineModeLeft()
    {
        // Arrange
        offlineMode.Enter(OfflineEntryReason.SignInUnreachable);
        using var sut = new OfflineBannerViewModel(offlineMode, testText.Ui);

        // Act
        sut.SignInOnlineCommand.Execute(null);

        // Assert
        Assert.False(offlineMode.IsOffline);
    }

    [Fact]
    public void GivenDisposed_WhenOfflineModeChanges_ThenNotNotified()
    {
        // Arrange
        var sut = new OfflineBannerViewModel(offlineMode, testText.Ui);
        var changed = 0;
        sut.PropertyChanged += (_, _) => changed++;
        sut.Dispose();

        // Act
        offlineMode.Enter(OfflineEntryReason.Chosen);

        // Assert
        Assert.Equal(0, changed);
    }
}
