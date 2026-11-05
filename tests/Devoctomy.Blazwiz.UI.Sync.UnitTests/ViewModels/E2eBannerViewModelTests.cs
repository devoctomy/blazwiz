using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class E2eBannerViewModelTests
{
    private readonly Mock<ISyncEncryptionService> mockEncryption = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly TestText testText = new();
    private E2eStatus status = E2eStatus.Unknown;

    public E2eBannerViewModelTests()
    {
        mockEncryption.SetupGet(x => x.Status).Returns(() => status);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("encryptionService", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(null!, mockNavigation.Object, testText.Sync)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(mockEncryption.Object, null!, testText.Sync)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(mockEncryption.Object, mockNavigation.Object, null!)).ParamName);
    }

    [Theory]
    [InlineData(E2eStatus.NeedsSetup, true)]
    [InlineData(E2eStatus.Unlocked, false)]
    [InlineData(E2eStatus.Disabled, false)]
    [InlineData(E2eStatus.Unsupported, false)]
    [InlineData(E2eStatus.Unknown, false)]
    public void GivenStatus_WhenGettingVisibility_ThenShownOnlyWhileLocked(E2eStatus current, bool visible)
    {
        // Arrange
        status = current;

        // Act
        using var sut = CreateSut();

        // Assert
        Assert.Equal(visible, sut.IsVisible);
        Assert.Equal(visible, sut.CanSetUp);
        Assert.Equal(visible, sut.Message.Length > 0);
    }

    [Fact]
    public void GivenTheBanner_WhenOpenSettings_ThenNavigatesToSettings()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        sut.OpenSettings();

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/settings"), Times.Once);
    }

    [Fact]
    public void GivenStatusChanges_WhenRaised_ThenVisibilityNotified()
    {
        // Arrange
        using var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        status = E2eStatus.NeedsSetup;

        // Act
        mockEncryption.Raise(x => x.StatusChanged += null, mockEncryption.Object, E2eStatus.NeedsSetup);

        // Assert
        Assert.Contains(nameof(E2eBannerViewModel.IsVisible), changed);
        Assert.Contains(nameof(E2eBannerViewModel.Message), changed);
    }

    private E2eBannerViewModel CreateSut() => new(mockEncryption.Object, mockNavigation.Object, testText.Sync);
}
