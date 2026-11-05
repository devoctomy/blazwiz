using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Microsoft.Extensions.Localization;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class SyncProfileDisplayNamesTests
{
    private readonly Mock<ISyncProfileRegistry> mockRegistry = new();
    private readonly Mock<IStringLocalizerFactory> mockLocalizerFactory = new();
    private readonly Mock<IStringLocalizer> mockLocalizer = new();
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly List<SyncProtectedProfile> profiles = [];

    public SyncProfileDisplayNamesTests()
    {
        mockLocalizerFactory.Setup(x => x.Create(typeof(SyncUiText))).Returns(mockLocalizer.Object);
        mockProfiles.SetupGet(x => x.Profiles).Returns(() => profiles);
        mockLocalizer.Setup(x => x["Sync.Profile.Files"]).Returns(new LocalizedString("Sync.Profile.Files", "Notes"));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new SyncProfileDisplayNames(null!, mockProfiles.Object, mockLocalizerFactory.Object)).ParamName);
        Assert.Equal("localizerFactory", Assert.Throws<ArgumentNullException>(() => new SyncProfileDisplayNames(mockRegistry.Object, mockProfiles.Object, null!)).ParamName);
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new SyncProfileDisplayNames(mockRegistry.Object, null!, mockLocalizerFactory.Object)).ParamName);
    }

    [Fact]
    public void GivenAProfileWithDisplayText_WhenGettingItsDisplayName_ThenItComesFromItsResources()
    {
        // Arrange
        var profile = new SyncProfileOptions { Name = "notes", DisplayText = "Sync.Profile.Files", DisplayTextResource = typeof(SyncUiText) };
        mockRegistry.Setup(x => x.Find("notes")).Returns(profile);

        // Act
        var name = CreateSut().GetDisplayName("notes");

        // Assert
        Assert.Equal("Notes", name);
    }

    [Fact]
    public void GivenAProfileWithoutDisplayText_WhenGettingItsDisplayName_ThenItsNameIsUsed()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("D");
        mockRegistry.Setup(x => x.Find(id)).Returns(new SyncProfileOptions { Name = id });

        // Act
        var name = CreateSut().GetDisplayName(id);

        // Assert
        Assert.Equal(id, name);
    }

    [Fact]
    public void GivenAProfileTheUserCreatedWithALabelHere_WhenGettingItsDisplayName_ThenItsLabelIsUsed()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("D");
        mockRegistry.Setup(x => x.Find(id)).Returns(new SyncProfileOptions { Name = id });
        profiles.Add(new SyncProtectedProfile { Name = id, Scope = new E2eKeyScope(id), IsCreatedByUser = true, Label = "Work" });

        // Act
        var name = CreateSut().GetDisplayName(id);

        // Assert
        Assert.Equal("Work", name);
    }

    [Fact]
    public void GivenAnUnknownProfile_WhenGettingItsDisplayName_ThenItsNameIsUsed()
    {
        // Act
        var name = CreateSut().GetDisplayName("gone");

        // Assert
        Assert.Equal("gone", name);
    }

    private SyncProfileDisplayNames CreateSut() => new(mockRegistry.Object, mockProfiles.Object, mockLocalizerFactory.Object);
}
