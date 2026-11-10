using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Events.AppSync.Channels;
using Devoctomy.Blazwiz.Events.AppSync.Extensions;
using Devoctomy.Blazwiz.Events.AppSync.Models;
using Devoctomy.Blazwiz.Events.AppSync.Sessions;
using Devoctomy.Blazwiz.Events.Channels;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Events.AppSync.UnitTests.Extensions;

public class AppSyncEventsServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullArguments_WhenAddBlazwizAppSyncEvents_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => AppSyncEventsServiceCollectionExtensions.AddBlazwizAppSyncEvents(null!, Configuration(), false));
        Assert.Throws<ArgumentNullException>(() => TestServices.Create().AddBlazwizAppSyncEvents(null!, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GivenEndpoints_WhenAddBlazwizAppSyncEvents_ThenTheAppSyncChannelIsUsed(bool allowLocalChannel)
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        services.AddBlazwizAppSyncEvents(Configuration(("Events:HttpEndpoint", "abc.appsync-api.eu-west-2.amazonaws.com"), ("Events:RealtimeEndpoint", "abc.appsync-realtime-api.eu-west-2.amazonaws.com")), allowLocalChannel);

        // Assert
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<AppSyncUserEventChannel>(scope.ServiceProvider.GetRequiredService<IUserEventChannel>());
        Assert.Equal("abc.appsync-api.eu-west-2.amazonaws.com", scope.ServiceProvider.GetRequiredService<AppSyncEventsConfig>().HttpEndpoint);
        Assert.True(scope.ServiceProvider.GetRequiredService<ISignOutEverywhereNotifier>().HasHandlers);
    }

    [Fact]
    public async Task GivenNoEndpointsInDevelopment_WhenAddBlazwizAppSyncEvents_ThenTheLocalChannelStandsIn()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        services.AddBlazwizAppSyncEvents(Configuration(("Events:HttpEndpoint", "abc.appsync-api.eu-west-2.amazonaws.com")), allowLocalChannel: true);

        // Assert
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<LocalUserEventChannel>(scope.ServiceProvider.GetRequiredService<IUserEventChannel>());
        Assert.True(scope.ServiceProvider.GetRequiredService<ISignOutEverywhereNotifier>().HasHandlers);
    }

    [Fact]
    public async Task GivenNoEndpointsDeployed_WhenAddBlazwizAppSyncEvents_ThenNoChannel_NoSignOutEverywhereHandler_AndAWarning()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        services.AddBlazwizAppSyncEvents(Configuration(), allowLocalChannel: false);

        // Assert
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.Null(scope.ServiceProvider.GetService<IUserEventChannel>());
        Assert.False(scope.ServiceProvider.GetRequiredService<ISignOutEverywhereNotifier>().HasHandlers);
        Assert.Contains(scope.ServiceProvider.GetServices<ISessionLifecycleHandler>(), h => h is MissingEventsConfigurationWarning);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("a", " ")]
    [InlineData(" ", "b")]
    public void GivenMissingEndpoint_WhenIsConfigured_ThenFalse(string http, string realtime)
    {
        // Act
        var result = AppSyncEventsServiceCollectionExtensions.IsConfigured(new AppSyncEventsConfig { HttpEndpoint = http, RealtimeEndpoint = realtime });

        // Assert
        Assert.False(result);
        Assert.False(AppSyncEventsServiceCollectionExtensions.IsConfigured(null));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
}
