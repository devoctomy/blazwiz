using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class E2eBannerViewModelTests
{
    private readonly Mock<ISyncEncryptionService> mockEncryption = new();
    private readonly Mock<IPasskeyUnlockService> mockPasskey = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private E2eStatus status = E2eStatus.Blocked;
    private bool hasPasskeyKeys;

    public E2eBannerViewModelTests()
    {
        mockEncryption.SetupGet(x => x.Status).Returns(() => status);
        mockEncryption.SetupGet(x => x.HasPasskeyKeys).Returns(() => hasPasskeyKeys);
        mockPasskey.Setup(x => x.CanUnlockAsync()).ReturnsAsync(true);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("encryptionService", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(null!, mockPasskey.Object, mockScheduler.Object, mockNavigation.Object)).ParamName);
        Assert.Equal("passkeyUnlockService", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(mockEncryption.Object, null!, mockScheduler.Object, mockNavigation.Object)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(mockEncryption.Object, mockPasskey.Object, null!, mockNavigation.Object)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(mockEncryption.Object, mockPasskey.Object, mockScheduler.Object, null!)).ParamName);
    }

    [Theory]
    [InlineData(E2eStatus.Blocked, true)]
    [InlineData(E2eStatus.NeedsSetup, true)]
    [InlineData(E2eStatus.DisabledElsewhere, true)]
    [InlineData(E2eStatus.Unlocked, false)]
    [InlineData(E2eStatus.Disabled, false)]
    [InlineData(E2eStatus.Unsupported, false)]
    [InlineData(E2eStatus.Unknown, false)]
    public void GivenStatus_WhenGettingIsVisible_ThenShownOnlyWhenTheUserIsNeeded(E2eStatus current, bool expected)
    {
        status = current;
        var sut = CreateSut();
        Assert.Equal(expected, sut.IsVisible);
        Assert.Equal(expected, !string.IsNullOrEmpty(sut.Message));
    }

    [Fact]
    public async Task GivenBlockedWithSavedPasskey_WhenInitialized_ThenOffersPasskeyUnlockAndExplainsPrf()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.CanUnlockWithPasskey);
        Assert.Contains("PRF", sut.Message);
        Assert.False(sut.CanSetUp);
    }

    [Fact]
    public async Task GivenUnlocks_WhenUnlockWithPasskey_ThenInitializesAndSyncs()
    {
        // Arrange
        mockPasskey.Setup(x => x.UnlockAsync()).ReturnsAsync(true);
        mockEncryption.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(E2eStatus.Unlocked);
        var sut = CreateSut();

        // Act
        await sut.UnlockWithPasskeyAsync();

        // Assert
        mockEncryption.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Once);
        Assert.Null(sut.Error);
    }

    [Fact]
    public async Task GivenUnlockedButNeedsSetup_WhenUnlockWithPasskey_ThenNoSync()
    {
        mockPasskey.Setup(x => x.UnlockAsync()).ReturnsAsync(true);
        mockEncryption.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(E2eStatus.NeedsSetup);
        await CreateSut().UnlockWithPasskeyAsync();
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenPasskeyNotUsed_WhenUnlockWithPasskey_ThenErrorAndNotInitialized()
    {
        // Arrange
        mockPasskey.Setup(x => x.UnlockAsync()).ReturnsAsync(false);
        var sut = CreateSut();

        // Act
        await sut.UnlockWithPasskeyAsync();

        // Assert
        Assert.NotNull(sut.Error);
        mockEncryption.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenNeedsSetupWithPasskeyKeys_WhenGettingActions_ThenOffersSetUpInSettings()
    {
        // Arrange
        status = E2eStatus.NeedsSetup;
        hasPasskeyKeys = true;
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        sut.OpenSettings();

        // Assert
        Assert.True(sut.CanSetUp);
        Assert.False(sut.CanUnlockWithPasskey);
        mockNavigation.Verify(x => x.NavigateTo("/settings"), Times.Once);
    }

    [Fact]
    public async Task GivenDisabledElsewhere_WhenResolve_ThenServiceResolvesAndDialogCloses()
    {
        // Arrange
        status = E2eStatus.DisabledElsewhere;
        hasPasskeyKeys = true;
        var sut = CreateSut();
        sut.OpenResolveDialog();

        // Act
        await sut.ResolveAsync();

        // Assert
        Assert.True(sut.CanResolve);
        mockEncryption.Verify(x => x.ResolveDisabledElsewhereAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.IsResolveDialogOpen);
    }

    [Fact]
    public async Task GivenResolveFails_WhenResolve_ThenErrorAndDialogStaysOpen()
    {
        // Arrange
        mockEncryption.Setup(x => x.ResolveDisabledElsewhereAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("no keys"));
        var sut = CreateSut();
        sut.OpenResolveDialog();

        // Act
        await sut.ResolveAsync();

        // Assert
        Assert.Equal("no keys", sut.Error);
        Assert.True(sut.IsResolveDialogOpen);
        sut.CloseResolveDialog();
        Assert.False(sut.IsResolveDialogOpen);
    }

    [Fact]
    public void GivenStatusChanges_WhenRaised_ThenVisibilityNotifiedUntilDisposed()
    {
        // Arrange
        var sut = CreateSut();
        var changed = 0;
        sut.PropertyChanged += (_, e) => changed += e.PropertyName == nameof(E2eBannerViewModel.IsVisible) ? 1 : 0;

        // Act
        mockEncryption.Raise(x => x.StatusChanged += null, mockEncryption.Object, E2eStatus.Unlocked);
        sut.Dispose();
        mockEncryption.Raise(x => x.StatusChanged += null, mockEncryption.Object, E2eStatus.Blocked);

        // Assert
        Assert.Equal(1, changed);
    }

    private E2eBannerViewModel CreateSut() => new(mockEncryption.Object, mockPasskey.Object, mockScheduler.Object, mockNavigation.Object);
}
