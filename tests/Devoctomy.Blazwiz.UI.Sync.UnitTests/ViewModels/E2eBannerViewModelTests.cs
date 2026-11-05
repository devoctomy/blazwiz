using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class E2eBannerViewModelTests
{
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<ISyncProfileDisplayNames> mockDisplayNames = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly TestText testText = new();
    private readonly List<SyncProtectedProfile> profiles = [];

    public E2eBannerViewModelTests()
    {
        mockProfiles.SetupGet(x => x.Profiles).Returns(() => profiles);
        mockDisplayNames.Setup(x => x.GetDisplayName(It.IsAny<string>())).Returns((string name) => name == "notes" ? "Notes" : name);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(null!, mockDisplayNames.Object, mockNavigation.Object, testText.Sync)).ParamName);
        Assert.Equal("displayNames", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(mockProfiles.Object, null!, mockNavigation.Object, testText.Sync)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(mockProfiles.Object, mockDisplayNames.Object, null!, testText.Sync)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new E2eBannerViewModel(mockProfiles.Object, mockDisplayNames.Object, mockNavigation.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenAProfileInCodeWithoutAPassphrase_WhenShown_ThenTheBannerNamesIt()
    {
        // Arrange
        profiles.Add(Profile("notes", isSetUp: false));

        // Act
        using var sut = CreateSut();

        // Assert
        Assert.True(sut.IsVisible);
        Assert.Equal("Notes needs a passphrase before it can be used.", sut.Message);
    }

    [Fact]
    public void GivenProfilesSetUpOrCreatedByTheUser_WhenShown_ThenNoBanner()
    {
        // Arrange
        profiles.Add(Profile("notes", isSetUp: true));
        profiles.Add(Profile(Guid.NewGuid().ToString("D"), isSetUp: true, isCreatedByUser: true));

        // Act
        using var sut = CreateSut();

        // Assert
        Assert.False(sut.IsVisible);
        Assert.Empty(sut.Message);
    }

    [Fact]
    public void GivenTheBanner_WhenAPassphraseIsSet_ThenItIsHidden()
    {
        // Arrange
        profiles.Add(Profile("notes", isSetUp: false));
        using var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        profiles[0].IsSetUp = true;
        mockProfiles.Raise(x => x.ProfilesChanged += null, mockProfiles.Object, EventArgs.Empty);

        // Assert
        Assert.False(sut.IsVisible);
        Assert.Contains(nameof(E2eBannerViewModel.IsVisible), changed);
    }

    [Fact]
    public void GivenTheBanner_WhenOpenSettings_ThenSettingsAreOpened()
    {
        // Act
        CreateSut().OpenSettingsCommand.Execute(null);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/settings"), Times.Once);
    }

    private static SyncProtectedProfile Profile(string name, bool isSetUp, bool isCreatedByUser = false) => new()
    {
        Name = name,
        Scope = new E2eKeyScope(Guid.NewGuid().ToString("D")),
        IsSetUp = isSetUp,
        IsCreatedByUser = isCreatedByUser,
    };

    private E2eBannerViewModel CreateSut() => new(mockProfiles.Object, mockDisplayNames.Object, mockNavigation.Object, testText.Sync);
}
