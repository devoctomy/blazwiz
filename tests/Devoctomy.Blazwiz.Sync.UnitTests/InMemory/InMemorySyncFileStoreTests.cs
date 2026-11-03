using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.InMemory;

public class InMemorySyncFileStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<TimeProvider> mockClock = new();

    public InMemorySyncFileStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new InMemorySyncFileStore(null!, mockClock.Object)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new InMemorySyncFileStore(mockUser.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenFiles_WhenWrittenListedReadAndDeleted_ThenStoreBehavesLikeAFileStore()
    {
        // Arrange
        var sut = new InMemorySyncFileStore(mockUser.Object, mockClock.Object);

        // Act
        await sut.WriteAsync("notes/a.md", [1, 2]);
        await sut.CreateFolderAsync("notes/Work/");
        var listed = await sut.ListAsync();
        var read = await sut.ReadAsync("notes/a.md");
        await sut.DeleteAsync("notes/a.md");

        // Assert
        Assert.Equal(["notes/Work/", "notes/a.md"], listed.Select(f => f.Key).Order(StringComparer.Ordinal));
        Assert.True(listed.Single(f => f.Key == "notes/Work/").IsFolder);
        Assert.Equal([1, 2], read!.Content);
        Assert.Equal(Now, read.ModifiedAt);
        Assert.Null(await sut.ReadAsync("notes/a.md"));
    }

    [Fact]
    public async Task GivenTwoUsers_WhenListAsync_ThenEachSeesOnlyTheirOwnFiles()
    {
        // Arrange
        var sut = new InMemorySyncFileStore(mockUser.Object, mockClock.Object);
        await sut.WriteAsync("notes/bob.md", [1]);
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("alice");

        // Act & Assert
        Assert.Empty(await sut.ListAsync());
    }

    [Fact]
    public async Task GivenInvalidInput_WhenCalled_ThenThrows()
    {
        // Act & Assert
        var sut = new InMemorySyncFileStore(mockUser.Object, mockClock.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateFolderAsync("notes/Work"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.WriteAsync(string.Empty, []));
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ListAsync());
    }
}
