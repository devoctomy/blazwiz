using Devoctomy.Blazwiz.UI.Components;
using Devoctomy.Blazwiz.TestSupport;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.UI.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Shell;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Devoctomy.Blazwiz.UI.Text;
using Devoctomy.Blazwiz.UI.ViewModels;
using Devoctomy.Blazwiz.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.UnitTests.Extensions;

public class UiServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullArguments_WhenAddBlazwizUi_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => UiServiceCollectionExtensions.AddBlazwizUi(null!, new ConfigurationBuilder().Build()));
        Assert.Throws<ArgumentNullException>(() => TestServices.Create().AddBlazwizUi(null!));
    }

    [Fact]
    public async Task GivenApp_WhenAddBlazwizUi_ThenShellServicesAndViewModelsAreResolvable()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        var result = services.AddBlazwizUi(new ConfigurationBuilder().Build());

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildValidated();
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<UserSettingsService>(scope.ServiceProvider.GetRequiredService<IUserSettingsService>());
        Assert.IsType<ThemeService>(scope.ServiceProvider.GetRequiredService<IThemeService>());
        Assert.IsType<AppBuildInfo>(scope.ServiceProvider.GetRequiredService<IAppBuildInfo>());
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$", scope.ServiceProvider.GetRequiredService<IAppBuildInfo>().FrameworkVersion);
        Assert.IsType<NavigationMenuService>(scope.ServiceProvider.GetRequiredService<INavigationMenuService>());
        Assert.IsType<ShellRegistry>(scope.ServiceProvider.GetRequiredService<IShellRegistry>());
        Assert.IsType<SettingsSectionRegistry>(scope.ServiceProvider.GetRequiredService<ISettingsSectionRegistry>());
        Assert.IsType<PageRegistry>(scope.ServiceProvider.GetRequiredService<IPageRegistry>());
        Assert.IsType<ShellContentService>(scope.ServiceProvider.GetRequiredService<IShellContentService>());
        Assert.IsType<LocalAuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MainLayoutViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LoginViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SettingsViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ChangePasswordViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ForgotPasswordViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LanguageSettingsViewModel>());
        Assert.IsType<ToastService>(scope.ServiceProvider.GetRequiredService<IToastService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ToastHostViewModel>());
        Assert.Contains(typeof(ToastHost), scope.ServiceProvider.GetRequiredService<IShellRegistry>().GetComponents(ShellRegion.Overlays));
        Assert.IsType<CultureService>(scope.ServiceProvider.GetRequiredService<ICultureService>());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ICultureService>().SupportedCultures);
        Assert.IsType<SessionEndedNoticeService>(scope.ServiceProvider.GetRequiredService<ISessionEndedNoticeService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProfileViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DeviceSettingsViewModel>());
        Assert.Contains(typeof(Components.Settings.DeviceSettingsSection), scope.ServiceProvider.GetRequiredService<ISettingsSectionRegistry>().GetSections());
    }

    [Fact]
    public void GivenApp_WhenAddBlazwizUi_ThenFrameworkPagesAndSettingsItemAreAdded()
    {
        // Arrange
        var services = TestServices.Create();

        // Act
        services.AddBlazwizUi(new ConfigurationBuilder().Build());

        // Assert
        using var provider = services.BuildServiceProvider();
        var pages = provider.GetRequiredService<IPageRegistry>();
        Assert.Equal(typeof(LoginView), pages.GetComponent(BlazwizPages.Login));
        Assert.Equal(typeof(ForgotPasswordView), pages.GetComponent(BlazwizPages.ForgotPassword));
        Assert.Equal(typeof(ProfileView), pages.GetComponent(BlazwizPages.Profile));
        Assert.Equal(typeof(SettingsView), pages.GetComponent(BlazwizPages.Settings));
        Assert.Contains(provider.GetServices<NavigationItem>(), i => i.PageKey == BlazwizPages.Settings);
    }

    [Fact]
    public void GivenSupportedCulturesInConfiguration_WhenAddBlazwizUi_ThenTheAppOffersThem()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Text:SupportedCultures:0"] = "en-GB",
                ["Text:SupportedCultures:1"] = "fr-FR",
            })
            .Build();
        var services = TestServices.Create();

        // Act
        services.AddBlazwizUi(configuration);

        // Assert
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Equal(["en-GB", "fr-FR"], scope.ServiceProvider.GetRequiredService<ICultureService>().SupportedCultures.Select(c => c.Name));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void GivenPseudoLocalizationSetting_WhenAddBlazwizUi_ThenTextIsMarkedOnlyWhenOn(string? setting, bool expected)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [UiServiceCollectionExtensions.PseudoLocalizationSetting] = setting,
            })
            .Build();
        var services = TestServices.Create();

        // Act
        services.AddBlazwizUi(configuration);

        // Assert
        using var provider = services.BuildServiceProvider();
        Assert.Equal(expected, provider.GetRequiredService<TextOptions>().PseudoLocalization);
        var label = provider.GetRequiredService<IStringLocalizer<UiText>>()["Offline.Switch.Label"].Value;
        Assert.Equal(expected, label.StartsWith("[!! ", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenConfiguration_WhenAddBlazwizUi_ThenAppSettingsAreBound()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:ApplicationTitle"] = "My App",
                ["AppSettings:ApplicationDescription"] = "Description",
                ["AppSettings:CopyrightMessage"] = "(c) Me",
            })
            .Build();
        var services = TestServices.Create();

        // Act
        services.AddBlazwizUi(configuration);

        // Assert
        using var provider = services.BuildServiceProvider();
        var appSettings = provider.GetRequiredService<AppSettings>();
        Assert.Equal("My App", appSettings.ApplicationTitle);
        Assert.Equal("Description", appSettings.ApplicationDescription);
        Assert.Equal("(c) Me", appSettings.CopyrightMessage);
    }
}
