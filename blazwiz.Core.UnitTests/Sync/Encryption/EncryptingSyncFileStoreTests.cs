using System.Security.Cryptography;
using System.Text;
using blazwiz.Core.Cryptography;
using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Encryption;
using blazwiz.Core.Sync.InMemory;
using blazwiz.Core.Sync.Interfaces;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Encryption;

public class EncryptingSyncFileStoreTests
{
    private const string Key = "notes/Shopping.md";

    private readonly InMemorySyncFileStore inner;
    private readonly E2eKeyRing ring = new(new InMemoryDerivedKeyStore());
    private readonly EncryptedFileFormat format = new(new BouncyCastleAesGcmAeadCipher());
    private readonly byte[] fileKey = RandomNumberGenerator.GetBytes(32);
    private readonly EncryptingSyncFileStore sut;

    public EncryptingSyncFileStoreTests()
    {
        var user = new Mock<ISyncUserContext>();
        user.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UnixEpoch);
        inner = new InMemorySyncFileStore(user.Object, clock.Object);
        sut = new EncryptingSyncFileStore(inner, ring, format);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("inner", Assert.Throws<ArgumentNullException>(() => new EncryptingSyncFileStore(null!, ring, format)).ParamName);
        Assert.Equal("keyRing", Assert.Throws<ArgumentNullException>(() => new EncryptingSyncFileStore(inner, null!, format)).ParamName);
        Assert.Equal("encryptedFileFormat", Assert.Throws<ArgumentNullException>(() => new EncryptingSyncFileStore(inner, ring, null!)).ParamName);
    }

    [Fact]
    public async Task GivenPlaintextMode_WhenWriteAsync_ThenStoredAsIs()
    {
        // Act
        await sut.WriteAsync(Key, Bytes("apple"));

        // Assert
        Assert.Equal(Bytes("apple"), (await inner.ReadAsync(Key))!.Content);
        Assert.Equal(Bytes("apple"), (await sut.ReadAsync(Key))!.Content);
    }

    [Fact]
    public async Task GivenUnlocked_WhenWriteAsync_ThenStoredEncryptedWithActiveKeyAndReadDecrypts()
    {
        // Arrange
        Unlock();
        var modifiedAt = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

        // Act
        await sut.WriteAsync(Key, Bytes("apple"), modifiedAt);

        // Assert
        var stored = (await inner.ReadAsync(Key))!;
        Assert.True(format.TryGetKeyId(stored.Content, out var keyId));
        Assert.Equal("k_1", keyId);
        Assert.Equal(modifiedAt, stored.ModifiedAt);
        var read = (await sut.ReadAsync(Key))!;
        Assert.Equal(Bytes("apple"), read.Content);
        Assert.Equal(modifiedAt, read.ModifiedAt);
        Assert.Equal(Key, read.Key);
    }

    [Fact]
    public async Task GivenRefuseMode_WhenWriteAsync_ThenThrowsLockedAndNothingStored()
    {
        // Arrange
        ring.Clear(E2eWriteMode.Refuse);

        // Act & Assert
        await Assert.ThrowsAsync<E2eLockedException>(() => sut.WriteAsync(Key, Bytes("apple")));
        Assert.Null(await inner.ReadAsync(Key));
    }

    [Fact]
    public async Task GivenEncryptedFileAndNoKey_WhenReadAsync_ThenThrowsLocked()
    {
        // Arrange
        await inner.WriteAsync(Key, format.Encrypt(Bytes("apple"), "k_other", fileKey));

        // Act & Assert
        await Assert.ThrowsAsync<E2eLockedException>(() => sut.ReadAsync(Key));
    }

    [Fact]
    public async Task GivenPlainFileWhileUnlocked_WhenReadAsync_ThenReturnedAsIs()
    {
        // Arrange
        await inner.WriteAsync(Key, Bytes("apple"));
        Unlock();

        // Act & Assert
        Assert.Equal(Bytes("apple"), (await sut.ReadAsync(Key))!.Content);
    }

    [Fact]
    public async Task GivenKeysHeldWritingPlaintext_WhenReadThenWrite_ThenFileDecryptedInPlace()
    {
        // Arrange
        Unlock();
        await sut.WriteAsync(Key, Bytes("apple"));
        ring.SetWriteMode(E2eWriteMode.Plaintext);

        // Act
        var file = (await sut.ReadAsync(Key))!;
        await sut.WriteAsync(Key, file.Content);

        // Assert
        Assert.Equal(Bytes("apple"), (await inner.ReadAsync(Key))!.Content);
    }

    [Fact]
    public async Task GivenMissingFile_WhenReadAsync_ThenNull()
    {
        Assert.Null(await sut.ReadAsync(Key));
    }

    [Fact]
    public async Task GivenRefuseMode_WhenFolderCreatedOrFileDeleted_ThenPassedThrough()
    {
        // Arrange
        await inner.WriteAsync(Key, Bytes("apple"));
        ring.Clear(E2eWriteMode.Refuse);

        // Act
        await sut.CreateFolderAsync("notes/Work/");
        await sut.DeleteAsync(Key);

        // Assert
        var files = await sut.ListAsync();
        Assert.Equal("notes/Work/", Assert.Single(files).Key);
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private void Unlock() => ring.Load(RandomNumberGenerator.GetBytes(32), new Dictionary<string, byte[]> { ["k_1"] = fileKey }, "k_1");
}
