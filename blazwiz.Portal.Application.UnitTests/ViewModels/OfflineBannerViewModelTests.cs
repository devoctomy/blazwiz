using blazwiz.Core.Offline;
using blazwiz.Core.Offline.Models;
using blazwiz.Portal.Application.ViewModels;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class OfflineBannerViewModelTests
{
    private readonly TestText testText = new();

    private readonly OfflineModeService offlineMode = new();

    [Fact]
    public void GivenNullOfflineModeService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new OfflineBannerViewModel(null!, testText.Ui)).ParamName);
    }

    [Fact]
    public void GivenOnline_WhenRead_ThenHidden()
    {
        using var sut = new OfflineBannerViewModel(offlineMode, testText.Ui);
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
        offlineMode.Enter(OfflineEntryReason.SignInUnreachable);
        using var sut = new OfflineBannerViewModel(offlineMode, testText.Ui);
        sut.SignInOnlineCommand.Execute(null);
        Assert.False(offlineMode.IsOffline);
    }

    [Fact]
    public void GivenDisposed_WhenOfflineModeChanges_ThenNotNotified()
    {
        var sut = new OfflineBannerViewModel(offlineMode, testText.Ui);
        var changed = 0;
        sut.PropertyChanged += (_, _) => changed++;
        sut.Dispose();
        offlineMode.Enter(OfflineEntryReason.Chosen);
        Assert.Equal(0, changed);
    }
}
