using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class EncryptingSyncFileStoreTests
{
    private const string Key = "notes/Shopping.md";

    private readonly InMemorySyncFileStore inner;
    private readonly E2eKeyScope scope = new(Guid.NewGuid().ToString("D"));
    private readonly E2eKeyRing ring;
    private readonly EncryptedFileFormat format = new(new BouncyCastleAesGcmAeadCipher());
    private readonly byte[] fileKey = RandomNumberGenerator.GetBytes(32);
    private readonly Mock<IE2eIdleLock> mockIdle = new();
    private readonly EncryptingSyncFileStore sut;

    public EncryptingSyncFileStoreTests()
    {
        var user = new Mock<ISyncUserContext>();
        user.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UnixEpoch);
        inner = new InMemorySyncFileStore(user.Object, clock.Object);
        ring = new E2eKeyRing(new InMemoryDerivedKeyStore(), scope);
        sut = new EncryptingSyncFileStore(inner, ring, format, mockIdle.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("inner", Assert.Throws<ArgumentNullException>(() => new EncryptingSyncFileStore(null!, ring, format)).ParamName);
        Assert.Equal("keyRing", Assert.Throws<ArgumentNullException>(() => new EncryptingSyncFileStore(inner, null!, format)).ParamName);
        Assert.Equal("encryptedFileFormat", Assert.Throws<ArgumentNullException>(() => new EncryptingSyncFileStore(inner, ring, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAnEmptyFileInTheStore_WhenRead_ThenThrowsInvalidData()
    {
        // Arrange: an encrypted file is never empty, so this one was truncated (the one change the tag cannot catch).
        Unlock();
        await inner.WriteAsync(Key, []);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => sut.ReadAsync(Key));
    }

    [Fact]
    public async Task GivenAFolderMarker_WhenRead_ThenItPassesThrough()
    {
        // Arrange
        Unlock();
        await inner.CreateFolderAsync("notes/Work/");

        // Act
        var folder = await sut.ReadAsync("notes/Work/");

        // Assert
        Assert.True(folder == null || folder.Content.Length == 0);
    }

    [Fact]
    public async Task GivenUnlocked_WhenWrittenAndRead_ThenStoredBoundToTheProfileAndPathAndReadBack()
    {
        // Arrange
        Unlock();

        // Act
        await sut.WriteAsync(Key, Bytes("milk"));

        // Assert
        var stored = (await inner.ReadAsync(Key))!.Content;
        Assert.True(stored.AsSpan().StartsWith(EncryptedFileFormat.MagicBytes));
        Assert.True(format.TryGetKeyId(stored, out var keyId));
        Assert.Equal("k_1", keyId);
        Assert.Equal("milk", Encoding.UTF8.GetString((await sut.ReadAsync(Key))!.Content));
        Assert.Equal("milk", Encoding.UTF8.GetString(format.Decrypt(stored, fileKey, new E2eFileBinding(scope.Id, Key))));
    }

    [Fact]
    public async Task GivenAFileMovedToAnotherPath_WhenRead_ThenItFailsToDecrypt()
    {
        // Arrange
        Unlock();
        await sut.WriteAsync(Key, Bytes("milk"));
        await inner.WriteAsync("notes/Moved.md", (await inner.ReadAsync(Key))!.Content);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(() => sut.ReadAsync("notes/Moved.md"));
    }

    [Fact]
    public async Task GivenAFileOfAnotherProfile_WhenRead_ThenThrowsInvalidData()
    {
        // Arrange
        Unlock();
        await inner.WriteAsync(Key, format.Encrypt(Bytes("milk"), "k_1", fileKey, new E2eFileBinding(Guid.NewGuid().ToString("D"), Key)));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => sut.ReadAsync(Key));
    }

    [Fact]
    public async Task GivenPlaintextInTheStore_WhenRead_ThenThrowsInvalidData()
    {
        // Arrange
        Unlock();
        await inner.WriteAsync(Key, Bytes("plain"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => sut.ReadAsync(Key));
    }

    [Fact]
    public async Task GivenLocked_WhenWriteAsync_ThenThrowsLockedAndNothingStored()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<E2eLockedException>(() => sut.WriteAsync(Key, Bytes("milk")));
        Assert.Equal(E2eErrorCodes.CannotEncrypt, ex.Code);
        Assert.Null(await inner.ReadAsync(Key));
    }

    [Fact]
    public async Task GivenAnEncryptedFileAndLocked_WhenReadAsync_ThenThrowsLocked()
    {
        // Arrange
        Unlock();
        await sut.WriteAsync(Key, Bytes("milk"));
        ring.Clear();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<E2eLockedException>(() => sut.ReadAsync(Key));
        Assert.Equal(E2eErrorCodes.CannotDecrypt, ex.Code);
    }

    [Fact]
    public async Task GivenUnlocked_WhenItsFilesAreWrittenAndRead_ThenItsIdleTimerIsRestartedEachTime()
    {
        // Arrange
        Unlock();

        // Act
        await sut.WriteAsync(Key, Bytes("milk"));
        await sut.ReadAsync(Key);

        // Assert
        mockIdle.Verify(x => x.Touch(scope), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenMissingFile_WhenReadAsync_ThenNull()
    {
        // Act & Assert
        Assert.Null(await sut.ReadAsync(Key));
    }

    [Fact]
    public async Task GivenLocked_WhenFolderCreatedOrFileDeleted_ThenPassedThrough()
    {
        // Arrange
        await inner.WriteAsync(Key, Bytes("apple"));

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
