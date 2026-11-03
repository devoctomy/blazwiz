using blazwiz.Portal.Application.Shell;

namespace blazwiz.Portal.Application.UnitTests.Shell;

public class ShellRegistriesTests
{
    [Fact]
    public void GivenNullRegistrations_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("registrations", Assert.Throws<ArgumentNullException>(() => new ShellRegistry(null!)).ParamName);
        Assert.Equal("registrations", Assert.Throws<ArgumentNullException>(() => new SettingsSectionRegistry(null!)).ParamName);
        Assert.Equal("registrations", Assert.Throws<ArgumentNullException>(() => new PageRegistry(null!)).ParamName);
    }

    [Fact]
    public void GivenComponentsInSeveralRegions_WhenGetComponents_ThenOnlyThatRegionsInOrder()
    {
        // Arrange
        var sut = new ShellRegistry(
        [
            new ShellComponentRegistration(ShellRegion.Banners, typeof(int), 20),
            new ShellComponentRegistration(ShellRegion.Floating, typeof(bool), 0),
            new ShellComponentRegistration(ShellRegion.Banners, typeof(string), 10),
        ]);

        // Act & Assert
        Assert.Equal([typeof(string), typeof(int)], sut.GetComponents(ShellRegion.Banners));
        Assert.Equal([typeof(bool)], sut.GetComponents(ShellRegion.Floating));
        Assert.Empty(sut.GetComponents(ShellRegion.Overlays));
    }

    [Fact]
    public void GivenSections_WhenGetSections_ThenInOrder()
    {
        // Arrange
        var sut = new SettingsSectionRegistry(
        [
            new SettingsSectionRegistration(typeof(int), 10_000),
            new SettingsSectionRegistration(typeof(string), 100),
        ]);

        // Act & Assert
        Assert.Equal([typeof(string), typeof(int)], sut.GetSections());
    }

    [Fact]
    public void GivenPageReplaced_WhenGetComponent_ThenLastRegistrationWins()
    {
        // Arrange
        var sut = new PageRegistry(
        [
            new PageRegistration(BlazwizPages.Login, typeof(string)),
            new PageRegistration(BlazwizPages.Login, typeof(int)),
        ]);

        // Act & Assert
        Assert.Equal(typeof(int), sut.GetComponent(BlazwizPages.Login));
        Assert.True(sut.IsEnabled(BlazwizPages.Login));
    }

    [Fact]
    public void GivenPageTurnedOff_WhenGetComponent_ThenNullAndNotEnabled()
    {
        // Arrange
        var sut = new PageRegistry(
        [
            new PageRegistration(BlazwizPages.Sync, typeof(string)),
            new PageRegistration(BlazwizPages.Sync, null),
        ]);

        // Act & Assert
        Assert.Null(sut.GetComponent(BlazwizPages.Sync));
        Assert.False(sut.IsEnabled(BlazwizPages.Sync));
    }

    [Fact]
    public void GivenPageNeverAdded_WhenGetComponent_ThenNullAndNotEnabled()
    {
        var sut = new PageRegistry([]);

        Assert.Null(sut.GetComponent(BlazwizPages.Profile));
        Assert.False(sut.IsEnabled(BlazwizPages.Profile));
    }

    [Fact]
    public void GivenBlankKey_WhenRegisteringOrLookingUp_ThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PageRegistration(" ", typeof(string)));
        Assert.Throws<ArgumentException>(() => new PageRegistry([]).GetComponent(string.Empty));
    }

    [Fact]
    public void GivenNullComponentTypes_WhenRegistering_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ShellComponentRegistration(ShellRegion.Banners, null!, 0));
        Assert.Throws<ArgumentNullException>(() => new SettingsSectionRegistration(null!, 0));
        Assert.Throws<ArgumentNullException>(() => new SignInOptionRegistration(null!, typeof(string)));
        Assert.Throws<ArgumentNullException>(() => new SignInOptionRegistration(typeof(string), null!));
    }

    [Fact]
    public void GivenContentService_WhenRequestReload_ThenRaised()
    {
        // Arrange
        var sut = new ShellContentService();
        var raised = 0;
        sut.ReloadRequested += (_, _) => raised++;

        // Act
        sut.RequestReload();

        // Assert
        Assert.Equal(1, raised);
    }
}
