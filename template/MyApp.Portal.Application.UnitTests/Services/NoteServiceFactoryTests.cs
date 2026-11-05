using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.UnitTests.Services;

public class NoteServiceFactoryTests
{
    private readonly Mock<ISyncProfileRegistry> mockRegistry = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<INotesStateStore> mockState = new();
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<INoteNameService> mockNames = new();
    private readonly Mock<INoteTreeService> mockTree = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new NoteServiceFactory(null!, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, mockTree.Object, TimeProvider.System)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new NoteServiceFactory(mockRegistry.Object, null!, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, mockTree.Object, TimeProvider.System)).ParamName);
        Assert.Equal("stateStore", Assert.Throws<ArgumentNullException>(() => new NoteServiceFactory(mockRegistry.Object, mockAuth.Object, null!, mockSync.Object, mockConnectivity.Object, mockNames.Object, mockTree.Object, TimeProvider.System)).ParamName);
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new NoteServiceFactory(mockRegistry.Object, mockAuth.Object, mockState.Object, null!, mockConnectivity.Object, mockNames.Object, mockTree.Object, TimeProvider.System)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new NoteServiceFactory(mockRegistry.Object, mockAuth.Object, mockState.Object, mockSync.Object, null!, mockNames.Object, mockTree.Object, TimeProvider.System)).ParamName);
        Assert.Equal("noteNameService", Assert.Throws<ArgumentNullException>(() => new NoteServiceFactory(mockRegistry.Object, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, null!, mockTree.Object, TimeProvider.System)).ParamName);
        Assert.Equal("noteTreeService", Assert.Throws<ArgumentNullException>(() => new NoteServiceFactory(mockRegistry.Object, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, null!, TimeProvider.System)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new NoteServiceFactory(mockRegistry.Object, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, mockTree.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenANotebook_WhenCreated_ThenTheServiceUsesItsFileStore()
    {
        // Arrange
        var notebook = new Notebook { Name = "nb1", Id = "nb1", Root = "profiles/nb1/" };
        var store = new Mock<ISyncFileStore>();
        store.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        mockRegistry.Setup(x => x.GetFileStore("nb1")).Returns(store.Object);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new Devoctomy.Blazwiz.Auth.Models.UserSession { Username = "bob" });
        mockTree.Setup(x => x.BuildEntries("profiles/nb1/", It.IsAny<IEnumerable<Devoctomy.Blazwiz.Sync.Models.SyncFileInfo>>(), "bob")).Returns([]);
        var sut = new NoteServiceFactory(mockRegistry.Object, mockAuth.Object, mockState.Object, mockSync.Object, mockConnectivity.Object, mockNames.Object, mockTree.Object, TimeProvider.System);

        // Act
        var entries = await sut.Create(notebook).GetEntriesAsync();

        // Assert
        Assert.Empty(entries);
        store.Verify(x => x.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Throws<ArgumentNullException>(() => sut.Create(null!));
    }
}
