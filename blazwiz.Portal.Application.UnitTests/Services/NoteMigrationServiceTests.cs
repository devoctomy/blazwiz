using System.Text;
using blazwiz.Core.Sync.InMemory;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Notes;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class NoteMigrationServiceTests
{
    private const string Owner = "bob";
    private const string Database = "blazwiz_notes";
    private static readonly DateTimeOffset Edited = new(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<IIndexedDbClient> mockClient = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<ISystemClock> mockClock = new();
    private readonly InMemorySyncFileStore files;
    private readonly List<NoteEntry> entries = [];
    private readonly List<LegacyNoteContent> contents = [];

    public NoteMigrationServiceTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        files = new InMemorySyncFileStore(mockUser.Object, mockClock.Object);
        mockClient.Setup(x => x.StoreExistsAsync(Database, NoteMigrationService.EntriesStore)).ReturnsAsync(true);
        mockClient.Setup(x => x.GetAllFromExistingAsync<NoteEntry>(Database, NoteMigrationService.EntriesStore)).ReturnsAsync(() => entries.ToList());
        mockClient.Setup(x => x.GetAllFromExistingAsync<LegacyNoteContent>(Database, NoteMigrationService.ContentStore)).ReturnsAsync(() => contents.ToList());
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var names = new NoteNameService();
        var tree = new NoteTreeService();
        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => new NoteMigrationService(null!, files, names, tree)).ParamName);
        Assert.Equal("fileStore", Assert.Throws<ArgumentNullException>(() => new NoteMigrationService(mockClient.Object, null!, names, tree)).ParamName);
        Assert.Equal("noteNameService", Assert.Throws<ArgumentNullException>(() => new NoteMigrationService(mockClient.Object, files, null!, tree)).ParamName);
        Assert.Equal("noteTreeService", Assert.Throws<ArgumentNullException>(() => new NoteMigrationService(mockClient.Object, files, names, null!)).ParamName);
    }

    [Fact]
    public async Task GivenOldEntries_WhenMigrateAsync_ThenFilesWrittenAtFolderPathsAndOldStoresRemoved()
    {
        // Arrange
        entries.Add(Entry("f1", "Work", null, NoteEntryKind.Folder));
        entries.Add(Entry("f2", "Empty", null, NoteEntryKind.Folder));
        entries.Add(Entry("n1", "Plan.md", "f1", NoteEntryKind.Note));
        entries.Add(Entry("n2", "Top.md", null, NoteEntryKind.Note));
        contents.Add(new LegacyNoteContent { Id = "n1", Content = "# Plan" });

        // Act
        await CreateSut().MigrateAsync(Owner);

        // Assert
        var keys = (await files.ListAsync()).Select(f => f.Key).Order(StringComparer.Ordinal);
        Assert.Equal(["notes/Empty/", "notes/Top.md", "notes/Work/", "notes/Work/Plan.md"], keys);
        var plan = await files.ReadAsync("notes/Work/Plan.md");
        Assert.Equal("# Plan", Encoding.UTF8.GetString(plan!.Content));
        Assert.Equal(Edited, plan.ModifiedAt);
        Assert.Empty((await files.ReadAsync("notes/Top.md"))!.Content);
        mockClient.Verify(x => x.DeleteManyFromExistingAsync(Database, NoteMigrationService.EntriesStore, It.Is<IEnumerable<object>>(k => k.Count() == 4)), Times.Once);
        mockClient.Verify(x => x.DeleteManyFromExistingAsync(Database, NoteMigrationService.ContentStore, It.IsAny<IEnumerable<object>>()), Times.Once);
        mockClient.Verify(x => x.DeleteStoresAsync(Database, It.Is<IEnumerable<string>>(s => s.SequenceEqual(new[] { "entries", "content" }))), Times.Once);
    }

    [Fact]
    public async Task GivenOtherUsersNotesRemain_WhenMigrateAsync_ThenOnlyOwnMigratedAndStoresKept()
    {
        // Arrange
        entries.Add(Entry("n1", "Mine.md", null, NoteEntryKind.Note));
        entries.Add(new NoteEntry { Id = "n2", Owner = "alice", Name = "Hers.md", Kind = NoteEntryKind.Note });

        // Act
        await CreateSut().MigrateAsync(Owner);

        // Assert
        Assert.Equal(["notes/Mine.md"], (await files.ListAsync()).Select(f => f.Key));
        mockClient.Verify(x => x.DeleteManyFromExistingAsync(Database, NoteMigrationService.EntriesStore, It.Is<IEnumerable<object>>(k => (string)k.Single() == "n1")), Times.Once);
        mockClient.Verify(x => x.DeleteStoresAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GivenNamesUnsafeForS3OrClashing_WhenMigrateAsync_ThenCleanedAndMadeUnique()
    {
        // Arrange
        entries.Add(Entry("n1", "Issue #4.md", null, NoteEntryKind.Note));
        entries.Add(Entry("n2", "Issue _4.md", null, NoteEntryKind.Note));
        entries.Add(Entry("n3", "...", null, NoteEntryKind.Folder));

        // Act
        await CreateSut().MigrateAsync(Owner);

        // Assert
        var keys = (await files.ListAsync()).Select(f => f.Key).Order(StringComparer.Ordinal);
        Assert.Equal(["notes/Issue _4 (2).md", "notes/Issue _4.md", "notes/Untitled/"], keys);
    }

    [Fact]
    public async Task GivenNoOldStore_WhenMigrateAsync_ThenNothingHappensAndNotCheckedAgain()
    {
        // Arrange
        mockClient.Setup(x => x.StoreExistsAsync(Database, NoteMigrationService.EntriesStore)).ReturnsAsync(false);
        var sut = CreateSut();

        // Act
        await sut.MigrateAsync(Owner);
        await sut.MigrateAsync(Owner);

        // Assert
        mockClient.Verify(x => x.StoreExistsAsync(Database, NoteMigrationService.EntriesStore), Times.Once);
        Assert.Empty(await files.ListAsync());
    }

    [Fact]
    public async Task GivenMigrated_WhenMigrateAsyncAgain_ThenNotRepeated()
    {
        // Arrange
        entries.Add(Entry("n1", "Mine.md", null, NoteEntryKind.Note));
        var sut = CreateSut();

        // Act
        await sut.MigrateAsync(Owner);
        await sut.MigrateAsync(Owner);

        // Assert
        mockClient.Verify(x => x.GetAllFromExistingAsync<NoteEntry>(Database, NoteMigrationService.EntriesStore), Times.Once);
    }

    private static NoteEntry Entry(string id, string name, string? parentId, NoteEntryKind kind) =>
        new() { Id = id, Owner = Owner, Name = name, ParentId = parentId, Kind = kind, ModifiedAt = Edited };

    private NoteMigrationService CreateSut() => new(mockClient.Object, files, new NoteNameService(), new NoteTreeService());
}
