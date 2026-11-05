using System.Text;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Moq;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services;

namespace MyApp.Portal.Application.UnitTests.Services;

public class NotebookServiceTests
{
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<ISyncProfileRegistry> mockRegistry = new();
    private readonly Mock<ISyncProfileDisplayNames> mockDisplayNames = new();
    private readonly List<SyncProtectedProfile> profiles = [];
    private readonly HashSet<E2eKeyScope> unlocked = [];
    private readonly Dictionary<string, Dictionary<string, byte[]>> stored = [];

    public NotebookServiceTests()
    {
        mockProfiles.SetupGet(x => x.Profiles).Returns(() => profiles);
        mockManager.Setup(x => x.IsUnlocked(It.IsAny<E2eKeyScope>())).Returns((E2eKeyScope s) => unlocked.Contains(s));
        mockDisplayNames.Setup(x => x.GetDisplayName(NoteService.ProfileName)).Returns("Notes");
        mockRegistry.Setup(x => x.GetFileStore(It.IsAny<string>())).Returns((string name) => Store(name));
        Add(NoteService.ProfileName, isCreatedByUser: false, isSetUp: false);
        Add("photos", isCreatedByUser: false, isSetUp: true);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new NotebookService(null!, mockManager.Object, mockRegistry.Object, mockDisplayNames.Object)).ParamName);
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new NotebookService(mockProfiles.Object, null!, mockRegistry.Object, mockDisplayNames.Object)).ParamName);
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new NotebookService(mockProfiles.Object, mockManager.Object, null!, mockDisplayNames.Object)).ParamName);
        Assert.Equal("displayNames", Assert.Throws<ArgumentNullException>(() => new NotebookService(mockProfiles.Object, mockManager.Object, mockRegistry.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenProfiles_WhenGettingNotebooks_ThenTheDefaultFirstThenThoseCreatedNamedWhenUnlocked()
    {
        // Arrange
        var unlockedId = Add(Guid.NewGuid().ToString("D"), isCreatedByUser: true, isSetUp: true);
        var lockedId = Add(Guid.NewGuid().ToString("D"), isCreatedByUser: true, isSetUp: true);
        unlocked.Add(new E2eKeyScope(unlockedId));
        await Store(unlockedId).WriteAsync($"profiles/{unlockedId}/.notebook", Encoding.UTF8.GetBytes("""{"Title":"Work"}"""));

        // Act
        var notebooks = await CreateSut().GetNotebooksAsync();

        // Assert
        Assert.Collection(
            notebooks,
            n =>
            {
                Assert.Equal(NoteService.ProfileName, n.Name);
                Assert.Equal("Notes", n.Title);
                Assert.Equal("notes/", n.Root);
                Assert.True(n.IsDefault);
                Assert.False(n.HasPassphrase);
            },
            n =>
            {
                Assert.Equal("Work", n.Title);
                Assert.Equal($"profiles/{unlockedId}/", n.Root);
                Assert.True(n.IsUnlocked);
                Assert.False(n.IsDefault);
            },
            n =>
            {
                Assert.Equal(lockedId, n.Title);
                Assert.False(n.IsUnlocked);
            });
        mockProfiles.Verify(x => x.SetLabelAsync(new E2eKeyScope(unlockedId), "Work"), Times.Once);
    }

    [Fact]
    public async Task GivenALockedNotebookLabelledOnThisDevice_WhenFound_ThenItsLabelIsShown()
    {
        // Arrange
        var id = Add(Guid.NewGuid().ToString("D"), isCreatedByUser: true, isSetUp: true);
        profiles[^1].Label = "Work";

        // Act
        var notebook = await CreateSut().FindAsync(id);

        // Assert
        Assert.Equal("Work", notebook!.Title);
        mockProfiles.Verify(x => x.SetLabelAsync(It.IsAny<E2eKeyScope>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenAnUnlockedNotebookWithoutItsNameYet_WhenGettingNotebooks_ThenItsIdIsShown()
    {
        // Arrange
        var id = Add(Guid.NewGuid().ToString("D"), isCreatedByUser: true, isSetUp: true);
        unlocked.Add(new E2eKeyScope(id));

        // Act
        var notebook = await CreateSut().FindAsync(id);

        // Assert
        Assert.Equal(id, notebook!.Title);
    }

    [Fact]
    public async Task GivenNoName_WhenFinding_ThenTheDefaultNotebook()
    {
        // Act & Assert
        Assert.Equal(NoteService.ProfileName, (await CreateSut().FindAsync(null))!.Name);
        Assert.Null(await CreateSut().FindAsync("photos"));
        Assert.Null(await CreateSut().FindAsync("gone"));
    }

    [Fact]
    public async Task GivenANameAndPassphrase_WhenCreated_ThenAProtectedProfileWithTheNameInside()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("D");
        mockProfiles.Setup(x => x.CreateAsync("correct horse battery", null, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                Add(id, isCreatedByUser: true, isSetUp: true);
                unlocked.Add(new E2eKeyScope(id));
            })
            .ReturnsAsync(new E2eKeyScope(id));

        // Act
        var notebook = await CreateSut().CreateAsync("  Work  ", "correct horse battery");

        // Assert
        Assert.Equal(id, notebook.Name);
        Assert.Equal("Work", notebook.Title);
        Assert.Contains($"profiles/{id}/.notebook", stored[id].Keys);
        mockProfiles.Verify(x => x.SetLabelAsync(new E2eKeyScope(id), "Work"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GivenNoName_WhenCreated_ThenThrowsAndNothingIsCreated()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotesException>(() => CreateSut().CreateAsync(" ", "correct horse battery"));
        mockProfiles.Verify(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private string Add(string name, bool isCreatedByUser, bool isSetUp)
    {
        var id = isCreatedByUser ? name : Guid.NewGuid().ToString("D");
        profiles.Add(new SyncProtectedProfile { Name = name, Scope = new E2eKeyScope(id), IsCreatedByUser = isCreatedByUser, IsSetUp = isSetUp });
        return id;
    }

    private ISyncFileStore Store(string name)
    {
        if (!stored.TryGetValue(name, out var files))
        {
            files = [];
            stored[name] = files;
        }

        var store = new Mock<ISyncFileStore>();
        store.Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => files.TryGetValue(key, out var content) ? new SyncFile { Key = key, Content = content } : null);
        store.Setup(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DateTimeOffset?, CancellationToken>((key, content, _, _) => files[key] = content)
            .Returns(Task.CompletedTask);
        return store.Object;
    }

    private NotebookService CreateSut() => new(mockProfiles.Object, mockManager.Object, mockRegistry.Object, mockDisplayNames.Object);
}
