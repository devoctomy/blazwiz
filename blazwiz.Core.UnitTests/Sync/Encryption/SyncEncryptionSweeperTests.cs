using System.Security.Cryptography;
using System.Text;
using blazwiz.Core.Cryptography;
using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync;
using blazwiz.Core.Sync.Encryption;
using blazwiz.Core.Sync.InMemory;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Time.Interfaces;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Encryption;

public class SyncEncryptionSweeperTests
{
    private const string Profile = "notes";
    private const string Owner = "bob";

    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly InMemorySyncFileStore files;
    private readonly InMemorySyncIndexStore index = new();
    private readonly E2eKeyRing ring = new(new InMemoryDerivedKeyStore());
    private readonly EncryptedFileFormat format = new(new BouncyCastleAesGcmAeadCipher());
    private readonly EncryptingSyncFileStore appStore;
    private readonly SyncEncryptionSweeper sut;

    public SyncEncryptionSweeperTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        var clock = new Mock<ISystemClock>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UnixEpoch);
        files = new InMemorySyncFileStore(mockUser.Object, clock.Object);
        var tracking = new TrackingSyncFileStore(Profile, files, index, mockUser.Object, Mock.Of<ISyncScheduler>(), clock.Object, format);
        appStore = new EncryptingSyncFileStore(tracking, ring, format);
        sut = new SyncEncryptionSweeper([new EncryptedSyncProfile(Profile, appStore, index)], mockUser.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionSweeper(null!, mockUser.Object)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionSweeper([], null!)).ParamName);
    }

    [Fact]
    public async Task GivenPlainFiles_WhenEncryptAsync_ThenEveryFileEncryptedMarkedAndPending()
    {
        // Arrange
        await appStore.WriteAsync("notes/a.md", Bytes("apple"));
        await appStore.WriteAsync("notes/b.md", Bytes("orange"));
        await appStore.CreateFolderAsync("notes/Work/");
        await SyncedAsync();
        Unlock();
        var reported = new List<E2eProgress>();

        // Act
        var count = await sut.EncryptAsync(new ListProgress(reported));

        // Assert
        Assert.Equal(2, count);
        foreach (var key in new[] { "notes/a.md", "notes/b.md" })
        {
            Assert.True(format.TryGetKeyId((await files.ReadAsync(key))!.Content, out var keyId));
            Assert.Equal("k_1", keyId);
            var record = (await index.GetAsync(Profile, Owner, key))!;
            Assert.True(record.Encrypted);
            Assert.Equal("k_1", record.KeyId);
            Assert.True(record.Dirty);
        }

        Assert.Equal(Bytes("apple"), (await appStore.ReadAsync("notes/a.md"))!.Content);
        Assert.False((await index.GetAsync(Profile, Owner, "notes/Work/"))!.Encrypted);
        Assert.All(reported, p => Assert.Equal(E2eStep.EncryptFiles, p.Step));
        Assert.Equal([0, 1, 2, 2], reported.Select(p => p.Done));
        Assert.True(reported[^1].Completed);
        Assert.Equal(2, await sut.CountEncryptedAsync());
    }

    [Fact]
    public async Task GivenSweepCutShort_WhenEncryptAsyncAgain_ThenCarriesOnWithFilesNotYetMarked()
    {
        // Arrange: one file already encrypted by an earlier sweep.
        await appStore.WriteAsync("notes/a.md", Bytes("apple"));
        await appStore.WriteAsync("notes/b.md", Bytes("orange"));
        Unlock();
        await appStore.WriteAsync("notes/a.md", Bytes("apple"));
        var before = (await files.ReadAsync("notes/a.md"))!.Content;

        // Act
        var count = await sut.EncryptAsync();

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(before, (await files.ReadAsync("notes/a.md"))!.Content);
        Assert.True((await index.GetAsync(Profile, Owner, "notes/b.md"))!.Encrypted);
    }

    [Fact]
    public async Task GivenOtherProfileAsked_WhenEncryptAsync_ThenNothingEncrypted()
    {
        await appStore.WriteAsync("notes/a.md", Bytes("apple"));
        Unlock();
        Assert.Equal(0, await sut.EncryptAsync(null, "photos"));
    }

    [Fact]
    public async Task GivenEncryptedFiles_WhenDecryptAsyncWritingPlaintext_ThenEveryFileDecryptedAndUnmarked()
    {
        // Arrange
        Unlock();
        await appStore.WriteAsync("notes/a.md", Bytes("apple"));
        await appStore.WriteAsync("notes/b.md", Bytes("orange"));
        await SyncedAsync();
        ring.SetWriteMode(E2eWriteMode.Plaintext);

        // Act
        var count = await sut.DecryptAsync();

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(Bytes("apple"), (await files.ReadAsync("notes/a.md"))!.Content);
        var record = (await index.GetAsync(Profile, Owner, "notes/a.md"))!;
        Assert.False(record.Encrypted);
        Assert.Null(record.KeyId);
        Assert.True(record.Dirty);
        Assert.Equal(0, await sut.CountEncryptedAsync());
    }

    [Fact]
    public async Task GivenPendingOnly_WhenDecryptAsync_ThenOnlyUnsyncedChangesDecrypted()
    {
        // Arrange
        Unlock();
        await appStore.WriteAsync("notes/synced.md", Bytes("apple"));
        await SyncedAsync();
        await appStore.WriteAsync("notes/pending.md", Bytes("orange"));
        ring.SetWriteMode(E2eWriteMode.Plaintext);

        // Act
        var count = await sut.DecryptAsync(pendingOnly: true);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(Bytes("orange"), (await files.ReadAsync("notes/pending.md"))!.Content);
        Assert.True(format.TryGetKeyId((await files.ReadAsync("notes/synced.md"))!.Content, out _));
    }

    [Fact]
    public async Task GivenFileWithKeyNotHeld_WhenDecryptAsync_ThenThrowsLocked()
    {
        // Arrange
        Unlock();
        await appStore.WriteAsync("notes/a.md", Bytes("apple"));
        ring.Clear(E2eWriteMode.Plaintext);

        // Act & Assert
        await Assert.ThrowsAsync<E2eLockedException>(() => sut.DecryptAsync());
    }

    [Fact]
    public async Task GivenSignedOut_WhenCountEncryptedAsync_ThenThrowsInvalidOperationException()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CountEncryptedAsync());
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private void Unlock() => ring.Load(RandomNumberGenerator.GetBytes(32), new Dictionary<string, byte[]> { ["k_1"] = RandomNumberGenerator.GetBytes(32) }, "k_1");

    // As if a pass had synced every file: nothing pending.
    private async Task SyncedAsync()
    {
        foreach (var record in await index.GetAllAsync(Profile, Owner))
        {
            record.Dirty = false;
            record.BaseETag = "etag";
            await index.SaveAsync(record);
        }
    }

    private sealed class ListProgress(List<E2eProgress> reported) : IProgress<E2eProgress>
    {
        public void Report(E2eProgress value) => reported.Add(value);
    }
}
