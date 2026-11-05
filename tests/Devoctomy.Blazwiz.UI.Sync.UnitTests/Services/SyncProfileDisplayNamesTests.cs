using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Interfaces;
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

    public SyncProfileDisplayNamesTests()
    {
        mockLocalizerFactory.Setup(x => x.Create(typeof(SyncUiText))).Returns(mockLocalizer.Object);
        mockLocalizer.Setup(x => x["Sync.Profile.Files"]).Returns(new LocalizedString("Sync.Profile.Files", "Notes"));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new SyncProfileDisplayNames(null!, mockLocalizerFactory.Object)).ParamName);
        Assert.Equal("localizerFactory", Assert.Throws<ArgumentNullException>(() => new SyncProfileDisplayNames(mockRegistry.Object, null!)).ParamName);
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
    public void GivenAnUnknownProfile_WhenGettingItsDisplayName_ThenItsNameIsUsed()
    {
        // Act
        var name = CreateSut().GetDisplayName("gone");

        // Assert
        Assert.Equal("gone", name);
    }

    private SyncProfileDisplayNames CreateSut() => new(mockRegistry.Object, mockLocalizerFactory.Object);
}
