using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Testing.UnitTests.Resources;
using Devoctomy.Blazwiz.UI.Sync.Text;

namespace Devoctomy.Blazwiz.Testing.UnitTests;

public class FrameworkTextTests
{
    [Fact]
    public void GivenNullModules_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FrameworkText(null!));
    }

    [Fact]
    public void GivenFrameworkText_WhenReadingUiText_ThenTheFrameworksWording()
    {
        // Arrange
        var text = new FrameworkText();

        // Act & Assert
        Assert.Equal("About", text.Ui["Settings.About.Title"].Value);
    }

    [Fact]
    public void GivenAnOverride_WhenReadingUiText_ThenTheOverride()
    {
        // Arrange
        var text = new FrameworkText();

        // Act
        new TextBuilder(text.Options).Override("Settings.About.Title", "About this app");

        // Assert
        Assert.Equal("About this app", text.Ui["Settings.About.Title"].Value);
    }

    [Fact]
    public void GivenAModule_WhenDescribingItsErrorCode_ThenItsText()
    {
        // Arrange
        var withModule = new FrameworkText(typeof(SyncUiText));
        var withoutModule = new FrameworkText();

        // Act
        var described = withModule.Errors.Describe(SyncErrorCodes.StorageMigrating, []);

        // Assert
        Assert.Equal(withModule.For<SyncUiText>()["Error." + SyncErrorCodes.StorageMigrating].Value, described);
        Assert.NotEqual(described, withoutModule.Errors.Describe(SyncErrorCodes.StorageMigrating, []));
    }

    [Fact]
    public void GivenOverridesOfFrameworkAndModuleText_WhenUnknownOverrideKeys_ThenOnlyTheKeysTheFrameworkDoesNotHave()
    {
        // Arrange
        var text = new FrameworkText(typeof(SyncUiText));

        // Act & Assert
        Assert.Equal(["Renamed.In.An.Upgrade"], text.UnknownOverrideKeys(typeof(TestOverrides)));
    }

    [Fact]
    public void GivenModuleNotIncluded_WhenUnknownOverrideKeys_ThenItsKeysAreUnknown()
    {
        // Arrange
        var text = new FrameworkText();

        // Act & Assert
        Assert.Equal(["Error.E2e.WrongPassphrase", "Renamed.In.An.Upgrade"], text.UnknownOverrideKeys(typeof(TestOverrides)));
    }

    [Fact]
    public void GivenOverridesWithDifferentPlaceholders_WhenMismatchedOverridePlaceholders_ThenThoseKeys()
    {
        // Arrange: Login.PageTitle drops the framework's {0}; ForgotPassword.PageTitle keeps it, worded differently.
        var text = new FrameworkText(typeof(SyncUiText));

        // Act
        var result = text.MismatchedOverridePlaceholders(typeof(TestOverrides));

        // Assert
        Assert.Equal(["Login.PageTitle"], result);
    }

    [Fact]
    public void GivenNullOverrides_WhenMismatchedOverridePlaceholders_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FrameworkText().MismatchedOverridePlaceholders(null!));
    }

    [Fact]
    public void GivenNullOverrides_WhenUnknownOverrideKeys_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FrameworkText().UnknownOverrideKeys(null!));
    }

    [Fact]
    public void GivenAnOverridesClassWithNoResources_WhenChecked_ThenNothingIsUnknownOrMismatched()
    {
        // Arrange: a class no resources are named after, so it has no neutral resource set.
        var text = new FrameworkText(typeof(SyncUiText));

        // Act
        var unknown = text.UnknownOverrideKeys(typeof(FrameworkTextTests));
        var mismatched = text.MismatchedOverridePlaceholders(typeof(FrameworkTextTests));

        // Assert
        Assert.Empty(unknown);
        Assert.Empty(mismatched);
    }

    [Fact]
    public void GivenAModuleWithNoResources_WhenChecked_ThenTheOtherTextIsStillCompared()
    {
        // Arrange: the module adds no keys, so it changes nothing.
        var withEmptyModule = new FrameworkText(typeof(SyncUiText), typeof(FrameworkTextTests));
        var without = new FrameworkText(typeof(SyncUiText));

        // Act
        var unknown = withEmptyModule.UnknownOverrideKeys(typeof(TestOverrides));
        var mismatched = withEmptyModule.MismatchedOverridePlaceholders(typeof(TestOverrides));

        // Assert
        Assert.Equal(without.UnknownOverrideKeys(typeof(TestOverrides)), unknown);
        Assert.Equal(without.MismatchedOverridePlaceholders(typeof(TestOverrides)), mismatched);
    }
}
