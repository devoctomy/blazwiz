using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Events.Channels;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.Events.Extensions;
using Devoctomy.Blazwiz.Events.Sessions;
using Devoctomy.Blazwiz.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Devoctomy.Blazwiz.Events.UnitTests.Extensions;

public class EventsServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAddBlazwizEvents_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => EventsServiceCollectionExtensions.AddBlazwizEvents(null!));
    }

    [Fact]
    public async Task GivenApp_WhenAddBlazwizEvents_ThenTheLocalChannelAndHandlersAreRegistered()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        var result = services.AddBlazwizEvents();

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<LocalUserEventChannel>(scope.ServiceProvider.GetRequiredService<IUserEventChannel>());
        Assert.Contains(scope.ServiceProvider.GetServices<ISessionLifecycleHandler>(), h => h is UserEventsSessionHandler);
        Assert.IsType<UserEventsSignOutHandler>(Assert.Single(scope.ServiceProvider.GetServices<ISignOutEverywhereHandler>()));
        Assert.True(scope.ServiceProvider.GetRequiredService<ISignOutEverywhereNotifier>().HasHandlers);
    }

    [Fact]
    public async Task GivenAChannelAddedFirst_WhenAddBlazwizEvents_ThenTheFirstChannelWins_AndHandlersAreAddedOnce()
    {
        // Arrange
        var services = TestServices.Create();
        services.AddScoped(_ => Mock.Of<IUserEventChannel>());

        // Act
        services.AddBlazwizEvents();
        services.AddBlazwizEvents();

        // Assert
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsNotType<LocalUserEventChannel>(scope.ServiceProvider.GetRequiredService<IUserEventChannel>());
        Assert.Single(scope.ServiceProvider.GetServices<ISignOutEverywhereHandler>());
        Assert.Single(scope.ServiceProvider.GetServices<ISessionLifecycleHandler>(), h => h is UserEventsSessionHandler);
    }
}
