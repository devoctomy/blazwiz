using blazwiz.Core.Extensions;
using blazwiz.Core.Greetings;
using blazwiz.Core.Greetings.Interfaces;
using blazwiz.Core.Time;
using blazwiz.Core.Time.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Core.UnitTests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAddBlazwizCoreServices_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddBlazwizCoreServices(null!));
    }

    [Fact]
    public void GivenServiceCollection_WhenAddBlazwizCoreServices_ThenReturnsSameCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddBlazwizCoreServices();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void GivenServiceCollection_WhenAddBlazwizCoreServices_ThenCoreServicesAreResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlazwizCoreServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var systemClock = scope.ServiceProvider.GetRequiredService<ISystemClock>();
        var greetingService = scope.ServiceProvider.GetRequiredService<IGreetingService>();

        // Assert
        Assert.IsType<SystemClock>(systemClock);
        Assert.IsType<GreetingService>(greetingService);
    }
}
