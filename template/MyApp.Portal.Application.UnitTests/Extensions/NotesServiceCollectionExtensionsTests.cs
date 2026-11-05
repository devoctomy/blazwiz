using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Sync.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using MyApp.Portal.Application.Extensions;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Application.Sync;
using MyApp.Portal.Application.ViewModels;

namespace MyApp.Portal.Application.UnitTests.Extensions;

public class NotesServiceCollectionExtensionsTests
{
    [Fact]
    public void GivenNullServices_WhenAdding_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => NotesServiceCollectionExtensions.AddNotes(null!));
    }

    [Fact]
    public async Task GivenTheApp_WhenNotesAdded_ThenServicesAndViewModelsAreResolvable()
    {
        // Arrange: the framework, as Program.cs adds it.
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton(Mock.Of<NavigationManager>());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Mock.Of<INoteEditorInteropService>());
        services.AddBlazwizApp(AppIdentity.Name, AppIdentity.Id);
        services.AddBlazwizUi(configuration);

        // Act
        services.AddBlazwizSyncModule(configuration, sync => sync.AddProfilesFromAssemblyContaining<NotesSyncProfile>());
        var result = services.AddNotes();
        services.AddAppServices();
        services.AddScoped<FilesViewModel>();
        services.AddScoped<NoteEditorViewModel>();
        services.AddScoped<NotebooksViewModel>();

        // Assert
        Assert.Same(services, result);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal([NoteService.ProfileName], scope.ServiceProvider.GetRequiredService<ISyncService>().Profiles);
        Assert.IsType<IndexedDbNotesStateStore>(scope.ServiceProvider.GetRequiredService<INotesStateStore>());
        Assert.IsType<NoteServiceFactory>(scope.ServiceProvider.GetRequiredService<INoteServiceFactory>());
        Assert.IsType<NotebookService>(scope.ServiceProvider.GetRequiredService<INotebookService>());
        Assert.IsType<NoteNameService>(scope.ServiceProvider.GetRequiredService<INoteNameService>());
        Assert.IsType<NoteTreeService>(scope.ServiceProvider.GetRequiredService<INoteTreeService>());
        Assert.IsType<MarkdownRenderer>(scope.ServiceProvider.GetRequiredService<IMarkdownRenderer>());
        Assert.IsType<MarkdownFormatService>(scope.ServiceProvider.GetRequiredService<IMarkdownFormatService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<FilesViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<NoteEditorViewModel>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<NotebooksViewModel>());
        Assert.Contains(scope.ServiceProvider.GetServices<NavigationItem>(), i => i.Href == "notebooks");
    }
}
