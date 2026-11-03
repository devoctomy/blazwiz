using blazwiz.Portal.Application.Extensions;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Shell;
using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Portal.Application.UnitTests.Extensions;

public class ShellServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenCallingAnyExtension_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ShellServiceCollectionExtensions.AddBlazwizShell(null!));
        Assert.Throws<ArgumentNullException>(() => ShellServiceCollectionExtensions.AddShellComponent<FakeComponent>(null!, ShellRegion.Banners));
        Assert.Throws<ArgumentNullException>(() => ShellServiceCollectionExtensions.AddSettingsSection<FakeComponent>(null!, 0));
        Assert.Throws<ArgumentNullException>(() => ShellServiceCollectionExtensions.AddNavigationItem(null!, new NavigationItem()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddNavigationItem(null!));
        Assert.Throws<ArgumentNullException>(() => ShellServiceCollectionExtensions.AddSessionLifecycleHandler<FakeHandler>(null!));
        Assert.Throws<ArgumentNullException>(() => ShellServiceCollectionExtensions.AddSignInOption<FakeOption, FakeComponent>(null!));
        Assert.Throws<ArgumentNullException>(() => ShellServiceCollectionExtensions.ReplacePage<FakeComponent>(null!, BlazwizPages.Login));
        Assert.Throws<ArgumentNullException>(() => ShellServiceCollectionExtensions.DisablePage(null!, BlazwizPages.Login));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().ReplacePage<FakeComponent>(" "));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().DisablePage(string.Empty));
    }

    [Fact]
    public void GivenModulesAndApp_WhenTheyAddToTheExtensionPoints_ThenEveryRegistryReflectsThem()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBlazwizShell();
        services.AddBlazwizShell();

        // Act
        services.AddShellComponent<FakeComponent>(ShellRegion.Overlays, order: 5);
        services.AddSettingsSection<FakeComponent>(order: 50);
        services.AddNavigationItem(new NavigationItem { Text = "Mine", PageKey = BlazwizPages.Sync });
        services.ReplacePage<FakeComponent>(BlazwizPages.Sync);
        services.ReplacePage<OtherComponent>(BlazwizPages.Login);
        services.DisablePage(BlazwizPages.Login);
        services.AddSessionLifecycleHandler<FakeHandler>();
        services.AddSessionLifecycleHandler<FakeHandler>();
        services.AddSignInOption<FakeOption, FakeComponent>();

        // Assert
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        Assert.Equal([typeof(FakeComponent)], scope.ServiceProvider.GetRequiredService<IShellRegistry>().GetComponents(ShellRegion.Overlays));
        Assert.Equal([typeof(FakeComponent)], scope.ServiceProvider.GetRequiredService<ISettingsSectionRegistry>().GetSections());
        Assert.Equal(typeof(FakeComponent), scope.ServiceProvider.GetRequiredService<IPageRegistry>().GetComponent(BlazwizPages.Sync));
        Assert.False(scope.ServiceProvider.GetRequiredService<IPageRegistry>().IsEnabled(BlazwizPages.Login));
        Assert.Equal("Mine", Assert.Single(scope.ServiceProvider.GetRequiredService<INavigationMenuService>().GetMainItems()).Text);
        Assert.IsType<FakeHandler>(Assert.Single(scope.ServiceProvider.GetServices<ISessionLifecycleHandler>()));
        Assert.Same(scope.ServiceProvider.GetRequiredService<FakeOption>(), Assert.Single(scope.ServiceProvider.GetServices<ISignInOption>()));
        var option = Assert.Single(scope.ServiceProvider.GetServices<SignInOptionRegistration>());
        Assert.Equal(typeof(FakeOption), option.OptionType);
        Assert.Equal(typeof(FakeComponent), option.ComponentType);
    }

    private sealed class FakeComponent : ComponentBase
    {
    }

    private sealed class OtherComponent : ComponentBase
    {
    }

    private sealed class FakeHandler : ISessionLifecycleHandler
    {
        public Task OnShellLoadingAsync(UserSession session) => Task.CompletedTask;
    }

    private sealed class FakeOption : ISignInOption
    {
        public bool IsSet => false;

        public Task LoadAsync() => Task.CompletedTask;

        public Task ApplyAsync() => Task.CompletedTask;
    }
}
