using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Notes;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class IndexedDbNotesStateStoreTests
{
    private readonly Mock<IIndexedDbClient> mockClient = new();

    [Fact]
    public void GivenNullClient_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => new IndexedDbNotesStateStore(null!)).ParamName);
    }

    [Fact]
    public void GivenStore_WhenDescribed_ThenStateStoreOfNotesDatabaseKeyedByOwner()
    {
        Assert.Equal(IndexedDbNotesStateStore.Database, IndexedDbNotesStateStore.Store.Database);
        Assert.EndsWith("_notes", IndexedDbNotesStateStore.Database, StringComparison.Ordinal);
        Assert.Equal("state", IndexedDbNotesStateStore.Store.Name);
        Assert.Equal(["owner"], IndexedDbNotesStateStore.Store.KeyPath);
    }

    [Fact]
    public async Task GivenOwner_WhenGetAsync_ThenReadsByOwner()
    {
        // Arrange
        var state = new NotesState { Owner = "bob", WelcomeNoteCreated = true };
        mockClient.Setup(x => x.GetAsync<NotesState>(IndexedDbNotesStateStore.Store, "bob")).ReturnsAsync(state);

        // Act & Assert
        Assert.Same(state, await new IndexedDbNotesStateStore(mockClient.Object).GetAsync("bob"));
    }

    [Fact]
    public async Task GivenState_WhenSaveAsync_ThenPutsIt()
    {
        // Arrange
        var state = new NotesState { Owner = "bob" };

        // Act
        await new IndexedDbNotesStateStore(mockClient.Object).SaveAsync(state);

        // Assert
        mockClient.Verify(x => x.PutAsync(IndexedDbNotesStateStore.Store, state), Times.Once);
    }

    [Fact]
    public async Task GivenInvalidArguments_WhenCalled_ThenThrows()
    {
        var sut = new IndexedDbNotesStateStore(mockClient.Object);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.GetAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
    }
}
